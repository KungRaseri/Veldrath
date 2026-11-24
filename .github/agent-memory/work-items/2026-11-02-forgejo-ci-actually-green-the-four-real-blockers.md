---
kind: work-item
date: 2026-11-02
area: infra
title: "Forgejo CI actually green: the four real blockers"
---

### Session-89 (2026-11-02) - Forgejo CI actually green: the four real blockers

Session-88 recorded the wrong root cause. Corrected after reading job logs, which ARE
reachable anonymously on this build:

  curl -s "https://git.kungraseri.cloud/Veldrath/Veldrath/actions/runs/<run_number>/jobs/<job_index>/attempt/1/logs"

The per-run jobs API 404s, which made it look like logs needed auth. They do not - only
the URL shape was wrong. Runner logs use emoji markers, not ##[error]: grep for
"star Run Main", "check Success - ", the cross mark, "gear [runner]" and "flag Job failed".

Blocker 1 - actions resolve from THIS INSTANCE's DEFAULT_ACTIONS_URL, which is
https://github.com, not data.forgejo.org. Proof in every log:
`git fetch 'https://github.com/actions/checkout' # ref=v7`. So `forgejo/upload-artifact`
cannot be cloned at all ("could not read Username for 'https://github.com'"). Use
`actions/upload-artifact`. The rule is: the name must exist at DEFAULT_ACTIONS_URL, and
the action must not call the GitHub API (codecov, gh-release, sticky-comment, check-runs).

Blocker 2 - artifact actions must be v3. v4+ embeds @actions/artifact v2 and fails at the
END of a green job with:
  ::error::@actions/artifact v2.0.0+, upload-artifact@v4+ and download-artifact@v4+ are not currently supported on GHES.

Blocker 3 - `$GITHUB_PATH` is not honoured by this runner. Installing the SDK and
exporting PATH inside the install step made the NEXT step fail with "dotnet: command not
found". Fixed by `scripts/ci-toolchain.sh`: install into /usr/local/share/dotnet, symlink
into /usr/local/bin (always on PATH), same for reportgenerator, and provision zip.

Blocker 4 - service containers are on the job network and are reached by SERVICE NAME.
`DATABASE_URL=...@127.0.0.1:5432` fails with a bare exit 1 after "applying migrations";
`...@postgres:5432` works. The mapped port is not published to the job's loopback.

Also: actions/setup-node with `cache: npm` spends minutes in the POST step tarring
node_modules; that is what makes web runs look stuck, not the build.

Outcome on run 55/56/59: Engine, Discord and SvelteKit Web are GREEN. Server and the
game-loop e2e job failed only on a transient DNS failure fetching an action
("Could not resolve host: github.com"), which is why setting
DEFAULT_ACTIONS_URL = https://data.forgejo.org is recommended in docs/ci-forgejo.md -
all six actions used here exist there with the exact tags.
