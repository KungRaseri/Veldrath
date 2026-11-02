#!/usr/bin/env bash
#
# Provision the CI toolchain without third-party setup actions.
#
# actions/setup-dotnet is not mirrored on data.forgejo.org, so the SDK is installed
# with dotnet-install.sh. It is installed under /usr/local and symlinked into
# /usr/local/bin rather than being exported through $GITHUB_PATH: the runner does
# not reliably honour that file, and when it does not, the install step succeeds and
# every later step fails with "dotnet: command not found". /usr/local/bin is on PATH
# in every step without help.
#
# Usage:
#   scripts/ci-toolchain.sh [dotnet-version] [--reportgenerator]
#
#   dotnet-version        defaults to $DOTNET_VERSION, then 10.0.402
#   --reportgenerator     also install dotnet-reportgenerator-globaltool and link it
#
# `zip` is provisioned when missing because every package job builds a zip archive
# and the base image is not guaranteed to carry it.
set -euo pipefail

DOTNET_VERSION="${DOTNET_VERSION:-10.0.402}"
WANT_REPORTGENERATOR=0

for arg in "$@"; do
    case "$arg" in
        --reportgenerator) WANT_REPORTGENERATOR=1 ;;
        --help|-h)
            sed -n '3,20p' "$0" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        -*) echo "error: unknown option: $arg" >&2; exit 2 ;;
        *)  DOTNET_VERSION="$arg" ;;
    esac
done

# Run a command, retrying through sudo when the runner is not root
run_privileged() {
    if "$@" 2>/dev/null; then
        return 0
    fi
    if command -v sudo >/dev/null 2>&1; then
        sudo "$@"
        return $?
    fi
    return 1
}

INSTALL_DIR=/usr/local/share/dotnet
if ! mkdir -p "$INSTALL_DIR" 2>/dev/null; then
    run_privileged mkdir -p "$INSTALL_DIR"
    run_privileged chown -R "$(id -u):$(id -g)" "$INSTALL_DIR" || true
fi

if [ ! -x "${INSTALL_DIR}/dotnet" ]; then
    echo "Installing the .NET SDK ${DOTNET_VERSION} into ${INSTALL_DIR} ..."
    curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
    bash /tmp/dotnet-install.sh --version "${DOTNET_VERSION}" --install-dir "${INSTALL_DIR}"
fi

if ! ln -sfn "${INSTALL_DIR}/dotnet" /usr/local/bin/dotnet 2>/dev/null; then
    run_privileged ln -sfn "${INSTALL_DIR}/dotnet" /usr/local/bin/dotnet
fi

export DOTNET_ROOT="${INSTALL_DIR}"
export PATH="/usr/local/bin:${PATH}"
if [ -n "${GITHUB_ENV:-}" ]; then
    echo "DOTNET_ROOT=${INSTALL_DIR}" >> "${GITHUB_ENV}"
fi

echo "dotnet: $(command -v dotnet) ($(dotnet --version))"

if [ "${WANT_REPORTGENERATOR}" = "1" ]; then
    if ! command -v reportgenerator >/dev/null 2>&1; then
        dotnet tool install --global dotnet-reportgenerator-globaltool
    fi
    REPORTGENERATOR_BIN="${HOME}/.dotnet/tools/reportgenerator"
    if [ -x "${REPORTGENERATOR_BIN}" ]; then
        if ! ln -sfn "${REPORTGENERATOR_BIN}" /usr/local/bin/reportgenerator 2>/dev/null; then
            run_privileged ln -sfn "${REPORTGENERATOR_BIN}" /usr/local/bin/reportgenerator
        fi
    fi
    echo "reportgenerator: $(command -v reportgenerator || echo missing)"
fi

if ! command -v zip >/dev/null 2>&1; then
    echo "Installing zip ..."
    if command -v apt-get >/dev/null 2>&1; then
        run_privileged apt-get update
        run_privileged apt-get install -y --no-install-recommends zip
    else
        echo "error: zip is required but no package manager is available" >&2
        exit 1
    fi
fi
