package main

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"strings"
	"testing"
	"time"

	chart "helm.sh/helm/v4/pkg/chart/v2"
	"helm.sh/helm/v4/pkg/release/common"
	releasev1 "helm.sh/helm/v4/pkg/release/v1"
)

func TestRequestFromNativeMapsFieldsAndDynamicValues(t *testing.T) {
	got, err := requestFromNative(nativeRequest{
		ABIVersion: nativeABIVersion,
		Operation:  "install", ReleaseName: "web-雪", Chart: "oci://charts.example/app",
		ValuesFiles:     []string{"prod.yaml", "overrides.yaml"},
		ValuesJSON:      `{"replicas":3,"enabled":true,"nested":{"zone":"west","ports":[80,443]}}`,
		CreateNamespace: true, Wait: true, TimeoutMilliseconds: 12345,
		Kubeconfig: "C:/配置/kube.yaml", KubeContext: "dev-日本", Namespace: "apps",
		AllNamespaces: true, Filter: "^web$", Revision: 7, InstallIfMissing: true,
		RepositoryConfig: "repositories.yaml", RepositoryCache: "cache",
		RegistryConfig: "registry.json", HelmDriver: "configmap",
		RepositoryName: "private", RepositoryURL: "https://example.test/雪",
		RepositoryUsername: "user", RepositoryPassword: "secret",
	})
	if err != nil {
		t.Fatal(err)
	}

	if got.Operation != "install" || got.ReleaseName != "web-雪" || got.Chart != "oci://charts.example/app" {
		t.Fatalf("unexpected operation fields: %#v", got)
	}
	if len(got.ValuesFiles) != 2 || got.ValuesFiles[0] != "prod.yaml" || got.ValuesFiles[1] != "overrides.yaml" {
		t.Fatalf("unexpected values files: %#v", got.ValuesFiles)
	}
	if got.Values["replicas"] != float64(3) || got.Values["enabled"] != true {
		t.Fatalf("unexpected scalar values: %#v", got.Values)
	}
	nested, ok := got.Values["nested"].(map[string]any)
	if !ok || nested["zone"] != "west" {
		t.Fatalf("unexpected nested values: %#v", got.Values["nested"])
	}
	ports, ok := nested["ports"].([]any)
	if !ok || len(ports) != 2 || ports[0] != float64(80) || ports[1] != float64(443) {
		t.Fatalf("unexpected dynamic JSON array: %#v", nested["ports"])
	}
	if !got.CreateNamespace || !got.Wait || got.TimeoutMilliseconds != 12345 || !got.AllNamespaces || !got.InstallIfMissing || got.Revision != 7 {
		t.Fatalf("numeric/boolean ABI fields were not preserved: %#v", got)
	}
	if got.Kubeconfig != "C:/配置/kube.yaml" || got.KubeContext != "dev-日本" || got.Namespace != "apps" || got.Filter != "^web$" {
		t.Fatalf("unexpected config fields: %#v", got)
	}
	if got.RepositoryConfig != "repositories.yaml" || got.RepositoryCache != "cache" || got.RegistryConfig != "registry.json" || got.HelmDriver != "configmap" {
		t.Fatalf("unexpected Helm config fields: %#v", got)
	}
	if got.RepositoryName != "private" || got.RepositoryURL != "https://example.test/雪" || got.RepositoryUsername != "user" || got.RepositoryPassword != "secret" {
		t.Fatalf("unexpected repository fields: %#v", got)
	}
}

func TestRequestFromNativeLeavesOmittedOptionalFieldsEmpty(t *testing.T) {
	got, err := requestFromNative(nativeRequest{ABIVersion: nativeABIVersion, Operation: "list"})
	if err != nil {
		t.Fatal(err)
	}
	if got.Kubeconfig != "" || got.KubeContext != "" || got.Namespace != "" || got.Filter != "" || got.RepositoryURL != "" {
		t.Fatalf("omitted optional strings were not empty: %#v", got)
	}
	if got.ValuesFiles != nil || got.Values != nil || got.CreateNamespace || got.Wait || got.AllNamespaces || got.InstallIfMissing {
		t.Fatalf("omitted optional fields were not empty: %#v", got)
	}
}

