#!/usr/bin/env bash
#
# Apply the branch-protection rules that make CI a merge gate.
#
# The rule is stated once, in docs/ci-forgejo.md ("Branch protection: what can
# block a merge"). This script is the executable copy of it: it creates the rule
# when it is missing, edits it when it exists, then reads it back and fails when
# the stored rule is not what was asked for.
#
# Required environment:
#   FORGEJO_TOKEN       A token with repository administration scope.
#
# Optional environment:
#   FORGEJO_SERVER_URL  Defaults to https://git.kungraseri.cloud
#   FORGEJO_REPOSITORY  owner/repo, defaults to Veldrath/Veldrath
#   BRANCHES            Space-separated rule names, defaults to "development main"
#   STATUS_CONTEXTS     JSON array of check patterns, defaults to ["*"]
#
# Usage:
#   FORGEJO_TOKEN=... scripts/apply-branch-protection.sh            # apply and verify
#   FORGEJO_TOKEN=... scripts/apply-branch-protection.sh --check    # verify only
#
# Why `["*"]` and not the five job names: every workflow is filtered by
# `on.pull_request.paths`, and Forgejo 15 reports no status at all for a workflow
# that the filter excluded — so a named context would wait forever on the pull
# requests that do not touch that component. `*` is a glob (gobwas/glob, matched
# with no separator), so it means "every check that reported must be green, and
# at least one must have reported". The full argument, with the source citations,
# is in docs/ci-forgejo.md.
#
# Idempotent: re-running with the same inputs changes nothing.
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/forgejo-api.sh
. "${SCRIPT_DIR}/lib/forgejo-api.sh"

FORGEJO_REPOSITORY="${FORGEJO_REPOSITORY:-Veldrath/Veldrath}"
BRANCHES="${BRANCHES:-development main}"
STATUS_CONTEXTS="${STATUS_CONTEXTS:-}"
[ -n "${STATUS_CONTEXTS}" ] || STATUS_CONTEXTS='["*"]'
CHECK_ONLY=0

usage() {
    sed -n '3,33p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-1}"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --check)  CHECK_ONLY=1 ;;
        --help|-h) usage 0 ;;
        *)        echo "error: unknown argument: $1" >&2; usage 2 ;;
    esac
    shift
done

forgejo_init --require-repository

# rule_body BRANCH -> the Create/EditBranchProtectionOption payload
rule_body() {
    cat <<JSON
{
  "rule_name": "$1",
  "branch_name": "$1",
  "enable_push": false,
  "enable_push_whitelist": false,
  "enable_status_check": true,
  "status_check_contexts": ${STATUS_CONTEXTS},
  "required_approvals": 0,
  "apply_to_admins": true,
  "block_on_outdated_branch": false,
  "block_on_rejected_reviews": false,
  "block_on_official_review_requests": false,
  "dismiss_stale_approvals": false,
  "require_signed_commits": false
}
JSON
}

# rule_exists BRANCH -> 0 when a rule with exactly that rule_name is present
rule_exists() {
    forgejo_get "/repos/${FORGEJO_REPOSITORY}/branch_protections" \
        | tr -d ' \n\r\t' \
        | grep -Fq "\"rule_name\":\"$1\""
}

# expected_substrings BRANCH -> one per line; every one must appear in the rule
# the server returns. The response is compacted first so formatting cannot make
# a correct rule look wrong.
expected_substrings() {
    cat <<FIELDS
"rule_name":"$1"
"enable_status_check":true
"status_check_contexts":$(printf '%s' "${STATUS_CONTEXTS}" | tr -d ' \n\r\t')
"required_approvals":0
"apply_to_admins":true
FIELDS
}

verify_rule() {
    local branch="$1" body missing=0 needed
    body="$(forgejo_get "/repos/${FORGEJO_REPOSITORY}/branch_protections/${branch}" | tr -d ' \n\r\t')"

    while IFS= read -r needed; do
        [ -n "${needed}" ] || continue
        if ! printf '%s' "${body}" | grep -Fq "${needed}"; then
            echo "  MISSING ${needed}" >&2
            missing=1
        fi
    done <<< "$(expected_substrings "${branch}")"

    # enable_push/push-whitelist are only meaningful as a pair: direct pushes to
    # the branch must be refused, otherwise the gate is bypassable with git push.
    if printf '%s' "${body}" | grep -Fq '"enable_push":true'; then
        echo "  MISSING \"enable_push\":false" >&2
        missing=1
    fi

    return "${missing}"
}

rc=0
for branch in ${BRANCHES}; do
    echo "== ${FORGEJO_REPOSITORY}: ${branch} =="

    if [ "${CHECK_ONLY}" = "1" ]; then
        if ! rule_exists "${branch}"; then
            echo "  no rule named '${branch}'" >&2
            rc=1
            continue
        fi
    elif rule_exists "${branch}"; then
        out="$(mktemp)"
        # The edit endpoint is PATCH; PUT answers 405 on this Forgejo version.
        code="$(forgejo_send PATCH "/repos/${FORGEJO_REPOSITORY}/branch_protections/${branch}" "$(rule_body "${branch}")" "${out}")"
        [ "${code}" = "200" ] || { echo "  PATCH -> ${code}" >&2; cat "${out}" >&2; rm -f "${out}"; rc=1; continue; }
        echo "  updated (PATCH 200)"
        rm -f "${out}"
    else
        out="$(mktemp)"
        code="$(forgejo_send POST "/repos/${FORGEJO_REPOSITORY}/branch_protections" "$(rule_body "${branch}")" "${out}")"
        [ "${code}" = "201" ] || { echo "  POST -> ${code}" >&2; cat "${out}" >&2; rm -f "${out}"; rc=1; continue; }
        echo "  created (POST 201)"
        rm -f "${out}"
    fi

    if verify_rule "${branch}"; then
        echo "  verified: status checks required on ${STATUS_CONTEXTS}, pushes refused, applies to admins"
    else
        echo "  the stored rule does not match the requested one" >&2
        rc=1
    fi
done

[ "${rc}" = "0" ] || { echo "branch protection is NOT in the intended state" >&2; exit 1; }
