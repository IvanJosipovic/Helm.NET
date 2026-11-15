---
name: helm-native-rebuild
description: Rebuild Helm.NET's checked-in native libraries from the Go shim by committing branch changes, dispatching the native update workflow, monitoring its generated PR, and merging it after successful builds.
---

# Helm Native Rebuild

Use this skill when asked to rebuild Helm.NET's packaged Helm Go shim binaries and land the workflow-generated native-library PR.

## Workflow

1. Inspect the current branch, `git status`, staged and unstaged diffs, remotes, and the workflow file `.github/workflows/update-helm-native.yaml`. Confirm the changes to commit belong to the requested work; do not sweep unrelated user changes into the commit. The workflow dispatch requires `target_branch`.
2. Commit the intended source changes and push the current branch. The user request to run this workflow and merge its PR authorizes these repository mutations; do not ask again for that same authorization.
3. Dispatch the workflow against the current branch:

   ```powershell
   $branch = git branch --show-current
   gh workflow run update-helm-native.yaml --ref $branch -f "target_branch=$branch"
   ```

   Record the run URL returned by `gh`. The workflow updates the Helm module to the latest stable version, runs `go test -cover ./...`, builds and checks all six RID libraries, then opens or updates `automation/update-helm-native` as a PR targeting `target_branch`.
4. Follow the run to completion with `gh run watch <run-id> --interval 20 --exit-status`. Keep the user informed during long builds. On failure, inspect the failing job log, correct the source or test issue on the target branch, commit and push the repair, and dispatch a new workflow run. A run uses the commit it checked out; pushing a repair does not fix a run already in progress.
5. After success, locate the open PR whose base is the target branch and whose head is `automation/update-helm-native`. Inspect its state, diff, and checks. Expect the six runtime libraries under `src/Helm.NET/runtimes/`. The workflow also runs `go get helm.sh/helm/v4@latest` and `go mod tidy`; inspect any `go.mod` or `go.sum` changes and ensure they fit the request before merging. Do not merge a failed, closed, unrelated, or unexpected PR.
6. Merge only after the workflow succeeds and GitHub reports the PR mergeable with required checks satisfied. Use a merge method permitted by repository settings (this repository rejects merge commits; squash is supported). Do not bypass protection rules.
7. Verify the PR is merged, the target branch contains its merge commit, and the local branch is fast-forwarded and clean. Report the source commit(s), workflow result/link, PR and merge commit, and any verification that was or was not run.

## Failure handling

- If authentication, dispatch, permissions, runner availability, or repository policy blocks progress, preserve the current state and report the exact blocker.
- If a failed workflow needs a code repair, keep the repair focused, commit and push it to the target branch, then run a fresh workflow. Do not repeatedly dispatch the same unchanged failing revision.
- If the generated PR contains an unexpected Helm dependency upgrade or changes outside the expected dependency files and six native libraries, stop before merging and explain the discrepancy.
- Do not force-push, rewrite branch history, delete unrelated branches, or override required reviews/checks.
