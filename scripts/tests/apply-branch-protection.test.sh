#!/usr/bin/env bash
#
# Offline regression tests for scripts/apply-branch-protection.sh.
#
# The point of that script is that the stored rule matches the documented
# policy. Its read-back is a set of substring assertions, and the interesting
# failures are silent widenings: an extra account, a team, or a deploy-key
# bypass on `main`; any push path at all on `development`. The live server must
# not carry those, so this test stubs the API response instead of touching it.
#
# No token, no network, no live rule is read or written.
#
# Usage: bash scripts/tests/apply-branch-protection.test.sh

set -uo pipefail

TEST_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APPLY="${TEST_DIR}/../apply-branch-protection.sh"

# The script sets -euo pipefail when loaded; switch -e back off so an expected
# failure is an assertion result rather than the end of the run.
# shellcheck disable=SC1090
. "${APPLY}"
set +e

# Deterministic inputs, whatever the caller's environment holds.
FORGEJO_REPOSITORY=Veldrath/Veldrath
BRANCHES="development main"
STATUS_CONTEXTS='["*"]'
PUSH_BYPASS_USERS=kungraseri
PUSH_BYPASS_JSON='["kungraseri"]'

checks=0
failures=0

fail() { failures=$((failures + 1)); printf 'FAIL %s\n' "$*" >&2; }

compact() { printf '%s' "$1" | tr -d ' \n\r\t'; }

# The script's only network call. verify_rule normally reads this shape from
# Forgejo; the stub keeps the assertions honest about what the server returns,
# not about what rule_body happens to have sent.
STUB_BODY=''
forgejo_get() { printf '%s' "${STUB_BODY}"; }

# assert_body LABEL BRANCH BODY WANT_RC [WANT_DRIFT]
# WANT_RC is 0 (rule matches) or 1 (drift reported). WANT_DRIFT is a substring
# the reported drift must name, so a case cannot pass by failing for the wrong
# reason.
assert_body() {
    local label="$1" branch="$2" body="$3" want="$4" want_drift="${5:-}"
    local out rc
    STUB_BODY="$(compact "${body}")"
    if out="$(verify_rule "${branch}" 2>&1)"; then rc=0; else rc=$?; fi
    checks=$((checks + 1))
    if [ "${rc}" -ne "${want}" ]; then
        fail "${label}: exit ${rc}, wanted ${want} (${out:-no output})"
    elif [ -n "${want_drift}" ] && ! printf '%s' "${out}" | grep -Fq -- "${want_drift}"; then
        fail "${label}: drift did not name ${want_drift} (${out:-no output})"
    fi
}

# assert_contains LABEL HAYSTACK NEEDLE
assert_contains() {
    checks=$((checks + 1))
    printf '%s' "$2" | grep -Fq -- "$3" || fail "$1: missing $3"
}

# mutate BODY SED-EXPR -> BODY with one field changed
mutate() { printf '%s' "$1" | sed "$2"; }

# assert_json LABEL BODY
assert_json() {
    command -v node >/dev/null 2>&1 || return 0
    checks=$((checks + 1))
    printf '%s' "$2" \
        | node -e 'let s="";process.stdin.on("data",d=>s+=d);process.stdin.on("end",()=>JSON.parse(s));' \
        || fail "$1: rule_body did not produce valid JSON"
}

dev_body="$(rule_body development)"
main_body="$(rule_body main)"

# --- the payload itself -----------------------------------------------------
# The bypass is one named account: no team and no deploy key may inherit it,
# and `development` may not gain a whitelist at all.
assert_payload_shape() {
    local name="$1" body="$2"
    assert_contains "payload ${name}: no push teams" "${body}" '"push_whitelist_teams": []'
    assert_contains "payload ${name}: no deploy-key push" "${body}" '"push_whitelist_deploy_keys": false'
}
assert_payload_shape development "${dev_body}"
assert_payload_shape main "${main_body}"
assert_contains "payload main: one account" "${main_body}" '"push_whitelist_usernames": ["kungraseri"]'
assert_contains "payload development: no accounts" "${dev_body}" '"push_whitelist_usernames": []'
assert_contains "payload development: no pushes" "${dev_body}" '"enable_push": false'
assert_json "payload development" "${dev_body}"
assert_json "payload main" "${main_body}"

# --- the documented rule passes ---------------------------------------------
# rule_body is what the API is sent; the response carries the same values, so a
# payload that verifies against itself also proves payload and assertions agree.
assert_body "development: documented policy" development "${dev_body}" 0
assert_body "main: documented policy" main "${main_body}" 0

