package main

import (
	"encoding/json"
	"fmt"
	"time"
)

type nativeRequest struct {
	ABIVersion          uint32
	Operation           string
	ReleaseName         string
	Chart               string
	ValuesFiles         []string
	ValuesJSON          string
	CreateNamespace     bool
	Wait                bool
	TimeoutMilliseconds int64
	Kubeconfig          string
	KubeContext         string
	Namespace           string
	AllNamespaces       bool
	Filter              string
	Revision            int
	InstallIfMissing    bool
	RepositoryConfig    string
	RepositoryCache     string
	RegistryConfig      string
	HelmDriver          string
	RepositoryName      string
	RepositoryURL       string
	RepositoryUsername  string
	RepositoryPassword  string
}

type nativeRelease struct {
	Name       string
	Namespace  string
	Revision   string
	Updated    string
	Status     string
	Chart      string
	AppVersion string
}

type nativeRepository struct {
	Name string
	URL  string
}

type nativeResponse struct {
	ABIVersion   uint32
	ResultKind   int32
	Error        string
	Text         string
	JSON         string
	Release      *nativeRelease
	Releases     []nativeRelease
	Repositories []nativeRepository
}

const nativeABIVersion = 1

const (
	nativeResultAck int32 = iota + 1
	nativeResultText
	nativeResultJSON
	nativeResultRelease
	nativeResultReleases
	nativeResultRepositories
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
	return nativeRelease{
		Name: stringValue(view["name"]), Namespace: stringValue(view["namespace"]),
		Revision: stringValue(view["revision"]), Updated: updated.Format(time.RFC3339Nano),
		Status: stringValue(view["status"]), Chart: stringValue(view["chart"]),
		AppVersion: stringValue(view["app_version"]),
	}, true
}

func stringValue(value any) string {
	result, _ := value.(string)
	return result
}

func invalidResult(message string) nativeResponse {
	return nativeResponse{ABIVersion: nativeABIVersion, Error: message}
}
