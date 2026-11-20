#!/usr/bin/env bash
#
# Post or update a sticky comment on a Forgejo pull request.
#
# This replaces marocchino/sticky-pull-request-comment, which is GitHub-only.
#
# Two modes:
#   --marker '<html comment>'            one comment per marker, replaced in place.
#   --marker '<html comment>' --section <name> --title <heading>
#                                        one comment per marker holding several named
#                                        sections (".NET", "Web", "E2E", ...). Each job
#                                        replaces only its own section, so concurrent
#                                        workflows never clobber each other's numbers.
#                                        --marker defaults to the coverage comment, and the
#                                        legacy one-comment-per-job coverage comments are
#                                        deleted for that marker only.
#
# Options:
#   --heading <markdown>   heading printed above the sections. Default '## Coverage
#                          summary'; the e2e reporter passes '## E2E summary' and its own
#                          marker, so coverage and test results stay separate documents.
#
# Required environment:
#   FORGEJO_TOKEN       In CI this is the per-job token (secrets.GITEA_TOKEN).
#   FORGEJO_REPOSITORY  owner/repo. Defaults to GITHUB_REPOSITORY in Actions.
#
# Usage:
#   scripts/forgejo-pr-comment.sh --pr 42 --section dotnet --title '.NET (all test projects)' \
#       --body-file CoverageReport/SummaryGithub.md
#   scripts/forgejo-pr-comment.sh --pr 42 --marker '<!-- ci-e2e -->' --heading '## E2E summary' \
#       --section e2e --title 'E2E (Playwright)' --body-file E2EReport/Summary.md
#
# Exits 0 without doing anything when there is no PR number or no body file, so a
# missing report never fails a build over a comment.
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/forgejo-api.sh
. "${SCRIPT_DIR}/lib/forgejo-api.sh"

SHARED_MARKER='<!-- ci-coverage -->'
LEGACY_MARKERS=(
    '<!-- ci-coverage-engine -->'
    '<!-- ci-coverage-server -->'
    '<!-- ci-coverage-discord -->'
    '<!-- ci-coverage-gate -->'
    '<!-- ci-coverage-web -->'
)

PR_NUMBER=""
BODY_FILE=""
MARKER=""
SECTION=""
TITLE=""
HEADING=""

# Print the header comment above `set -euo pipefail` as the help text. Derived from the file
# rather than a line range: the range silently stopped matching every time the header grew.
usage() {
    awk 'NR == 1 { next } /^set -euo pipefail$/ { exit } { sub(/^# ?/, ""); print }' "$0"
    exit "${1:-1}"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --pr)        [ $# -ge 2 ] || { echo "error: --pr needs a value" >&2; exit 2; }; PR_NUMBER="$2"; shift 2 ;;
        --body-file) [ $# -ge 2 ] || { echo "error: --body-file needs a value" >&2; exit 2; }; BODY_FILE="$2"; shift 2 ;;
        --marker)    [ $# -ge 2 ] || { echo "error: --marker needs a value" >&2; exit 2; }; MARKER="$2"; shift 2 ;;
        --section)   [ $# -ge 2 ] || { echo "error: --section needs a value" >&2; exit 2; }; SECTION="$2"; shift 2 ;;
        --title)     [ $# -ge 2 ] || { echo "error: --title needs a value" >&2; exit 2; }; TITLE="$2"; shift 2 ;;
        --heading)   [ $# -ge 2 ] || { echo "error: --heading needs a value" >&2; exit 2; }; HEADING="$2"; shift 2 ;;
        --help|-h)   usage 0 ;;
        *)           echo "error: unknown argument: $1" >&2; usage 2 ;;
    esac
done

HEADING="${HEADING:-## Coverage summary}"

if [ -n "${SECTION}" ]; then
    [ -n "${TITLE}" ] || { echo "error: --section needs --title" >&2; exit 2; }
    # Default keeps every existing coverage caller byte-identical; the e2e reporter passes its
    # own marker so its sections live in their own comment.
    MARKER="${MARKER:-${SHARED_MARKER}}"
else
    [ -n "${MARKER}" ] || { echo "error: --marker (or --section) is required" >&2; exit 2; }
fi

if [ -z "${PR_NUMBER}" ] || [ "${PR_NUMBER}" = "0" ] || [ "${PR_NUMBER}" = "null" ]; then
    echo "No pull request number available; skipping the comment."
    exit 0
fi
if [ ! -s "${BODY_FILE}" ]; then
    echo "No body at ${BODY_FILE}; skipping the comment."
    exit 0
fi

forgejo_init --require-repository

