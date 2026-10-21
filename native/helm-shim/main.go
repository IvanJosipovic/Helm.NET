package main

import (
	"context"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"log/slog"
	"net/http"
	"os"
	"path/filepath"
	"slices"
	"strconv"
	"strings"
	"sync"
	"time"

	"github.com/Masterminds/semver/v3"
	"helm.sh/helm/v4/pkg/action"
	chartci "helm.sh/helm/v4/pkg/chart"
	"helm.sh/helm/v4/pkg/chart/common"
	"helm.sh/helm/v4/pkg/chart/loader"
	chartloader "helm.sh/helm/v4/pkg/chart/v2/loader"
	"helm.sh/helm/v4/pkg/cli"
	helmsearch "helm.sh/helm/v4/pkg/cmd/search"
	"helm.sh/helm/v4/pkg/downloader"
	"helm.sh/helm/v4/pkg/getter"
	"helm.sh/helm/v4/pkg/kube"
	"helm.sh/helm/v4/pkg/postrenderer"
	"helm.sh/helm/v4/pkg/registry"
	releasev1 "helm.sh/helm/v4/pkg/release/v1"
	repo "helm.sh/helm/v4/pkg/repo/v1"
	"helm.sh/helm/v4/pkg/storage/driver"
	"helm.sh/helm/v4/pkg/tlsutil"
)

