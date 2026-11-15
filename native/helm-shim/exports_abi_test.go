//go:build helmshimtest && cgo

package main

import (
	"reflect"
	"testing"
)

func TestRequestFromCMapsUtf8ArraysOptionsAndFlags(t *testing.T) {
	got, err := testNativeRequestFromC()
	if err != nil {
		t.Fatal(err)
	}
	if got.ABIVersion != nativeABIVersion || got.Operation != "install" || got.ReleaseName != "web-雪" || got.Chart != "oci://charts.example/app" {
		t.Fatalf("unexpected request identity fields: %#v", got)
	}
	if !reflect.DeepEqual(got.ValuesFiles, []string{"prod-雪.yaml", "overrides.yaml"}) || got.ValuesJSON != `{"replicas":3,"enabled":true}` {
		t.Fatalf("request strings or arrays changed: %#v", got)
	}
	if !got.CreateNamespace || !got.Wait || got.TimeoutMilliseconds != 12345 || !got.AllNamespaces || got.Revision != 7 || !got.InstallIfMissing {
		t.Fatalf("request scalar fields changed: %#v", got)
	}
	if got.RepositoryURL != "https://example.test/雪" || got.RepositoryUsername != "user" || got.RepositoryPassword != "secret" {
		t.Fatalf("repository fields changed: %#v", got)
	}
	if got.Version != "1.2.3" || got.SearchTerm != "nginx" || got.VersionConstraint != ">=1.0" || got.Selector != "app=web" || got.DryRun != "server" {
		t.Fatalf("Helm option strings changed: %#v", got)
	}
	if got.Max != 12 || got.Offset != 3 || got.HistoryMax != 20 || got.WaitStrategy != "watcher" || got.KubeVersion != "1.31" {
		t.Fatalf("Helm option scalars changed: %#v", got)
	}
	if got.PostRendererArgs != nil || !reflect.DeepEqual(got.ApiVersions, []string{"v1", "apps/v1"}) {
		t.Fatalf("Helm option arrays changed: %#v", got)
	}
	if !got.AllValues || !got.Versions || !got.Devel || !got.Regexp || !got.FailOnNoResult || !got.Deployed || !got.Failed || !got.Pending || !got.Superseded || !got.Uninstalled || !got.Uninstalling || !got.Date || !got.ResetValues || !got.ReuseValues || !got.ResetThenReuseValues || !got.HideSecret || !got.DisableHooks || !got.WaitForJobs || !got.RollbackOnFailure || !got.Reverse || !got.DependencyUpdate || !got.SkipCrds || !got.SkipSchemaValidation || !got.DisableOpenApiValidation || !got.ForceUpdate || !got.Replace || !got.ForceReplace || !got.ForceConflicts || !got.TakeOwnership || !got.CleanupOnFail || !got.DryRunUninstall || !got.IgnoreNotFound || !got.KeepHistory || !got.InsecureSkipTlsVerify || !got.PassCredentials || !got.PlainHttp || !got.IncludeCrds || !got.SkipTests || !got.IsUpgrade {
		t.Fatalf("one or more ABI flags were not converted: %#v", got)
	}
}

func TestRequestFromCMapsNullStringsAndEmptyArrays(t *testing.T) {
	got, err := testEmptyNativeRequestFromC()
	if err != nil {
		t.Fatal(err)
	}
	if got.Operation != "" || got.Kubeconfig != "" || got.KubeContext != "" || got.ValuesFiles != nil || got.PostRendererArgs != nil || got.ApiVersions != nil {
		t.Fatalf("empty native fields were not mapped to empty Go values: %#v", got)
	}
	if got.AllValues || got.IsUpgrade || got.AllNamespaces || got.Wait {
		t.Fatalf("unset native flags were treated as enabled: %#v", got)
	}
}

