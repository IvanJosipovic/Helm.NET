package main

import (
	"context"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"reflect"
	"sort"
	"strings"
	"sync/atomic"
	"testing"
	"time"

	"helm.sh/helm/v4/pkg/action"
	chartci "helm.sh/helm/v4/pkg/chart"
	chartcommon "helm.sh/helm/v4/pkg/chart/common"
	chart "helm.sh/helm/v4/pkg/chart/v2"
	chartloader "helm.sh/helm/v4/pkg/chart/v2/loader"
	chartutil "helm.sh/helm/v4/pkg/chart/v2/util"
	"helm.sh/helm/v4/pkg/cli"
	"helm.sh/helm/v4/pkg/registry"
	repo "helm.sh/helm/v4/pkg/repo/v1"
)

func chartCatalogSettings(t *testing.T, withRepositories bool) *cli.EnvSettings {
	t.Helper()
	root := t.TempDir()
	settings := cli.New()
	settings.RepositoryConfig = filepath.Join(root, "repositories.yaml")
	settings.RepositoryCache = filepath.Join(root, "cache")
	if err := os.MkdirAll(settings.RepositoryCache, 0o700); err != nil {
		t.Fatal(err)
	}
	file := repo.NewFile()
	if withRepositories {
		file.Add(&repo.Entry{Name: "stable", URL: "https://stable.example"},
			&repo.Entry{Name: "stable-extra", URL: "https://extra.example"})
	}
	if err := file.WriteFile(settings.RepositoryConfig, 0o600); err != nil {
		t.Fatal(err)
	}
	return settings
}

func writeChartCatalogIndex(t *testing.T, settings *cli.EnvSettings, repositoryName string, chartVersions map[string][]string) {
	t.Helper()
	index := repo.NewIndexFile()
	for chartName, versions := range chartVersions {
		for _, version := range versions {
			index.Entries[chartName] = append(index.Entries[chartName], &repo.ChartVersion{
				Metadata: &chart.Metadata{APIVersion: "v2", Name: chartName, Version: version, AppVersion: "app-" + version, Description: chartName + " description", Keywords: []string{"web-server"}},
				URLs:     []string{"https://example.invalid/" + chartName + "-" + version + ".tgz"},
			})
		}
	}
	path := filepath.Join(settings.RepositoryCache, repositoryName+"-index.yaml")
	if err := index.WriteFile(path, 0o600); err != nil {
		t.Fatal(err)
	}
}

func TestChartSearchUsesCachedIndexes(t *testing.T) {
	settings := chartCatalogSettings(t, true)
	writeChartCatalogIndex(t, settings, "stable", map[string][]string{
		"nginx": {"1.0.0", "2.0.0", "2.1.0-rc.1"},
		"redis": {"7.0.0"},
	})
	writeChartCatalogIndex(t, settings, "stable-extra", map[string][]string{
		"nginx": {"3.0.0"},
	})

	data, message := executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "ngi"}, settings, nil, time.Second)
	if message != "" {
		t.Fatalf("searching cached charts failed: %s", message)
	}
	search := data.([]map[string]any)
	if len(search) != 2 {
		t.Fatalf("expected latest stable version per matching chart, got %#v", search)
	}
	if got := catalogStrings(search); !reflect.DeepEqual(sortedCatalogStrings(got), []string{
		"stable-extra/nginx@3.0.0", "stable/nginx@2.0.0",
	}) {
		t.Fatalf("unexpected search results: %#v", got)
	}
	data, message = executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "ngi", Versions: true}, settings, nil, time.Second)
	if message != "" || len(data.([]map[string]any)) != 3 {
		t.Fatalf("expected all matching versions with versions enabled, got %#v, %q", data, message)
	}
	data, message = executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "^nginx", Regexp: true, VersionConstraint: ">=2.0.0 <3.0.0", Versions: true}, settings, nil, time.Second)
	if message != "" || !reflect.DeepEqual(sortedCatalogStrings(catalogStrings(data.([]map[string]any))), []string{"stable/nginx@2.0.0"}) {
		t.Fatalf("expected regexp and semantic version filters to select nginx 2.0.0, got %#v, %q", data, message)
	}
	data, message = executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "nginx", Devel: true, Versions: true}, settings, nil, time.Second)
	if message != "" || len(data.([]map[string]any)) != 4 {
		t.Fatalf("expected development version when devel is enabled, got %#v, %q", data, message)
	}
	_, message = executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "missing", FailOnNoResult: true}, settings, nil, time.Second)
	if message != "no results found" {
		t.Fatalf("expected fail-on-no-result error, got %q", message)
	}
	for _, item := range search {
		if item["description"] != item["chartName"].(string)+" description" {
			t.Fatalf("chart description missing from search result: %#v", item)
		}
		if item["app_version"] != "app-"+item["version"].(string) {
			t.Fatalf("chart app version missing from search result: %#v", item)
		}
	}
	data, message = executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "web-server", Versions: true}, settings, nil, time.Second)
	if message != "" || len(data.([]map[string]any)) == 0 {
		t.Fatalf("expected keyword search to match charts: %#v, %q", data, message)
	}
	data, message = executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "stable-extra", Versions: true}, settings, nil, time.Second)
	if message != "" || !reflect.DeepEqual(sortedCatalogStrings(catalogStrings(data.([]map[string]any))), []string{"stable-extra/nginx@3.0.0"}) {
		t.Fatalf("expected repository-name search to match stable-extra charts: %#v, %q", data, message)
	}
	data, message = executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "description", Versions: true}, settings, nil, time.Second)
	if message != "" || len(data.([]map[string]any)) != 4 {
		t.Fatalf("expected description search to match every chart version: %#v, %q", data, message)
	}
	data, message = executeAction(context.Background(), request{Operation: "chartSearch"}, settings, nil, time.Second)
	if message != "" || len(data.([]map[string]any)) != 3 {
		t.Fatalf("expected empty query to list latest stable charts: %#v, %q", data, message)
	}
}

