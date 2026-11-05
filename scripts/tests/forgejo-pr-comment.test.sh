#!/usr/bin/env bash
#
# Offline regression tests for scripts/forgejo-pr-comment.sh.
#
# The defect these lock down: find_comment printed the matching comment id twice. It
# printed once from flush() when the *next* "@@COMMENT" line arrived, and awk then ran
# its END rule — which calls flush() again — even though the first match had already
# called exit(). Whenever the matching coverage comment was followed by another comment
# (a review comment is enough), COMMENT_ID held "746\n746" and the PATCH URL became
# .../issues/comments/746<newline>746. curl answered "(3) bad/illegal URL", the job died
# on its last step, and the sticky comment stayed on the previous head's numbers. The
# same double call appended the existing body to its output file twice as well; the
# section merge hides that one (it rebuilds sections by name), the URL does not.
#
# Every case runs the real script against a fake curl on PATH: no token, no network, no
# Forgejo. The fake serves one comment list per case (the compact single-line JSON the
# API returns) and records the method, URL and payload of every request, so a case
# asserts what the script would have sent rather than what this test assumes it sends.
#
# Usage: bash scripts/tests/forgejo-pr-comment.test.sh

set -uo pipefail

TEST_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SCRIPT="${TEST_DIR}/../forgejo-pr-comment.sh"

PR=42
REPO='Veldrath/Veldrath'
SHARED_MARKER='<!-- ci-coverage -->'
LEGACY_MARKER='<!-- ci-marker-demo -->'

WORK="$(mktemp -d)"
# KEEP_WORK=1 leaves the fixture/stub directory behind and prints its path, which is how a
# failing assertion gets inspected after the run.
trap 'if [ -z "${KEEP_WORK:-}" ]; then rm -rf "${WORK}"; else printf "kept: %s\n" "${WORK}"; fi' EXIT
BIN="${WORK}/bin"
STUB="${WORK}/stub"
mkdir -p "${BIN}" "${STUB}"

# ---------------------------------------------------------------------------
# Fake curl. Routes on the URL, records every request, and keeps a "live" comment
# list so the script's post-write read-back sees the section it just wrote (the JSON
# body it returns carries the marker and the report's first summary line).
# ---------------------------------------------------------------------------
cat > "${BIN}/curl" <<'STUB'
#!/usr/bin/env bash
set -uo pipefail

method=GET
out=""
data=""
url=""
while [ $# -gt 0 ]; do
    case "$1" in
        -X) method="$2"; shift 2 ;;
        -o) out="$2"; shift 2 ;;
        -d) data="$2"; shift 2 ;;
        -H|-w) shift 2 ;;
        -*) shift ;;
        *) url="$1"; shift ;;
    esac
done

seq=$(( $(cat "${STUB_DIR}/seq" 2>/dev/null || echo 0) + 1 ))
printf '%s' "${seq}" > "${STUB_DIR}/seq"
printf '%s' "${method}" > "${STUB_DIR}/req.${seq}.method"
printf '%s' "${url}" > "${STUB_DIR}/req.${seq}.url"
[ -n "${data}" ] && printf '%s' "${data}" > "${STUB_DIR}/req.${seq}.payload"

case "${url}" in
    'http://stub.invalid/'*) ;;
    *) printf 'stub: request outside the stub host: %s\n' "${url}" >&2; printf '000'; exit 22 ;;
esac

# live ID -> one comment carrying the marker and the summary line the read-back greps
# for. Written after a successful write, served by every later GET.
live() {
    printf '[{"id":%s,"body":"%s\\n## Coverage summary\\n\\n%s","user":{"id":1,"login":"ci"}}]\n' \
        "$1" "${STUB_MARKER}" "${STUB_TOKEN}" > "${STUB_DIR}/live.json"
}

case "${method}" in
    GET)
        if [ -f "${STUB_DIR}/live.json" ]; then
            cat "${STUB_DIR}/live.json"
        else
            cat "${STUB_DIR}/comments.json"
        fi
        exit 0
        ;;
    PATCH)
        printf '{"id":%s}' "${url##*/}" > "${out:-/dev/null}"
        live "${url##*/}"
        printf '200'
        exit 0
        ;;
    POST)
        printf '{"id":999}' > "${out:-/dev/null}"
        live 999
        printf '201'
        exit 0
        ;;
    DELETE)
        printf '{}' > "${out:-/dev/null}"
        printf '204'
        exit 0
        ;;
    *)
        printf 'stub: unexpected method %s\n' "${method}" >&2
        printf '000'
        exit 22
        ;;
esac
STUB
chmod +x "${BIN}/curl"

