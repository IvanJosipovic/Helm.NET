package main

import (
	"encoding/json"
	"fmt"
	"time"

	releasev1 "helm.sh/helm/v4/pkg/release/v1"
)

type nativeRequest struct {
	ABIVersion               uint32
	Operation                string
	ReleaseName              string
	Chart                    string
	ValuesFiles              []string
	ValuesJSON               string
	CreateNamespace          bool
	Wait                     bool
	TimeoutMilliseconds      int64
	Kubeconfig               string
	KubeContext              string
	Namespace                string
	AllNamespaces            bool
	Filter                   string
	Revision                 int
	InstallIfMissing         bool
	RepositoryConfig         string
	RepositoryCache          string
	RegistryConfig           string
	HelmDriver               string
	RepositoryName           string
	RepositoryURL            string
	RepositoryUsername       string
	RepositoryPassword       string
	Version                  string
	SearchTerm               string
	AllValues                bool
	Versions                 bool
	Devel                    bool
	Regexp                   bool
	VersionConstraint        string
	FailOnNoResult           bool
	Max                      int
	Offset                   int
	Selector                 string
	Deployed                 bool
	Failed                   bool
	Pending                  bool
	Superseded               bool
	Uninstalled              bool
	Uninstalling             bool
	Date                     bool
	ResetValues              bool
	ReuseValues              bool
	ResetThenReuseValues     bool
	DryRun                   string
	HideSecret               bool
	DisableHooks             bool
	PostRenderer             string
	PostRendererArgs         []string
	WaitForJobs              bool
	RollbackOnFailure        bool
	Reverse                  bool
	DependencyUpdate         bool
	SkipCrds                 bool
	SkipSchemaValidation     bool
	DisableOpenApiValidation bool
	Description              string
	ForceUpdate              bool
	WaitStrategy             string
	Replace                  bool
	ForceReplace             bool
	ForceConflicts           bool
	TakeOwnership            bool
	CleanupOnFail            bool
	HistoryMax               int
	DryRunUninstall          bool
	IgnoreNotFound           bool
	KeepHistory              bool
	Cascade                  string
	Repo                     string
	CaFile                   string
	CertFile                 string
	KeyFile                  string
	InsecureSkipTlsVerify    bool
	PassCredentials          bool
	PlainHttp                bool
	ApiVersions              []string
	KubeVersion              string
	IncludeCrds              bool
	SkipTests                bool
	IsUpgrade                bool
}

type nativeRelease struct {
	Name          string
	Namespace     string
	Revision      int32
	Updated       string
	Status        string
	Chart         string
	AppVersion    string
	ChartName     string
	ChartVersion  string
	Description   string
	Notes         string
	ResourcesJSON string
	Hooks         []nativeHook
}

type nativeHook struct {
	Name           string
	Kind           string
	Path           string
	Manifest       string
	Events         []string
	StartedAt      string
	CompletedAt    string
	Phase          string
	Weight         int32
	DeletePolicies []string
}

type nativeRepository struct {
	Name string
	URL  string
}

type nativeChartSearchResult struct {
	Reference   string
	ChartName   string
	Version     string
	AppVersion  string
	Description string
}

type nativeResponse struct {
	ABIVersion         uint32
	ResultKind         int32
	Error              string
	Text               string
	JSON               string
	Release            *nativeRelease
	Releases           []nativeRelease
	Repositories       []nativeRepository
	ChartSearchResults []nativeChartSearchResult
}

const nativeABIVersion = 1

const (
	nativeResultAck int32 = iota + 1
	nativeResultText
	nativeResultJSON
	nativeResultRelease
	nativeResultReleases
	nativeResultRepositories
	nativeResultChartSearchResults
)

