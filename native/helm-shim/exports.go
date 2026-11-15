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
	}, nil
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
	target.revision = C.CString(source.Revision)
	target.updated = C.CString(source.Updated)
	target.status = C.CString(source.Status)
	target.chart = C.CString(source.Chart)
	target.app_version = C.CString(source.AppVersion)
}

func freeRelease(release *C.HelmRelease) {
	if release == nil {
		return
	}
	C.free(unsafe.Pointer(release.name))
	C.free(unsafe.Pointer(release.namespace_name))
	C.free(unsafe.Pointer(release.revision))
	C.free(unsafe.Pointer(release.updated))
	C.free(unsafe.Pointer(release.status))
	C.free(unsafe.Pointer(release.chart))
	C.free(unsafe.Pointer(release.app_version))
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
}