type request struct {
	Operation                string         `json:"operation"`
	Kubeconfig               string         `json:"kubeconfig,omitempty"`
	KubeContext              string         `json:"kubeContext,omitempty"`
	Namespace                string         `json:"namespace,omitempty"`
	Chart                    string         `json:"chart,omitempty"`
	ReleaseName              string         `json:"releaseName,omitempty"`
	ValuesFiles              []string       `json:"valuesFiles,omitempty"`
	Values                   map[string]any `json:"values,omitempty"`
	Wait                     bool           `json:"wait,omitempty"`
	CreateNamespace          bool           `json:"createNamespace,omitempty"`
	TimeoutMilliseconds      int64          `json:"timeoutMilliseconds,omitempty"`
	AllNamespaces            bool           `json:"allNamespaces,omitempty"`
	Filter                   string         `json:"filter,omitempty"`
	Revision                 int            `json:"revision,omitempty"`
	InstallIfMissing         bool           `json:"installIfMissing,omitempty"`
	RepositoryConfig         string         `json:"repositoryConfig,omitempty"`
	RepositoryCache          string         `json:"repositoryCache,omitempty"`
	RegistryConfig           string         `json:"registryConfig,omitempty"`
	HelmDriver               string         `json:"helmDriver,omitempty"`
	RepositoryName           string         `json:"repositoryName,omitempty"`
	RepositoryURL            string         `json:"repositoryUrl,omitempty"`
	RepositoryUsername       string         `json:"repositoryUsername,omitempty"`
	RepositoryPassword       string         `json:"repositoryPassword,omitempty"`
	ForceUpdate              bool           `json:"forceUpdate,omitempty"`
	Version                  string         `json:"version,omitempty"`
	SearchTerm               string         `json:"searchTerm,omitempty"`
	AllValues                bool           `json:"allValues,omitempty"`
	Versions                 bool           `json:"versions,omitempty"`
	Devel                    bool           `json:"devel,omitempty"`
	Regexp                   bool           `json:"regexp,omitempty"`
	VersionConstraint        string         `json:"versionConstraint,omitempty"`
	FailOnNoResult           bool           `json:"failOnNoResult,omitempty"`
	Max                      int            `json:"max,omitempty"`
	Offset                   int            `json:"offset,omitempty"`
	Selector                 string         `json:"selector,omitempty"`
	Deployed                 bool           `json:"deployed,omitempty"`
	Failed                   bool           `json:"failed,omitempty"`
	Pending                  bool           `json:"pending,omitempty"`
	Superseded               bool           `json:"superseded,omitempty"`
	Uninstalled              bool           `json:"uninstalled,omitempty"`
	Uninstalling             bool           `json:"uninstalling,omitempty"`
	Date                     bool           `json:"date,omitempty"`
	ResetValues              bool           `json:"resetValues,omitempty"`
	ReuseValues              bool           `json:"reuseValues,omitempty"`
	ResetThenReuseValues     bool           `json:"resetThenReuseValues,omitempty"`
	DryRun                   string         `json:"dryRun,omitempty"`
	HideSecret               bool           `json:"hideSecret,omitempty"`
	DisableHooks             bool           `json:"disableHooks,omitempty"`
	PostRenderer             string         `json:"postRenderer,omitempty"`
	PostRendererArgs         []string       `json:"postRendererArgs,omitempty"`
	WaitForJobs              bool           `json:"waitForJobs,omitempty"`
	RollbackOnFailure        bool           `json:"rollbackOnFailure,omitempty"`
	Reverse                  bool           `json:"reverse,omitempty"`
	DependencyUpdate         bool           `json:"dependencyUpdate,omitempty"`
	SkipCrds                 bool           `json:"skipCrds,omitempty"`
	SkipSchemaValidation     bool           `json:"skipSchemaValidation,omitempty"`
	DisableOpenApiValidation bool           `json:"disableOpenApiValidation,omitempty"`
	Description              string         `json:"description,omitempty"`
	WaitStrategy             string         `json:"waitStrategy,omitempty"`
	Replace                  bool           `json:"replace,omitempty"`
	ForceReplace             bool           `json:"forceReplace,omitempty"`
	ForceConflicts           bool           `json:"forceConflicts,omitempty"`
	TakeOwnership            bool           `json:"takeOwnership,omitempty"`
	CleanupOnFail            bool           `json:"cleanupOnFail,omitempty"`
	HistoryMax               int            `json:"historyMax,omitempty"`
	DryRunUninstall          bool           `json:"dryRunUninstall,omitempty"`
	IgnoreNotFound           bool           `json:"ignoreNotFound,omitempty"`
	KeepHistory              bool           `json:"keepHistory,omitempty"`
	Cascade                  string         `json:"cascade,omitempty"`
	Repo                     string         `json:"repo,omitempty"`
	CaFile                   string         `json:"caFile,omitempty"`
	CertFile                 string         `json:"certFile,omitempty"`
	KeyFile                  string         `json:"keyFile,omitempty"`
	InsecureSkipTlsVerify    bool           `json:"insecureSkipTlsVerify,omitempty"`
	PassCredentials          bool           `json:"passCredentials,omitempty"`
	PlainHttp                bool           `json:"plainHttp,omitempty"`
	ApiVersions              []string       `json:"apiVersions,omitempty"`
	KubeVersion              string         `json:"kubeVersion,omitempty"`
	IncludeCrds              bool           `json:"includeCrds,omitempty"`
	SkipTests                bool           `json:"skipTests,omitempty"`
	IsUpgrade                bool           `json:"isUpgrade,omitempty"`
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
	case "list", "template", "getManifest", "install", "upgrade", "uninstall", "status", "history", "rollback", "values", "repoList", "chartSearch", "repoAdd", "repoRemove", "repoUpdate":
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
		if req.DryRun != "" && req.DryRun != "none" && req.DryRun != "client" && req.DryRun != "server" {
			return nil, "dryRun must be one of none, client, or server"
		}
	}
	if req.WaitStrategy != "" && req.WaitStrategy != "watcher" && req.WaitStrategy != "hookOnly" && req.WaitStrategy != "legacy" {
		return nil, "waitStrategy must be one of watcher, hookOnly, or legacy"
	}
	if req.ForceReplace && req.ForceConflicts && (req.Operation == "install" || req.Operation == "upgrade" || req.Operation == "rollback") {
		return nil, "forceReplace and forceConflicts cannot be used together"
	}
	if req.Operation == "rollback" && req.DryRun != "" && req.DryRun != "none" && req.DryRun != "client" && req.DryRun != "server" {
		return nil, "dryRun must be one of none, client, or server"
	}
	if req.Operation == "uninstall" && req.Cascade != "" && req.Cascade != "background" && req.Cascade != "foreground" && req.Cascade != "orphan" {
		return nil, "cascade must be one of background, foreground, or orphan"
	}
	if req.Operation == "uninstall" && req.Cascade == "" {
		req.Cascade = "background"
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

	return executeAction(ctx, req, settings, cfg, timeout)
}

