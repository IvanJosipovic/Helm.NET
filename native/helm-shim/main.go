package main

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"log/slog"
	"os"
	"path/filepath"
	"strconv"
	"strings"
	"sync"
	"time"

	"helm.sh/helm/v4/pkg/action"
	"helm.sh/helm/v4/pkg/chart/common"
	"helm.sh/helm/v4/pkg/chart/loader"
	chartloader "helm.sh/helm/v4/pkg/chart/v2/loader"
	"helm.sh/helm/v4/pkg/cli"
	"helm.sh/helm/v4/pkg/getter"
	"helm.sh/helm/v4/pkg/kube"
	releasev1 "helm.sh/helm/v4/pkg/release/v1"
	repo "helm.sh/helm/v4/pkg/repo/v1"
	"helm.sh/helm/v4/pkg/storage/driver"
)

type request struct {
	Operation           string         `json:"operation"`
	Kubeconfig          string         `json:"kubeconfig,omitempty"`
	KubeContext         string         `json:"kubeContext,omitempty"`
	Namespace           string         `json:"namespace,omitempty"`
	Chart               string         `json:"chart,omitempty"`
	ReleaseName         string         `json:"releaseName,omitempty"`
	ValuesFiles         []string       `json:"valuesFiles,omitempty"`
	Values              map[string]any `json:"values,omitempty"`
	Wait                bool           `json:"wait,omitempty"`
	CreateNamespace     bool           `json:"createNamespace,omitempty"`
	TimeoutMilliseconds int64          `json:"timeoutMilliseconds,omitempty"`
	AllNamespaces       bool           `json:"allNamespaces,omitempty"`
	Filter              string         `json:"filter,omitempty"`
	Revision            int            `json:"revision,omitempty"`
	InstallIfMissing    bool           `json:"installIfMissing,omitempty"`
	RepositoryConfig    string         `json:"repositoryConfig,omitempty"`
	RepositoryCache     string         `json:"repositoryCache,omitempty"`
	RegistryConfig      string         `json:"registryConfig,omitempty"`
	HelmDriver          string         `json:"helmDriver,omitempty"`
	RepositoryName      string         `json:"repositoryName,omitempty"`
	RepositoryURL       string         `json:"repositoryUrl,omitempty"`
	RepositoryUsername  string         `json:"repositoryUsername,omitempty"`
	RepositoryPassword  string         `json:"repositoryPassword,omitempty"`
	Version             string         `json:"version,omitempty"`
	SearchTerm          string         `json:"searchTerm,omitempty"`
}

type result struct {
	Data  any    `json:"data,omitempty"`
	Error string `json:"error,omitempty"`
}

var repositoryFileMu sync.Mutex

func handleJSON(input []byte) (encoded []byte) {
	response := result{}
	defer func() {
		if recovered := recover(); recovered != nil {
			response = result{Error: fmt.Sprintf("Helm native call panicked: %v", recovered)}
			encoded, _ = json.Marshal(response)
		}
	}()
	var req request
	if err := json.Unmarshal(input, &req); err != nil {
		response.Error = err.Error()
	} else {
		response.Data, response.Error = execute(req)
	}
	encoded, err := json.Marshal(response)
	if err != nil {
		return []byte(`{"error":"failed to encode Helm response"}`)
	}
	return encoded
}

