#!/usr/bin/env bash
#
# Run the coverage reporter without keeping a copy of it in this repository.
#
# The tool lives in its own repository (codecov/codecov-report) and is pinned to a
# release tag here: bump COVERAGE_REPORT_REF to adopt a new version. It is fetched on
# demand into .coverage-report/<ref>/ (gitignored) and run with the .NET SDK that
# scripts/ci-toolchain.sh installs.
#
# It takes ReportGenerator's arguments (-reports:, -targetdir:, -reporttypes:, ...),
# computes method and full-method coverage without a PRO license, and adds
# -minimumcoverage: for a coverage gate.
#
# Usage:
#   scripts/coverage-report.sh "-reports:./TestResults/*/coverage.cobertura.xml" \
#       "-targetdir:./CoverageReport" "-reporttypes:Html;Cobertura;MarkdownSummaryGithub"
#
# Environment overrides:
#   COVERAGE_REPORT_REPO  default https://git.kungraseri.cloud/codecov/codecov-report.git
#   COVERAGE_REPORT_REF   default v1.0.2
#   COVERAGE_REPORT_DIR   default <repo>/.coverage-report/<ref>
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TOOL_REPO="${COVERAGE_REPORT_REPO:-https://git.kungraseri.cloud/codecov/codecov-report.git}"
TOOL_REF="${COVERAGE_REPORT_REF:-v1.0.2}"
TOOL_DIR="${COVERAGE_REPORT_DIR:-${REPO_ROOT}/.coverage-report/${TOOL_REF}}"
PROJECT="${TOOL_DIR}/src/CoverageReport/CoverageReport.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
    echo "error: dotnet is not on PATH (run scripts/ci-toolchain.sh first)" >&2
    exit 127
fi

if [ ! -f "${PROJECT}" ]; then
    echo "Fetching coverage reporter ${TOOL_REF} from ${TOOL_REPO} ..."
    mkdir -p "$(dirname "${TOOL_DIR}")"
    rm -rf "${TOOL_DIR}"
    CLONE_ARGS=(clone --quiet --depth 1 --branch "${TOOL_REF}")
    # The repository is public, so an anonymous clone works. When a token is present
    # it is used anyway, which keeps a private mirror or fork working unchanged.
    if [ -n "${GITEA_TOKEN:-}" ]; then
        git -c advice.detachedHead=false -c "http.extraHeader=Authorization: token ${GITEA_TOKEN}" "${CLONE_ARGS[@]}" "${TOOL_REPO}" "${TOOL_DIR}"
    else
        git -c advice.detachedHead=false "${CLONE_ARGS[@]}" "${TOOL_REPO}" "${TOOL_DIR}"
    fi
fi

# --verbosity quiet hides the restore/build chatter; the tool prints its own summary.
exec dotnet run --project "${PROJECT}" --configuration Release --verbosity quiet -- "$@"
