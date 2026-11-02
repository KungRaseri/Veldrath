#!/usr/bin/env bash
#
# Post or update a sticky comment on a Forgejo pull request.
#
# This replaces marocchino/sticky-pull-request-comment, which is GitHub-only. The
# comment is found by a hidden marker string: if a comment containing the marker
# already exists it is edited in place, otherwise a new one is created. That keeps
# repeated CI runs from piling up one comment per push.
#
# Required environment:
#   FORGEJO_TOKEN       In CI this is the per-job token (secrets.GITEA_TOKEN).
#   FORGEJO_REPOSITORY  owner/repo. Defaults to GITHUB_REPOSITORY in Actions.
#
# Usage:
#   scripts/forgejo-pr-comment.sh --pr 42 \
#       --body-file CoverageReport/SummaryGithub.md \
#       --marker '<!-- ci-coverage-server -->'
#
# Exits 0 without doing anything when there is no PR number or no body file, so a
# missing coverage report never fails a build over a comment.
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/forgejo-api.sh
. "${SCRIPT_DIR}/lib/forgejo-api.sh"

PR_NUMBER=""
BODY_FILE=""
MARKER=""

usage() {
    sed -n '3,25p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-1}"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --pr)        [ $# -ge 2 ] || { echo "error: --pr needs a value" >&2; exit 2; }; PR_NUMBER="$2"; shift 2 ;;
        --body-file) [ $# -ge 2 ] || { echo "error: --body-file needs a value" >&2; exit 2; }; BODY_FILE="$2"; shift 2 ;;
        --marker)    [ $# -ge 2 ] || { echo "error: --marker needs a value" >&2; exit 2; }; MARKER="$2"; shift 2 ;;
        --help|-h)   usage 0 ;;
        *)           echo "error: unknown argument: $1" >&2; usage 2 ;;
    esac
done

[ -n "${MARKER}" ] || { echo "error: --marker is required" >&2; exit 2; }

if [ -z "${PR_NUMBER}" ] || [ "${PR_NUMBER}" = "0" ] || [ "${PR_NUMBER}" = "null" ]; then
    echo "No pull request number available; skipping the comment."
    exit 0
fi
if [ ! -s "${BODY_FILE}" ]; then
    echo "No body at ${BODY_FILE}; skipping the comment."
    exit 0
fi

forgejo_init --require-repository

# The marker is appended so the next run can find this comment again
COMMENT_BODY="$(cat "${BODY_FILE}")

${MARKER}"
BODY_JSON="$(printf '%s' "${COMMENT_BODY}" | json_escape)"

echo "Looking for an existing comment carrying the marker ..."
# Walk the comment list object by object so the id always belongs to the comment
# whose body carries the marker, rather than to a neighbouring nested user object.
EXISTING_COMMENT_ID=""
while IFS= read -r COMMENT_CHUNK; do
    [ -n "${COMMENT_CHUNK}" ] || continue
    case "${COMMENT_CHUNK}" in
        *"${MARKER}"*)
            EXISTING_COMMENT_ID="$(printf '%s' "${COMMENT_CHUNK}" | json_extract id)"
            break
            ;;
    esac
done <<COMMENT_LIST
$(forgejo_get_optional "/repos/${FORGEJO_REPOSITORY}/issues/${PR_NUMBER}/comments?limit=100" | tr '}' '\n')
COMMENT_LIST

RESPONSE_FILE="$(mktemp)"
if [ -n "${EXISTING_COMMENT_ID}" ]; then
    echo "Updating comment ${EXISTING_COMMENT_ID} ..."
    CODE="$(forgejo_send PATCH "/repos/${FORGEJO_REPOSITORY}/issues/comments/${EXISTING_COMMENT_ID}" \
        "{\"body\":\"${BODY_JSON}\"}" "${RESPONSE_FILE}")"
    if [ "${CODE}" != "200" ] && [ "${CODE}" != "201" ]; then
        echo "error: updating the comment failed with HTTP ${CODE}" >&2
        cat "${RESPONSE_FILE}" >&2
        rm -f "${RESPONSE_FILE}"
        exit 1
    fi
else
    echo "Creating a new comment on pull request ${PR_NUMBER} ..."
    CODE="$(forgejo_send POST "/repos/${FORGEJO_REPOSITORY}/issues/${PR_NUMBER}/comments" \
        "{\"body\":\"${BODY_JSON}\"}" "${RESPONSE_FILE}")"
    if [ "${CODE}" != "201" ] && [ "${CODE}" != "200" ]; then
        echo "error: creating the comment failed with HTTP ${CODE}" >&2
        cat "${RESPONSE_FILE}" >&2
        rm -f "${RESPONSE_FILE}"
        exit 1
    fi
fi
rm -f "${RESPONSE_FILE}"

echo "Comment posted."
