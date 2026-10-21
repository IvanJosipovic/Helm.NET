package main

import (
	"context"
	"os"
	"path/filepath"
	"reflect"
	"sort"
	"strings"
	"testing"
	"time"

	chart "helm.sh/helm/v4/pkg/chart/v2"
	"helm.sh/helm/v4/pkg/cli"
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
				Metadata: &chart.Metadata{APIVersion: "v2", Name: chartName, Version: version},
				URLs:     []string{"https://example.invalid/" + chartName + "-" + version + ".tgz"},
			})
		}
	}
	path := filepath.Join(settings.RepositoryCache, repositoryName+"-index.yaml")
	if err := index.WriteFile(path, 0o600); err != nil {
		t.Fatal(err)
	}
}

func TestChartListUsesCachedIndexesAndExactRepositoryNames(t *testing.T) {
	settings := chartCatalogSettings(t, true)
	writeChartCatalogIndex(t, settings, "stable", map[string][]string{
		"nginx": {"1.0.0", "2.0.0"},
		"redis": {"7.0.0"},
	})
	writeChartCatalogIndex(t, settings, "stable-extra", map[string][]string{
		"nginx": {"3.0.0"},
	})

	data, message := executeAction(context.Background(), request{Operation: "chartList"}, settings, nil, time.Second)
	if message != "" {
		t.Fatalf("listing all cached charts failed: %s", message)
	}
	all := data.([]map[string]any)
	if len(all) != 4 {
		t.Fatalf("expected all four chart versions, got %#v", all)
	}
	if got := catalogStrings(all); !reflect.DeepEqual(got, []string{
		"stable-extra/nginx@3.0.0", "stable/nginx@1.0.0", "stable/nginx@2.0.0", "stable/redis@7.0.0",
	}) {
		t.Fatalf("unexpected deterministic catalog order: %#v", got)
	}

	filtered, message := executeAction(context.Background(), request{Operation: "chartList", RepositoryName: "stable"}, settings, nil, time.Second)
	if message != "" {
		t.Fatalf("listing one cached repository failed: %s", message)
	}
	charts := filtered.([]map[string]any)
	if len(charts) != 3 {
		t.Fatalf("expected three stable versions, got %#v", charts)
	}
	for _, item := range charts {
		if !strings.HasPrefix(item["reference"].(string), "stable/") {
			t.Fatalf("exact repository filter included similarly named repository: %#v", item)
		}
	}

	search, message := executeAction(context.Background(), request{Operation: "chartSearch", SearchTerm: "ngi"}, settings, nil, time.Second)
	if message != "" {
		t.Fatalf("searching cached indexes failed: %s", message)
	}
	gotSearch := catalogStrings(search.([]map[string]any))
	sort.Strings(gotSearch)
	if !reflect.DeepEqual(gotSearch, []string{
		"stable-extra/nginx@3.0.0", "stable/nginx@1.0.0", "stable/nginx@2.0.0",
	}) {
		t.Fatalf("substring search behavior changed: %#v", gotSearch)
	}
}

func TestChartListHandlesEmptyUnknownMissingAndMalformedIndexes(t *testing.T) {
	t.Run("no configured repositories", func(t *testing.T) {
		settings := chartCatalogSettings(t, false)
		data, message := executeAction(context.Background(), request{Operation: "chartList"}, settings, nil, time.Second)
		if message != "" || len(data.([]map[string]any)) != 0 {
			t.Fatalf("expected empty catalog, got %#v, %q", data, message)
		}
	})

	t.Run("valid empty index", func(t *testing.T) {
		settings := chartCatalogSettings(t, true)
		writeChartCatalogIndex(t, settings, "stable", nil)
		writeChartCatalogIndex(t, settings, "stable-extra", nil)
		data, message := executeAction(context.Background(), request{Operation: "chartList"}, settings, nil, time.Second)
		if message != "" || len(data.([]map[string]any)) != 0 {
			t.Fatalf("expected empty catalog from valid empty indexes, got %#v, %q", data, message)
		}
	})

	t.Run("exact unknown repository", func(t *testing.T) {
		settings := chartCatalogSettings(t, true)
		_, message := executeAction(context.Background(), request{Operation: "chartList", RepositoryName: "stable-extra-suffix"}, settings, nil, time.Second)
		if !strings.Contains(message, `configured repository "stable-extra-suffix" was not found`) {
			t.Fatalf("expected actionable exact-name error, got %q", message)
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
			_, message := executeAction(context.Background(), request{Operation: "chartList", RepositoryName: "stable"}, settings, nil, time.Second)
			if !strings.Contains(message, `repository "stable"`) || !strings.Contains(message, "chart index") {
				t.Fatalf("expected repository-specific index error, got %q", message)
			}
		})
	}
}

func catalogStrings(charts []map[string]any) []string {
	result := make([]string, len(charts))
	for i, item := range charts {
		result[i] = item["reference"].(string) + "@" + item["version"].(string)
	}
	return result
}
