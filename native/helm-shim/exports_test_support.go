//go:build helmshimtest && cgo

package main

/*
#include <stdlib.h>
#include "helm_abi.h"
*/
import "C"

import "unsafe"

func testNativeRequestFromC() (nativeRequest, error) {
	allocations := make([]unsafe.Pointer, 0, 32)
	newString := func(value string) *C.char {
		pointer := C.CString(value)
		allocations = append(allocations, unsafe.Pointer(pointer))
		return pointer
	}
	newStringArray := func(values ...string) C.HelmStringArray {
		if len(values) == 0 {
			return C.HelmStringArray{}
		}
		memory := C.calloc(C.size_t(len(values)), C.size_t(unsafe.Sizeof(uintptr(0))))
		allocations = append(allocations, memory)
		items := unsafe.Slice((**C.char)(memory), len(values))
		for index, value := range values {
			items[index] = newString(value)
		}
		return C.HelmStringArray{items: (**C.char)(memory), length: C.size_t(len(values))}
	}
	defer func() {
		for index := len(allocations) - 1; index >= 0; index-- {
			C.free(allocations[index])
		}
	}()

	input := C.HelmRequest{
		abi_version:          C.uint32_t(nativeABIVersion),
		operation:            newString("install"),
		release_name:         newString("web-雪"),
		chart:                newString("oci://charts.example/app"),
		values_json:          newString(`{"replicas":3,"enabled":true}`),
		create_namespace:     1,
		wait:                 1,
		timeout_milliseconds: 12345,
		kubeconfig:           newString("kubeconfig.yaml"),
		kube_context:         newString("dev-日本"),
		namespace_name:       newString("apps"),
		all_namespaces:       1,
		filter:               newString("^web$"),
		revision:             7,
		install_if_missing:   1,
		repository_config:    newString("repositories.yaml"),
		repository_cache:     newString("cache"),
		registry_config:      newString("registry.json"),
		helm_driver:          newString("configmap"),
		repository_name:      newString("private"),
		repository_url:       newString("https://example.test/雪"),
		repository_username:  newString("user"),
		repository_password:  newString("secret"),
	}
	input.values_files = newStringArray("prod-雪.yaml", "overrides.yaml")
	input.options = C.HelmRequestOptions{
		version:            newString("1.2.3"),
		search_term:        newString("nginx"),
		version_constraint: newString(">=1.0"),
		selector:           newString("app=web"),
		dry_run:            newString("server"),
		post_renderer:      newString("renderer"),
		description:        newString("release description"),
		wait_strategy:      newString("watcher"),
		cascade:            newString("foreground"),
		repo:               newString("https://charts.example"),
		ca_file:            newString("ca.pem"),
		cert_file:          newString("client.pem"),
		key_file:           newString("client.key"),
		kube_version:       newString("1.31"),
		max:                12,
		offset:             3,
		history_max:        20,
		flags:              C.uint64_t((uint64(1) << 39) - 1),
	}
	input.options.post_renderer_args = newStringArray()
	input.options.api_versions = newStringArray("v1", "apps/v1")

	return requestFromC(&input)
}

func testEmptyNativeRequestFromC() (nativeRequest, error) {
	var input C.HelmRequest
	input.abi_version = C.uint32_t(nativeABIVersion)
	return requestFromC(&input)
}

func testRequestFromCNull() error {
	_, err := requestFromC(nil)
	return err
}

func testRequestFromCWithInvalidArray(field string) error {
	var input C.HelmRequest
	input.abi_version = C.uint32_t(nativeABIVersion)
	switch field {
	case "values_files":
		input.values_files.length = 1
	case "post_renderer_args":
		input.options.post_renderer_args.length = 1
	case "api_versions":
		input.options.api_versions.length = 1
	}
	_, err := requestFromC(&input)
	return err
}

func testStringsFromCNullArray() error {
	_, err := stringsFromC(nil, 1)
	return err
}

func testStringsFromCTooLargeArray() error {
	maxInt := int(^uint(0) >> 1)
	values := C.malloc(1)
	defer C.free(values)
	_, err := stringsFromC((**C.char)(values), C.size_t(maxInt)+1)
	return err
}

func testExecuteWithABIVersion(version uint32) string {
	input := (*C.HelmRequest)(C.calloc(1, C.size_t(C.sizeof_HelmRequest)))
	if input == nil {
		return "test request allocation failed"
	}
	defer C.free(unsafe.Pointer(input))
	input.abi_version = C.uint32_t(version)
	input.values_files.length = 1

	response := HelmExecuteV1(input)
	if response == nil {
		return "native response allocation failed"
	}
	defer HelmFreeResponseV1(response)
	return C.GoString(response.error)
}