# ---------------------------------------------------------------------------
# Fixtures and body files.
#
# The comment lists are one line each because that is what the API returns and what
# comment-json.awk decodes; the bodies exercise the decoder's hard cases (a brace, a
# quote, newlines) and the merge awk's section markers. The existing coverage comment
# holds a .NET section and a web section: the runs below own .NET, so the web section
# is what proves the rest of the comment survives the rewrite.
# ---------------------------------------------------------------------------
FIX="${WORK}/fixtures"
mkdir -p "${FIX}"

# The shape under test: review, then the coverage comment, then a later review comment.
cat > "${FIX}/coverage-then-more.json" <<'JSON'
[{"id":101,"body":"a review comment with a brace } and a quote \"x\"\nsecond line","user":{"id":5,"login":"rev"}},{"id":746,"body":"<!-- ci-coverage -->\n## Coverage summary\n\n<!-- coverage-section:dotnet -->\n\n### .NET (all test projects)\n\nTotal lines 31995\n\n<!-- /coverage-section:dotnet -->\n\n<!-- coverage-section:web -->\n\n### Web\n\nWeb lines 31000\n\n<!-- /coverage-section:web -->","user":{"id":5,"login":"ci"}},{"id":900,"body":"later review comment that must not matter","user":{"id":5,"login":"rev"}}]
JSON

# The coverage comment last: the END-only path, which never duplicated anything.
cat > "${FIX}/coverage-last.json" <<'JSON'
[{"id":101,"body":"a review comment with a brace } and a quote \"x\"","user":{"id":5,"login":"rev"}},{"id":746,"body":"<!-- ci-coverage -->\n## Coverage summary\n\n<!-- coverage-section:dotnet -->\n\n### .NET (all test projects)\n\nTotal lines 31995\n\n<!-- /coverage-section:dotnet -->\n\n<!-- coverage-section:web -->\n\n### Web\n\nWeb lines 31000\n\n<!-- /coverage-section:web -->","user":{"id":5,"login":"ci"}}]
JSON

# Two comments carry the shared marker: the first is updated, the second is superseded.
# Only the first carries a web section, so the payload proves which body was the base.
cat > "${FIX}/coverage-duplicate.json" <<'JSON'
[{"id":746,"body":"<!-- ci-coverage -->\n## Coverage summary\n\n<!-- coverage-section:dotnet -->\n\n### .NET (all test projects)\n\nTotal lines 31995\n\n<!-- /coverage-section:dotnet -->\n\n<!-- coverage-section:web -->\n\n### Web\n\nWeb lines 31000\n\n<!-- /coverage-section:web -->","user":{"id":5,"login":"ci"}},{"id":750,"body":"<!-- ci-coverage -->\n## Coverage summary\n\n<!-- coverage-section:dotnet -->\n\n### .NET (all test projects)\n\nTotal lines 31990\n\n<!-- /coverage-section:dotnet -->","user":{"id":5,"login":"ci"}},{"id":900,"body":"a later review comment","user":{"id":5,"login":"rev"}}]
JSON

# No comment carries the marker.
cat > "${FIX}/no-match.json" <<'JSON'
[{"id":101,"body":"a review comment with a brace }","user":{"id":5,"login":"rev"}},{"id":900,"body":"another review comment","user":{"id":5,"login":"rev"}}]
JSON

# Legacy single-marker mode: the marker is present and a later comment follows it.
cat > "${FIX}/legacy-then-more.json" <<'JSON'
[{"id":746,"body":"old legacy body\n<!-- ci-marker-demo -->","user":{"id":5,"login":"ci"}},{"id":900,"body":"later review comment","user":{"id":5,"login":"rev"}}]
JSON

# Legacy single-marker mode, marker comment last.
cat > "${FIX}/legacy-last.json" <<'JSON'
[{"id":500,"body":"some tool output\n<!-- ci-marker-demo -->","user":{"id":5,"login":"ci"}}]
JSON

# Legacy single-marker mode, no marker anywhere.
cat > "${FIX}/legacy-no-match.json" <<'JSON'
[{"id":500,"body":"some tool output with no marker","user":{"id":5,"login":"ci"}}]
JSON

# Decoder drift, not a server response: an id that is not a single integer. The guard
# has to refuse it here, because the value's next stop is the PATCH URL.
cat > "${FIX}/malformed-id.json" <<'JSON'
[{"id":"746x","body":"<!-- ci-coverage -->\n## Coverage summary\n\nTotal lines 31995","user":{"id":5,"login":"ci"}}]
JSON

cat > "${WORK}/dotnet.md" <<'BODY'
# Veldrath .NET coverage (all test projects)
## Coverage summary