func execute(req request) (any, string) {
	switch req.Operation {
	case "list", "template", "templateRelease", "getManifest", "install", "upgrade", "uninstall", "status", "history", "rollback", "values", "repoList", "chartSearch", "repoAdd", "repoRemove", "repoUpdate":
	default:
		return nil, "unsupported operation: " + req.Operation
	}
	if req.Operation == "template" || req.Operation == "install" || req.Operation == "upgrade" {
		if req.Chart == "" {
			return nil, "chart is required"
		}
		if req.ReleaseName == "" {
			return nil, "releaseName is required"
		}
	}
	if req.Namespace == "" {
		req.Namespace = "default"
	}
	settings := cli.New()
	if req.Kubeconfig != "" {
		settings.KubeConfig = req.Kubeconfig
	}
	if req.KubeContext != "" {
		settings.KubeContext = req.KubeContext
	}
	if req.RepositoryConfig != "" {
		settings.RepositoryConfig = req.RepositoryConfig
	}
	if req.RepositoryCache != "" {
		settings.RepositoryCache = req.RepositoryCache
	}
	if req.RegistryConfig != "" {
		settings.RegistryConfig = req.RegistryConfig
	}
	settings.SetNamespace(req.Namespace)
	cfg := action.NewConfiguration(action.ConfigurationSetLogger(slog.New(slog.NewTextHandler(os.Stderr, nil)).Handler()))
	helmDriver := req.HelmDriver
	if helmDriver == "" {
		helmDriver = os.Getenv("HELM_DRIVER")
	}
	storageNamespace := req.Namespace
	if req.Operation == "list" && req.AllNamespaces {
		storageNamespace = ""
	}
	if err := cfg.Init(settings.RESTClientGetter(), storageNamespace, helmDriver); err != nil {
		return nil, err.Error()
	}

	timeout := time.Duration(req.TimeoutMilliseconds) * time.Millisecond
	if timeout <= 0 {
		timeout = 5 * time.Minute
	}
	ctx, cancel := context.WithTimeout(context.Background(), timeout)
	defer cancel()

	switch req.Operation {
	case "repoList":
		repositoryFileMu.Lock()
		defer repositoryFileMu.Unlock()
		file, err := loadRepositoryFile(settings.RepositoryConfig)
		if err != nil {
			return nil, err.Error()
		}
		entries := make([]map[string]string, 0, len(file.Repositories))
		for _, entry := range file.Repositories {
			entries = append(entries, map[string]string{"name": entry.Name, "url": entry.URL})
		}
		return entries, ""
	case "chartSearch":
		if req.SearchTerm == "" {
			return nil, "searchTerm is required"
		}
		repositoryFileMu.Lock()
		defer repositoryFileMu.Unlock()
		file, err := loadRepositoryFile(settings.RepositoryConfig)
		if err != nil {
			return nil, err.Error()
		}
		term := strings.ToLower(req.SearchTerm)
		results := make([]map[string]any, 0)
		for _, entry := range file.Repositories {
			indexPath := filepath.Join(settings.RepositoryCache, entry.Name+"-index.yaml")
			index, err := repo.LoadIndexFile(indexPath)
			if err != nil {
				return nil, fmt.Sprintf("failed to load index for repository %s: %v", entry.Name, err)
			}
			for chartName, versions := range index.Entries {
				if !strings.Contains(strings.ToLower(chartName), term) {
					continue
				}
				for _, version := range versions {
					results = append(results, map[string]any{"reference": entry.Name + "/" + chartName, "chartName": chartName, "version": version.Version})
				}
			}
		}
		return results, ""
	case "templateRelease":
		get := action.NewGet(cfg)
		get.Version = req.Revision
		stored, err := get.Run(req.ReleaseName)
		if err != nil {
			return nil, err.Error()
		}
		rel, ok := stored.(*releasev1.Release)
		if !ok || rel == nil || rel.Chart == nil {
			return nil, fmt.Sprintf("unexpected Helm release type %T", stored)
		}
		install := action.NewInstall(cfg)
		install.ReleaseName = rel.Name
		install.Namespace = rel.Namespace
		install.DryRunStrategy = action.DryRunClient
		install.DisableHooks = true
		install.WaitStrategy = kube.HookOnlyStrategy
		rendered, err := install.RunWithContext(ctx, rel.Chart, rel.Config)
		if err != nil {
			return nil, err.Error()
		}
		return rendered.(*releasev1.Release).Manifest, ""
	case "getManifest":
		get := action.NewGet(cfg)
		get.Version = req.Revision
		stored, err := get.Run(req.ReleaseName)
		if err != nil {
			return nil, err.Error()
		}
		rel, ok := stored.(*releasev1.Release)
		if !ok || rel == nil {
			return nil, fmt.Sprintf("unexpected Helm release type %T", stored)
		}
		return rel.Manifest, ""
	case "repoAdd":
		if req.RepositoryName == "" || req.RepositoryURL == "" {
			return nil, "repository name and URL are required"
		}
		repositoryFileMu.Lock()
		defer repositoryFileMu.Unlock()
		file, err := loadRepositoryFile(settings.RepositoryConfig)
		if err != nil {
			return nil, err.Error()
		}
		entry := &repo.Entry{Name: req.RepositoryName, URL: req.RepositoryURL, Username: req.RepositoryUsername, Password: req.RepositoryPassword}
		chartRepo, err := repo.NewChartRepository(entry, getter.All(settings))
		if err != nil {
			return nil, err.Error()
		}
		chartRepo.CachePath = settings.RepositoryCache
		if _, err := chartRepo.DownloadIndexFile(); err != nil {
			return nil, err.Error()
		}
		file.Update(entry)
		if err := file.WriteFile(settings.RepositoryConfig, 0o600); err != nil {
			return nil, err.Error()
		}
		return map[string]bool{"succeeded": true}, ""
	case "repoRemove":
		repositoryFileMu.Lock()
		defer repositoryFileMu.Unlock()
		file, err := loadRepositoryFile(settings.RepositoryConfig)
		if err != nil {
			return nil, err.Error()
		}
		if !file.Remove(req.RepositoryName) {
			return nil, "repository does not exist: " + req.RepositoryName
		}
		if err := file.WriteFile(settings.RepositoryConfig, 0o600); err != nil {
			return nil, err.Error()
		}
		return map[string]bool{"succeeded": true}, ""
	case "repoUpdate":
		repositoryFileMu.Lock()
		defer repositoryFileMu.Unlock()
		file, err := loadRepositoryFile(settings.RepositoryConfig)
		if err != nil {
			return nil, err.Error()
		}
		for _, entry := range file.Repositories {
			chartRepo, err := repo.NewChartRepository(entry, getter.All(settings))
			if err != nil {
				return nil, err.Error()
			}
			chartRepo.CachePath = settings.RepositoryCache
			if _, err := chartRepo.DownloadIndexFile(); err != nil {
				return nil, err.Error()
			}
		}
		return map[string]bool{"succeeded": true}, ""
	case "list":
		list := action.NewList(cfg)
		list.All = true
		list.AllNamespaces = req.AllNamespaces
		list.Filter = req.Filter
		releases, err := list.Run()
		if err != nil {
			return nil, err.Error()
		}
		views := make([]map[string]any, 0, len(releases))
		for _, item := range releases {
			view, err := releaseView(item)
			if err != nil {
				return nil, err.Error()
			}
			views = append(views, view)
		}
		return views, ""
	case "template", "install", "upgrade":
		chartPathOptions := action.ChartPathOptions{}
		chartPathOptions.Version = req.Version
		chartPath, err := chartPathOptions.LocateChart(req.Chart, settings)
		if err != nil {
			return nil, err.Error()
		}
		ch, err := loader.Load(chartPath)
		if err != nil {
			return nil, err.Error()
		}
		values := map[string]any{}
		for _, file := range req.ValuesFiles {
			fileValues, err := common.ReadValuesFile(file)
			if err != nil {
				return nil, err.Error()
			}
			values = chartloader.MergeMaps(values, fileValues)
		}
		values = chartloader.MergeMaps(values, req.Values)
		var releaseResult any
		installMode := req.Operation == "install"
		if req.Operation == "upgrade" && req.InstallIfMissing {
			_, statusErr := action.NewStatus(cfg).Run(req.ReleaseName)
			if statusErr != nil {
				if !errors.Is(statusErr, driver.ErrReleaseNotFound) {
					return nil, statusErr.Error()
				}
				installMode = true
			}
		}
		if req.Operation == "upgrade" && !installMode {
			upgrade := action.NewUpgrade(cfg)
			upgrade.Namespace = req.Namespace
			upgrade.Timeout = timeout
			upgrade.WaitStrategy = kube.HookOnlyStrategy
			if req.Wait {
				upgrade.WaitStrategy = kube.StatusWatcherStrategy
			}
			releaseResult, err = upgrade.RunWithContext(ctx, req.ReleaseName, ch, values)
		} else {
			install := action.NewInstall(cfg)
			install.ReleaseName = req.ReleaseName
			install.Namespace = req.Namespace
			install.CreateNamespace = req.CreateNamespace
			install.WaitStrategy = kube.HookOnlyStrategy
			if req.Wait {
				install.WaitStrategy = kube.StatusWatcherStrategy
			}
			install.Timeout = timeout
			if req.Operation == "template" {
				install.DryRunStrategy = action.DryRunClient
				install.DisableHooks = true
			}
			releaseResult, err = install.RunWithContext(ctx, ch, values)
		}
		if err != nil {
			return nil, err.Error()
		}
		if req.Operation == "template" {
			return releaseResult.(*releasev1.Release).Manifest, ""
		}
		view, err := releaseView(releaseResult)
		if err != nil {
			return nil, err.Error()
		}
		return view, ""
	case "status":
		releaseResult, err := action.NewStatus(cfg).Run(req.ReleaseName)
		if err != nil {
			return nil, err.Error()
		}
		view, err := releaseView(releaseResult)
		if err != nil {
			return nil, err.Error()
		}
		return view, ""
	case "history":
		releases, err := action.NewHistory(cfg).Run(req.ReleaseName)
		if err != nil {
			return nil, err.Error()
		}
		views := make([]map[string]any, 0, len(releases))
		for _, item := range releases {
			view, err := releaseView(item)
			if err != nil {
				return nil, err.Error()
			}
			views = append(views, view)
		}
		return views, ""
	case "rollback":
		rollback := action.NewRollback(cfg)
		rollback.Version = req.Revision
		rollback.WaitStrategy = kube.HookOnlyStrategy
		if req.Wait {
			rollback.WaitStrategy = kube.StatusWatcherStrategy
		}
		if err := rollback.Run(req.ReleaseName); err != nil {
			return nil, err.Error()
		}
		return map[string]bool{"succeeded": true}, ""
	case "uninstall":
		uninstall := action.NewUninstall(cfg)
		uninstall.WaitStrategy = kube.HookOnlyStrategy
		if req.Wait {
			uninstall.WaitStrategy = kube.StatusWatcherStrategy
		}
		if _, err := uninstall.Run(req.ReleaseName); err != nil {
			return nil, err.Error()
		}
		return map[string]bool{"succeeded": true}, ""
	case "values":
		values, err := action.NewGetValues(cfg).Run(req.ReleaseName)
		if err != nil {
			return nil, err.Error()
		}
		return values, ""
	}
	return nil, errors.New("unreachable operation").Error()
}

