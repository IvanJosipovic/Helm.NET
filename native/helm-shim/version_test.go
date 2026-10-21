package main

import (
	"context"
	"crypto/sha256"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"testing"
	"time"

	"helm.sh/helm/v4/pkg/action"
	"helm.sh/helm/v4/pkg/chart/common"
	chart "helm.sh/helm/v4/pkg/chart/v2"
	chartutil "helm.sh/helm/v4/pkg/chart/v2/util"
	"helm.sh/helm/v4/pkg/cli"
	"helm.sh/helm/v4/pkg/kube"
	kubefake "helm.sh/helm/v4/pkg/kube/fake"
	"helm.sh/helm/v4/pkg/registry"
	releasev1 "helm.sh/helm/v4/pkg/release/v1"
	repo "helm.sh/helm/v4/pkg/repo/v1"
	"helm.sh/helm/v4/pkg/storage"
	"helm.sh/helm/v4/pkg/storage/driver"
)

func versionTestConfiguration() *action.Configuration {
	cfg := action.NewConfiguration()
	cfg.Releases = storage.Init(driver.NewMemory())
	cfg.KubeClient = &kubefake.FailingKubeClient{
		PrintingKubeClient: kubefake.PrintingKubeClient{Out: io.Discard},
	}
	cfg.Capabilities = common.DefaultCapabilities.Copy()
	return cfg
}

func versionTestRepository(t *testing.T) *cli.EnvSettings {
	t.Helper()
	directory := t.TempDir()
	for _, version := range []string{"1.0.0", "2.0.0", "3.0.0"} {
		ch := &chart.Chart{
			Metadata: &chart.Metadata{APIVersion: "v2", Name: "example", Version: version},
			Values:   map[string]any{"setting": "default"},
			Templates: []*common.File{{
				Name: "templates/configmap.yaml",
				Data: []byte("apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}\n  namespace: {{ .Release.Namespace }}\ndata:\n  version: {{ .Chart.Version | quote }}\n  setting: {{ .Values.setting | quote }}\n  fromFile: {{ .Values.fromFile | quote }}\n"),
			}},
		}
		if _, err := chartutil.Save(ch, directory); err != nil {
			t.Fatal(err)
		}
	}
	server := httptest.NewServer(http.FileServer(http.Dir(directory)))
	t.Cleanup(server.Close)
	index, err := repo.IndexDirectory(directory, server.URL)
	if err != nil {
		t.Fatal(err)
	}
	if err := index.WriteFile(filepath.Join(directory, "index.yaml"), 0o600); err != nil {
		t.Fatal(err)
	}
	settings := cli.New()
	settings.RepositoryConfig = filepath.Join(t.TempDir(), "repositories.yaml")
	settings.RepositoryCache = t.TempDir()
	_, message := execute(request{
		Operation: "repoAdd", RepositoryName: "test", RepositoryURL: server.URL,
		RepositoryConfig: settings.RepositoryConfig, RepositoryCache: settings.RepositoryCache,
	})
	if message != "" {
		t.Fatal(message)
	}
	return settings
}