Total lines 32003
Line coverage 91.2%
BODY

cat > "${WORK}/web.md" <<'BODY'
# Veldrath web coverage
## Coverage summary

Web lines 8123
BODY

# The read-back check greps for the first non-blank line after "## Coverage summary".
DOTNET_TOKEN='Total lines 32003'

# ---------------------------------------------------------------------------
# Runners and assertions.
# ---------------------------------------------------------------------------
checks=0
failures=0
fail() { failures=$((failures + 1)); printf 'FAIL %s\n' "$*" >&2; }
check() { checks=$((checks + 1)); }

RUN_OUT=""
RUN_RC=0
STUB_MARKER="${SHARED_MARKER}"
STUB_TOKEN="${DOTNET_TOKEN}"

# run_script STUB_MARKER STUB_TOKEN FIXTURE [ARGS...]
run_script() {
    STUB_MARKER="$1"
    STUB_TOKEN="$2"
    local fixture="$3"
    shift 3

    rm -rf "${STUB}"
    mkdir -p "${STUB}"
    cp "${fixture}" "${STUB}/comments.json"

    # Pin BOTH base URLs: forgejo-api.sh prefers an already-set FORGEJO_API_URL over
    # FORGEJO_SERVER_URL, and the CI runner exports a FORGEJO_API_URL of its own, which
    # would otherwise send the script at the real server past the stub (and past PATH).
    PATH="${BIN}:${PATH}" \
    FORGEJO_SERVER_URL='http://stub.invalid' \
    FORGEJO_API_URL='http://stub.invalid/api/v1' \
    FORGEJO_REPOSITORY="${REPO}" \
    FORGEJO_TOKEN='offline-test-token' \
    STUB_DIR="${STUB}" \
    STUB_MARKER="${STUB_MARKER}" \
    STUB_TOKEN="${STUB_TOKEN}" \
        bash "${SCRIPT}" --pr "${PR}" "$@" > "${WORK}/run.out" 2>&1
    RUN_RC=$?
    RUN_OUT="$(cat "${WORK}/run.out")"
}

req_count() { cat "${STUB}/seq" 2>/dev/null || echo 0; }
req_field() { cat "${STUB}/req.$1.$2" 2>/dev/null || true; }

# first_request METHOD FIELD -> FIELD of the first recorded request with that method
first_request() {
    local want="$1" field="$2" i
    for ((i = 1; i <= $(req_count); i++)); do
        if [ "$(req_field "${i}" method)" = "${want}" ]; then
            req_field "${i}" "${field}"
            return 0
        fi
    done
    return 1
}

count_method() {
    local want="$1" i n=0
    for ((i = 1; i <= $(req_count); i++)); do
        [ "$(req_field "${i}" method)" = "${want}" ] && n=$((n + 1))
    done
    printf '%s' "${n}"
}

# first_payload_file METHOD -> path of the payload the first request of that method sent
first_payload_file() {
    local want="$1" i
    for ((i = 1; i <= $(req_count); i++)); do
        if [ "$(req_field "${i}" method)" = "${want}" ]; then
            printf '%s' "${STUB}/req.${i}.payload"
            return 0
        fi
    done
    return 1
}

assert_rc() { check; [ "${RUN_RC}" -eq "$1" ] || fail "$2: exit ${RUN_RC}, wanted $1"; }

# assert_line LABEL TEXT — TEXT must appear as a whole line of the script's output. This
# is what distinguishes "Updating ... 746 (section: dotnet)" from the broken
# "Updating ... 746" + "746 (section: dotnet)" that the duplicate id produced.
assert_line() {
    check
    printf '%s\n' "${RUN_OUT}" | grep -qxF -- "$2" || fail "$1: no output line [${RUN_OUT//$'\n'/ | }]"
}

assert_contains() {
    check
    printf '%s' "${RUN_OUT}" | grep -qF -- "$2" || fail "$1: output does not contain [$2] (${RUN_OUT//$'\n'/ | })"
}

assert_methods() { # LABEL METHOD WANT
    local got
    got="$(count_method "$2")"
    check
    [ "${got}" = "$3" ] || fail "$1: ${2} requests ${got}, wanted $3"
}

assert_url() { # LABEL METHOD WANT
    local got
    if ! got="$(first_request "$2" url)"; then
        check
        fail "$1: no ${2} request was sent"
        return 0
    fi
    check
    [ "${got}" = "$3" ] || fail "$1: ${2} url [${got}], wanted [$3]"
}