func executeAction(ctx context.Context, req request, settings *cli.EnvSettings, cfg *action.Configuration, timeout time.Duration) (any, string) {
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
		repositoryFileMu.Lock()
		defer repositoryFileMu.Unlock()
		file, err := repo.LoadFile(settings.RepositoryConfig)
		if errors.Is(err, os.ErrNotExist) || (err == nil && len(file.Repositories) == 0) {
			return nil, "no repositories configured"
		}
		if err != nil {
			return nil, err.Error()
		}
		index := helmsearch.NewIndex()
		for _, entry := range file.Repositories {
			if entry == nil {
				continue
			}
			indexPath := filepath.Join(settings.RepositoryCache, entry.Name+"-index.yaml")
			chartIndex, loadErr := repo.LoadIndexFile(indexPath)
			if loadErr != nil {
				slog.Warn("repo is corrupt or missing", "repo", entry.Name, "error", loadErr)
				continue
			}
			index.AddRepo(entry.Name, chartIndex, true)
		}
		var matches []*helmsearch.Result
		if req.SearchTerm == "" {
			matches = index.All()
		} else {
			matches, err = index.Search(req.SearchTerm, 25, req.Regexp)
			if err != nil {
				return nil, err.Error()
			}
		}
		helmsearch.SortScore(matches)
		var constraint *semver.Constraints
		versionConstraint := req.VersionConstraint
		if versionConstraint == "" {
			versionConstraint = ">0.0.0"
			if req.Devel {
				versionConstraint = ">0.0.0-0"
			}
		}
		constraint, err = semver.NewConstraint(versionConstraint)
		if err != nil {
			return nil, fmt.Sprintf("an invalid version/constraint format: %v", err)
		}
		results := make([]map[string]any, 0, len(matches))
		seen := make(map[string]bool)
		for _, match := range matches {
			version, parseErr := semver.NewVersion(match.Chart.Version)
			if parseErr != nil || !constraint.Check(version) || (!req.Versions && seen[match.Name]) {
				continue
			}
			seen[match.Name] = true
			results = append(results, map[string]any{
				"reference": match.Name, "chartName": match.Chart.Name,
				"version": match.Chart.Version, "app_version": match.Chart.AppVersion,
				"description": match.Chart.Description,
			})
		}
		if req.FailOnNoResult && len(results) == 0 {
			return nil, "no results found"
		}
		return results, ""
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
		if existing := file.Get(req.RepositoryName); existing != nil && !req.ForceUpdate {
			if existing.URL == entry.URL && existing.Username == entry.Username && existing.Password == entry.Password {
				return map[string]bool{"succeeded": true}, ""
			}
			return nil, fmt.Sprintf("repository name (%s) already exists, please specify a different name", req.RepositoryName)
		}
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
		list.Limit = req.Max
		list.Offset = req.Offset
		list.AllNamespaces = req.AllNamespaces
		list.Filter = req.Filter
		list.Selector = req.Selector
		list.Deployed = req.Deployed
		list.Failed = req.Failed
		list.Pending = req.Pending
		list.Superseded = req.Superseded
		list.Uninstalled = req.Uninstalled
		list.Uninstalling = req.Uninstalling
		list.ByDate = req.Date
		list.SortReverse = req.Reverse
		list.SetStateMask()
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
		if registry.IsOCI(req.Chart) && cfg.RegistryClient == nil {
			client, err := newRegistryClient(req, settings.RegistryConfig)
			if err != nil {
				return nil, err.Error()
			}
			cfg.RegistryClient = client
		}
		install := action.NewInstall(cfg)
		install.ChartPathOptions.Version = req.Version
		applyChartPathOptions(&install.ChartPathOptions, req)
		upgrade := action.NewUpgrade(cfg)
		upgrade.ChartPathOptions.Version = req.Version
		applyChartPathOptions(&upgrade.ChartPathOptions, req)
		chartPathOptions := &install.ChartPathOptions
		if req.Operation == "upgrade" {
			chartPathOptions = &upgrade.ChartPathOptions
		}
		if req.PostRenderer != "" {
			renderer, err := postrenderer.NewPostRendererPlugin(settings, req.PostRenderer, req.PostRendererArgs...)
			if err != nil {
				return nil, err.Error()
			}
			install.PostRenderer = renderer
			upgrade.PostRenderer = renderer
		}
		chartPath, err := chartPathOptions.LocateChart(req.Chart, settings)
		if err != nil {
			return nil, err.Error()
		}
		ch, err := loader.Load(chartPath)
		if err != nil {
			return nil, err.Error()
		}
		if req.Operation == "template" || req.Operation == "install" || req.Operation == "upgrade" {
			ch, err = prepareChartDependencies(chartPath, ch, settings, cfg.RegistryClient, req)
			if err != nil {
				return nil, err.Error()
			}
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
			upgrade.Namespace = req.Namespace
			upgrade.Timeout = timeout
			upgrade.DryRunStrategy = parseDryRun(req.DryRun)
			upgrade.HideSecret = req.HideSecret
			upgrade.DisableHooks = req.DisableHooks
			upgrade.WaitForJobs = req.WaitForJobs
			upgrade.RollbackOnFailure = req.RollbackOnFailure
			upgrade.ResetValues = req.ResetValues
			upgrade.ReuseValues = req.ReuseValues
			upgrade.ResetThenReuseValues = req.ResetThenReuseValues
			upgrade.DependencyUpdate = req.DependencyUpdate
			upgrade.SkipCRDs = req.SkipCrds
			upgrade.Devel = req.Devel
			upgrade.SkipSchemaValidation = req.SkipSchemaValidation
			upgrade.DisableOpenAPIValidation = req.DisableOpenApiValidation
			upgrade.Description = req.Description
			upgrade.WaitStrategy = requestedWaitStrategy(req)
			upgrade.ForceReplace = req.ForceReplace
			upgrade.ForceConflicts = req.ForceConflicts
			upgrade.TakeOwnership = req.TakeOwnership
			upgrade.CleanupOnFail = req.CleanupOnFail
			releaseResult, err = upgrade.RunWithContext(ctx, req.ReleaseName, ch, values)
		} else {
			install.ReleaseName = req.ReleaseName
			install.Namespace = req.Namespace
			install.CreateNamespace = req.CreateNamespace
			install.WaitStrategy = requestedWaitStrategy(req)
			install.Timeout = timeout
			install.DryRunStrategy = parseDryRun(req.DryRun)
			install.HideSecret = req.HideSecret
			install.DisableHooks = req.DisableHooks
			install.WaitForJobs = req.WaitForJobs
			install.RollbackOnFailure = req.RollbackOnFailure
			install.DependencyUpdate = req.DependencyUpdate
			install.SkipCRDs = req.SkipCrds
			install.Devel = req.Devel
			install.SkipSchemaValidation = req.SkipSchemaValidation
			install.DisableOpenAPIValidation = req.DisableOpenApiValidation
			install.Description = req.Description
			install.Replace = req.Replace
			install.ForceReplace = req.ForceReplace
			install.ForceConflicts = req.ForceConflicts
			install.TakeOwnership = req.TakeOwnership
			if req.Operation == "template" {
				install.DryRunStrategy = parseTemplateDryRun(req.DryRun)
				install.HideSecret = req.HideSecret
				install.Namespace = req.Namespace
				install.DisableHooks = req.DisableHooks
				install.IncludeCRDs = req.IncludeCrds
				install.APIVersions = append(install.APIVersions, req.ApiVersions...)
				install.IsUpgrade = req.IsUpgrade
				if req.KubeVersion != "" {
					version, parseErr := common.ParseKubeVersion(req.KubeVersion)
					if parseErr != nil {
						return nil, fmt.Sprintf("invalid kubeVersion: %v", parseErr)
					}
					install.KubeVersion = version
				}
			}
			releaseResult, err = install.RunWithContext(ctx, ch, values)
		}
		if err != nil {
			return nil, err.Error()
		}
		if req.Operation == "template" {
			release := releaseResult.(*releasev1.Release)
			if req.DisableHooks || len(release.Hooks) == 0 {
				return release.Manifest, ""
			}
			var rendered strings.Builder
			fmt.Fprintln(&rendered, strings.TrimSpace(release.Manifest))
			renderedHook := false
			if !req.DisableHooks {
				for _, hook := range release.Hooks {
					if len(hook.Events) == 0 {
						continue
					}
					if req.SkipTests && slices.Contains(hook.Events, releasev1.HookTest) {
						continue
					}
					fmt.Fprintf(&rendered, "---\n# Source: %s\n%s\n", hook.Path, hook.Manifest)
					renderedHook = true
				}
			}
			if !renderedHook {
				return release.Manifest, ""
			}
			return rendered.String(), ""
		}
		view, err := releaseView(releaseResult)
		if err != nil {
			return nil, err.Error()
		}
		return view, ""
	case "status":
		status := action.NewStatus(cfg)
		status.Version = req.Revision
		releaseResult, err := status.Run(req.ReleaseName)
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
		if req.Max > 0 && len(views) > req.Max {
			views = views[len(views)-req.Max:]
		}
		return views, ""
	case "rollback":
		rollback := action.NewRollback(cfg)
		rollback.Version = req.Revision
		rollback.Timeout = timeout
		rollback.WaitStrategy = requestedWaitStrategy(req)
		rollback.WaitForJobs = req.WaitForJobs
		rollback.DisableHooks = req.DisableHooks
		rollback.DryRunStrategy = parseDryRun(req.DryRun)
		rollback.ForceReplace = req.ForceReplace
		rollback.ForceConflicts = req.ForceConflicts
		rollback.CleanupOnFail = req.CleanupOnFail
		rollback.MaxHistory = req.HistoryMax
		rollback.Description = req.Description
		if err := rollback.Run(req.ReleaseName); err != nil {
			return nil, err.Error()
		}
		return map[string]bool{"succeeded": true}, ""
	case "uninstall":
		uninstall := action.NewUninstall(cfg)
		uninstall.WaitStrategy = requestedWaitStrategy(req)
		uninstall.Timeout = timeout
		uninstall.DisableHooks = req.DisableHooks
		uninstall.DryRun = req.DryRunUninstall
		uninstall.IgnoreNotFound = req.IgnoreNotFound
		uninstall.KeepHistory = req.KeepHistory
		uninstall.DeletionPropagation = req.Cascade
		uninstall.Description = req.Description
		if _, err := uninstall.Run(req.ReleaseName); err != nil {
			return nil, err.Error()
		}
		return map[string]bool{"succeeded": true}, ""
	case "values":
		getValues := action.NewGetValues(cfg)
		getValues.Version = req.Revision
		getValues.AllValues = req.AllValues
		values, err := getValues.Run(req.ReleaseName)
		if err != nil {
			return nil, err.Error()
		}
		return values, ""
	}
	return nil, errors.New("unreachable operation").Error()
}