func TestChartVersionsAcrossTemplateInstallAndUpgrade(t *testing.T) {
	settings := versionTestRepository(t)
	cfg := versionTestConfiguration()
	valuesFile := filepath.Join(t.TempDir(), "values.yaml")
	if err := os.WriteFile(valuesFile, []byte("fromFile: retained\nsetting: from-file\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	run := func(operation, name, version string, installIfMissing bool) any {
		t.Helper()
		actionConfig := cfg
		if operation == "template" {
			actionConfig = versionTestConfiguration()
		}
		data, message := executeAction(context.Background(), request{
			Operation: operation, ReleaseName: name, Chart: "test/example", Version: version,
			Namespace: "apps", InstallIfMissing: installIfMissing, Wait: true, CreateNamespace: true,
			ValuesFiles: []string{valuesFile}, Values: map[string]any{"setting": "override"},
		}, settings, actionConfig, 30*time.Second)
		if message != "" {
			t.Fatalf("%s version %q failed: %s", operation, version, message)
		}
		return data
	}
	checkRelease := func(data any, version, revision string) {
		t.Helper()
		view, ok := data.(map[string]any)
		if !ok || view["chartVersion"] != version || view["revision"] != revision || view["namespace"] != "apps" || view["status"] != "deployed" {
			t.Fatalf("unexpected release: %#v", data)
		}
		stored, err := cfg.Releases.Last(view["name"].(string))
		if err != nil {
			t.Fatal(err)
		}
		rel := stored.(*releasev1.Release)
		for _, expected := range []string{`version: "` + version + `"`, `setting: "override"`, `fromFile: "retained"`, "namespace: apps"} {
			if !strings.Contains(rel.Manifest, expected) {
				t.Fatalf("manifest missing %q:\n%s", expected, rel.Manifest)
			}
		}
		preview := run("template", rel.Name, version, false).(string)
		if preview != rel.Manifest {
			t.Fatalf("template and deployment differ:\n%s\n%s", preview, rel.Manifest)
		}
	}

	checkRelease(run("install", "explicit", "1.0.0", false), "1.0.0", "1")
	checkRelease(run("upgrade", "explicit", "2.0.0", false), "2.0.0", "2")
	checkRelease(run("upgrade", "missing", "1.0.0", true), "1.0.0", "1")
	checkRelease(run("install", "latest", "", false), "3.0.0", "1")
	checkRelease(run("upgrade", "explicit", "", false), "3.0.0", "3")
	checkRelease(run("upgrade", "latest-missing", "", true), "3.0.0", "1")
	waitStrategies := cfg.KubeClient.(*kubefake.FailingKubeClient).RecordedWaitStrategies
	if len(waitStrategies) == 0 {
		t.Fatal("expected wait strategies to be recorded")
	}
	for _, strategy := range waitStrategies {
		if strategy != kube.StatusWatcherStrategy {
			t.Fatalf("wait flag was not preserved: %v", waitStrategies)
		}
	}
}

func TestUnavailableChartVersionsNeverFallBack(t *testing.T) {
	settings := versionTestRepository(t)
	for _, operation := range []string{"template", "install", "upgrade"} {
		for _, version := range []string{"99.0.0", "not-a-version"} {
			t.Run(operation+"/"+version, func(t *testing.T) {
				cfg := versionTestConfiguration()
				data, message := executeAction(context.Background(), request{
					Operation: operation, ReleaseName: "unavailable", Chart: "test/example",
					Version: version, Namespace: "default", InstallIfMissing: true,
				}, settings, cfg, time.Minute)
				if message == "" || data != nil || !strings.Contains(message, version) {
					t.Fatalf("expected explicit version error, got %#v, %q", data, message)
				}
				_, err := cfg.Releases.Last("unavailable")
				if !errors.Is(err, driver.ErrReleaseNotFound) {
					t.Fatalf("failed resolution must not install a release: %v", err)
				}
			})
		}
	}
}

func TestOCIChartVersionsAcrossTemplateInstallAndUpgrade(t *testing.T) {
	const manifestMediaType = "application/vnd.oci.image.manifest.v1+json"
	content := map[string][]byte{}
	descriptor := func(mediaType string, data []byte) map[string]any {
		digest := fmt.Sprintf("sha256:%x", sha256.Sum256(data))
		content["/v2/example/blobs/"+digest] = data
		return map[string]any{"mediaType": mediaType, "digest": digest, "size": len(data)}
	}
	marshal := func(value any) []byte {
		t.Helper()
		data, err := json.Marshal(value)
		if err != nil {
			t.Fatal(err)
		}
		return data
	}
	for _, version := range []string{"1.0.0", "2.0.0", "3.0.0"} {
		ch := &chart.Chart{
			Metadata: &chart.Metadata{APIVersion: "v2", Name: "example", Version: version},
			Templates: []*common.File{{
				Name: "templates/configmap.yaml",
				Data: []byte("apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}\ndata:\n  version: {{ .Chart.Version | quote }}\n"),
			}},
		}
		archive, err := chartutil.Save(ch, t.TempDir())
		if err != nil {
			t.Fatal(err)
		}
		data, err := os.ReadFile(archive)
		if err != nil {
			t.Fatal(err)
		}
		manifest := marshal(map[string]any{
			"schemaVersion": 2, "mediaType": manifestMediaType,
			"config": descriptor(registry.ConfigMediaType, marshal(ch.Metadata)),
			"layers": []any{descriptor(registry.ChartLayerMediaType, data)},
		})
		content["/v2/example/manifests/"+version] = manifest
		content[fmt.Sprintf("/v2/example/manifests/sha256:%x", sha256.Sum256(manifest))] = manifest
	}
	content["/v2/"] = []byte(`{}`)
	content["/v2/example/tags/list"] = []byte(`{"name":"example","tags":["1.0.0","2.0.0","3.0.0"]}`)
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		data, ok := content[r.URL.Path]
		if !ok {
			http.NotFound(w, r)
			return
		}
		w.Header().Set("Content-Length", strconv.Itoa(len(data)))
		w.Header().Set("Docker-Content-Digest", fmt.Sprintf("sha256:%x", sha256.Sum256(data)))
		if strings.Contains(r.URL.Path, "/manifests/") {
			w.Header().Set("Content-Type", manifestMediaType)
		} else {
			w.Header().Set("Content-Type", "application/octet-stream")
		}
		if r.Method != http.MethodHead {
			_, _ = w.Write(data)
		}
	}))
	defer server.Close()
	settings := cli.New()
	settings.RepositoryCache = t.TempDir()
	cfg := versionTestConfiguration()
	client, err := registry.NewClient(registry.ClientOptPlainHTTP(), registry.ClientOptCredentialsFile(filepath.Join(t.TempDir(), "registry.json")))
	if err != nil {
		t.Fatal(err)
	}
	cfg.RegistryClient = client
	chartRef := "oci://" + strings.TrimPrefix(server.URL, "http://") + "/example"
	for _, test := range []struct {
		operation, name, version, expected string
		installIfMissing                   bool
	}{
		{"template", "oci", "1.0.0", "1.0.0", false},
		{"install", "oci", "1.0.0", "1.0.0", false},
		{"upgrade", "oci", "2.0.0", "2.0.0", false},
		{"upgrade", "oci-missing", "1.0.0", "1.0.0", true},
		{"install", "oci-latest", "", "3.0.0", false},
		{"upgrade", "oci", "99.0.0", "", false},
	} {
		t.Run(test.operation+"/"+test.name+"/"+test.version, func(t *testing.T) {
			actionConfig := cfg
			if test.operation == "template" {
				actionConfig = versionTestConfiguration()
				actionConfig.RegistryClient = client
			}
			data, message := executeAction(context.Background(), request{
				Operation: test.operation, ReleaseName: test.name, Chart: chartRef,
				Version: test.version, Namespace: "default", InstallIfMissing: test.installIfMissing,
			}, settings, actionConfig, time.Minute)
			if test.expected == "" {
				if message == "" || data != nil {
					t.Fatalf("expected unavailable OCI version to fail, got %#v, %q", data, message)
				}
				return
			}
			if message != "" {
				t.Fatal(message)
			}
			if test.operation == "template" {
				if !strings.Contains(data.(string), `version: "`+test.expected+`"`) {
					t.Fatalf("unexpected OCI preview: %#v", data)
				}
			} else if data.(map[string]any)["chartVersion"] != test.expected {
				t.Fatalf("unexpected OCI chart version: %#v", data)
			}
		})
	}
}

