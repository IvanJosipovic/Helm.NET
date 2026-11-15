package main

/*
#include <stdlib.h>
#include "helm_abi.h"
*/
import "C"

import (
	"fmt"
	"unsafe"
)

//export HelmExecuteV1
func HelmExecuteV1(input *C.HelmRequest) (response *C.HelmResponse) {
	response = allocateResponse()
	if response == nil {
		return nil
	}
	response.abi_version = C.HELM_ABI_VERSION
	defer func() {
		if recovered := recover(); recovered != nil {
			freeResponseContents(response)
			response.abi_version = C.HELM_ABI_VERSION
			response.result_kind = 0
			response.error = C.CString(fmt.Sprintf("Helm native call panicked: %v", recovered))
		}
	}()

	if input == nil {
		response.error = C.CString("native request is null")
		return response
	}
	if err := validateABIVersion(uint32(input.abi_version)); err != nil {
		response.error = C.CString(err.Error())
		return response
	}

	inputRequest, err := requestFromC(input)
	if err != nil {
		response.error = C.CString(err.Error())
		return response
	}
	request, err := requestFromNative(inputRequest)
	if err != nil {
		response.error = C.CString(err.Error())
		return response
	}
	data, errorMessage := execute(request)
	result := responseFromResult(request.Operation, data, errorMessage)
	return writeResponse(response, result)
}

//export HelmFreeResponseV1
func HelmFreeResponseV1(response *C.HelmResponse) {
	if response == nil {
		return
	}
	freeResponseContents(response)
	C.free(unsafe.Pointer(response))
}