func TestShowValuesReturnsChartDefaults(t *testing.T) {
	settings := chartCatalogSettings(t, false)
	chartPath := filepath.Join(t.TempDir(), "chart")
	if err := os.MkdirAll(chartPath, 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(chartPath, "Chart.yaml"), []byte("apiVersion: v2\nname: defaults\nversion: 1.2.3\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	const expected = "replicas: 2\nimage:\n  tag: stable\n"
	if err := os.WriteFile(filepath.Join(chartPath, "values.yaml"), []byte(expected), 0o600); err != nil {
		t.Fatal(err)
	}

	data, message := executeAction(context.Background(), request{Operation: "showValues", Chart: chartPath}, settings, action.NewConfiguration(), time.Second)
	if message != "" {
		t.Fatalf("showing chart defaults failed: %s", message)
	}
	if got, ok := data.(string); !ok || got != expected+"\n" {
		t.Fatalf("expected original values.yaml text, got %#v", data)
	}
}

func TestShowValuesFromRepositoryUsesRequestedVersion(t *testing.T) {
	settings := versionTestRepository(t)

	data, message := executeAction(context.Background(), request{
		Operation: "showValues", Chart: "test/example", Version: "2.0.0",
	}, settings, action.NewConfiguration(), time.Second)
	if message != "" {
		t.Fatalf("showing repository chart defaults failed: %s", message)
	}
	if got, ok := data.(string); !ok || !strings.Contains(got, "setting: default") {
		t.Fatalf("expected versioned repository values.yaml, got %#v", data)
	}
}

func TestShowValuesDownloadsAuthenticatedRepositoryChart(t *testing.T) {
	chartDirectory := t.TempDir()
	_, err := chartutil.Save(&chart.Chart{
		Metadata: &chart.Metadata{APIVersion: "v2", Name: "private", Version: "1.2.3"},
		Raw:      []*chartcommon.File{{Name: "values.yaml", Data: []byte("replicas: 2\n")}},
	}, chartDirectory)
	if err != nil {
		t.Fatal(err)
	}
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		username, password, ok := r.BasicAuth()
		if !ok || username != "chart-user" || password != "chart-password" {
			http.Error(w, "authentication required", http.StatusUnauthorized)
			return
		}
		http.FileServer(http.Dir(chartDirectory)).ServeHTTP(w, r)
	}))
	t.Cleanup(server.Close)
	index, err := repo.IndexDirectory(chartDirectory, server.URL)
	if err != nil {
		t.Fatal(err)
	}
	if err := index.WriteFile(filepath.Join(chartDirectory, "index.yaml"), 0o600); err != nil {
		t.Fatal(err)
	}

	settings := chartCatalogSettings(t, false)
	data, message := executeAction(context.Background(), request{
		Operation: "showValues", Chart: "private", Version: "1.2.3", Repo: server.URL,
		RepositoryUsername: "chart-user", RepositoryPassword: "chart-password",
	}, settings, action.NewConfiguration(), time.Second)
	if message != "" {
		t.Fatalf("showing authenticated repository chart defaults failed: %s", message)
	}
	if got, ok := data.(string); !ok || !strings.Contains(got, "replicas: 2") {
		t.Fatalf("expected authenticated chart defaults, got %#v", data)
	}
}

