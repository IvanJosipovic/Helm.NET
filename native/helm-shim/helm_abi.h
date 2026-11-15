#ifndef HELM_ABI_H
#define HELM_ABI_H

#include <stddef.h>
#include <stdint.h>

#define HELM_ABI_VERSION 1u

typedef struct HelmStringArray {
    const char *const *items;
    size_t length;
} HelmStringArray;

typedef struct HelmRequestOptions {
    const char *version;
    const char *search_term;
    const char *version_constraint;
    const char *selector;
    const char *dry_run;
    const char *post_renderer;
    const char *description;
    const char *wait_strategy;
    const char *cascade;
    const char *repo;
    const char *ca_file;
    const char *cert_file;
    const char *key_file;
    const char *kube_version;
    HelmStringArray post_renderer_args;
    HelmStringArray api_versions;
    int32_t max;
    int32_t offset;
    int32_t history_max;
    uint64_t flags;
} HelmRequestOptions;

#define HELM_REQUEST_ALL_VALUES (UINT64_C(1) << 0)
#define HELM_REQUEST_VERSIONS (UINT64_C(1) << 1)
#define HELM_REQUEST_DEVEL (UINT64_C(1) << 2)
#define HELM_REQUEST_REGEXP (UINT64_C(1) << 3)
#define HELM_REQUEST_FAIL_ON_NO_RESULT (UINT64_C(1) << 4)
#define HELM_REQUEST_DEPLOYED (UINT64_C(1) << 5)
#define HELM_REQUEST_FAILED (UINT64_C(1) << 6)
#define HELM_REQUEST_PENDING (UINT64_C(1) << 7)
#define HELM_REQUEST_SUPERSEDED (UINT64_C(1) << 8)
#define HELM_REQUEST_UNINSTALLED (UINT64_C(1) << 9)
#define HELM_REQUEST_UNINSTALLING (UINT64_C(1) << 10)
#define HELM_REQUEST_DATE (UINT64_C(1) << 11)
#define HELM_REQUEST_RESET_VALUES (UINT64_C(1) << 12)
#define HELM_REQUEST_REUSE_VALUES (UINT64_C(1) << 13)
#define HELM_REQUEST_RESET_THEN_REUSE_VALUES (UINT64_C(1) << 14)
#define HELM_REQUEST_HIDE_SECRET (UINT64_C(1) << 15)
#define HELM_REQUEST_DISABLE_HOOKS (UINT64_C(1) << 16)
#define HELM_REQUEST_WAIT_FOR_JOBS (UINT64_C(1) << 17)
#define HELM_REQUEST_ROLLBACK_ON_FAILURE (UINT64_C(1) << 18)
#define HELM_REQUEST_REVERSE (UINT64_C(1) << 19)
#define HELM_REQUEST_DEPENDENCY_UPDATE (UINT64_C(1) << 20)
#define HELM_REQUEST_SKIP_CRDS (UINT64_C(1) << 21)
#define HELM_REQUEST_SKIP_SCHEMA_VALIDATION (UINT64_C(1) << 22)
#define HELM_REQUEST_DISABLE_OPENAPI_VALIDATION (UINT64_C(1) << 23)
#define HELM_REQUEST_FORCE_UPDATE (UINT64_C(1) << 24)
#define HELM_REQUEST_REPLACE (UINT64_C(1) << 25)
#define HELM_REQUEST_FORCE_REPLACE (UINT64_C(1) << 26)
#define HELM_REQUEST_FORCE_CONFLICTS (UINT64_C(1) << 27)
#define HELM_REQUEST_TAKE_OWNERSHIP (UINT64_C(1) << 28)
#define HELM_REQUEST_CLEANUP_ON_FAIL (UINT64_C(1) << 29)
#define HELM_REQUEST_DRY_RUN_UNINSTALL (UINT64_C(1) << 30)
#define HELM_REQUEST_IGNORE_NOT_FOUND (UINT64_C(1) << 31)
#define HELM_REQUEST_KEEP_HISTORY (UINT64_C(1) << 32)
#define HELM_REQUEST_INSECURE_SKIP_TLS_VERIFY (UINT64_C(1) << 33)
#define HELM_REQUEST_PASS_CREDENTIALS (UINT64_C(1) << 34)
#define HELM_REQUEST_PLAIN_HTTP (UINT64_C(1) << 35)
#define HELM_REQUEST_INCLUDE_CRDS (UINT64_C(1) << 36)
#define HELM_REQUEST_SKIP_TESTS (UINT64_C(1) << 37)
#define HELM_REQUEST_IS_UPGRADE (UINT64_C(1) << 38)

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
    HelmRequestOptions options;
} HelmRequest;

typedef struct HelmStringArrayOwned {
    char **items;
    size_t length;
} HelmStringArrayOwned;

typedef struct HelmHookExecution {
    char *started_at;
    char *completed_at;
    char *phase;
} HelmHookExecution;

typedef struct HelmReleaseHook {
    char *name;
    char *kind;
    char *path;
    char *manifest;
    HelmStringArrayOwned events;
    HelmHookExecution last_run;
    int32_t weight;
    HelmStringArrayOwned delete_policies;
} HelmReleaseHook;

typedef struct HelmReleaseHookArray {
    HelmReleaseHook *items;
    size_t length;
} HelmReleaseHookArray;

typedef struct HelmRelease {
    char *name;
    char *namespace_name;
    int32_t revision;
    char *updated;
    char *status;
    char *chart;
    char *app_version;
    char *chart_name;
    char *chart_version;
    char *description;
    char *notes;
    char *resources_json;
    HelmReleaseHookArray hooks;
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

typedef struct HelmChartSearchResult {
    char *reference;
    char *chart_name;
    char *version;
    char *app_version;
    char *description;
} HelmChartSearchResult;

typedef struct HelmChartSearchResultArray {
    HelmChartSearchResult *items;
    size_t length;
} HelmChartSearchResultArray;

enum HelmResultKind {
    HELM_RESULT_ACK = 1,
    HELM_RESULT_TEXT = 2,
    HELM_RESULT_JSON = 3,
    HELM_RESULT_RELEASE = 4,
    HELM_RESULT_RELEASES = 5,
    HELM_RESULT_REPOSITORIES = 6,
    HELM_RESULT_CHART_SEARCH_RESULTS = 7
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
    HelmChartSearchResultArray chart_search_results;
} HelmResponse;

#endif