func loadRepositoryFile(path string) (*repo.File, error) {
	file, err := repo.LoadFile(path)
	if err == nil {
		return file, nil
	}
	if errors.Is(err, os.ErrNotExist) {
		return repo.NewFile(), nil
	}
	return nil, err
}

func releaseView(value any) (map[string]any, error) {
	rel, ok := value.(*releasev1.Release)
	if !ok || rel == nil {
		return nil, fmt.Errorf("unexpected Helm release type %T", value)
	}
	chartName, chartVersion, appVersion := "", "", ""
	if rel.Chart != nil && rel.Chart.Metadata != nil {
		chartName = rel.Chart.Metadata.Name
		chartVersion = rel.Chart.Metadata.Version
		appVersion = rel.Chart.Metadata.AppVersion
	}
	status := ""
	updated := time.Time{}
	if rel.Info != nil {
		status = rel.Info.Status.String()
		updated = rel.Info.LastDeployed
	}
	return map[string]any{
		"name":         rel.Name,
		"namespace":    rel.Namespace,
		"revision":     strconv.Itoa(rel.Version),
		"updated":      updated,
		"status":       status,
		"chart":        chartName + "-" + chartVersion,
		"chartName":    chartName,
		"chartVersion": chartVersion,
		"app_version":  appVersion,
	}, nil
}

func main() {}