func requestFromC(input *C.HelmRequest) (nativeRequest, error) {
	if input == nil {
		return nativeRequest{}, fmt.Errorf("native request is null")
	}
	valuesFiles, err := stringsFromC(input.values_files.items, input.values_files.length)
	if err != nil {
		return nativeRequest{}, err
	}
	postRendererArgs, err := stringsFromC(input.options.post_renderer_args.items, input.options.post_renderer_args.length)
	if err != nil {
		return nativeRequest{}, err
	}
	apiVersions, err := stringsFromC(input.options.api_versions.items, input.options.api_versions.length)
	if err != nil {
		return nativeRequest{}, err
	}
	flags := uint64(input.options.flags)
	return nativeRequest{
		ABIVersion: uint32(input.abi_version), Operation: stringFromC(input.operation),
		ReleaseName: stringFromC(input.release_name), Chart: stringFromC(input.chart),
		ValuesFiles: valuesFiles, ValuesJSON: stringFromC(input.values_json),
		CreateNamespace: input.create_namespace != 0, Wait: input.wait != 0,
		TimeoutMilliseconds: int64(input.timeout_milliseconds), Kubeconfig: stringFromC(input.kubeconfig),
		KubeContext: stringFromC(input.kube_context), Namespace: stringFromC(input.namespace_name),
		AllNamespaces: input.all_namespaces != 0, Filter: stringFromC(input.filter),
		Revision: int(input.revision), InstallIfMissing: input.install_if_missing != 0,
		RepositoryConfig: stringFromC(input.repository_config), RepositoryCache: stringFromC(input.repository_cache),
		RegistryConfig: stringFromC(input.registry_config), HelmDriver: stringFromC(input.helm_driver),
		RepositoryName: stringFromC(input.repository_name), RepositoryURL: stringFromC(input.repository_url),
		RepositoryUsername: stringFromC(input.repository_username), RepositoryPassword: stringFromC(input.repository_password),
		Version: stringFromC(input.options.version), SearchTerm: stringFromC(input.options.search_term),
		AllValues: requestFlag(flags, C.HELM_REQUEST_ALL_VALUES),
		Versions:  requestFlag(flags, C.HELM_REQUEST_VERSIONS), Devel: requestFlag(flags, C.HELM_REQUEST_DEVEL),
		Regexp: requestFlag(flags, C.HELM_REQUEST_REGEXP), VersionConstraint: stringFromC(input.options.version_constraint),
		FailOnNoResult: requestFlag(flags, C.HELM_REQUEST_FAIL_ON_NO_RESULT),
		Max:            int(input.options.max), Offset: int(input.options.offset), Selector: stringFromC(input.options.selector),
		Deployed: requestFlag(flags, C.HELM_REQUEST_DEPLOYED), Failed: requestFlag(flags, C.HELM_REQUEST_FAILED),
		Pending: requestFlag(flags, C.HELM_REQUEST_PENDING), Superseded: requestFlag(flags, C.HELM_REQUEST_SUPERSEDED),
		Uninstalled: requestFlag(flags, C.HELM_REQUEST_UNINSTALLED), Uninstalling: requestFlag(flags, C.HELM_REQUEST_UNINSTALLING),
		Date: requestFlag(flags, C.HELM_REQUEST_DATE), ResetValues: requestFlag(flags, C.HELM_REQUEST_RESET_VALUES),
		ReuseValues:          requestFlag(flags, C.HELM_REQUEST_REUSE_VALUES),
		ResetThenReuseValues: requestFlag(flags, C.HELM_REQUEST_RESET_THEN_REUSE_VALUES),
		DryRun:               stringFromC(input.options.dry_run), HideSecret: requestFlag(flags, C.HELM_REQUEST_HIDE_SECRET),
		DisableHooks: requestFlag(flags, C.HELM_REQUEST_DISABLE_HOOKS),
		PostRenderer: stringFromC(input.options.post_renderer), PostRendererArgs: postRendererArgs,
		WaitForJobs:              requestFlag(flags, C.HELM_REQUEST_WAIT_FOR_JOBS),
		RollbackOnFailure:        requestFlag(flags, C.HELM_REQUEST_ROLLBACK_ON_FAILURE),
		Reverse:                  requestFlag(flags, C.HELM_REQUEST_REVERSE),
		DependencyUpdate:         requestFlag(flags, C.HELM_REQUEST_DEPENDENCY_UPDATE),
		SkipCrds:                 requestFlag(flags, C.HELM_REQUEST_SKIP_CRDS),
		SkipSchemaValidation:     requestFlag(flags, C.HELM_REQUEST_SKIP_SCHEMA_VALIDATION),
		DisableOpenApiValidation: requestFlag(flags, C.HELM_REQUEST_DISABLE_OPENAPI_VALIDATION),
		Description:              stringFromC(input.options.description),
		ForceUpdate:              requestFlag(flags, C.HELM_REQUEST_FORCE_UPDATE),
		WaitStrategy:             stringFromC(input.options.wait_strategy), Replace: requestFlag(flags, C.HELM_REQUEST_REPLACE),
		ForceReplace:    requestFlag(flags, C.HELM_REQUEST_FORCE_REPLACE),
		ForceConflicts:  requestFlag(flags, C.HELM_REQUEST_FORCE_CONFLICTS),
		TakeOwnership:   requestFlag(flags, C.HELM_REQUEST_TAKE_OWNERSHIP),
		CleanupOnFail:   requestFlag(flags, C.HELM_REQUEST_CLEANUP_ON_FAIL),
		HistoryMax:      int(input.options.history_max),
		DryRunUninstall: requestFlag(flags, C.HELM_REQUEST_DRY_RUN_UNINSTALL),
		IgnoreNotFound:  requestFlag(flags, C.HELM_REQUEST_IGNORE_NOT_FOUND),
		KeepHistory:     requestFlag(flags, C.HELM_REQUEST_KEEP_HISTORY), Cascade: stringFromC(input.options.cascade),
		Repo: stringFromC(input.options.repo), CaFile: stringFromC(input.options.ca_file),
		CertFile: stringFromC(input.options.cert_file), KeyFile: stringFromC(input.options.key_file),
		InsecureSkipTlsVerify: requestFlag(flags, C.HELM_REQUEST_INSECURE_SKIP_TLS_VERIFY),
		PassCredentials:       requestFlag(flags, C.HELM_REQUEST_PASS_CREDENTIALS),
		PlainHttp:             requestFlag(flags, C.HELM_REQUEST_PLAIN_HTTP), ApiVersions: apiVersions,
		KubeVersion: stringFromC(input.options.kube_version),
		IncludeCrds: requestFlag(flags, C.HELM_REQUEST_INCLUDE_CRDS),
		SkipTests:   requestFlag(flags, C.HELM_REQUEST_SKIP_TESTS), IsUpgrade: requestFlag(flags, C.HELM_REQUEST_IS_UPGRADE),
	}, nil
}

func requestFlag(flags uint64, flag C.uint64_t) bool {
	return flags&uint64(flag) != 0
}

func stringFromC(value *C.char) string {
	if value == nil {
		return ""
	}
	return C.GoString(value)
}

