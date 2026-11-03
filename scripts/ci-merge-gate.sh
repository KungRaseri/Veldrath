#!/usr/bin/env bash
# ci-merge-gate.sh — the always-on merge gate for `docs/ci-forgejo.md`.
#
# This script is the whole body of `.github/workflows/merge-gate.yml`, the one
# `pull_request` workflow that declares no `paths` filter. It exists because branch
# protection on `development` and `main` requires `status_check_contexts: ["*"]`:
# with zero statuses on the head commit Forgejo reads `pending` and refuses the merge
# (`services/pull/commit_status.go`, v15.0.11; measured on probe PR #32). A change that
# matches no other workflow's `pull_request.paths` list — a root dotfile, a `.slnx`,
# `docker-compose.dev.yml`, `config/grafana/**` — reports nothing, so this job is the
# status that keeps the `*` rule satisfiable.
#
# It must stay OFFLINE and DEPENDENCY-FREE — no network, no SDK, no npm, no docker, no
# service container — because it runs on every pull request. The only dependencies are
# bash, git, awk, sed and grep.
#
#   usage: bash scripts/ci-merge-gate.sh [--lint | --uncovered | --help]
#     --lint        (default) fatal checks; exit 0 clean, 1 violation, 2 usage error
#     --uncovered   list tracked files no pull_request.paths pattern matches; always exit 0
#
# Run it locally with: bash scripts/ci-merge-gate.sh --lint
#
# `--lint` enforces the four rules the documentation states (docs/ci-forgejo.md,
# "The always-on check that keeps * satisfiable"):
#   L1  every workflow file declares a top-level `name:` — that string is the status
#       context prefix.
#   L2  every real `uses:` names an action mirrored on this instance (see the table in
#       docs/ci-forgejo.md). Comment lines are ignored; local actions (`uses: ./...`)
#       are exempt.
#   L3  every `pull_request` workflow triggers on both `main` and `development`.
#   L4  at least one `pull_request` workflow declares neither `paths` nor `paths-ignore`.
#
# `--uncovered` re-implements the workflow-filter glob semantics so the inventory in
# docs/ci-forgejo.md can be regenerated: `**/` -> `(.+/)?`, `**` -> `.*`,
# `*` -> `[^/]*`, `?` -> `[^/]`, everything else escaped. That is the translation
# `PatternToRegex` performs in code.forgejo.org/forgejo/runner
# `act/workflowpattern/workflow_pattern.go`, which is what
# `matchPullRequestEvent` (modules/actions/workflows.go, v15.0.11) compiles each
# pattern with. The `**/` -> `(.+/)?` case is what makes `**/*.md` match a root
# `AGENTS.md`; the plain `**` -> `.*` form would wrongly report root files as uncovered.

set -uo pipefail

ROOT="$(git -C "$(dirname "$0")" rev-parse --show-toplevel 2>/dev/null)" || ROOT=""
if [ -z "$ROOT" ]; then
  echo "ci-merge-gate: cannot resolve the repository root from '$(dirname "$0")'" >&2
  exit 2
fi

WORKFLOW_DIR="$ROOT/.github/workflows"

# Every action the runner on this instance can resolve (docs/ci-forgejo.md, section 1).
# Do not widen this to "be safe": an entry has to be mirrored on the instance first.
ALLOWED_ACTIONS=(
  'actions/checkout@v7'
  'actions/setup-node@v7'
  'actions/upload-artifact@v3'
  'docker/setup-buildx-action@v4'
  'docker/login-action@v3'
  'docker/build-push-action@v7'
)

usage() {
  cat <<'USAGE'
usage: bash scripts/ci-merge-gate.sh [--lint | --uncovered | --help]
  --lint        (default) fatal checks; exit 0 clean, 1 violation, 2 usage error
  --uncovered   list tracked files no pull_request.paths pattern matches; always exit 0
USAGE
}