WORK="$(mktemp -d)"
RESPONSE_FILE="${WORK}/response.json"
BODY_NEW="${WORK}/body.md"
trap 'rm -rf "${WORK}"' EXIT

# Fetch every comment, decoded to "@@COMMENT <id>" + body lines.
fetch_comments() {
    forgejo_get_optional "/repos/${FORGEJO_REPOSITORY}/issues/${PR_NUMBER}/comments?limit=100" \
        | awk -f "${SCRIPT_DIR}/lib/comment-json.awk" > "$1"
}

# find_comment FILE OUT_BODY -> prints the id of the comment holding $MARKER, body to OUT_BODY
find_comment() {
    awk -v marker="${MARKER}" '
        # The match calls exit(), which still runs END — and END calls flush() again.
        # Without the guard the id was printed twice, so COMMENT_ID held "746\n746"
        # and the PATCH URL became `.../comments/746<newline>746`, which curl rejects
        # as `(3) bad/illegal URL`. The body was appended to OUT_BODY twice as well.
        function flush() {
            if (found) { return }
            if (id != "" && index(body, marker) > 0) {
                print id > "/dev/stdout"
                printf "%s", body > out
                found = 1
                exit
            }
        }
        /^@@COMMENT / { flush(); id = $2; body = ""; next }
        { body = body $0 "\n" }
        END { flush() }
    ' out="$2" "$1"
}

# validate_comment_id ID -> 0 when ID is empty (no comment carries the marker) or a single
# integer. Anything else is a decoder bug, not a server condition: it used to travel into
# the PATCH URL and fail there as an opaque `curl (3) bad/illegal URL`.
validate_comment_id() {
    local id="$1"
    [ -n "${id}" ] || return 0
    case "${id}" in
        *[!0-9]*)
            echo "error: comment lookup returned a malformed id: $(printf '%s' "${id}" | tr '\n' ' ')" >&2
            return 1
            ;;
    esac
}

# ids_of_comments_containing FILE STRING -> ids whose body contains STRING
ids_of_comments_containing() {
    awk -v needle="$2" '
        /^@@COMMENT / { if (id != "" && index(body, needle) > 0) print id; id = $2; body = ""; next }
        { body = body $0 "\n" }
        END { if (id != "" && index(body, needle) > 0) print id }
    ' "$1"
}

# The line that proves our section landed, used for the read-back check. It is the first
# non-blank line after the comment's heading, and the heading is now an option: the e2e comment
# says "## E2E summary", so a hard-coded "## Coverage summary" would silently disable the check.
TOKEN="$(awk -v heading="${HEADING}" 'BEGIN{seen=0} $0 == heading {seen=1; next} seen && /^[[:space:]]*$/{next} seen{print; exit}' "${BODY_FILE}")"