func stringsFromC(values **C.char, length C.size_t) ([]string, error) {
	if length == 0 {
		return nil, nil
	}
	if values == nil {
		return nil, fmt.Errorf("native string array is null")
	}
	maxInt := int(^uint(0) >> 1)
	if uint64(length) > uint64(maxInt) {
		return nil, fmt.Errorf("native string array is too large")
	}
	input := unsafe.Slice(values, int(length))
	result := make([]string, len(input))
	for index, value := range input {
		result[index] = stringFromC(value)
	}
	return result, nil
}

func allocateResponse() *C.HelmResponse {
	return (*C.HelmResponse)(C.calloc(1, C.size_t(C.sizeof_HelmResponse)))
}

func writeResponse(target *C.HelmResponse, source nativeResponse) *C.HelmResponse {
	target.abi_version = C.HELM_ABI_VERSION
	target.result_kind = C.int32_t(source.ResultKind)
	if source.Error != "" {
		target.error = C.CString(source.Error)
		return target
	}
	switch source.ResultKind {
	case nativeResultAck:
	case nativeResultText:
		target.text = C.CString(source.Text)
	case nativeResultJSON:
		target.json = C.CString(source.JSON)
	case nativeResultRelease:
		if source.Release != nil {
			target.release = allocateRelease(*source.Release)
		}
	case nativeResultReleases:
		if len(source.Releases) > 0 {
			target.releases.items = (*C.HelmRelease)(C.calloc(C.size_t(len(source.Releases)), C.size_t(C.sizeof_HelmRelease)))
			target.releases.length = C.size_t(len(source.Releases))
			items := unsafe.Slice(target.releases.items, len(source.Releases))
			for index, release := range source.Releases {
				writeRelease(&items[index], release)
			}
		}
	case nativeResultRepositories:
		if len(source.Repositories) > 0 {
			target.repositories.items = (*C.HelmRepository)(C.calloc(C.size_t(len(source.Repositories)), C.size_t(C.sizeof_HelmRepository)))
			target.repositories.length = C.size_t(len(source.Repositories))
			items := unsafe.Slice(target.repositories.items, len(source.Repositories))
			for index, repository := range source.Repositories {
				items[index].name = C.CString(repository.Name)
				items[index].url = C.CString(repository.URL)
			}
		}
	case nativeResultChartSearchResults:
		if len(source.ChartSearchResults) > 0 {
			target.chart_search_results.items = (*C.HelmChartSearchResult)(C.calloc(C.size_t(len(source.ChartSearchResults)), C.size_t(C.sizeof_HelmChartSearchResult)))
			target.chart_search_results.length = C.size_t(len(source.ChartSearchResults))
			items := unsafe.Slice(target.chart_search_results.items, len(source.ChartSearchResults))
			for index, result := range source.ChartSearchResults {
				items[index].reference = C.CString(result.Reference)
				items[index].chart_name = C.CString(result.ChartName)
				items[index].version = C.CString(result.Version)
				items[index].app_version = C.CString(result.AppVersion)
				items[index].description = C.CString(result.Description)
			}
		}
	}
	return target
}

func allocateRelease(release nativeRelease) *C.HelmRelease {
	value := (*C.HelmRelease)(C.calloc(1, C.size_t(C.sizeof_HelmRelease)))
	if value != nil {
		writeRelease(value, release)
	}
	return value
}

func writeRelease(target *C.HelmRelease, source nativeRelease) {
	target.name = C.CString(source.Name)
	target.namespace_name = C.CString(source.Namespace)
	target.revision = C.int32_t(source.Revision)
	target.updated = C.CString(source.Updated)
	target.status = C.CString(source.Status)
	target.chart = C.CString(source.Chart)
	target.app_version = C.CString(source.AppVersion)
	target.chart_name = C.CString(source.ChartName)
	target.chart_version = C.CString(source.ChartVersion)
	target.description = C.CString(source.Description)
	target.notes = C.CString(source.Notes)
	target.resources_json = C.CString(source.ResourcesJSON)
	if len(source.Hooks) == 0 {
		return
	}
	target.hooks.items = (*C.HelmReleaseHook)(C.calloc(C.size_t(len(source.Hooks)), C.size_t(C.sizeof_HelmReleaseHook)))
	target.hooks.length = C.size_t(len(source.Hooks))
	items := unsafe.Slice(target.hooks.items, len(source.Hooks))
	for index, hook := range source.Hooks {
		writeHook(&items[index], hook)
	}
}