func TestShowValuesReportsLocateAndLoadFailures(t *testing.T) {
	settings := chartCatalogSettings(t, false)
	t.Run("chart cannot be located", func(t *testing.T) {
		_, message := executeAction(context.Background(), request{
			Operation: "showValues", Chart: filepath.Join(t.TempDir(), "missing-chart"),
		}, settings, action.NewConfiguration(), time.Second)
		if message == "" {
			t.Fatal("expected chart location failure")
		}
	})

	t.Run("chart cannot be loaded", func(t *testing.T) {
		chartPath := filepath.Join(t.TempDir(), "invalid-chart")
		if err := os.MkdirAll(chartPath, 0o700); err != nil {
			t.Fatal(err)
		}
		if err := os.WriteFile(filepath.Join(chartPath, "Chart.yaml"), []byte("not: [valid"), 0o600); err != nil {
			t.Fatal(err)
		}
		_, message := executeAction(context.Background(), request{
			Operation: "showValues", Chart: chartPath,
		}, settings, action.NewConfiguration(), time.Second)
		if message == "" {
			t.Fatal("expected chart loading failure")
		}
	})

	t.Run("OCI client is initialized before invalid reference is rejected", func(t *testing.T) {
		cfg := action.NewConfiguration()
		_, message := executeAction(context.Background(), request{
			Operation: "showValues", Chart: "oci://",
		}, settings, cfg, time.Second)
		if message == "" {
			t.Fatal("expected invalid OCI chart reference failure")
		}
		if cfg.RegistryClient == nil {
			t.Fatal("expected Helm registry client initialization")
		}
	})

	t.Run("OCI client initialization failure is returned", func(t *testing.T) {
		caFile := filepath.Join(t.TempDir(), "invalid-ca.pem")
		if err := os.WriteFile(caFile, []byte("not a PEM certificate"), 0o600); err != nil {
			t.Fatal(err)
		}
		_, message := executeAction(context.Background(), request{
			Operation: "showValues", Chart: "oci://registry.invalid/charts/web", CaFile: caFile,
		}, settings, action.NewConfiguration(), time.Second)
		if !strings.Contains(message, "can't parse registry CA file") {
			t.Fatalf("expected registry client configuration error, got %q", message)
		}
	})

	t.Run("existing OCI client is reused", func(t *testing.T) {
		client, err := registry.NewClient()
		if err != nil {
			t.Fatal(err)
		}
		cfg := action.NewConfiguration()
		cfg.RegistryClient = client
		_, message := executeAction(context.Background(), request{
			Operation: "showValues", Chart: "oci://",
		}, settings, cfg, time.Second)
		if message == "" {
			t.Fatal("expected invalid OCI chart reference failure")
		}
	})
}

func TestChartSearchHandlesEmptyMissingAndMalformedIndexes(t *testing.T) {
	t.Run("no configured repositories", func(t *testing.T) {
		settings := chartCatalogSettings(t, false)
		data, message := executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "nginx"}, settings, nil, time.Second)
		if message != "no repositories configured" {
			t.Fatalf("expected Helm CLI no-repositories error, got %#v, %q", data, message)
		}
	})

	t.Run("valid empty index", func(t *testing.T) {
		settings := chartCatalogSettings(t, true)
		writeChartCatalogIndex(t, settings, "stable", nil)
		writeChartCatalogIndex(t, settings, "stable-extra", nil)
		data, message := executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "nginx"}, settings, nil, time.Second)
		if message != "" || len(data.([]map[string]any)) != 0 {
			t.Fatalf("expected no search results from valid empty indexes, got %#v, %q", data, message)
		}
	})

	for _, test := range []struct {
		name    string
		content string
	}{
		{name: "missing", content: ""},
		{name: "malformed", content: "apiVersion: v1\nentries: [\n"},
	} {
		t.Run(test.name+" index", func(t *testing.T) {
			settings := chartCatalogSettings(t, true)
			if test.content != "" {
				if err := os.WriteFile(filepath.Join(settings.RepositoryCache, "stable-index.yaml"), []byte(test.content), 0o600); err != nil {
					t.Fatal(err)
				}
			}
			data, message := executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "nginx"}, settings, nil, time.Second)
			if message != "" || len(data.([]map[string]any)) != 0 {
				t.Fatalf("expected Helm CLI to skip missing or malformed repository index, got %#v, %q", data, message)
			}
		})
	}
}

