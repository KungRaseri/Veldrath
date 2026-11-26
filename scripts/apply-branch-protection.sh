#!/usr/bin/env bash
#
# Apply the branch-protection rules that make CI a merge gate.
#
# The rule is stated once, in docs/ci-forgejo.md ("Branch protection: what can
# block a merge"). This script is the executable copy of it: it creates the rule
# when it is missing, edits it when it exists, then reads it back and fails when
# the stored rule is not what was asked for.
#
# Two exceptions to "no direct pushes, by anyone", both deliberate and both
# recorded in that section of the doc:
#
#   * apply_to_admins is false. At the operator's request (2026-11-03) the rule
#     no longer applies to administrators, so a repository admin also gets the
#     force_merge override that CheckPullMergeable already gives an instance
#     admin.
#   * `main` accepts pushes from PUSH_BYPASS_USERS, the operator's own account.
#     Measured on this Forgejo version: apply_to_admins:false does NOT lift
#     enable_push:false - the pre-receive hook refuses the administrator too - so
#     the push whitelist is the only break-glass path for a broken deploy.
#     `development` still refuses every push.
#
# Because both rules carry an exception, both pin `push_whitelist_teams: []`
# and `push_whitelist_deploy_keys: false` in the payload *and* in the read-back:
# `main`'s bypass is the one named account, never a team or a deploy key.
#
# Offline regression tests: scripts/tests/apply-branch-protection.test.sh (no
# token, no network - the API call is stubbed). The docs workflow runs them.
#
# Required environment:
#   FORGEJO_TOKEN       A token with repository administration scope.
#
# Optional environment:
#   FORGEJO_SERVER_URL  Defaults to https://git.kungraseri.cloud
#   FORGEJO_REPOSITORY  owner/repo, defaults to Veldrath/Veldrath
#   BRANCHES            Space-separated rule names, defaults to "development main"
#   STATUS_CONTEXTS     JSON array of check patterns, defaults to ["*"]
#   PUSH_BYPASS_USERS   Comma-separated accounts allowed to push to `main`,
#                       defaults to "kungraseri"; set it empty to refuse every push
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
PUSH_BYPASS_USERS="${PUSH_BYPASS_USERS-kungraseri}"

# The approval policy is one required approving review from an account that is
# not the author's, on both rules. The number lives in one constant because
# rule_body() and expected_substrings() must never disagree: a payload asking
# for 1 verified against an expectation of 0 is exactly the drift this script
# exists to catch.
REQUIRED_APPROVALS=1

# PUSH_BYPASS_JSON is the JSON array the rule stores, e.g. ["kungraseri"].
PUSH_BYPASS_JSON='[]'
if [ -n "${PUSH_BYPASS_USERS}" ]; then
    PUSH_BYPASS_JSON="[$(printf '%s' "${PUSH_BYPASS_USERS}" | tr ',' '\n' \
        | awk 'NF { gsub(/^[[:space:]]+|[[:space:]]+$/, ""); printf "%s\"%s\"", (n++ ? "," : ""), $0 }')]"
fi

# usage -> the header comment block, from line 3 to the line before
# `set -euo pipefail`. The end is located rather than hard-coded so adding a
# line to the header cannot truncate --help.
usage() {
    local last
    last="$(awk '/^set -euo pipefail$/ { print NR - 1; exit }' "$0")"
    sed -n "3,${last}p" "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-1}"
}

# Argument parsing, applying and verifying live in main() at the bottom of the
# file: scripts/tests/apply-branch-protection.test.sh sources this file for the
# policy functions and must not touch the API.

# push_policy BRANCH -> "open" when the branch accepts direct pushes, else "closed".
# Only `main` can be open: it is the branch every deployment builds from, so a
# broken pin or a deploy-blocking file has to be fixable without waiting on a
# status check. `development` is pull-request-only, always.
push_policy() {
    case "$1" in
        main) if [ -n "${PUSH_BYPASS_USERS}" ]; then printf 'open'; else printf 'closed'; fi ;;
        *)    printf 'closed' ;;
    esac
}