func requestFromNative(input nativeRequest) (request, error) {
	if input.ABIVersion != nativeABIVersion {
		return request{}, fmt.Errorf("unsupported Helm native ABI version: %d", input.ABIVersion)
	}
	if input.Operation == "" {
		return request{}, fmt.Errorf("operation is required")
	}

	var values map[string]any
	if input.ValuesJSON != "" {
		if err := json.Unmarshal([]byte(input.ValuesJSON), &values); err != nil {
			return request{}, fmt.Errorf("invalid values JSON: %w", err)
		}
	}

	return request{
		Operation: input.Operation, ReleaseName: input.ReleaseName, Chart: input.Chart,
		ValuesFiles: input.ValuesFiles, Values: values, CreateNamespace: input.CreateNamespace,
		Wait: input.Wait, TimeoutMilliseconds: input.TimeoutMilliseconds,
		Kubeconfig: input.Kubeconfig, KubeContext: input.KubeContext, Namespace: input.Namespace,
		AllNamespaces: input.AllNamespaces, Filter: input.Filter, Revision: input.Revision,
		InstallIfMissing: input.InstallIfMissing, RepositoryConfig: input.RepositoryConfig,
		RepositoryCache: input.RepositoryCache, RegistryConfig: input.RegistryConfig,
		HelmDriver: input.HelmDriver, RepositoryName: input.RepositoryName,
		RepositoryURL: input.RepositoryURL, RepositoryUsername: input.RepositoryUsername,
		RepositoryPassword: input.RepositoryPassword,
		Version:            input.Version, SearchTerm: input.SearchTerm, AllValues: input.AllValues,
		Versions: input.Versions, Devel: input.Devel, Regexp: input.Regexp,
		VersionConstraint: input.VersionConstraint, FailOnNoResult: input.FailOnNoResult,
		Max: input.Max, Offset: input.Offset, Selector: input.Selector,
		Deployed: input.Deployed, Failed: input.Failed, Pending: input.Pending,
		Superseded: input.Superseded, Uninstalled: input.Uninstalled, Uninstalling: input.Uninstalling,
		Date: input.Date, ResetValues: input.ResetValues, ReuseValues: input.ReuseValues,
		ResetThenReuseValues: input.ResetThenReuseValues, DryRun: input.DryRun,
		HideSecret: input.HideSecret, DisableHooks: input.DisableHooks,
		PostRenderer: input.PostRenderer, PostRendererArgs: input.PostRendererArgs,
		WaitForJobs: input.WaitForJobs, RollbackOnFailure: input.RollbackOnFailure,
		Reverse: input.Reverse, DependencyUpdate: input.DependencyUpdate,
		SkipCrds: input.SkipCrds, SkipSchemaValidation: input.SkipSchemaValidation,
		DisableOpenApiValidation: input.DisableOpenApiValidation, Description: input.Description,
		ForceUpdate: input.ForceUpdate, WaitStrategy: input.WaitStrategy,
		Replace: input.Replace, ForceReplace: input.ForceReplace, ForceConflicts: input.ForceConflicts,
		TakeOwnership: input.TakeOwnership, CleanupOnFail: input.CleanupOnFail,
		HistoryMax: input.HistoryMax, DryRunUninstall: input.DryRunUninstall,
		IgnoreNotFound: input.IgnoreNotFound, KeepHistory: input.KeepHistory, Cascade: input.Cascade,
		Repo: input.Repo, CaFile: input.CaFile, CertFile: input.CertFile, KeyFile: input.KeyFile,
		InsecureSkipTlsVerify: input.InsecureSkipTlsVerify, PassCredentials: input.PassCredentials,
		PlainHttp: input.PlainHttp, ApiVersions: input.ApiVersions, KubeVersion: input.KubeVersion,
		IncludeCrds: input.IncludeCrds, SkipTests: input.SkipTests, IsUpgrade: input.IsUpgrade,
	}, nil
}