func TestNativeRequestConversionRejectsNullAndInvalidArrays(t *testing.T) {
	if err := testRequestFromCNull(); err == nil || err.Error() != "native request is null" {
		t.Fatalf("expected null request error, got %v", err)
	}
	for _, field := range []string{"values_files", "post_renderer_args", "api_versions"} {
		if err := testRequestFromCWithInvalidArray(field); err == nil || err.Error() != "native string array is null" {
			t.Errorf("%s: expected null string array error, got %v", field, err)
		}
	}
	if err := testStringsFromCNullArray(); err == nil || err.Error() != "native string array is null" {
		t.Fatalf("expected null string array error, got %v", err)
	}
	if err := testStringsFromCTooLargeArray(); err == nil || err.Error() != "native string array is too large" {
		t.Fatalf("expected oversized string array error, got %v", err)
	}
}

func TestExportedExecuteValidatesAbiAndHandlesErrors(t *testing.T) {
	if got := testExecuteWithABIVersion(nativeABIVersion + 1); got != "unsupported Helm native ABI version: 2" {
		t.Fatalf("expected ABI version validation before reading arrays, got %q", got)
	}
	if got := testExecuteNullRequest(); got != "native request is null" {
		t.Fatalf("unexpected null-request response: %q", got)
	}
	if got := testExecuteUnsupportedOperation(); got != "unsupported operation: not-an-operation" {
		t.Fatalf("unexpected unsupported-operation response: %q", got)
	}
	testFreeNilResponse()
}

func TestNativeResponseAllocationSerializationAndFreeAcrossResultKinds(t *testing.T) {
	started := "2026-01-02T03:04:05Z"
	completed := "2026-01-02T03:04:06Z"
	release := nativeRelease{
		Name: "web", Namespace: "apps", Revision: 3, Updated: started, Status: "deployed",
		Chart: "nginx-1.2.3", AppVersion: "2.0", ChartName: "nginx", ChartVersion: "1.2.3",
		Description: "release description", Notes: "release notes", ResourcesJSON: `{"pods":["web"]}`,
		Hooks: []nativeHook{{
			Name: "smoke", Kind: "Pod", Path: "templates/hook.yaml", Manifest: "kind: Pod",
			Events: []string{"test"}, StartedAt: started, CompletedAt: completed,
			Phase: "Succeeded", Weight: 4, DeletePolicies: []string{"before-hook-creation"},
		}, {Name: "no-events"}},
	}
	cases := []struct {
		name     string
		response nativeResponse
	}{
		{name: "ack", response: nativeResponse{ABIVersion: nativeABIVersion, ResultKind: nativeResultAck}},
		{name: "error", response: nativeResponse{ABIVersion: nativeABIVersion, Error: "release not found"}},
		{name: "text", response: nativeResponse{ABIVersion: nativeABIVersion, ResultKind: nativeResultText, Text: "rendered yaml"}},
		{name: "json", response: nativeResponse{ABIVersion: nativeABIVersion, ResultKind: nativeResultJSON, JSON: `{"nested":{"enabled":true}}`}},
		{name: "release", response: nativeResponse{ABIVersion: nativeABIVersion, ResultKind: nativeResultRelease, Release: &release}},
		{name: "releases", response: nativeResponse{ABIVersion: nativeABIVersion, ResultKind: nativeResultReleases, Releases: []nativeRelease{release}}},
		{name: "repositories", response: nativeResponse{ABIVersion: nativeABIVersion, ResultKind: nativeResultRepositories, Repositories: []nativeRepository{{Name: "stable", URL: "https://charts.example"}}}},
		{name: "chart search", response: nativeResponse{ABIVersion: nativeABIVersion, ResultKind: nativeResultChartSearchResults, ChartSearchResults: []nativeChartSearchResult{{Reference: "stable/nginx", ChartName: "nginx", Version: "1.2.3", AppVersion: "2.0", Description: "Nginx chart"}}}},
	}
	for _, test := range cases {
		t.Run(test.name, func(t *testing.T) {
			got := testNativeResponseRoundTrip(test.response)
			if !reflect.DeepEqual(got, test.response) {
				t.Fatalf("C ABI round trip changed response:\n got: %#v\nwant: %#v", got, test.response)
			}
		})
	}
}