func writeHook(target *C.HelmReleaseHook, source nativeHook) {
	target.name = C.CString(source.Name)
	target.kind = C.CString(source.Kind)
	target.path = C.CString(source.Path)
	target.manifest = C.CString(source.Manifest)
	target.events = allocateStringArray(source.Events)
	target.last_run.started_at = C.CString(source.StartedAt)
	target.last_run.completed_at = C.CString(source.CompletedAt)
	target.last_run.phase = C.CString(source.Phase)
	target.weight = C.int32_t(source.Weight)
	target.delete_policies = allocateStringArray(source.DeletePolicies)
}

func allocateStringArray(values []string) C.HelmStringArrayOwned {
	array := C.HelmStringArrayOwned{length: C.size_t(len(values))}
	if len(values) == 0 {
		return array
	}
	array.items = (**C.char)(C.calloc(C.size_t(len(values)), C.size_t(unsafe.Sizeof(uintptr(0)))))
	items := unsafe.Slice(array.items, len(values))
	for index, value := range values {
		items[index] = C.CString(value)
	}
	return array
}

func freeStringArray(array C.HelmStringArrayOwned) {
	if array.items == nil {
		return
	}
	items := unsafe.Slice(array.items, int(array.length))
	for _, value := range items {
		C.free(unsafe.Pointer(value))
	}
	C.free(unsafe.Pointer(array.items))
}

func freeHook(hook *C.HelmReleaseHook) {
	C.free(unsafe.Pointer(hook.name))
	C.free(unsafe.Pointer(hook.kind))
	C.free(unsafe.Pointer(hook.path))
	C.free(unsafe.Pointer(hook.manifest))
	freeStringArray(hook.events)
	C.free(unsafe.Pointer(hook.last_run.started_at))
	C.free(unsafe.Pointer(hook.last_run.completed_at))
	C.free(unsafe.Pointer(hook.last_run.phase))
	freeStringArray(hook.delete_policies)
}

func freeRelease(release *C.HelmRelease) {
	if release == nil {
		return
	}
	C.free(unsafe.Pointer(release.name))
	C.free(unsafe.Pointer(release.namespace_name))
	C.free(unsafe.Pointer(release.updated))
	C.free(unsafe.Pointer(release.status))
	C.free(unsafe.Pointer(release.chart))
	C.free(unsafe.Pointer(release.app_version))
	C.free(unsafe.Pointer(release.chart_name))
	C.free(unsafe.Pointer(release.chart_version))
	C.free(unsafe.Pointer(release.description))
	C.free(unsafe.Pointer(release.notes))
	C.free(unsafe.Pointer(release.resources_json))
	if release.hooks.items != nil {
		items := unsafe.Slice(release.hooks.items, int(release.hooks.length))
		for index := range items {
			freeHook(&items[index])
		}
		C.free(unsafe.Pointer(release.hooks.items))
	}
}

func freeResponseContents(response *C.HelmResponse) {
	if response == nil {
		return
	}
	C.free(unsafe.Pointer(response.error))
	C.free(unsafe.Pointer(response.text))
	C.free(unsafe.Pointer(response.json))
	freeRelease(response.release)
	C.free(unsafe.Pointer(response.release))
	if response.releases.items != nil {
		items := unsafe.Slice(response.releases.items, int(response.releases.length))
		for index := range items {
			freeRelease(&items[index])
		}
		C.free(unsafe.Pointer(response.releases.items))
	}
	if response.repositories.items != nil {
		items := unsafe.Slice(response.repositories.items, int(response.repositories.length))
		for index := range items {
			C.free(unsafe.Pointer(items[index].name))
			C.free(unsafe.Pointer(items[index].url))
		}
		C.free(unsafe.Pointer(response.repositories.items))
	}
	if response.chart_search_results.items != nil {
		items := unsafe.Slice(response.chart_search_results.items, int(response.chart_search_results.length))
		for index := range items {
			C.free(unsafe.Pointer(items[index].reference))
			C.free(unsafe.Pointer(items[index].chart_name))
			C.free(unsafe.Pointer(items[index].version))
			C.free(unsafe.Pointer(items[index].app_version))
			C.free(unsafe.Pointer(items[index].description))
		}
		C.free(unsafe.Pointer(response.chart_search_results.items))
	}
	response.abi_version = 0
	response.result_kind = 0
	response.error = nil
	response.text = nil
	response.json = nil
	response.release = nil
	response.releases.items = nil
	response.releases.length = 0
	response.repositories.items = nil
	response.repositories.length = 0
	response.chart_search_results.items = nil
	response.chart_search_results.length = 0
}