func parseDryRun(value string) action.DryRunStrategy {
	switch value {
	case "client":
		return action.DryRunClient
	case "server":
		return action.DryRunServer
	default:
		return action.DryRunNone
	}
}

func parseTemplateDryRun(value string) action.DryRunStrategy {
	if value == "" || value == "none" || value == "client" {
		return action.DryRunClient
	}
	return action.DryRunServer
}

func requestedWaitStrategy(req request) kube.WaitStrategy {
	if req.WaitStrategy != "" {
		return kube.WaitStrategy(req.WaitStrategy)
	}
	if req.Wait {
		return kube.StatusWatcherStrategy
	}
	return kube.HookOnlyStrategy
}

func applyChartPathOptions(options *action.ChartPathOptions, req request) {
	options.RepoURL = req.Repo
	options.Username = req.RepositoryUsername
	options.Password = req.RepositoryPassword
	options.CaFile = req.CaFile
	options.CertFile = req.CertFile
	options.KeyFile = req.KeyFile
	options.InsecureSkipTLSVerify = req.InsecureSkipTlsVerify
	options.PassCredentialsAll = req.PassCredentials
	options.PlainHTTP = req.PlainHttp
}

func newRegistryClient(req request, registryConfig string) (*registry.Client, error) {
	options := []registry.ClientOption{
		registry.ClientOptCredentialsFile(registryConfig),
		registry.ClientOptBasicAuth(req.RepositoryUsername, req.RepositoryPassword),
	}
	if req.CertFile != "" && req.KeyFile != "" || req.CaFile != "" || req.InsecureSkipTlsVerify {
		tlsConfig, err := tlsutil.NewTLSConfig(
			tlsutil.WithInsecureSkipVerify(req.InsecureSkipTlsVerify),
			tlsutil.WithCertKeyPairFiles(req.CertFile, req.KeyFile),
			tlsutil.WithCAFile(req.CaFile),
		)
		if err != nil {
			return nil, fmt.Errorf("can't create registry TLS config: %w", err)
		}
		options = append(options, registry.ClientOptHTTPClient(&http.Client{
			Transport: &http.Transport{TLSClientConfig: tlsConfig, Proxy: http.ProxyFromEnvironment},
		}))
	} else if req.PlainHttp {
		options = append(options, registry.ClientOptPlainHTTP())
	}
	return registry.NewClient(options...)
}

