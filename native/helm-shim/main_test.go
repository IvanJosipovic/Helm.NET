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

func TestHandleJSONRejectsMalformedInput(t *testing.T) {
	var got result
	if err := json.Unmarshal(handleJSON([]byte("{")), &got); err != nil {
		t.Fatal(err)
	}
	if got.Error == "" {
		t.Fatal("expected a JSON parsing error")
	}
}

func TestHandleJSONReturnsUnsupportedOperation(t *testing.T) {
	var got result
	if err := json.Unmarshal(handleJSON([]byte(`{"operation":"not-an-operation"}`)), &got); err != nil {
		t.Fatal(err)
	}
	if got.Error != "unsupported operation: not-an-operation" {
		t.Fatalf("unexpected error: %q", got.Error)
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