func TestRequestFromNativeRejectsUnsupportedABIVersion(t *testing.T) {
	_, err := requestFromNative(nativeRequest{ABIVersion: nativeABIVersion + 1, Operation: "list"})
	if err == nil || err.Error() != "unsupported Helm native ABI version: 2" {
		t.Fatalf("expected unsupported ABI version error, got %v", err)
	}
}

func TestRequestFromNativeRejectsMissingOperation(t *testing.T) {
	_, err := requestFromNative(nativeRequest{ABIVersion: nativeABIVersion})
	if err == nil || err.Error() != "operation is required" {
		t.Fatalf("expected missing operation error, got %v", err)
	}
}

func TestRequestFromNativeRejectsInvalidValuesJSON(t *testing.T) {
	_, err := requestFromNative(nativeRequest{ABIVersion: nativeABIVersion, Operation: "template", ValuesJSON: `{"items":[`})
	if err == nil || !strings.Contains(err.Error(), "invalid values JSON") {
		t.Fatalf("expected invalid dynamic JSON error, got %v", err)
	}
}

func TestResponseFromResultEncodesTypedResultKinds(t *testing.T) {
	deployed := time.Date(2026, time.January, 2, 3, 4, 5, 123456789, time.FixedZone("UTC+2", 2*60*60))
	release := map[string]any{
		"name": "web", "namespace": "apps", "revision": "3", "updated": deployed,
		"status": "deployed", "chart": "nginx-1.2.3", "app_version": "2.0",
	}
	tests := []struct {
		name      string
		operation string
		data      any
		kind      int32
	}{
		{name: "text", operation: "template", data: "rendered yaml", kind: nativeResultText},
		{name: "single release", operation: "status", data: release, kind: nativeResultRelease},
		{name: "release array", operation: "list", data: []map[string]any{release}, kind: nativeResultReleases},
		{name: "repository array", operation: "repoList", data: []map[string]string{{"name": "stable", "url": "https://example.test"}}, kind: nativeResultRepositories},
		{name: "dynamic JSON", operation: "values", data: map[string]any{"nested": map[string]any{"enabled": true}}, kind: nativeResultJSON},
		{name: "acknowledgement", operation: "repoUpdate", data: nil, kind: nativeResultAck},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			got := responseFromResult(test.operation, test.data, "")
			if got.ABIVersion != nativeABIVersion || got.ResultKind != test.kind || got.Error != "" {
				t.Fatalf("unexpected typed response header: %#v", got)
			}
			switch test.kind {
			case nativeResultText:
				if got.Text != "rendered yaml" {
					t.Fatalf("unexpected text result: %q", got.Text)
				}
			case nativeResultRelease:
				want := nativeRelease{
					Name: "web", Namespace: "apps", Revision: "3",
					Updated: deployed.Format(time.RFC3339Nano), Status: "deployed",
					Chart: "nginx-1.2.3", AppVersion: "2.0",
				}
				if got.Release == nil || *got.Release != want {
					t.Fatalf("got release %#v, want %#v", got.Release, want)
				}
			case nativeResultReleases:
				want := nativeRelease{
					Name: "web", Namespace: "apps", Revision: "3",
					Updated: deployed.Format(time.RFC3339Nano), Status: "deployed",
					Chart: "nginx-1.2.3", AppVersion: "2.0",
				}
				if len(got.Releases) != 1 || got.Releases[0] != want {
					t.Fatalf("got releases %#v, want one %#v", got.Releases, want)
				}
			case nativeResultRepositories:
				if len(got.Repositories) != 1 || got.Repositories[0].Name != "stable" || got.Repositories[0].URL != "https://example.test" {
					t.Fatalf("unexpected repositories result: %#v", got.Repositories)
				}
			case nativeResultJSON:
				var decoded map[string]any
				if err := json.Unmarshal([]byte(got.JSON), &decoded); err != nil {
					t.Fatal(err)
				}
				if decoded["nested"].(map[string]any)["enabled"] != true {
					t.Fatalf("dynamic JSON leaf changed: %s", got.JSON)
				}
			case nativeResultAck:
				if got.Text != "" || got.JSON != "" || got.Release != nil || len(got.Releases) != 0 || len(got.Repositories) != 0 {
					t.Fatalf("acknowledgement populated an unrelated result field: %#v", got)
				}
			}
		})
	}
}