func TestRepoAddRequiresForceToReplaceExistingRepository(t *testing.T) {
	var requests atomic.Int32
	server := httptest.NewServer(http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		requests.Add(1)
		_, _ = w.Write([]byte("apiVersion: v1\nentries: {}\n"))
	}))
	defer server.Close()
	settings := chartCatalogSettings(t, false)
	file := repo.NewFile()
	file.Add(&repo.Entry{Name: "stable", URL: "https://old.example"})
	if err := file.WriteFile(settings.RepositoryConfig, 0o600); err != nil {
		t.Fatal(err)
	}
	call := func(url string, force bool) string {
		_, message := executeAction(context.Background(), request{Operation: "repoAdd", RepositoryName: "stable", RepositoryURL: url, ForceUpdate: force}, settings, nil, time.Second)
		return message
	}
	if message := call("https://old.example", false); message != "" || requests.Load() != 0 {
		t.Fatalf("same repository should be a no-op without downloading: error=%q requests=%d", message, requests.Load())
	}
	if message := call("https://different.example", false); !strings.Contains(message, "already exists") || requests.Load() != 0 {
		t.Fatalf("different repository should require force update: error=%q requests=%d", message, requests.Load())
	}
	unchanged, err := repo.LoadFile(settings.RepositoryConfig)
	if err != nil || unchanged.Get("stable").URL != "https://old.example" {
		t.Fatalf("rejected update changed the existing repository: %#v, %v", unchanged, err)
	}
	if message := call(server.URL, true); message != "" || requests.Load() == 0 {
		t.Fatalf("force update should replace existing repository: error=%q requests=%d", message, requests.Load())
	}
	updated, err := repo.LoadFile(settings.RepositoryConfig)
	if err != nil || updated.Get("stable").URL != server.URL {
		t.Fatalf("forced repository URL not persisted: %#v, %v", updated, err)
	}
}

func TestPrepareChartDependenciesUpdatesMissingLocalDependency(t *testing.T) {
	root := t.TempDir()
	chartPath := filepath.Join(root, "parent")
	dependencyPath := filepath.Join(root, "child")
	if err := os.MkdirAll(chartPath, 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.MkdirAll(dependencyPath, 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(chartPath, "Chart.yaml"), []byte("apiVersion: v2\nname: parent\nversion: 1.0.0\ndependencies:\n- name: child\n  version: 1.0.0\n  repository: file://../child\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(dependencyPath, "Chart.yaml"), []byte("apiVersion: v2\nname: child\nversion: 1.0.0\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	parent, err := chartloader.Load(chartPath)
	if err != nil {
		t.Fatal(err)
	}
	settings := cli.New()
	settings.RepositoryCache = filepath.Join(root, "repo-cache")
	settings.ContentCache = filepath.Join(root, "content-cache")
	settings.RepositoryConfig = filepath.Join(root, "repositories.yaml")

	if _, err := prepareChartDependencies(chartPath, parent, settings, nil, request{}); err == nil || !strings.Contains(err.Error(), "helm dependency build") {
		t.Fatalf("expected missing dependency failure when update is disabled, got %v", err)
	}

	updated, err := prepareChartDependencies(chartPath, parent, settings, nil, request{DependencyUpdate: true})
	if err != nil {
		t.Fatalf("dependency update failed: %v", err)
	}
	accessor, err := chartci.NewAccessor(updated)
	if err != nil {
		t.Fatal(err)
	}
	dependencies := accessor.Dependencies()
	if len(dependencies) != 1 {
		t.Fatalf("updated chart did not load its dependency: %#v", dependencies)
	}
	dependency, err := chartci.NewAccessor(dependencies[0])
	if err != nil || dependency.Name() != "child" {
		t.Fatalf("unexpected loaded dependency: %#v, %v", dependency, err)
	}
	if _, err := os.Stat(filepath.Join(chartPath, "charts", "child-1.0.0.tgz")); err != nil {
		t.Fatalf("dependency archive was not downloaded: %v", err)
	}
}

func catalogStrings(charts []map[string]any) []string {
	result := make([]string, len(charts))
	for i, item := range charts {
		result[i] = item["reference"].(string) + "@" + item["version"].(string)
	}
	return result
}

func sortedCatalogStrings(values []string) []string {
	sort.Strings(values)
	return values
}