func TestGetManifestUsesStoredManifestWhenDependencyTemplatesAreMissing(t *testing.T) {
	cfg := versionTestConfiguration()
	settings := cli.New()
	parent := &chart.Chart{
		Metadata: &chart.Metadata{
			APIVersion: "v2", Name: "parent", Version: "1.0.0",
			Dependencies: []*chart.Dependency{{Name: "remote-cluster", Version: "1.0.0"}},
		},
		Values: map[string]any{},
		Templates: []*common.File{{
			Name: "templates/configmap.yaml",
			Data: []byte("apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ include \"remote-cluster.fullname\" . }}\n"),
		}},
	}
	parent.AddDependency(&chart.Chart{
		Metadata: &chart.Metadata{APIVersion: "v2", Name: "remote-cluster", Version: "1.0.0", Type: "library"},
		Templates: []*common.File{{
			Name: "templates/_helpers.tpl",
			Data: []byte(`{{- define "remote-cluster.fullname" -}}{{ .Release.Name }}-remote{{- end -}}`),
		}},
	})
	install := action.NewInstall(cfg)
	install.ReleaseName = "stored"
	install.Namespace = "default"
	install.DryRunStrategy = action.DryRunClient
	original, err := install.RunWithContext(context.Background(), parent, map[string]any{})
	if err != nil {
		t.Fatal(err)
	}
	// Secrets/configmaps persist JSON, unlike Helm's memory driver.
	encoded, err := json.Marshal(original)
	if err != nil {
		t.Fatal(err)
	}
	var stored releasev1.Release
	if err := json.Unmarshal(encoded, &stored); err != nil {
		t.Fatal(err)
	}
	if len(stored.Chart.Dependencies()) != 0 || len(stored.Chart.Metadata.Dependencies) != 1 {
		t.Fatal("expected dependency metadata, but not dependency chart contents, to survive storage")
	}
	if err := cfg.Releases.Create(&stored); err != nil {
		t.Fatal(err)
	}
	data, message := executeAction(context.Background(), request{
		Operation: "getManifest", ReleaseName: "stored",
	}, settings, cfg, time.Minute)
	if message != "" || !strings.Contains(data.(string), "name: stored-remote") {
		t.Fatalf("stored manifest should remain usable: %#v, %s", data, message)
	}
}