func TestResponseFromResultPreservesEmptyTypedArrays(t *testing.T) {
	releases := responseFromResult("list", []map[string]any{}, "")
	if releases.ResultKind != nativeResultReleases || releases.Releases == nil || len(releases.Releases) != 0 {
		t.Fatalf("expected an empty allocated release array, got %#v", releases)
	}

	repositories := responseFromResult("repoList", []map[string]string{}, "")
	if repositories.ResultKind != nativeResultRepositories || repositories.Repositories == nil || len(repositories.Repositories) != 0 {
		t.Fatalf("expected an empty allocated repository array, got %#v", repositories)
	}
}

func TestResponseFromResultKeepsActionErrorSeparateFromTypedData(t *testing.T) {
	got := responseFromResult("status", nil, "release not found")
	if got.ABIVersion != nativeABIVersion || got.Error != "release not found" || got.ResultKind != 0 || got.Release != nil {
		t.Fatalf("unexpected error response: %#v", got)
	}
}

func TestResponseFromResultRejectsInvalidTypedResult(t *testing.T) {
	got := responseFromResult("status", "not a release", "")
	if got.Error != "expected release result" || got.ResultKind != 0 || got.ABIVersion != nativeABIVersion {
		t.Fatalf("unexpected invalid result response: %#v", got)
	}
}

func TestResponseFromResultRejectsMismatchedPayloads(t *testing.T) {
	tests := []struct {
		name      string
		operation string
		data      any
		wantError string
	}{
		{name: "text type", operation: "template", data: 42, wantError: "expected text result"},
		{name: "release list type", operation: "history", data: "not a list", wantError: "expected release list result"},
		{name: "release item", operation: "list", data: []map[string]any{{"updated": "not a time"}}, wantError: "invalid release in result list"},
		{name: "repository list type", operation: "repoList", data: "not a list", wantError: "expected repository list result"},
		{name: "unencodable dynamic JSON", operation: "values", data: make(chan int), wantError: "failed to encode Helm response"},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			got := responseFromResult(test.operation, test.data, "")
			if got.Error != test.wantError || got.ResultKind != 0 || got.ABIVersion != nativeABIVersion {
				t.Fatalf("got response %#v, want error %q", got, test.wantError)
			}
		})
	}
}

func TestExecuteDispatchesUnsupportedOperationErrorWithoutEnvelope(t *testing.T) {
	req, err := requestFromNative(nativeRequest{ABIVersion: nativeABIVersion, Operation: "not-an-operation"})
	if err != nil {
		t.Fatal(err)
	}
	data, actionError := execute(req)
	got := responseFromResult(req.Operation, data, actionError)
	if got.Error != "unsupported operation: not-an-operation" || got.ResultKind != 0 {
		t.Fatalf("unexpected dispatch error response: %#v", got)
	}
}

func TestExecuteRequiresChartAndReleaseName(t *testing.T) {
	for _, operation := range []string{"template", "install", "upgrade"} {
		t.Run(operation+" requires chart", func(t *testing.T) {
			_, got := execute(request{Operation: operation, ReleaseName: "web"})
			if got != "chart is required" {
				t.Fatalf("got %q", got)
			}
		})
		t.Run(operation+" requires release name", func(t *testing.T) {
			_, got := execute(request{Operation: operation, Chart: "./chart"})
			if got != "releaseName is required" {
				t.Fatalf("got %q", got)
			}
		})
	}
}