# --- drift on main ----------------------------------------------------------
assert_body "main: extra team" main \
    "$(mutate "${main_body}" 's/"push_whitelist_teams": \[\]/"push_whitelist_teams": ["release-team"]/')" \
    1 '"push_whitelist_teams":[]'
assert_body "main: deploy-key bypass" main \
    "$(mutate "${main_body}" 's/"push_whitelist_deploy_keys": false/"push_whitelist_deploy_keys": true/')" \
    1 '"push_whitelist_deploy_keys":false'
assert_body "main: extra account" main \
    "$(mutate "${main_body}" 's/"push_whitelist_usernames": \["kungraseri"\]/"push_whitelist_usernames": ["kungraseri", "someone"]/')" \
    1 '"push_whitelist_usernames":["kungraseri"]'
assert_body "main: no accounts" main \
    "$(mutate "${main_body}" 's/"push_whitelist_usernames": \["kungraseri"\]/"push_whitelist_usernames": []/')" \
    1 '"push_whitelist_usernames":["kungraseri"]'
assert_body "main: pushes disabled" main \
    "$(mutate "${main_body}" 's/"enable_push": true/"enable_push": false/')" \
    1 '"enable_push":true'
assert_body "main: whitelist disabled" main \
    "$(mutate "${main_body}" 's/"enable_push_whitelist": true/"enable_push_whitelist": false/')" \
    1 '"enable_push_whitelist":true'
assert_body "main: admin exemption restored" main \
    "$(mutate "${main_body}" 's/"apply_to_admins": false/"apply_to_admins": true/')" \
    1 '"apply_to_admins":false'
assert_body "main: status gate disabled" main \
    "$(mutate "${main_body}" 's/"enable_status_check": true/"enable_status_check": false/')" \
    1 '"enable_status_check":true'
assert_body "main: named contexts" main \
    "$(mutate "${main_body}" 's/"status_check_contexts": \["\*"\]/"status_check_contexts": ["CI"]/')" \
    1 '"status_check_contexts":["*"]'

# --- drift on development ---------------------------------------------------
assert_body "development: direct pushes enabled" development \
    "$(mutate "${dev_body}" 's/"enable_push": false/"enable_push": true/')" \
    1 '"enable_push":false'
assert_body "development: account bypass" development \
    "$(mutate "${dev_body}" 's/"push_whitelist_usernames": \[\]/"push_whitelist_usernames": ["kungraseri"]/')" \
    1 '"push_whitelist_usernames":[]'
assert_body "development: team bypass" development \
    "$(mutate "${dev_body}" 's/"push_whitelist_teams": \[\]/"push_whitelist_teams": ["release-team"]/')" \
    1 '"push_whitelist_teams":[]'
assert_body "development: deploy-key bypass" development \
    "$(mutate "${dev_body}" 's/"push_whitelist_deploy_keys": false/"push_whitelist_deploy_keys": true/')" \
    1 '"push_whitelist_deploy_keys":false'

# --- PUSH_BYPASS_USERS= switches main to the closed policy ------------------
saved_users="${PUSH_BYPASS_USERS}"
saved_json="${PUSH_BYPASS_JSON}"
saved_main="${main_body}"

PUSH_BYPASS_USERS=''
PUSH_BYPASS_JSON='[]'
closed_main="$(rule_body main)"

assert_contains "PUSH_BYPASS_USERS=: main payload refuses pushes" "${closed_main}" '"enable_push": false'
assert_body "PUSH_BYPASS_USERS=: closed main passes" main "${closed_main}" 0
assert_body "PUSH_BYPASS_USERS=: an open main is drift" main "${saved_main}" 1 '"enable_push":false'

PUSH_BYPASS_USERS="${saved_users}"
PUSH_BYPASS_JSON="${saved_json}"

# --- rule lookup ------------------------------------------------------------
STUB_BODY="$(compact "${saved_main}")"
checks=$((checks + 1))
rule_exists development && fail "rule_exists: found 'development' in a main-only response"
checks=$((checks + 1))
rule_exists main || fail "rule_exists: did not find 'main'"

# --- --help still prints the header (no token, no network) ------------------
checks=$((checks + 1))
help_out="$(bash "${APPLY}" --help 2>&1)"
help_rc=$?
if [ "${help_rc}" -ne 0 ] || ! printf '%s' "${help_out}" | grep -Fq 'PUSH_BYPASS_USERS'; then
    fail "--help: exit ${help_rc}, header missing PUSH_BYPASS_USERS"
fi

printf '\n%d checks, %d failed\n' "${checks}" "${failures}"
[ "${failures}" -eq 0 ]
