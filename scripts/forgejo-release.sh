#!/usr/bin/env bash
#
# Publish a release on Forgejo, with attached assets.
#
# Releases used to be published by softprops/action-gh-release, which only works
# against GitHub. Forgejo has a first-class releases API, so this script talks to
# it directly and needs no third-party action at all.
#
# Required environment:
#   FORGEJO_TOKEN       An access token with `write:repository` scope. In CI this
#                       is the token Forgejo mints per job (secrets.GITEA_TOKEN).
#   FORGEJO_REPOSITORY  owner/repo, e.g. Veldrath/Veldrath.
#
# Optional environment:
#   FORGEJO_SERVER_URL  Defaults to https://git.kungraseri.cloud
#
# Usage:
#   scripts/forgejo-release.sh --tag server/v0.1.42 \
#       --name "Veldrath Server 0.1.42" \
#       --body-file release-body.md \
#       --asset Veldrath-Server-0.1.42.zip
#
# Idempotent: re-running for the same tag reuses the existing release and
# replaces assets of the same name, so a re-run of the package job does not fail
# with "release already exists" or duplicate attachments.
#
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/forgejo-api.sh
. "${SCRIPT_DIR}/lib/forgejo-api.sh"

TAG=""
NAME=""
BODY_FILE=""
ASSETS=()

usage() {
    sed -n '3,28p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-1}"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --tag)        [ $# -ge 2 ] || { echo "error: --tag needs a value" >&2; exit 2; }; TAG="$2"; shift 2 ;;
        --name)       [ $# -ge 2 ] || { echo "error: --name needs a value" >&2; exit 2; }; NAME="$2"; shift 2 ;;
        --body-file)  [ $# -ge 2 ] || { echo "error: --body-file needs a value" >&2; exit 2; }; BODY_FILE="$2"; shift 2 ;;
        --asset)      [ $# -ge 2 ] || { echo "error: --asset needs a value" >&2; exit 2; }; ASSETS+=("$2"); shift 2 ;;
        --help|-h)    usage 0 ;;
        *)            echo "error: unknown argument: $1" >&2; usage 2 ;;
    esac
done

[ -n "${TAG}" ] || { echo "error: --tag is required" >&2; exit 2; }
[ -n "${NAME}" ] || NAME="${TAG}"
[ -n "${BODY_FILE}" ] || { echo "error: --body-file is required" >&2; exit 2; }
[ -f "${BODY_FILE}" ] || { echo "error: body file not found: ${BODY_FILE}" >&2; exit 2; }

forgejo_init --require-repository

RELEASES_PATH="/repos/${FORGEJO_REPOSITORY}/releases"

echo "Looking for an existing release for tag ${TAG} ..."
RELEASE_ID="$(forgejo_get_optional "${RELEASES_PATH}/tags/${TAG}" | json_extract id || true)"

if [ -n "${RELEASE_ID}" ]; then
    echo "Release already exists (id ${RELEASE_ID}); reusing it."
    RELEASE_HTML_URL="$(forgejo_get_optional "${RELEASES_PATH}/${RELEASE_ID}" | json_string html_url || true)"
else
    echo "Creating release ${TAG} ..."
    BODY_ESCAPED="$(json_escape < "${BODY_FILE}")"
    NAME_ESCAPED="$(printf '%s' "${NAME}" | json_escape)"
    TAG_ESCAPED="$(printf '%s' "${TAG}" | json_escape)"

    RESPONSE_FILE="$(mktemp)"
    HTTP_CODE="$(forgejo_send POST "${RELEASES_PATH}" \
        "{\"tag_name\":\"${TAG_ESCAPED}\",\"name\":\"${NAME_ESCAPED}\",\"body\":\"${BODY_ESCAPED}\",\"draft\":false,\"prerelease\":false}" \
        "${RESPONSE_FILE}")"

    if [ "${HTTP_CODE}" != "201" ] && [ "${HTTP_CODE}" != "200" ]; then
        echo "error: release creation failed with HTTP ${HTTP_CODE}" >&2
        cat "${RESPONSE_FILE}" >&2
        rm -f "${RESPONSE_FILE}"
        exit 1
    fi

    RELEASE_ID="$(json_extract id < "${RESPONSE_FILE}")"
    RELEASE_HTML_URL="$(json_string html_url < "${RESPONSE_FILE}")"
    rm -f "${RESPONSE_FILE}"

    [ -n "${RELEASE_ID}" ] || { echo "error: could not read the new release id" >&2; exit 1; }
    echo "Created release ${TAG} (id ${RELEASE_ID})."
fi

# Replace an asset of the same name so re-runs stay idempotent
for ASSET in "${ASSETS[@]}"; do
    [ -f "${ASSET}" ] || { echo "error: asset not found: ${ASSET}" >&2; exit 1; }
    ASSET_NAME="$(basename "${ASSET}")"

    # Walk the asset list object by object: splitting on '}' keeps each asset's id
    # next to its own name, so a name match cannot pick up a different asset's id.
    EXISTING_ASSET_ID=""
    while IFS= read -r ASSET_CHUNK; do
        [ -n "${ASSET_CHUNK}" ] || continue
        if [ "$(printf '%s' "${ASSET_CHUNK}" | json_string name)" = "${ASSET_NAME}" ]; then
            EXISTING_ASSET_ID="$(printf '%s' "${ASSET_CHUNK}" | json_extract id)"
            break
        fi
    done <<ASSET_LIST
$(forgejo_get_optional "${RELEASES_PATH}/${RELEASE_ID}/assets" | tr '}' '\n')
ASSET_LIST

    RESPONSE_FILE="$(mktemp)"
    if [ -n "${EXISTING_ASSET_ID}" ]; then
        echo "Replacing existing asset ${ASSET_NAME} (id ${EXISTING_ASSET_ID}) ..."
        forgejo_send DELETE "${RELEASES_PATH}/${RELEASE_ID}/assets/${EXISTING_ASSET_ID}" "" "${RESPONSE_FILE}" > /dev/null
    fi

    echo "Uploading ${ASSET_NAME} ..."
    UPLOAD_STARTED="$(date +%s)"
    UPLOAD_CODE="$(forgejo_upload "${RELEASES_PATH}/${RELEASE_ID}/assets?name=${ASSET_NAME}" attachment "${ASSET}" "${RESPONSE_FILE}")"
    if [ "${UPLOAD_CODE}" != "201" ] && [ "${UPLOAD_CODE}" != "200" ]; then
        echo "error: uploading ${ASSET_NAME} failed with HTTP ${UPLOAD_CODE}" >&2
        cat "${RESPONSE_FILE}" >&2
        rm -f "${RESPONSE_FILE}"
        exit 1
    fi
    rm -f "${RESPONSE_FILE}"
    echo "Uploaded ${ASSET_NAME} in $(( $(date +%s) - UPLOAD_STARTED ))s"
done

if [ -n "${RELEASE_HTML_URL:-}" ]; then
    echo "Release published: ${RELEASE_HTML_URL}"
else
    echo "Release published (id ${RELEASE_ID})."
fi