func TestExecuteRendersLocalChartWithoutKubernetes(t *testing.T) {
	chartDir := filepath.Join(t.TempDir(), "chart")
	if err := os.MkdirAll(filepath.Join(chartDir, "templates"), 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(chartDir, "Chart.yaml"), []byte("apiVersion: v2\nname: example\nversion: 1.0.0\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	template := "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}\ndata:\n  setting: {{ .Values.setting | quote }}\n"
	if err := os.WriteFile(filepath.Join(chartDir, "templates", "configmap.yaml"), []byte(template), 0o600); err != nil {
		t.Fatal(err)
	}

	data, errorMessage := execute(request{
		Operation: "template", ReleaseName: "web", Chart: chartDir,
		Values: map[string]any{"setting": "from-test"},
	})
	if errorMessage != "" {
		t.Fatalf("template failed: %s", errorMessage)
	}
	manifest, ok := data.(string)
	if !ok || !strings.Contains(manifest, "name: web") || !strings.Contains(manifest, `setting: "from-test"`) {
		t.Fatalf("unexpected rendered manifest: %#v", data)
	}
}

func TestExecuteListsEmptyRepositories(t *testing.T) {
	repositoryConfig := filepath.Join(t.TempDir(), "repositories.yaml")
	data, errorMessage := execute(request{
		Operation: "repoList", RepositoryConfig: repositoryConfig,
		RepositoryCache: filepath.Join(t.TempDir(), "cache"),
	})
	if errorMessage != "" {
		t.Fatalf("repository list failed: %s", errorMessage)
	}
	repositories, ok := data.([]map[string]string)
	if !ok || len(repositories) != 0 {
		t.Fatalf("expected empty repository list, got %#v", data)
	}
}

func TestExecuteHandlesReleaseOperationsWithoutCluster(t *testing.T) {
	kubeconfig := filepath.Join(t.TempDir(), "invalid-kubeconfig")
	if err := os.WriteFile(kubeconfig, []byte("clusters: ["), 0o600); err != nil {
		t.Fatal(err)
	}
	baseRequest := request{
		Kubeconfig:       kubeconfig,
		RepositoryConfig: filepath.Join(t.TempDir(), "repositories.yaml"),
		RepositoryCache:  filepath.Join(t.TempDir(), "cache"),
	}
	tests := []struct {
		name      string
		operation string
		check     func(*testing.T, any, string)
	}{
		{name: "list", operation: "list", check: requireActionError},
		{name: "history", operation: "history", check: requireActionError},
		{name: "status", operation: "status", check: requireActionError},
		{name: "rollback", operation: "rollback", check: requireActionError},
		{name: "uninstall", operation: "uninstall", check: requireActionError},
		{name: "values", operation: "values", check: requireActionError},
	}
	for _, test := range tests {
		t.Run(test.name, func(t *testing.T) {
			req := baseRequest
			req.Operation = test.operation
			req.ReleaseName = "missing"
			data, message := execute(req)
			test.check(t, data, message)
		})
	}
}

func TestExecuteListsEmptyReleasesInKindCluster(t *testing.T) {
	if os.Getenv("HELM_SHIM_TEST_CLUSTER") != "true" {
		t.Skip("requires the Kind cluster configured by go-test CI")
	}

	data, errorMessage := execute(request{Operation: "list"})
	if errorMessage != "" {
		t.Fatalf("list failed: %s", errorMessage)
	}
	if releases, ok := data.([]map[string]any); !ok || len(releases) != 0 {
		t.Fatalf("expected empty release list, got %#v", data)
	}
}

func TestExecuteReleaseLifecycleInKindCluster(t *testing.T) {
	if os.Getenv("HELM_SHIM_TEST_CLUSTER") != "true" {
		t.Skip("requires the Kind cluster configured by go-test CI")
	}

	chartDir := filepath.Join(t.TempDir(), "chart")
	if err := os.MkdirAll(filepath.Join(chartDir, "templates"), 0o700); err != nil {
		t.Fatal(err)
	}
	files := map[string]string{
		"Chart.yaml":               "apiVersion: v2\nname: integration\nversion: 1.0.0\n",
		"values.yaml":              "setting: initial\n",
		"templates/configmap.yaml": "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}\ndata:\n  setting: {{ .Values.setting | quote }}\n",
	}
	for name, contents := range files {
		if err := os.WriteFile(filepath.Join(chartDir, name), []byte(contents), 0o600); err != nil {
			t.Fatal(err)
		}
	}

	const namespace = "helm-shim-integration"
	const releaseName = "lifecycle"
	run := func(req request) any {
		t.Helper()
		req.Namespace = namespace
		data, errorMessage := execute(req)
		if errorMessage != "" {
			t.Fatalf("%s failed: %s", req.Operation, errorMessage)
		}
		return data
	}

	installed, ok := run(request{
		Operation: "install", ReleaseName: releaseName, Chart: chartDir,
		Values: map[string]any{"setting": "installed"}, CreateNamespace: true,
	}).(map[string]any)
	if !ok || installed["name"] != releaseName || installed["namespace"] != namespace {
		t.Fatalf("unexpected installed release: %#v", installed)
	}

	listed, ok := run(request{Operation: "list", AllNamespaces: true}).([]map[string]any)
	if !ok || !containsRelease(listed, releaseName) {
		t.Fatalf("installed release missing from list: %#v", listed)
	}

	status, ok := run(request{Operation: "status", ReleaseName: releaseName}).(map[string]any)
	if !ok || status["status"] != "deployed" {
		t.Fatalf("unexpected release status: %#v", status)
	}

	values, ok := run(request{Operation: "values", ReleaseName: releaseName}).(map[string]any)
	if !ok || values["setting"] != "installed" {
		t.Fatalf("unexpected installed values: %#v", values)
	}

	upgraded, ok := run(request{
		Operation: "upgrade", ReleaseName: releaseName, Chart: chartDir,
		Values: map[string]any{"setting": "upgraded"},
	}).(map[string]any)
	if !ok || upgraded["revision"] != "2" {
		t.Fatalf("unexpected upgraded release: %#v", upgraded)
	}

	history, ok := run(request{Operation: "history", ReleaseName: releaseName}).([]map[string]any)
	if !ok || len(history) != 2 {
		t.Fatalf("expected two release revisions, got %#v", history)
	}

	run(request{Operation: "rollback", ReleaseName: releaseName, Revision: 1})
	rolledBack, ok := run(request{Operation: "status", ReleaseName: releaseName}).(map[string]any)
	if !ok || rolledBack["revision"] != "3" {
		t.Fatalf("unexpected release after rollback: %#v", rolledBack)
	}
	values, ok = run(request{Operation: "values", ReleaseName: releaseName}).(map[string]any)
	if !ok || values["setting"] != "installed" {
		t.Fatalf("rollback did not restore revision 1 values: %#v", values)
	}

	run(request{Operation: "uninstall", ReleaseName: releaseName})
	listed, ok = run(request{Operation: "list", AllNamespaces: true}).([]map[string]any)
	if !ok || containsRelease(listed, releaseName) {
		t.Fatalf("uninstalled release remains in list: %#v", listed)
	}
}

func containsRelease(releases []map[string]any, name string) bool {
	for _, release := range releases {
		if release["name"] == name {
			return true
		}
	}
	return false
}

func TestExecuteInstallAndUpgradeRejectUnlocatableCharts(t *testing.T) {
	for _, operation := range []string{"install", "upgrade"} {
		t.Run(operation, func(t *testing.T) {
			_, message := execute(request{
				Operation: operation, ReleaseName: "web", Chart: filepath.Join(t.TempDir(), "missing-chart"),
			})
			if message == "" {
				t.Fatal("expected missing chart to fail")
			}
		})
	}
}

func TestExecuteRejectsInvalidRepositoryOperations(t *testing.T) {
	baseRequest := request{
		RepositoryConfig: filepath.Join(t.TempDir(), "repositories.yaml"),
		RepositoryCache:  filepath.Join(t.TempDir(), "cache"),
	}
	t.Run("add requires name and url", func(t *testing.T) {
		req := baseRequest
		req.Operation = "repoAdd"
		_, message := execute(req)
		if message != "repository name and URL are required" {
			t.Fatalf("got %q", message)
		}
	})
	t.Run("remove requires an existing name", func(t *testing.T) {
		req := baseRequest
		req.Operation = "repoRemove"
		req.RepositoryName = "missing"
		_, message := execute(req)
		if message != "repository does not exist: missing" {
			t.Fatalf("got %q", message)
		}
	})
}

func requireActionError(t *testing.T, _ any, message string) {
	t.Helper()
	if message == "" {
		t.Fatal("expected action to fail for an unknown release")
	}
}

func TestExecuteAddsUpdatesListsAndRemovesRepository(t *testing.T) {
	requests := 0
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, _ *http.Request) {
		requests++
		w.Header().Set("Content-Type", "application/yaml")
		_, _ = w.Write([]byte("apiVersion: v1\nentries: {}\n"))
	}))
	defer server.Close()

	repositoryConfig := filepath.Join(t.TempDir(), "repositories.yaml")
	request := request{
		RepositoryConfig: repositoryConfig,
		RepositoryCache:  filepath.Join(t.TempDir(), "cache"),
	}

	request.Operation = "repoAdd"
	request.RepositoryName = "test"
	request.RepositoryURL = server.URL
	if _, errorMessage := execute(request); errorMessage != "" {
		t.Fatalf("repository add failed: %s", errorMessage)
	}

	request.Operation = "repoList"
	data, errorMessage := execute(request)
	if errorMessage != "" {
		t.Fatalf("repository list failed: %s", errorMessage)
	}
	repositories, ok := data.([]map[string]string)
	if !ok || len(repositories) != 1 || repositories[0]["name"] != "test" || repositories[0]["url"] != server.URL {
		t.Fatalf("unexpected repositories: %#v", data)
	}

	request.Operation = "repoUpdate"
	if _, errorMessage := execute(request); errorMessage != "" {
		t.Fatalf("repository update failed: %s", errorMessage)
	}
	if requests != 2 {
		t.Fatalf("expected add and update to fetch the index, got %d requests", requests)
	}

	request.Operation = "repoRemove"
	if _, errorMessage := execute(request); errorMessage != "" {
		t.Fatalf("repository remove failed: %s", errorMessage)
	}
	request.Operation = "repoList"
	data, errorMessage = execute(request)
	if errorMessage != "" {
		t.Fatalf("repository list after removal failed: %s", errorMessage)
	}
	if repositories, ok = data.([]map[string]string); !ok || len(repositories) != 0 {
		t.Fatalf("expected no remaining repositories, got %#v", data)
	}
}

