package main

import (
	"encoding/json"
	"net/http"
	"net/http/httptest"
	"os"
	"path/filepath"
	"reflect"
	"strings"
	"testing"
	"time"

	"helm.sh/helm/v4/pkg/action"
	chart "helm.sh/helm/v4/pkg/chart/v2"
	"helm.sh/helm/v4/pkg/kube"
	"helm.sh/helm/v4/pkg/release/common"
	releasev1 "helm.sh/helm/v4/pkg/release/v1"
	"k8s.io/apimachinery/pkg/apis/meta/v1/unstructured"
	"k8s.io/apimachinery/pkg/runtime"
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

func TestRequestFromNativeMapsHelmCliOptions(t *testing.T) {
	got, err := requestFromNative(nativeRequest{
		ABIVersion: nativeABIVersion, Operation: "upgrade", Version: "1.2.3", SearchTerm: "nginx",
		Versions: true, Devel: true, Regexp: true, VersionConstraint: ">=1.0", FailOnNoResult: true,
		Max: 12, Offset: 3, Selector: "app=web", Deployed: true, Failed: true, Pending: true,
		Superseded: true, Uninstalled: true, Uninstalling: true, Date: true, Reverse: true,
		ResetValues: true, ReuseValues: true, ResetThenReuseValues: true, DryRun: "server",
		HideSecret: true, DisableHooks: true, PostRenderer: "renderer", PostRendererArgs: []string{"--flag"},
		WaitForJobs: true, RollbackOnFailure: true, DependencyUpdate: true, SkipCrds: true,
		SkipSchemaValidation: true, DisableOpenApiValidation: true, Description: "description",
		ForceUpdate: true, WaitStrategy: "watcher", Replace: true, ForceReplace: true,
		ForceConflicts: true, TakeOwnership: true, CleanupOnFail: true, HistoryMax: 20,
		DryRunUninstall: true, IgnoreNotFound: true, KeepHistory: true, Cascade: "foreground",
		Repo: "https://charts.example", CaFile: "ca.pem", CertFile: "client.pem", KeyFile: "client.key",
		InsecureSkipTlsVerify: true, PassCredentials: true, PlainHttp: true,
		ApiVersions: []string{"v1", "apps/v1"}, KubeVersion: "1.31", IncludeCrds: true,
		SkipTests: true, IsUpgrade: true,
	})
	if err != nil {
		t.Fatal(err)
	}

	if got.Version != "1.2.3" || got.SearchTerm != "nginx" || !got.Versions || !got.Devel || !got.Regexp || got.VersionConstraint != ">=1.0" || !got.FailOnNoResult {
		t.Fatalf("chart search options were not preserved: %#v", got)
	}
	if got.Max != 12 || got.Offset != 3 || got.Selector != "app=web" || !got.Deployed || !got.Failed || !got.Pending || !got.Superseded || !got.Uninstalled || !got.Uninstalling || !got.Date || !got.Reverse {
		t.Fatalf("list options were not preserved: %#v", got)
	}
	if !got.ResetValues || !got.ReuseValues || !got.ResetThenReuseValues || got.DryRun != "server" || !got.HideSecret || !got.DisableHooks || got.PostRenderer != "renderer" || len(got.PostRendererArgs) != 1 {
		t.Fatalf("render/upgrade options were not preserved: %#v", got)
	}
	if !got.WaitForJobs || !got.RollbackOnFailure || !got.DependencyUpdate || !got.SkipCrds || !got.SkipSchemaValidation || !got.DisableOpenApiValidation || got.Description != "description" || !got.ForceUpdate || got.WaitStrategy != "watcher" {
		t.Fatalf("install options were not preserved: %#v", got)
	}
	if !got.Replace || !got.ForceReplace || !got.ForceConflicts || !got.TakeOwnership || !got.CleanupOnFail || got.HistoryMax != 20 || !got.DryRunUninstall || !got.IgnoreNotFound || !got.KeepHistory || got.Cascade != "foreground" {
		t.Fatalf("rollback/uninstall options were not preserved: %#v", got)
	}
	if got.Repo != "https://charts.example" || got.CaFile != "ca.pem" || got.CertFile != "client.pem" || got.KeyFile != "client.key" || !got.InsecureSkipTlsVerify || !got.PassCredentials || !got.PlainHttp || len(got.ApiVersions) != 2 || got.KubeVersion != "1.31" || !got.IncludeCrds || !got.SkipTests || !got.IsUpgrade {
		t.Fatalf("chart source/capability options were not preserved: %#v", got)
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
		"name": "web", "namespace": "apps", "revision": 3, "updated": deployed,
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
		{name: "chart search array", operation: "chartSearch", data: []map[string]any{{"reference": "stable/nginx", "chartName": "nginx", "version": "1.2.3", "app_version": "2.0", "description": "Nginx chart"}}, kind: nativeResultChartSearchResults},
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
					Name: "web", Namespace: "apps", Revision: 3,
					Updated: deployed.Format(time.RFC3339Nano), Status: "deployed",
					Chart: "nginx-1.2.3", AppVersion: "2.0",
				}
				if got.Release == nil || !reflect.DeepEqual(*got.Release, want) {
					t.Fatalf("got release %#v, want %#v", got.Release, want)
				}
			case nativeResultReleases:
				want := nativeRelease{
					Name: "web", Namespace: "apps", Revision: 3,
					Updated: deployed.Format(time.RFC3339Nano), Status: "deployed",
					Chart: "nginx-1.2.3", AppVersion: "2.0",
				}
				if len(got.Releases) != 1 || !reflect.DeepEqual(got.Releases[0], want) {
					t.Fatalf("got releases %#v, want one %#v", got.Releases, want)
				}
			case nativeResultRepositories:
				if len(got.Repositories) != 1 || got.Repositories[0].Name != "stable" || got.Repositories[0].URL != "https://example.test" {
					t.Fatalf("unexpected repositories result: %#v", got.Repositories)
				}
			case nativeResultChartSearchResults:
				if len(got.ChartSearchResults) != 1 || got.ChartSearchResults[0] != (nativeChartSearchResult{
					Reference: "stable/nginx", ChartName: "nginx", Version: "1.2.3",
					AppVersion: "2.0", Description: "Nginx chart",
				}) {
					t.Fatalf("unexpected chart search results: %#v", got.ChartSearchResults)
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

	searchResults := responseFromResult("chartSearch", []map[string]any{}, "")
	if searchResults.ResultKind != nativeResultChartSearchResults || searchResults.ChartSearchResults == nil || len(searchResults.ChartSearchResults) != 0 {
		t.Fatalf("expected an empty allocated chart search array, got %#v", searchResults)
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

func TestNewRegistryClientRejectsInvalidCABundle(t *testing.T) {
	caFile := filepath.Join(t.TempDir(), "invalid-ca.pem")
	if err := os.WriteFile(caFile, []byte("not a PEM certificate"), 0o600); err != nil {
		t.Fatal(err)
	}

	_, err := newRegistryClient(request{CaFile: caFile}, filepath.Join(t.TempDir(), "registry.json"))
	if err == nil || !strings.Contains(err.Error(), "can't parse registry CA file") {
		t.Fatalf("expected an invalid CA bundle error, got %v", err)
	}
}

func TestExecuteRequiresChartAndReleaseName(t *testing.T) {
	for _, operation := range []string{"template", "install", "upgrade", "showValues"} {
		t.Run(operation+" requires chart", func(t *testing.T) {
			_, got := execute(request{Operation: operation, ReleaseName: "web"})
			if got != "chart is required" {
				t.Fatalf("got %q", got)
			}
		})
	}
	for _, operation := range []string{"template", "install", "upgrade"} {
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

func TestExecuteTemplateSupportsCliRenderingOptions(t *testing.T) {
	chartDir := filepath.Join(t.TempDir(), "chart")
	for _, directory := range []string{filepath.Join(chartDir, "templates"), filepath.Join(chartDir, "crds")} {
		if err := os.MkdirAll(directory, 0o700); err != nil {
			t.Fatal(err)
		}
	}
	if err := os.WriteFile(filepath.Join(chartDir, "Chart.yaml"), []byte("apiVersion: v2\nname: example\nversion: 1.0.0\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	template := "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}\n  namespace: {{ .Release.Namespace }}\ndata:\n  customApi: {{ .Capabilities.APIVersions.Has \"example.io/v1\" | quote }}\n  kubeVersion: {{ .Capabilities.KubeVersion.Version | quote }}\n  upgrade: {{ .Release.IsUpgrade | quote }}\n"
	if err := os.WriteFile(filepath.Join(chartDir, "templates", "configmap.yaml"), []byte(template), 0o600); err != nil {
		t.Fatal(err)
	}
	hook := "apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}-test\n  annotations:\n    helm.sh/hook: test\n"
	if err := os.WriteFile(filepath.Join(chartDir, "templates", "test-hook.yaml"), []byte(hook), 0o600); err != nil {
		t.Fatal(err)
	}
	crd := "apiVersion: apiextensions.k8s.io/v1\nkind: CustomResourceDefinition\nmetadata:\n  name: widgets.example.io\nspec:\n  group: example.io\n  scope: Namespaced\n  names:\n    plural: widgets\n    singular: widget\n    kind: Widget\n  versions:\n  - name: v1\n    served: true\n    storage: true\n    schema:\n      openAPIV3Schema:\n        type: object\n"
	if err := os.WriteFile(filepath.Join(chartDir, "crds", "widgets.yaml"), []byte(crd), 0o600); err != nil {
		t.Fatal(err)
	}

	base := request{Operation: "template", ReleaseName: "web", Chart: chartDir, Namespace: "apps", ApiVersions: []string{"example.io/v1"}, KubeVersion: "1.30.0", IncludeCrds: true, IsUpgrade: true}
	data, message := execute(base)
	if message != "" {
		t.Fatalf("template failed: %s", message)
	}
	manifest := data.(string)
	for _, expected := range []string{"namespace: apps", `customApi: "true"`, `kubeVersion: "v1.30.0"`, `upgrade: "true"`, "kind: CustomResourceDefinition", "name: web-test"} {
		if !strings.Contains(manifest, expected) {
			t.Errorf("template output missing %q:\n%s", expected, manifest)
		}
	}

	base.SkipTests = true
	data, message = execute(base)
	if message != "" {
		t.Fatalf("template with skipTests failed: %s", message)
	}
	manifest = data.(string)
	if strings.Contains(manifest, "name: web-test") {
		t.Fatalf("test hook was rendered despite skipTests:\n%s", manifest)
	}
	if !strings.Contains(manifest, "kind: CustomResourceDefinition") {
		t.Fatalf("CRD was omitted despite includeCrds:\n%s", manifest)
	}
}

func TestExecuteValidatesWaitAndCascadeOptions(t *testing.T) {
	if _, message := execute(request{Operation: "rollback", WaitStrategy: "not-a-strategy"}); message != "waitStrategy must be one of watcher, hookOnly, or legacy" {
		t.Fatalf("unexpected wait strategy error: %q", message)
	}
	if _, message := execute(request{Operation: "uninstall", Cascade: "invalid"}); message != "cascade must be one of background, foreground, or orphan" {
		t.Fatalf("unexpected cascade error: %q", message)
	}
}

func TestRequestedWaitStrategyMatchesHelmCliValues(t *testing.T) {
	for _, test := range []struct {
		name     string
		request  request
		expected kube.WaitStrategy
	}{
		{name: "omitted wait defaults hook-only", request: request{}, expected: kube.HookOnlyStrategy},
		{name: "boolean wait defaults watcher", request: request{Wait: true}, expected: kube.StatusWatcherStrategy},
		{name: "explicit legacy strategy", request: request{WaitStrategy: "legacy"}, expected: kube.LegacyStrategy},
		{name: "explicit hook-only strategy", request: request{WaitStrategy: "hookOnly"}, expected: kube.HookOnlyStrategy},
	} {
		t.Run(test.name, func(t *testing.T) {
			if actual := requestedWaitStrategy(test.request); actual != test.expected {
				t.Fatalf("got %q, want %q", actual, test.expected)
			}
		})
	}
}

func TestApplyChartPathOptions(t *testing.T) {
	options := &action.ChartPathOptions{}
	applyChartPathOptions(options, request{
		Repo: "https://charts.example", RepositoryUsername: "user", RepositoryPassword: "password",
		CaFile: "ca.pem", CertFile: "client.pem", KeyFile: "client.key",
		InsecureSkipTlsVerify: true, PassCredentials: true, PlainHttp: true,
	})
	if options.RepoURL != "https://charts.example" || options.Username != "user" || options.Password != "password" || options.CaFile != "ca.pem" || options.CertFile != "client.pem" || options.KeyFile != "client.key" || !options.InsecureSkipTLSVerify || !options.PassCredentialsAll || !options.PlainHTTP {
		t.Fatalf("chart path options were not mapped from the request: %+v", options)
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

func TestExecuteListsReleasesAcrossNamespacesInKindCluster(t *testing.T) {
	if os.Getenv("HELM_SHIM_TEST_CLUSTER") != "true" {
		t.Skip("requires the Kind cluster configured by go-test CI")
	}

	chartDir := filepath.Join(t.TempDir(), "chart")
	if err := os.MkdirAll(filepath.Join(chartDir, "templates"), 0o700); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(chartDir, "Chart.yaml"), []byte("apiVersion: v2\nname: integration\nversion: 1.0.0\n"), 0o600); err != nil {
		t.Fatal(err)
	}
	if err := os.WriteFile(filepath.Join(chartDir, "templates", "configmap.yaml"), []byte("apiVersion: v1\nkind: ConfigMap\nmetadata:\n  name: {{ .Release.Name }}\n"), 0o600); err != nil {
		t.Fatal(err)
	}

	const releaseName = "all-namespaces-regression"
	const otherNamespace = "helm-shim-list-regression"
	namespaces := []string{"default", "kube-system", otherNamespace}
	t.Cleanup(func() {
		for _, namespace := range namespaces {
			_, _ = execute(request{Operation: "uninstall", Namespace: namespace, ReleaseName: releaseName})
		}
	})
	for _, namespace := range namespaces {
		if _, err := execute(request{
			Operation: "install", Namespace: namespace, ReleaseName: releaseName,
			Chart: chartDir, CreateNamespace: namespace == otherNamespace,
		}); err != "" {
			t.Fatalf("install in %s failed: %s", namespace, err)
		}
	}
	// The configured namespace is intentionally empty. AllNamespaces must change
	// the storage namespace as well as the action option.
	all, err := execute(request{Operation: "list", Namespace: "helm-shim-empty-list", AllNamespaces: true, Filter: "^" + releaseName + "$"})
	if err != "" {
		t.Fatalf("all-namespace list failed: %s", err)
	}
	allReleases, ok := all.([]map[string]any)
	if !ok || len(allReleases) != len(namespaces) {
		t.Fatalf("expected one matching release in each namespace, got %#v", all)
	}
	seen := make(map[string]bool, len(namespaces))
	for _, release := range allReleases {
		if release["name"] != releaseName {
			t.Errorf("unexpected release name: %#v", release)
		}
		seen[release["namespace"].(string)] = true
	}
	for _, namespace := range namespaces {
		if !seen[namespace] {
			t.Errorf("all-namespace list omitted namespace %q: %#v", namespace, allReleases)
		}
	}

	// Without AllNamespaces, the same request remains scoped to the configured namespace.
	scoped, err := execute(request{Operation: "list", Namespace: "kube-system", Filter: "^" + releaseName + "$"})
	if err != "" {
		t.Fatalf("namespace-scoped list failed: %s", err)
	}
	scopedReleases, ok := scoped.([]map[string]any)
	if !ok || len(scopedReleases) != 1 || scopedReleases[0]["namespace"] != "kube-system" {
		t.Fatalf("expected only kube-system release, got %#v", scoped)
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
	if !ok || upgraded["revision"] != 2 {
		t.Fatalf("unexpected upgraded release: %#v", upgraded)
	}

	history, ok := run(request{Operation: "history", ReleaseName: releaseName}).([]map[string]any)
	if !ok || len(history) != 2 {
		t.Fatalf("expected two release revisions, got %#v", history)
	}

	run(request{Operation: "rollback", ReleaseName: releaseName, Revision: 1})
	rolledBack, ok := run(request{Operation: "status", ReleaseName: releaseName}).(map[string]any)
	if !ok || rolledBack["revision"] != 3 {
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
		Info: &releasev1.Info{
			Status: common.StatusDeployed, LastDeployed: deployed, Description: "release description", Notes: "release notes",
			Resources: map[string][]runtime.Object{"v1/Pod": {&unstructured.Unstructured{Object: map[string]any{"metadata": map[string]any{"name": "web"}}}}},
		},
		Chart: &chart.Chart{Metadata: &chart.Metadata{Name: "nginx", Version: "1.2.3", AppVersion: "2.0"}},
		Hooks: []*releasev1.Hook{{Name: "test-hook", Kind: "Pod", Events: []releasev1.HookEvent{releasev1.HookTest}}},
	}

	view, err := releaseView(rel)
	if err != nil {
		t.Fatal(err)
	}
	if view["name"] != "web" || view["namespace"] != "apps" || view["revision"] != 4 || view["status"] != "deployed" || view["chart"] != "nginx-1.2.3" || view["chartName"] != "nginx" || view["chartVersion"] != "1.2.3" || view["app_version"] != "2.0" {
		t.Fatalf("unexpected release view: %#v", view)
	}
	if !view["updated"].(time.Time).Equal(deployed) {
		t.Fatalf("unexpected updated time: %#v", view["updated"])
	}
	if view["description"] != "release description" || view["notes"] != "release notes" || len(view["hooks"].([]*releasev1.Hook)) != 1 {
		t.Fatalf("release detail fields missing: %#v", view)
	}
	encoded, err := json.Marshal(view)
	if err != nil {
		t.Fatalf("failed to encode projected release: %v", err)
	}
	var payload map[string]any
	if err := json.Unmarshal(encoded, &payload); err != nil {
		t.Fatalf("failed to decode projected release: %v", err)
	}
	resources := payload["resources"].(map[string]any)
	pods := resources["v1/Pod"].([]any)
	if pods[0].(map[string]any)["metadata"].(map[string]any)["name"] != "web" {
		t.Fatalf("resource objects did not survive JSON projection: %s", encoded)
	}
	hooks := payload["hooks"].([]any)
	if hooks[0].(map[string]any)["events"].([]any)[0] != "test" {
		t.Fatalf("hook details did not survive JSON projection: %s", encoded)
	}
}

func TestReleaseViewHandlesMissingOptionalData(t *testing.T) {
	view, err := releaseView(&releasev1.Release{Name: "web"})
	if err != nil {
		t.Fatal(err)
	}
	if view["chart"] != "-" || view["app_version"] != "" || view["status"] != "" || view["description"] != "" || view["notes"] != "" || !view["updated"].(time.Time).IsZero() {
		t.Fatalf("unexpected view for release with no optional data: %#v", view)
	}
}

func TestReleaseViewRejectsUnexpectedType(t *testing.T) {
	_, err := releaseView("not a release")
	if err == nil || !strings.Contains(err.Error(), "unexpected Helm release type string") {
		t.Fatalf("expected type error, got %v", err)
	}
}
