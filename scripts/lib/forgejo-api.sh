#!/usr/bin/env bash
#
# Shared Forgejo REST API helpers.
#
# Sourced by the CI shell steps that replaced GitHub-only actions:
#   scripts/forgejo-release.sh    (was softprops/action-gh-release)
#   scripts/forgejo-pr-comment.sh (was marocchino/sticky-pull-request-comment)
#
# Deliberately dependency-free: curl plus awk/sed only, so it runs on any runner
# image without jq, python or PowerShell.
#
# Environment (all optional when running inside Forgejo Actions):
#   FORGEJO_SERVER_URL  Base URL. Falls back to GITHUB_SERVER_URL, then
#                       https://git.kungraseri.cloud
#   FORGEJO_REPOSITORY  owner/repo. Falls back to GITHUB_REPOSITORY
#   FORGEJO_TOKEN       Access token. Falls back to GITEA_TOKEN, then GITHUB_TOKEN
#
# Call forgejo_init after parsing arguments; it validates and exports the above.

FORGEJO_SERVER_URL="${FORGEJO_SERVER_URL:-${GITHUB_SERVER_URL:-https://git.kungraseri.cloud}}"
FORGEJO_REPOSITORY="${FORGEJO_REPOSITORY:-${GITHUB_REPOSITORY:-}}"
FORGEJO_TOKEN="${FORGEJO_TOKEN:-${GITEA_TOKEN:-${GITHUB_TOKEN:-}}}"

# forgejo_init [--require-repository] [--require-token]
forgejo_init() {
    local require_repository=0 require_token=1
    while [ $# -gt 0 ]; do
        case "$1" in
            --require-repository) require_repository=1 ;;
            --no-token)            require_token=0 ;;
            *)                     ;;
        esac
        shift
    done

    FORGEJO_SERVER_URL="${FORGEJO_SERVER_URL%/}"
    FORGEJO_API_URL="${FORGEJO_API_URL:-${FORGEJO_SERVER_URL}/api/v1}"
    export FORGEJO_SERVER_URL FORGEJO_API_URL FORGEJO_REPOSITORY FORGEJO_TOKEN

    if [ "${require_repository}" = "1" ] && [ -z "${FORGEJO_REPOSITORY}" ]; then
        echo "error: FORGEJO_REPOSITORY (or GITHUB_REPOSITORY) must be set, e.g. Veldrath/Veldrath" >&2
        return 2
    fi
    if [ "${require_token}" = "1" ] && [ -z "${FORGEJO_TOKEN}" ]; then
        echo "error: a token is required: set FORGEJO_TOKEN (in CI: secrets.GITEA_TOKEN)" >&2
        return 2
    fi
}

# forgejo_url PATH -> absolute URL
forgejo_url() {
    printf '%s%s' "${FORGEJO_API_URL}" "$1"
}

# Escape a string for embedding in a JSON string literal.
json_escape() {
    awk 'BEGIN { ORS = "" }
    {
        gsub(/\\/, "\\\\")
        gsub(/"/, "\\\"")
        gsub(/\t/, "\\t")
        gsub(/\r/, "")
        printf "%s", $0
        printf "\\n"
    }' | sed 's/\\n$//'
}

# forgejo_get PATH -> body on stdout; exits non-zero on HTTP failure
forgejo_get() {
    curl -fsS -H "Authorization: token ${FORGEJO_TOKEN}" -H 'Accept: application/json' \
        "$(forgejo_url "$1")"
}

# forgejo_get_optional PATH -> body on stdout, empty when the resource is missing
forgejo_get_optional() {
    curl -sS -H "Authorization: token ${FORGEJO_TOKEN}" -H 'Accept: application/json' \
        "$(forgejo_url "$1")" 2>/dev/null || true
}

# forgejo_send METHOD PATH DATA OUTFILE -> prints the HTTP status code.
# The response body is written to OUTFILE rather than a global: callers capture
# the status code with $(...), which runs in a subshell, so a global set inside
# would never reach them.
forgejo_send() {
    local method="$1" path="$2" data="${3:-}" outfile="${4:?forgejo_send needs an output file}"

    local -a args=(-sS -o "${outfile}" -w '%{http_code}' -X "${method}"
        -H "Authorization: token ${FORGEJO_TOKEN}"
        -H 'Content-Type: application/json')
    [ -n "${data}" ] && args+=(-d "${data}")

    curl "${args[@]}" "$(forgejo_url "${path}")"
}

# forgejo_upload PATH FIELD FILE OUTFILE -> uploads a multipart attachment,
# prints the status code and writes the body to OUTFILE.
forgejo_upload() {
    local path="$1" field="$2" file="$3" outfile="${4:?forgejo_upload needs an output file}"
    curl -sS -o "${outfile}" -w '%{http_code}' -X POST \
        -H "Authorization: token ${FORGEJO_TOKEN}" \
        -F "${field}=@${file}" \
        "$(forgejo_url "${path}")"
}

# json_extract KEY -> first (unquoted) scalar value for KEY from stdin.
#
# Uses awk's index() rather than a sed `.*` prefix on purpose: a greedy prefix
# finds the LAST occurrence of the key, which in a Gitea/Forgejo release object
# is the nested author/repository id, not the release id. Whitespace around the
# colon is tolerated so pretty-printed responses work too.
json_extract() {
    awk -v key="$1" 'BEGIN { q = "\""; k = q key q }
    {
        i = index($0, k)
        if (i == 0) { exit }
        rest = substr($0, i + length(k))
        sub(/^[[:space:]]*:[[:space:]]*/, "", rest)
        sub(/^"/, "", rest)
        if (match(rest, /^[^",}]*/)) { print substr(rest, 1, RLENGTH) }
        exit
    }'
}

# json_string KEY -> first quoted string value for KEY from stdin
json_string() {
    awk -v key="$1" 'BEGIN { q = "\""; k = q key q }
    {
        i = index($0, k)
        if (i == 0) { exit }
        rest = substr($0, i + length(k))
        sub(/^[[:space:]]*:[[:space:]]*"/, "", rest)
        if (match(rest, /^[^"]*/)) { print substr(rest, 1, RLENGTH) }
        exit
    }'
}