func TestLoadRepositoryFileCreatesEmptyFileWhenMissing(t *testing.T) {
	file, err := loadRepositoryFile(filepath.Join(t.TempDir(), "missing.yaml"))
	if err != nil {
		t.Fatal(err)
	}
	if file == nil || file.Repositories == nil || len(file.Repositories) != 0 {
		t.Fatalf("expected initialized empty repository file, got %#v", file)
	}
}

func TestLoadRepositoryFileLoadsExistingFile(t *testing.T) {
	path := filepath.Join(t.TempDir(), "repositories.yaml")
	content := "apiVersion: v1\nrepositories:\n- name: stable\n  url: https://example.test\n"
	if err := os.WriteFile(path, []byte(content), 0o600); err != nil {
		t.Fatal(err)
	}

	file, err := loadRepositoryFile(path)
	if err != nil {
		t.Fatal(err)
	}
	if len(file.Repositories) != 1 || file.Repositories[0].Name != "stable" || file.Repositories[0].URL != "https://example.test" {
		t.Fatalf("unexpected repositories: %#v", file.Repositories)
	}
}

func TestLoadRepositoryFileReturnsParseErrors(t *testing.T) {
	path := filepath.Join(t.TempDir(), "repositories.yaml")
	if err := os.WriteFile(path, []byte("repositories: ["), 0o600); err != nil {
		t.Fatal(err)
	}
	if _, err := loadRepositoryFile(path); err == nil {
		t.Fatal("expected invalid YAML to return an error")
	}
}