func testExecuteNullRequest() string {
	response := HelmExecuteV1(nil)
	if response == nil {
		return "native response allocation failed"
	}
	defer HelmFreeResponseV1(response)
	return C.GoString(response.error)
}

func testExecuteUnsupportedOperation() string {
	input := (*C.HelmRequest)(C.calloc(1, C.size_t(C.sizeof_HelmRequest)))
	if input == nil {
		return "test request allocation failed"
	}
	defer C.free(unsafe.Pointer(input))
	input.abi_version = C.HELM_ABI_VERSION
	input.operation = C.CString("not-an-operation")
	defer C.free(unsafe.Pointer(input.operation))

	response := HelmExecuteV1(input)
	if response == nil {
		return "native response allocation failed"
	}
	defer HelmFreeResponseV1(response)
	return C.GoString(response.error)
}

func testFreeNilResponse() {
	HelmFreeResponseV1(nil)
}

func testNativeResponseRoundTrip(source nativeResponse) nativeResponse {
	target := allocateResponse()
	if target == nil {
		return nativeResponse{Error: "native response allocation failed"}
	}
	defer HelmFreeResponseV1(target)
	return nativeResponseFromC(writeResponse(target, source))
}

func nativeResponseFromC(response *C.HelmResponse) nativeResponse {
	result := nativeResponse{
		ABIVersion: uint32(response.abi_version),
		ResultKind: int32(response.result_kind),
		Error:      stringFromC(response.error),
		Text:       stringFromC(response.text),
		JSON:       stringFromC(response.json),
	}
	if response.release != nil {
		release := nativeReleaseFromC(response.release)
		result.Release = &release
	}
	if response.releases.items != nil || response.releases.length != 0 {
		items := unsafe.Slice(response.releases.items, int(response.releases.length))
		result.Releases = make([]nativeRelease, len(items))
		for index := range items {
			result.Releases[index] = nativeReleaseFromC(&items[index])
		}
	}
	if response.repositories.items != nil || response.repositories.length != 0 {
		items := unsafe.Slice(response.repositories.items, int(response.repositories.length))
		result.Repositories = make([]nativeRepository, len(items))
		for index, item := range items {
			result.Repositories[index] = nativeRepository{Name: stringFromC(item.name), URL: stringFromC(item.url)}
		}
	}
	if response.chart_search_results.items != nil || response.chart_search_results.length != 0 {
		items := unsafe.Slice(response.chart_search_results.items, int(response.chart_search_results.length))
		result.ChartSearchResults = make([]nativeChartSearchResult, len(items))
		for index, item := range items {
			result.ChartSearchResults[index] = nativeChartSearchResult{
				Reference: stringFromC(item.reference), ChartName: stringFromC(item.chart_name),
				Version: stringFromC(item.version), AppVersion: stringFromC(item.app_version),
				Description: stringFromC(item.description),
			}
		}
	}
	return result
}

func nativeReleaseFromC(release *C.HelmRelease) nativeRelease {
	result := nativeRelease{
		Name: stringFromC(release.name), Namespace: stringFromC(release.namespace_name),
		Revision: int32(release.revision), Updated: stringFromC(release.updated),
		Status: stringFromC(release.status), Chart: stringFromC(release.chart),
		AppVersion: stringFromC(release.app_version), ChartName: stringFromC(release.chart_name),
		ChartVersion: stringFromC(release.chart_version), Description: stringFromC(release.description),
		Notes: stringFromC(release.notes), ResourcesJSON: stringFromC(release.resources_json),
	}
	if release.hooks.items != nil || release.hooks.length != 0 {
		items := unsafe.Slice(release.hooks.items, int(release.hooks.length))
		result.Hooks = make([]nativeHook, len(items))
		for index, hook := range items {
			result.Hooks[index] = nativeHook{
				Name: stringFromC(hook.name), Kind: stringFromC(hook.kind),
				Path: stringFromC(hook.path), Manifest: stringFromC(hook.manifest),
				Events:      nativeStringArrayFromC(hook.events),
				StartedAt:   stringFromC(hook.last_run.started_at),
				CompletedAt: stringFromC(hook.last_run.completed_at),
				Phase:       stringFromC(hook.last_run.phase), Weight: int32(hook.weight),
				DeletePolicies: nativeStringArrayFromC(hook.delete_policies),
			}
		}
	}
	return result
}

func nativeStringArrayFromC(array C.HelmStringArrayOwned) []string {
	if array.items == nil && array.length == 0 {
		return nil
	}
	items := unsafe.Slice(array.items, int(array.length))
	result := make([]string, len(items))
	for index, item := range items {
		result[index] = stringFromC(item)
	}
	return result
}