assert_occurrences() { # LABEL FILE NEEDLE WANT
    local got
    got="$(grep -oF -- "$3" "$2" 2>/dev/null | wc -l | tr -d ' ')"
    check
    [ "${got}" = "$4" ] || fail "$1: [$3] appears ${got} times in the payload, wanted $4"
}

assert_payload_occurrences() { # LABEL METHOD NEEDLE WANT
    local file
    if ! file="$(first_payload_file "$2")" || [ ! -f "${file}" ]; then
        check
        fail "$1: no ${2} request carried a payload"
        return 0
    fi
    assert_occurrences "$1" "${file}" "$3" "$4"
}

# Every recorded URL must be one line of the expected shape. A newline inside the id —
# the reported failure — fails this even when the method and the first line look right.
assert_scalar_urls() {
    local i url
    for ((i = 1; i <= $(req_count); i++)); do
        url="$(req_field "${i}" url)"
        check
        printf '%s' "${url}" \
            | grep -Eq '^http://stub\.invalid/api/v1/repos/Veldrath/Veldrath/(issues/[0-9]+/comments(\?limit=100)?|issues/comments/[0-9]+)$' \
            || fail "$1: request ${i} is not a scalar URL [${url//$'\n'/\\n}]"
    done
}

# ---------------------------------------------------------------------------
# Case A — the reported failure: shared marker, match followed by later comments.
# ---------------------------------------------------------------------------
run_script "${SHARED_MARKER}" "${DOTNET_TOKEN}" "${FIX}/coverage-then-more.json" \
    --section dotnet --title '.NET (all test projects)' --body-file "${WORK}/dotnet.md"

assert_rc 0 'A: exit'
assert_methods 'A' PATCH 1
assert_methods 'A' POST 0
assert_methods 'A' DELETE 0
assert_url 'A' PATCH 'http://stub.invalid/api/v1/repos/Veldrath/Veldrath/issues/comments/746'
assert_line 'A: one-line update message' 'Updating the shared coverage comment 746 (section: dotnet) ...'
assert_contains 'A: read-back accepted' 'Comment posted.'
assert_scalar_urls 'A'
assert_payload_occurrences 'A: shared marker once' PATCH '<!-- ci-coverage -->' 1
assert_payload_occurrences 'A: dotnet section opened once' PATCH '<!-- coverage-section:dotnet -->' 1
assert_payload_occurrences 'A: dotnet section closed once' PATCH '<!-- /coverage-section:dotnet -->' 1
assert_payload_occurrences 'A: .NET heading once' PATCH '### .NET (all test projects)' 1
assert_payload_occurrences 'A: new numbers once' PATCH 'Total lines 32003' 1
assert_payload_occurrences 'A: old numbers replaced' PATCH 'Total lines 31995' 0
assert_payload_occurrences 'A: web section kept' PATCH '<!-- coverage-section:web -->' 1
assert_payload_occurrences 'A: web body kept once' PATCH 'Web lines 31000' 1

# ---------------------------------------------------------------------------
# Case B — the match is the last comment: the END-only path.
# ---------------------------------------------------------------------------
run_script "${SHARED_MARKER}" "${DOTNET_TOKEN}" "${FIX}/coverage-last.json" \
    --section dotnet --title '.NET (all test projects)' --body-file "${WORK}/dotnet.md"

assert_rc 0 'B: exit'
assert_methods 'B' PATCH 1
assert_methods 'B' POST 0
assert_url 'B' PATCH 'http://stub.invalid/api/v1/repos/Veldrath/Veldrath/issues/comments/746'
assert_line 'B: one-line update message' 'Updating the shared coverage comment 746 (section: dotnet) ...'
assert_scalar_urls 'B'
assert_payload_occurrences 'B: dotnet section opened once' PATCH '<!-- coverage-section:dotnet -->' 1
assert_payload_occurrences 'B: web body kept once' PATCH 'Web lines 31000' 1

# ---------------------------------------------------------------------------
# Case C — no comment carries the marker: create, never patch.
# ---------------------------------------------------------------------------
run_script "${SHARED_MARKER}" "${DOTNET_TOKEN}" "${FIX}/no-match.json" \
    --section dotnet --title '.NET (all test projects)' --body-file "${WORK}/dotnet.md"

assert_rc 0 'C: exit'
assert_methods 'C' PATCH 0
assert_methods 'C' POST 1
assert_url 'C' POST 'http://stub.invalid/api/v1/repos/Veldrath/Veldrath/issues/42/comments'
assert_line 'C: create message' 'Creating the shared coverage comment on pull request 42 (section: dotnet) ...'
assert_scalar_urls 'C'
assert_payload_occurrences 'C: new numbers written once' POST 'Total lines 32003' 1
assert_payload_occurrences 'C: dotnet section opened once' POST '<!-- coverage-section:dotnet -->' 1