# ---------------------------------------------------------------------------
# YAML scan: a line-oriented reader for the shape these workflows actually use —
# `on:` at column 0, `push:`/`pull_request:` at indent 2, `branches:`/`paths:` at
# indent 4, `- item` at indent 6, plus the one `paths: &anchor` / `paths: *anchor`
# pair in docs.yml. It is deliberately not a YAML parser.
# ---------------------------------------------------------------------------
AWK_SCAN="$(cat <<'AWK'
function trim(s) { sub(/^[ \t]+/, "", s); sub(/[ \t]+$/, "", s); return s }
function indent_of(s) { match(s, /^ */); return RLENGTH }
function nocomment(s) { sub(/[ \t]+#.*$/, "", s); return s }
function unquote(s) { gsub(/^['"]/, "", s); gsub(/['"]$/, "", s); return s }

function inline(rest, kind, ln,   s, n, i, arr, v) {
  s = rest
  sub(/^\[/, "", s)
  sub(/\].*$/, "", s)
  n = split(s, arr, ",")
  for (i = 1; i <= n; i++) {
    v = unquote(trim(arr[i]))
    if (v == "") continue
    if (kind == "BR") { nbr++; brv[nbr] = v; brl[nbr] = ln }
    else if (kind == "PATH") { npth++; pth[npth] = v; pthl[npth] = ln }
    else if (kind == "IGNORE") { nig++; igv[nig] = v }
  }
}

BEGIN { in_on = 0; sect = ""; key = ""; anch = ""; pr = 0 }

{
  line = $0
  sub(/\r$/, "", line)
  if (line ~ /^[ \t]*$/) next
  if (line ~ /^[ \t]*#/) next

  raw = trim(nocomment(line))
  # `uses:` — matched by first non-space token, so a comment mentioning it is skipped
  # above and `- uses:` is included.
  if (raw ~ /^uses:[ \t]/ || raw ~ /^- uses:[ \t]/) {
    u = raw
    sub(/^- /, "", u)
    sub(/^uses:[ \t]*/, "", u)
    u = unquote(trim(u))
    if (u != "") printf "USES\t%d\t%s\n", NR, u
  }

  ind = indent_of(line)
  if (ind == 0) {
    if (raw ~ /^name:/ && nameln == 0) {
      nameln = NR
      nameval = unquote(trim(substr(raw, 6)))
    }
    in_on = (raw == "on:" || raw ~ /^on:[ \t]*$/)
    sect = ""; key = ""; anch = ""
    next
  }
  if (!in_on) next

  if (ind == 2) {
    sect = raw
    sub(/:.*$/, "", sect)
    key = ""; anch = ""
    if (sect == "pull_request") { pr = 1; prln = NR }
    next
  }

  if (ind == 4) {
    key = raw
    sub(/:.*$/, "", key)
    rest = ""
    if (index(raw, ":") > 0) rest = trim(substr(raw, index(raw, ":") + 1))
    anch = ""
    if (rest ~ /^&/) { anch = substr(rest, 2); sub(/[ \t].*$/, "", anch) }
    if (sect == "pull_request") {
      if (key == "branches") {
        brkey = 1; brln = NR
        if (rest ~ /^\[/) inline(rest, "BR", NR)
      } else if (key == "paths") {
        pathskey = 1; pathsln = NR
        if (rest ~ /^\[/) inline(rest, "PATH", NR)
        else if (rest ~ /^\*/) alias_paths = substr(rest, 2); sub(/[ \t].*$/, "", alias_paths)
      } else if (key == "paths-ignore") {
        ignorekey = 1; ignoreln = NR
        if (rest ~ /^\[/) inline(rest, "IGNORE", NR)
        else if (rest ~ /^\*/) alias_ignore = substr(rest, 2); sub(/[ \t].*$/, "", alias_ignore)
      }
    }
    next
  }

  if (raw ~ /^- /) {
    val = unquote(trim(substr(raw, 3)))
    if (val == "") next
    if (anch != "") { ancn[anch]++; ancv[anch, ancn[anch]] = val; ancl[anch, ancn[anch]] = NR; next }
    if (sect == "pull_request") {
      if (key == "branches") { nbr++; brv[nbr] = val; brl[nbr] = NR }
      else if (key == "paths") { npth++; pth[npth] = val; pthl[npth] = NR }
      else if (key == "paths-ignore") { nig++; igv[nig] = val }
    }
    next
  }
}

END {
  if (nameln > 0) printf "NAME\t%d\t%s\n", nameln, nameval
  if (pr) printf "PR\t%d\n", prln
  if (brkey) printf "BRKEY\t%d\n", brln
  for (i = 1; i <= nbr; i++) printf "BR\t%d\t%s\n", brl[i], brv[i]
  if (pathskey) printf "PATHSKEY\t%d\n", pathsln
  if (alias_paths != "") {
    if (alias_paths in ancn) {
      for (i = 1; i <= ancn[alias_paths]; i++) printf "PATH\t%d\t%s\n", ancl[alias_paths, i], ancv[alias_paths, i]
    } else {
      printf "BADALIAS\t%d\t%s\n", pathsln, alias_paths
    }
  }
  for (i = 1; i <= npth; i++) printf "PATH\t%d\t%s\n", pthl[i], pth[i]
  if (ignorekey) printf "IGNOREKEY\t%d\n", ignoreln
  if (alias_ignore != "") {
    if (alias_ignore in ancn) {
      for (i = 1; i <= ancn[alias_ignore]; i++) printf "IGNORE\t%d\t%s\n", ancl[alias_ignore, i], ancv[alias_ignore, i]
    }
  }
  for (i = 1; i <= nig; i++) printf "IGNORE\t%d\t%s\n", igl[i], igv[i]
}
AWK
)"

scan_get() { # <scan.file> <key> -> first line number recorded for that key
  awk -F'\t' -v k="$2" '$1 == k { print $2; exit }' "$1"
}

scan_values() { # <scan.file> <key> -> "line<TAB>value" per record
  awk -F'\t' -v k="$2" '$1 == k { print $2 "\t" $3 }' "$1"
}

# ---------------------------------------------------------------------------
# Shared scan of every workflow definition.
# ---------------------------------------------------------------------------
FILES=()
SCANS=()
WORKDIR=""
trap '[ -n "$WORKDIR" ] && rm -rf "$WORKDIR"' EXIT

scan_workflows() {
  WORKDIR="$(mktemp -d)"
  local f rel
  shopt -s nullglob
  for f in "$WORKFLOW_DIR"/*.yml "$WORKFLOW_DIR"/*.yaml; do
    [ -f "$f" ] || continue
    rel="${f#"$ROOT"/}"
    awk "$AWK_SCAN" "$f" > "$WORKDIR/${#FILES[@]}.scan"
    FILES+=("$rel")
    SCANS+=("$WORKDIR/${#SCANS[@]}.scan")
  done
  shopt -u nullglob
}

# ---------------------------------------------------------------------------
# Glob -> ERE, following the runner's PatternToRegex (see the header).
# ---------------------------------------------------------------------------
glob_to_regex() {
  local p="$1"
  local out=""
  local i=0
  local len=${#p}
  local c
  while [ "$i" -lt "$len" ]; do
    c="${p:i:1}"
    case "$c" in
      '*')
        if [ "${p:i+1:1}" = '*' ]; then
          if [ "${p:i+2:1}" = '/' ]; then
            out+='(.+/)?'
            i=$((i + 3))
          else
            out+='.*'
            i=$((i + 2))
          fi
        else
          out+='[^/]*'
          i=$((i + 1))
        fi
        ;;
      '?')
        out+='[^/]'
        i=$((i + 1))
        ;;
      [A-Za-z0-9/_-])
        out+="$c"
        i=$((i + 1))
        ;;
      *)
        out+="\\$c"
        i=$((i + 1))
        ;;
    esac
  done
  printf '%s' "$out"
}

# Fills PR_PATTERNS (every pull_request path pattern, anchors resolved) and
# REGEXES (the same list translated to ERE).
load_pr_patterns() {
  PR_PATTERNS=()
  REGEXES=()
  local idx line pat
  for idx in "${!SCANS[@]}"; do
    while IFS=$'\t' read -r line pat; do
      [ -n "${pat:-}" ] || continue
      PR_PATTERNS+=("$pat")
      REGEXES+=("$(glob_to_regex "$pat")")
    done < <(scan_values "${SCANS[$idx]}" PATH)
  done
}

# Prints the tracked paths that no pull_request.paths pattern matches, one per line.
list_uncovered() {
  local path i rx hit
  while IFS= read -r path; do
    [ -n "$path" ] || continue
    hit=0
    for i in "${!REGEXES[@]}"; do
      rx="${REGEXES[$i]}"
      if [[ "$path" =~ ^$rx$ ]]; then
        hit=1
        break
      fi
    done
    [ "$hit" -eq 1 ] || printf '%s\n' "$path"
  done < <(git -C "$ROOT" ls-files)
}

# ---------------------------------------------------------------------------
# --lint
# ---------------------------------------------------------------------------
run_lint() {
  local fail=0 idx rel ln fname val br main_v dev_v unfiltered=0
  local l4_file="" l4_line=1
  local total_paths=0

  scan_workflows
  if [ "${#FILES[@]}" -eq 0 ]; then
    echo "ci-merge-gate: no workflow definitions found in .github/workflows" >&2
    exit 2
  fi

  printf 'ci-merge-gate: linting %d workflow definitions in .github/workflows\n\n' "${#FILES[@]}"

  # L1 — every workflow declares a top-level name: (the status context prefix).
  for idx in "${!FILES[@]}"; do
    rel="${FILES[$idx]}"
    if [ -z "$(scan_get "${SCANS[$idx]}" NAME)" ]; then
      printf '✗ L1 %s:1: no top-level `name:`\n' "$rel"
      printf '  fix: add `name: <workflow name>` as the first key of %s — Forgejo builds the status context from it\n' "$rel"
      fail=1
    fi
  done

  # L2 — every real uses: names an action mirrored on this instance.
  for idx in "${!FILES[@]}"; do
    rel="${FILES[$idx]}"
    while IFS=$'\t' read -r ln val; do
      [ -n "$val" ] || continue
      case "$val" in
        ./*) continue ;; # local action in this repository: no mirroring needed
      esac
      local allowed=0 a
      for a in "${ALLOWED_ACTIONS[@]}"; do
        if [ "$val" = "$a" ]; then allowed=1; break; fi
      done
      if [ "$allowed" -eq 0 ]; then
        printf '✗ L2 %s:%s: uses: %s is not an action mirrored on this instance\n' "$rel" "$ln" "$val"
        printf '  fix: import the action onto the instance (POST /repos/migrate), then add it to the allow-list in scripts/ci-merge-gate.sh and to the table in docs/ci-forgejo.md\n'
        fail=1
      fi
    done < <(scan_values "${SCANS[$idx]}" USES)
  done

  # L3 — every pull_request workflow triggers on both main and development.
  for idx in "${!FILES[@]}"; do
    rel="${FILES[$idx]}"
    [ -n "$(scan_get "${SCANS[$idx]}" PR)" ] || continue
    local brln
    brln="$(scan_get "${SCANS[$idx]}" BRKEY)"
    [ -n "$brln" ] || continue # no branches filter: every base branch triggers, fine
    main_v=0; dev_v=0
    while IFS=$'\t' read -r ln br; do
      [ -n "$br" ] || continue
      [ "$br" = "main" ] && main_v=1
      [ "$br" = "development" ] && dev_v=1
    done < <(scan_values "${SCANS[$idx]}" BR)
    if [ "$main_v" -eq 0 ] || [ "$dev_v" -eq 0 ]; then
      local found
      found="$(scan_values "${SCANS[$idx]}" BR | awk -F'\t' '{ printf "%s%s", sep, $2; sep = ", " }')"
      printf '✗ L3 %s:%s: pull_request.branches does not list both main and development (found: %s)\n' \
        "$rel" "$brln" "${found:-none}"
      printf '  fix: use `branches: [ main, development ]` — a pull request into development otherwise runs nothing (the bug fixed in #24)\n'
      fail=1
    fi
  done

  # L4 — at least one pull_request workflow has no paths / paths-ignore filter.
  for idx in "${!FILES[@]}"; do
    rel="${FILES[$idx]}"
    [ -n "$(scan_get "${SCANS[$idx]}" PR)" ] || continue
    if [ -z "$(scan_get "${SCANS[$idx]}" PATHSKEY)" ] && [ -z "$(scan_get "${SCANS[$idx]}" IGNOREKEY)" ]; then
      unfiltered=$((unfiltered + 1))
    fi
    if [ "$rel" = ".github/workflows/merge-gate.yml" ]; then
      l4_file="$rel"
      ln="$(scan_get "${SCANS[$idx]}" PATHSKEY)"
      [ -n "$ln" ] && l4_line="$ln"
    fi
  done
  if [ "$unfiltered" -eq 0 ]; then
    [ -n "$l4_file" ] || l4_file=".github/workflows"
    printf '✗ L4 %s:%s: every pull_request workflow declares paths or paths-ignore, so a root-config-only pull request reports zero statuses\n' \
      "$l4_file" "$l4_line"
    printf '  fix: remove paths/paths-ignore from .github/workflows/merge-gate.yml — with every workflow filtered, `["*"]` reads `pending` and refuses the merge (docs/ci-forgejo.md)\n'
    fail=1
  fi

  if [ "$fail" -ne 0 ]; then
    printf '\nci-merge-gate: FAILED — see the ✗ lines above\n' >&2
    exit 1
  fi

  # Success: one row per workflow, then the uncovered count.
  printf '  %-42s %-14s %s\n' "workflow" "pull_request" "path filters"
  for idx in "${!FILES[@]}"; do
    rel="${FILES[$idx]}"
    if [ -n "$(scan_get "${SCANS[$idx]}" PR)" ]; then
      fname="yes"
      ln="$(scan_values "${SCANS[$idx]}" PATH | wc -l | tr -d ' ')"
      total_paths=$((total_paths + ln))
    else
      fname="no"
      ln="0"
    fi
    printf '  %-42s %-14s %s\n' "$rel" "$fname" "$ln"
  done

  load_pr_patterns
  local uncovered
  uncovered="$(list_uncovered | wc -l | tr -d ' ')"
  printf '\n%d tracked files are matched by no pull_request.paths pattern (see --uncovered)\n' "$uncovered"
  printf '✓ merge gate OK — %d workflow definitions, %d pull_request path patterns, %d always-on\n' \
    "${#FILES[@]}" "$total_paths" "$unfiltered"
}

# ---------------------------------------------------------------------------
# --uncovered
# ---------------------------------------------------------------------------
run_uncovered() {
  scan_workflows
  load_pr_patterns

  local badalias idx ln name
  for idx in "${!SCANS[@]}"; do
    while IFS=$'\t' read -r ln name; do
      [ -n "$name" ] || continue
      printf 'ci-merge-gate: %s:%s: unresolvable YAML anchor `*%s` in paths\n' "${FILES[$idx]}" "$ln" "$name" >&2
    done < <(scan_values "${SCANS[$idx]}" BADALIAS)
  done

  local list group total=0
  local -a order=()
  local -A buckets=()
  list="$(list_uncovered)"
  while IFS= read -r path; do
    [ -n "$path" ] || continue
    total=$((total + 1))
    case "$path" in
      */*) group="${path%%/*}" ;;
      *) group="(repository root)" ;;
    esac
    if [ -z "${buckets[$group]+set}" ]; then
      buckets[$group]=""
      order+=("$group")
    fi
    buckets[$group]+="$path"$'\n'
  done <<< "$list"

  for group in "${order[@]}"; do
    printf '=== %s (%s)\n' "$group" "$(printf '%s' "${buckets[$group]}" | wc -l | tr -d ' ')"
    printf '%s\n' "${buckets[$group]%$'\n'}"
    printf '\n'
  done

  printf '%d tracked files are matched by no pull_request.paths pattern\n' "$total"
  printf '(git ls-files: %s paths; %d pull_request path patterns; regenerate the table in docs/ci-forgejo.md, "Files that no component workflow builds")\n' \
    "$(git -C "$ROOT" ls-files | wc -l | tr -d ' ')" "${#PR_PATTERNS[@]}"
}

case "${1:-}" in
  "" | --lint) MODE=lint ;;
  --uncovered) MODE=uncovered ;;
  -h | --help) usage; exit 0 ;;
  *)
    echo "ci-merge-gate: unknown argument '${1}'" >&2
    usage >&2
    exit 2
    ;;
esac
if [ "$#" -gt 1 ]; then
  echo "ci-merge-gate: expected one argument, got $#" >&2
  usage >&2
  exit 2
fi

case "$MODE" in
  lint) run_lint ;;
  uncovered) run_uncovered ;;
esac

exit 0