func responseFromResult(operation string, data any, errorMessage string) nativeResponse {
	response := nativeResponse{ABIVersion: nativeABIVersion}
	if errorMessage != "" {
		response.Error = errorMessage
		return response
	}

	switch operation {
	case "template", "showValues", "getManifest":
		text, ok := data.(string)
		if !ok {
			return invalidResult("expected text result")
		}
		response.ResultKind = nativeResultText
		response.Text = text
	case "install", "upgrade", "status":
		release, ok := nativeReleaseFromView(data)
		if !ok {
			return invalidResult("expected release result")
		}
		response.ResultKind = nativeResultRelease
		response.Release = &release
	case "list", "history":
		views, ok := data.([]map[string]any)
		if !ok {
			return invalidResult("expected release list result")
		}
		response.ResultKind = nativeResultReleases
		response.Releases = make([]nativeRelease, 0, len(views))
		for _, view := range views {
			release, ok := nativeReleaseFromView(view)
			if !ok {
				return invalidResult("invalid release in result list")
			}
			response.Releases = append(response.Releases, release)
		}
	case "values":
		encoded, err := json.Marshal(data)
		if err != nil {
			return invalidResult("failed to encode Helm response")
		}
		response.ResultKind = nativeResultJSON
		response.JSON = string(encoded)
	case "repoList":
		repositories, ok := data.([]map[string]string)
		if !ok {
			return invalidResult("expected repository list result")
		}
		response.ResultKind = nativeResultRepositories
		response.Repositories = make([]nativeRepository, 0, len(repositories))
		for _, repository := range repositories {
			response.Repositories = append(response.Repositories, nativeRepository{Name: repository["name"], URL: repository["url"]})
		}
	case "chartSearch":
		results, ok := data.([]map[string]any)
		if !ok {
			return invalidResult("expected chart search result")
		}
		response.ResultKind = nativeResultChartSearchResults
		response.ChartSearchResults = make([]nativeChartSearchResult, 0, len(results))
		for _, result := range results {
			response.ChartSearchResults = append(response.ChartSearchResults, nativeChartSearchResult{
				Reference: stringValue(result["reference"]), ChartName: stringValue(result["chartName"]),
				Version: stringValue(result["version"]), AppVersion: stringValue(result["app_version"]),
				Description: stringValue(result["description"]),
			})
		}
	case "rollback", "uninstall", "repoAdd", "repoRemove", "repoUpdate":
		response.ResultKind = nativeResultAck
	default:
		return invalidResult("unsupported operation: " + operation)
	}
	return response
}

func nativeReleaseFromView(data any) (nativeRelease, bool) {
	view, ok := data.(map[string]any)
	if !ok {
		return nativeRelease{}, false
	}
	updated, ok := view["updated"].(time.Time)
	if !ok {
		return nativeRelease{}, false
	}
	revision, ok := integerValue(view["revision"])
	if !ok {
		return nativeRelease{}, false
	}
	release := nativeRelease{
		Name: stringValue(view["name"]), Namespace: stringValue(view["namespace"]),
		Revision: int32(revision), Updated: formatTime(updated),
		Status: stringValue(view["status"]), Chart: stringValue(view["chart"]),
		AppVersion: stringValue(view["app_version"]), ChartName: stringValue(view["chartName"]),
		ChartVersion: stringValue(view["chartVersion"]), Description: stringValue(view["description"]),
		Notes: stringValue(view["notes"]),
	}
	if resources := view["resources"]; resources != nil {
		encoded, err := json.Marshal(resources)
		if err != nil {
			return nativeRelease{}, false
		}
		release.ResourcesJSON = string(encoded)
	}
	hooks, ok := view["hooks"].([]*releasev1.Hook)
	if ok {
		release.Hooks = make([]nativeHook, 0, len(hooks))
		for _, hook := range hooks {
			if hook == nil {
				continue
			}
			events := make([]string, 0, len(hook.Events))
			for _, event := range hook.Events {
				events = append(events, string(event))
			}
			deletePolicies := make([]string, 0, len(hook.DeletePolicies))
			for _, policy := range hook.DeletePolicies {
				deletePolicies = append(deletePolicies, string(policy))
			}
			release.Hooks = append(release.Hooks, nativeHook{
				Name: hook.Name, Kind: hook.Kind, Path: hook.Path, Manifest: hook.Manifest,
				Events: events, StartedAt: formatOptionalTime(hook.LastRun.StartedAt),
				CompletedAt: formatOptionalTime(hook.LastRun.CompletedAt), Phase: string(hook.LastRun.Phase),
				Weight: int32(hook.Weight), DeletePolicies: deletePolicies,
			})
		}
	}
	return release, true
}

func formatTime(value time.Time) string {
	if value.IsZero() {
		return ""
	}
	return value.Format(time.RFC3339Nano)
}

func formatOptionalTime(value time.Time) string {
	return formatTime(value)
}

func integerValue(value any) (int, bool) {
	switch number := value.(type) {
	case int:
		return number, true
	case int32:
		return int(number), true
	case int64:
		return int(number), true
	default:
		return 0, false
	}
}

func stringValue(value any) string {
	result, _ := value.(string)
	return result
}

func invalidResult(message string) nativeResponse {
	return nativeResponse{ABIVersion: nativeABIVersion, Error: message}
}