# ---------------------------------------------------------------------------
# Case D — two comments carry the shared marker: keep the first, delete the rest.
# ---------------------------------------------------------------------------
run_script "${SHARED_MARKER}" "${DOTNET_TOKEN}" "${FIX}/coverage-duplicate.json" \
    --section dotnet --title '.NET (all test projects)' --body-file "${WORK}/dotnet.md"

assert_rc 0 'D: exit'
assert_methods 'D' PATCH 1
assert_methods 'D' POST 0
assert_methods 'D' DELETE 1
assert_url 'D' DELETE 'http://stub.invalid/api/v1/repos/Veldrath/Veldrath/issues/comments/750'
assert_url 'D' PATCH 'http://stub.invalid/api/v1/repos/Veldrath/Veldrath/issues/comments/746'
assert_scalar_urls 'D'
assert_payload_occurrences 'D: kept comment was the base' PATCH 'Web lines 31000' 1
assert_payload_occurrences 'D: kept body replaced' PATCH 'Total lines 31995' 0
assert_payload_occurrences 'D: superseded body dropped' PATCH 'Total lines 31990' 0
assert_payload_occurrences 'D: new numbers written once' PATCH 'Total lines 32003' 1

# ---------------------------------------------------------------------------
# Case E — legacy --marker mode, match followed by later comments.
# ---------------------------------------------------------------------------
run_script "${LEGACY_MARKER}" '' "${FIX}/legacy-then-more.json" \
    --marker "${LEGACY_MARKER}" --body-file "${WORK}/web.md"

assert_rc 0 'E: exit'
assert_methods 'E' PATCH 1
assert_methods 'E' POST 0
assert_url 'E' PATCH 'http://stub.invalid/api/v1/repos/Veldrath/Veldrath/issues/comments/746'
assert_line 'E: one-line update message' 'Updating comment 746 ...'
assert_scalar_urls 'E'
assert_payload_occurrences 'E: marker once' PATCH "${LEGACY_MARKER}" 1
assert_payload_occurrences 'E: new body written once' PATCH 'Web lines 8123' 1
assert_payload_occurrences 'E: whole body replaced' PATCH 'old legacy body' 0

# ---------------------------------------------------------------------------
# Case F — legacy --marker mode, marker comment last.
# ---------------------------------------------------------------------------
run_script "${LEGACY_MARKER}" '' "${FIX}/legacy-last.json" \
    --marker "${LEGACY_MARKER}" --body-file "${WORK}/web.md"

assert_rc 0 'F: exit'
assert_methods 'F' PATCH 1
assert_methods 'F' POST 0
assert_url 'F' PATCH 'http://stub.invalid/api/v1/repos/Veldrath/Veldrath/issues/comments/500'
assert_line 'F: one-line update message' 'Updating comment 500 ...'
assert_scalar_urls 'F'
assert_payload_occurrences 'F: marker once' PATCH "${LEGACY_MARKER}" 1

# ---------------------------------------------------------------------------
# Case G — legacy --marker mode, no marker anywhere: create.
# ---------------------------------------------------------------------------
run_script "${LEGACY_MARKER}" '' "${FIX}/legacy-no-match.json" \
    --marker "${LEGACY_MARKER}" --body-file "${WORK}/web.md"

assert_rc 0 'G: exit'
assert_methods 'G' PATCH 0
assert_methods 'G' POST 1
assert_url 'G' POST 'http://stub.invalid/api/v1/repos/Veldrath/Veldrath/issues/42/comments'
assert_line 'G: create message' 'Creating a new comment on pull request 42 ...'
assert_scalar_urls 'G'
assert_payload_occurrences 'G: marker appended once' POST "${LEGACY_MARKER}" 1

# ---------------------------------------------------------------------------
# Case H — a non-integer id fails loudly and sends nothing, instead of being spliced
# into a URL for curl to reject with "(3) bad/illegal URL".
# ---------------------------------------------------------------------------
run_script "${SHARED_MARKER}" "${DOTNET_TOKEN}" "${FIX}/malformed-id.json" \
    --section dotnet --title '.NET (all test projects)' --body-file "${WORK}/dotnet.md"

assert_rc 1 'H: exit'
assert_methods 'H' PATCH 0
assert_methods 'H' POST 0
assert_methods 'H' DELETE 0
assert_contains 'H: names the malformed id' 'malformed id: 746x'

printf '\n%d checks, %d failed\n' "${checks}" "${failures}"
[ "${failures}" -eq 0 ]