func TestReleaseViewMapsReleaseData(t *testing.T) {
	deployed := time.Date(2026, time.January, 2, 3, 4, 5, 0, time.UTC)
	rel := &releasev1.Release{
		Name: "web", Namespace: "apps", Version: 4, Manifest: "rendered",
		Info:  &releasev1.Info{Status: common.StatusDeployed, LastDeployed: deployed},
		Chart: &chart.Chart{Metadata: &chart.Metadata{Name: "nginx", Version: "1.2.3", AppVersion: "2.0"}},
	}

	view, err := releaseView(rel)
	if err != nil {
		t.Fatal(err)
	}
	if view["name"] != "web" || view["namespace"] != "apps" || view["revision"] != "4" || view["status"] != "deployed" || view["chart"] != "nginx-1.2.3" || view["app_version"] != "2.0" || view["manifest"] != "rendered" {
		t.Fatalf("unexpected release view: %#v", view)
	}
	if !view["updated"].(time.Time).Equal(deployed) {
		t.Fatalf("unexpected updated time: %#v", view["updated"])
	}
}

func TestReleaseViewHandlesMissingOptionalData(t *testing.T) {
	view, err := releaseView(&releasev1.Release{Name: "web"})
	if err != nil {
		t.Fatal(err)
	}
	if view["chart"] != "-" || view["app_version"] != "" || view["status"] != "" || !view["updated"].(time.Time).IsZero() {
		t.Fatalf("unexpected view for release with no optional data: %#v", view)
	}
}

func TestReleaseViewRejectsUnexpectedType(t *testing.T) {
	_, err := releaseView("not a release")
	if err == nil || !strings.Contains(err.Error(), "unexpected Helm release type string") {
		t.Fatalf("expected type error, got %v", err)
	}
}
