#!/usr/bin/env bash
#
# Generate Version Script (bash port of generate-version.ps1)
# Generates semantic version: [Major].[Minor].[Patch]-[git hash]
# Where Patch = commit count on current branch
#
# Component-specific versions are stored in versions/*.props:
#   --props-file versions/engine.props   -> RealmEngine libraries
#   --props-file versions/server.props   -> Veldrath.Server
#   --props-file versions/discord.props  -> Veldrath.Discord
#
# If --props-file is not given, reads VersionMajor/VersionMinor from the root
# Directory.Build.props (used by test projects and local dev where a specific
# component is not targeted).
#
# This exists because the Forgejo runner image is not guaranteed to ship
# PowerShell. It produces byte-identical output to the .ps1 script, so the two
# are interchangeable: local development and Windows can keep using the .ps1,
# while CI uses this one.
#
# Usage:
#   scripts/generate-version.sh [--output-format string|env|msbuild] [--props-file PATH]
#
set -euo pipefail

OUTPUT_FORMAT="string"
PROPS_FILE=""

usage() {
    sed -n '3,20p' "$0" | sed 's/^# \{0,1\}//'
    exit "${1:-1}"
}

while [ $# -gt 0 ]; do
    case "$1" in
        --output-format|-o)
            [ $# -ge 2 ] || { echo "error: --output-format needs a value" >&2; exit 2; }
            OUTPUT_FORMAT="$2"
            shift 2
            ;;
        --props-file|-p)
            [ $# -ge 2 ] || { echo "error: --props-file needs a value" >&2; exit 2; }
            PROPS_FILE="$2"
            shift 2
            ;;
        --help|-h)
            usage 0
            ;;
        *)
            echo "error: unknown argument: $1" >&2
            usage 2
            ;;
    esac
done

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

# Resolve the props file to read VersionMajor/VersionMinor from
if [ -z "${PROPS_FILE}" ]; then
    PROPS_FILE="${REPO_ROOT}/Directory.Build.props"
elif [ "${PROPS_FILE#/}" = "${PROPS_FILE}" ]; then
    # Relative paths resolve against the repo root, not the caller's cwd
    PROPS_FILE="${REPO_ROOT}/${PROPS_FILE}"
fi

if [ ! -f "${PROPS_FILE}" ]; then
    echo "error: props file not found at: ${PROPS_FILE}" >&2
    exit 1
fi

# Read a single <Tag>value</Tag> out of the MSBuild props file. Tolerates CRLF,
# the property sitting on a shared line, and attributes on the element itself —
# Directory.Build.props declares `<VersionMajor Condition="...">0</VersionMajor>`,
# which the PowerShell original mis-reads when no --props-file is given.
read_prop() {
    tr -d '\r' < "${PROPS_FILE}" \
        | sed -n "s/.*<$1[^>]*>[[:space:]]*\([^<]*\)[[:space:]]*<\/$1>.*/\1/p" \
        | head -1
}

VERSION_MAJOR="$(read_prop VersionMajor)"
VERSION_MINOR="$(read_prop VersionMinor)"

if [ -z "${VERSION_MAJOR}" ] || [ -z "${VERSION_MINOR}" ]; then
    echo "error: VersionMajor and VersionMinor must be set in: ${PROPS_FILE}" >&2
    exit 1
fi

# Git commit count as the patch version
if COMMIT_COUNT="$(git -C "${REPO_ROOT}" rev-list --count HEAD 2>/dev/null)"; then
    :
else
    echo "warning: not a git repository or git unavailable, using patch version 0" >&2
    COMMIT_COUNT=0
fi

if GIT_HASH="$(git -C "${REPO_ROOT}" rev-parse --short=7 HEAD 2>/dev/null)"; then
    :
else
    echo "warning: failed to get git hash, using 'dev'" >&2
    GIT_HASH="dev"
fi

VERSION="${VERSION_MAJOR}.${VERSION_MINOR}.${COMMIT_COUNT}-${GIT_HASH}"
VERSION_WITHOUT_HASH="${VERSION_MAJOR}.${VERSION_MINOR}.${COMMIT_COUNT}"
FILE_VERSION="${VERSION_MAJOR}.${VERSION_MINOR}.${COMMIT_COUNT}.0"

case "${OUTPUT_FORMAT}" in
    string)
        echo "${VERSION}"
        ;;
    env)
        # For CI environment files (GITHUB_ENV / GITEA_ENV)
        echo "VERSION=${VERSION}"
        echo "VERSION_NO_HASH=${VERSION_WITHOUT_HASH}"
        echo "FILE_VERSION=${FILE_VERSION}"
        echo "VERSION_MAJOR=${VERSION_MAJOR}"
        echo "VERSION_MINOR=${VERSION_MINOR}"
        echo "VERSION_PATCH=${COMMIT_COUNT}"
        echo "GIT_HASH=${GIT_HASH}"
        ;;
    msbuild)
        echo "/p:Version=${VERSION}"
        echo "/p:AssemblyVersion=${VERSION_WITHOUT_HASH}"
        echo "/p:FileVersion=${FILE_VERSION}"
        echo "/p:InformationalVersion=${VERSION}"
        ;;
    *)
        echo "error: unknown output format: ${OUTPUT_FORMAT}. Use 'string', 'env', or 'msbuild'" >&2
        exit 1
        ;;
esac