# rule_body BRANCH -> the Create/EditBranchProtectionOption payload
rule_body() {
    local branch="$1"
    local push_fields='  "enable_push": false,
  "enable_push_whitelist": false,
  "push_whitelist_usernames": [],
  "push_whitelist_teams": [],
  "push_whitelist_deploy_keys": false,'
    if [ "$(push_policy "${branch}")" = "open" ]; then
        push_fields="  \"enable_push\": true,
  \"enable_push_whitelist\": true,
  \"push_whitelist_usernames\": ${PUSH_BYPASS_JSON},
  \"push_whitelist_teams\": [],
  \"push_whitelist_deploy_keys\": false,"
    fi
    cat <<JSON
{
  "rule_name": "${branch}",
  "branch_name": "${branch}",
${push_fields}
  "enable_status_check": true,
  "status_check_contexts": ${STATUS_CONTEXTS},
  "required_approvals": ${REQUIRED_APPROVALS},
  "apply_to_admins": false,
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
    local branch="$1"
    cat <<FIELDS
"rule_name":"${branch}"
"enable_status_check":true
"status_check_contexts":$(printf '%s' "${STATUS_CONTEXTS}" | tr -d ' \n\r\t')
"required_approvals":${REQUIRED_APPROVALS}
"apply_to_admins":false
"push_whitelist_teams":[]
"push_whitelist_deploy_keys":false
FIELDS

    if [ "$(push_policy "${branch}")" = "open" ]; then
        printf '%s\n' '"enable_push":true'
        printf '%s\n' '"enable_push_whitelist":true'
        printf '%s\n' "\"push_whitelist_usernames\":${PUSH_BYPASS_JSON}"
    else
        printf '%s\n' '"enable_push":false'
        printf '%s\n' '"enable_push_whitelist":false'
        printf '%s\n' '"push_whitelist_usernames":[]'
    fi
}

# push_summary BRANCH -> a human-readable description of the push policy
push_summary() {
    if [ "$(push_policy "$1")" = "open" ]; then
        printf 'direct pushes allowed only for %s' "${PUSH_BYPASS_USERS}"
    else
        printf 'all direct pushes refused'
    fi
}

verify_rule() {
    local branch="$1" body missing=0 needed approval_field
    approval_field="\"required_approvals\":${REQUIRED_APPROVALS}"
    body="$(forgejo_get "/repos/${FORGEJO_REPOSITORY}/branch_protections/${branch}" | tr -d ' \n\r\t')"

    while IFS= read -r needed; do
        [ -n "${needed}" ] || continue
        if [ "${needed}" = "${approval_field}" ]; then
            # The count is the policy only when the JSON value ends there: the
            # read-back is compacted above, so a real value is followed by `,`
            # or `}`. A plain substring match would also accept 10.
            if ! printf '%s' "${body}" | grep -Eq "${approval_field}[,}]"; then
                echo "  MISSING ${needed}" >&2
                missing=1
            fi
            continue
        fi
        if ! printf '%s' "${body}" | grep -Fq "${needed}"; then
            echo "  MISSING ${needed}" >&2
            missing=1
        fi
    done <<< "$(expected_substrings "${branch}")"

    return "${missing}"
}

main() {
    local check_only=0
    while [ $# -gt 0 ]; do
        case "$1" in
            --check)   check_only=1 ;;
            --help|-h) usage 0 ;;
            *)         echo "error: unknown argument: $1" >&2; usage 2 ;;
        esac
        shift
    done

    forgejo_init --require-repository

    local rc=0 branch out code
    for branch in ${BRANCHES}; do
        echo "== ${FORGEJO_REPOSITORY}: ${branch} =="

        if [ "${check_only}" = "1" ]; then
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
            echo "  verified: status checks required on ${STATUS_CONTEXTS}; $(push_summary "${branch}"); push whitelist account-only (no teams, no deploy keys); administrators exempt (apply_to_admins:false)"
        else
            echo "  the stored rule does not match the requested one" >&2
            rc=1
        fi
    done

    [ "${rc}" = "0" ] || { echo "branch protection is NOT in the intended state" >&2; exit 1; }
}

# Sourced by the tests: define the functions, do not touch the API. Executed
# directly: apply and verify.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then
    main "$@"
fi
