#ifndef HELM_ABI_H
#define HELM_ABI_H

#include <stddef.h>
#include <stdint.h>

#define HELM_ABI_VERSION 1u

typedef struct HelmStringArray {
    const char *const *items;
    size_t length;
} HelmStringArray;

typedef struct HelmRequest {
    uint32_t abi_version;
    const char *operation;
    const char *release_name;
    const char *chart;
    HelmStringArray values_files;
    const char *values_json;
    int32_t create_namespace;
    int32_t wait;
    int64_t timeout_milliseconds;
    const char *kubeconfig;
    const char *kube_context;
    const char *namespace_name;
    int32_t all_namespaces;
    const char *filter;
    int32_t revision;
    int32_t install_if_missing;
    const char *repository_config;
    const char *repository_cache;
    const char *registry_config;
    const char *helm_driver;
    const char *repository_name;
    const char *repository_url;
    const char *repository_username;
    const char *repository_password;
} HelmRequest;

typedef struct HelmRelease {
    char *name;
    char *namespace_name;
    char *revision;
    char *updated;
    char *status;
    char *chart;
    char *app_version;
} HelmRelease;

typedef struct HelmReleaseArray {
    HelmRelease *items;
    size_t length;
} HelmReleaseArray;

typedef struct HelmRepository {
    char *name;
    char *url;
} HelmRepository;

typedef struct HelmRepositoryArray {
    HelmRepository *items;
    size_t length;
} HelmRepositoryArray;

enum HelmResultKind {
    HELM_RESULT_ACK = 1,
    HELM_RESULT_TEXT = 2,
    HELM_RESULT_JSON = 3,
    HELM_RESULT_RELEASE = 4,
    HELM_RESULT_RELEASES = 5,
    HELM_RESULT_REPOSITORIES = 6
};

typedef struct HelmResponse {
    uint32_t abi_version;
    int32_t result_kind;
    char *error;
    char *text;
    char *json;
    HelmRelease *release;
    HelmReleaseArray releases;
    HelmRepositoryArray repositories;
} HelmResponse;

#endif