if [ -n "${SECTION}" ]; then
    COMMENT_ID=""
    for attempt in 1 2 3 4; do
        fetch_comments "${WORK}/comments.txt"
        rm -f "${WORK}/existing.md"
        COMMENT_ID="$(find_comment "${WORK}/comments.txt" "${WORK}/existing.md")"
        validate_comment_id "${COMMENT_ID}" || exit 1

        # Drop the old one-comment-per-job coverage comments (and duplicate copies of the
        # comment we are about to write) so the PR ends up with one comment per marker. The
        # legacy markers belong to the coverage comment, so they are only cleaned up there.
        LEGACY_IDS=""
        if [ "${MARKER}" = "${SHARED_MARKER}" ]; then
            for legacy in "${LEGACY_MARKERS[@]}"; do
                ids="$(ids_of_comments_containing "${WORK}/comments.txt" "${legacy}")"
                [ -n "${ids}" ] && LEGACY_IDS="${LEGACY_IDS}${ids}"$'\n'
            done
        fi
        if [ -n "${COMMENT_ID}" ]; then
            duplicates="$(ids_of_comments_containing "${WORK}/comments.txt" "${MARKER}" | grep -v -x "${COMMENT_ID}" || true)"
            [ -n "${duplicates}" ] && LEGACY_IDS="${LEGACY_IDS}${duplicates}"$'\n'
        fi

        if [ -n "${LEGACY_IDS}" ]; then
            LEGACY_IDS="$(printf '%s' "${LEGACY_IDS}" | sort -u)"
            while IFS= read -r stale; do
                [ -n "${stale}" ] || continue
                echo "Removing superseded comment ${stale} ..."
                CODE="$(forgejo_send DELETE "/repos/${FORGEJO_REPOSITORY}/issues/comments/${stale}" "" "${RESPONSE_FILE}")"
                case "${CODE}" in
                    200|204) ;;
                    *) echo "warning: could not delete comment ${stale} (HTTP ${CODE})" >&2 ;;
                esac
            done <<< "${LEGACY_IDS}"
        fi

        # Build the merged body: our section replaced, every other section kept.
        : > "${WORK}/existing-body.md"
        if [ -s "${WORK}/existing.md" ]; then
            cp "${WORK}/existing.md" "${WORK}/existing-body.md"
        fi
        awk -v section="${SECTION}" -v title="${TITLE}" -v bodyfile="${BODY_FILE}" \
            -v marker="${MARKER}" -v heading="${HEADING}" \
            -f "${SCRIPT_DIR}/lib/merge-coverage-sections.awk" \
            "${WORK}/existing-body.md" > "${BODY_NEW}"

        BODY_JSON="$(json_escape < "${BODY_NEW}")"
        if [ -n "${COMMENT_ID}" ]; then
            echo "Updating the shared comment ${COMMENT_ID} (section: ${SECTION}) ..."
            CODE="$(forgejo_send PATCH "/repos/${FORGEJO_REPOSITORY}/issues/comments/${COMMENT_ID}" \
                "{\"body\":\"${BODY_JSON}\"}" "${RESPONSE_FILE}")"
            [ "${CODE}" = "200" ] || [ "${CODE}" = "201" ] || {
                echo "error: updating the comment failed with HTTP ${CODE}" >&2
                cat "${RESPONSE_FILE}" >&2
                exit 1
            }
        else
            echo "Creating the shared comment on pull request ${PR_NUMBER} (section: ${SECTION}) ..."
            CODE="$(forgejo_send POST "/repos/${FORGEJO_REPOSITORY}/issues/${PR_NUMBER}/comments" \
                "{\"body\":\"${BODY_JSON}\"}" "${RESPONSE_FILE}")"
            [ "${CODE}" = "201" ] || [ "${CODE}" = "200" ] || {
                echo "error: creating the comment failed with HTTP ${CODE}" >&2
                cat "${RESPONSE_FILE}" >&2
                exit 1
            }
        fi

        # Read back: a concurrent job may have written the comment between our read and
        # write, which would drop this section. Retry with the fresh body if so.
        fetch_comments "${WORK}/verify.txt"
        find_comment "${WORK}/verify.txt" "${WORK}/verify-body.md" >/dev/null
        if [ -z "${TOKEN}" ] || grep -qF -- "${TOKEN}" "${WORK}/verify-body.md"; then
            echo "Comment posted."
            exit 0
        fi

        if [ "${attempt}" -lt 4 ]; then
            echo "Section ${SECTION} was overwritten by a concurrent job; retrying ..."
            sleep 2
        fi
    done

    echo "warning: the shared comment does not show the ${SECTION} section after 4 attempts" >&2
    exit 0
fi

# Legacy single-marker mode.
COMMENT_BODY="$(cat "${BODY_FILE}")

${MARKER}"
BODY_JSON="$(printf '%s' "${COMMENT_BODY}" | json_escape)"

echo "Looking for an existing comment carrying the marker ..."
fetch_comments "${WORK}/comments.txt"
rm -f "${WORK}/existing.md"
COMMENT_ID="$(find_comment "${WORK}/comments.txt" "${WORK}/existing.md")"
validate_comment_id "${COMMENT_ID}" || exit 1

if [ -n "${COMMENT_ID}" ]; then
    echo "Updating comment ${COMMENT_ID} ..."
    CODE="$(forgejo_send PATCH "/repos/${FORGEJO_REPOSITORY}/issues/comments/${COMMENT_ID}" \
        "{\"body\":\"${BODY_JSON}\"}" "${RESPONSE_FILE}")"
    [ "${CODE}" = "200" ] || [ "${CODE}" = "201" ] || {
        echo "error: updating the comment failed with HTTP ${CODE}" >&2
        cat "${RESPONSE_FILE}" >&2
        exit 1
    }
else
    echo "Creating a new comment on pull request ${PR_NUMBER} ..."
    CODE="$(forgejo_send POST "/repos/${FORGEJO_REPOSITORY}/issues/${PR_NUMBER}/comments" \
        "{\"body\":\"${BODY_JSON}\"}" "${RESPONSE_FILE}")"
    [ "${CODE}" = "201" ] || [ "${CODE}" = "200" ] || {
        echo "error: creating the comment failed with HTTP ${CODE}" >&2
        cat "${RESPONSE_FILE}" >&2
        exit 1
    }
fi

echo "Comment posted."