func prepareChartDependencies(chartPath string, ch chartci.Charter, settings *cli.EnvSettings, registryClient *registry.Client, req request) (chartci.Charter, error) {
	accessor, err := chartci.NewAccessor(ch)
	if err != nil {
		return nil, err
	}
	dependencies := accessor.MetaDependencies()
	if len(dependencies) == 0 {
		return ch, nil
	}
	if err := action.CheckDependencies(ch, dependencies); err != nil {
		if !req.DependencyUpdate {
			return nil, fmt.Errorf("an error occurred while checking for chart dependencies. You may need to run 'helm dependency build' to fetch missing dependencies: %w", err)
		}
		getterOptions := []getter.Option{
			getter.WithPassCredentialsAll(req.PassCredentials),
			getter.WithTLSClientConfig(req.CertFile, req.KeyFile, req.CaFile),
			getter.WithInsecureSkipVerifyTLS(req.InsecureSkipTlsVerify),
			getter.WithPlainHTTP(req.PlainHttp),
			getter.WithBasicAuth(req.RepositoryUsername, req.RepositoryPassword),
		}
		if registryClient != nil {
			getterOptions = append(getterOptions, getter.WithRegistryClient(registryClient))
		}
		manager := &downloader.Manager{
			Out: io.Discard, ChartPath: chartPath, Getters: getter.All(settings, getterOptions...),
			RepositoryConfig: settings.RepositoryConfig, RepositoryCache: settings.RepositoryCache,
			ContentCache: settings.ContentCache, RegistryClient: registryClient,
		}
		if err := manager.Update(); err != nil {
			return nil, err
		}
		ch, err = loader.Load(chartPath)
		if err != nil {
			return nil, fmt.Errorf("failed reloading chart after repo update: %w", err)
		}
	}
	return ch, nil
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
	description, notes := "", ""
	var resources any
	if rel.Info != nil {
		status = rel.Info.Status.String()
		updated = rel.Info.LastDeployed
		description = rel.Info.Description
		notes = rel.Info.Notes
		resources = rel.Info.Resources
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
		"description":  description,
		"notes":        notes,
		"resources":    resources,
		"hooks":        rel.Hooks,
	}, nil
}

func main() {}
