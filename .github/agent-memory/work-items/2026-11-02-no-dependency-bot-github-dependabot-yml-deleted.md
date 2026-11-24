---
kind: work-item
date: 2026-11-02
area: infra
title: "No dependency bot: `.github/dependabot.yml` deleted"
---

### Session-91 (2026-11-02) — No dependency bot: `.github/dependabot.yml` deleted

Forgejo runs no Dependabot service, so `.github/dependabot.yml` was inert; it is deleted (PR #9,
which moved its content into docs). The update cadence and every major-version hold now live in
[`docs/ci-forgejo.md`](../../../docs/ci-forgejo.md), section "Dependency and action updates (there is
no bot)": a first-Monday-of-the-month grouped NuGet (`Directory.Packages.props`) + npm (`web/`)
review, MediatR kept **below 13**, the coupled SvelteKit / Vite / TypeScript majors moved as one
migration, and action tags never swept.

Earlier session entries that describe the file as configuring nuget / npm / github-actions / docker
are historical — do not go looking for `.github/dependabot.yml`, and do not re-create it. If a bot
is ever wanted here it is self-hosted Renovate with `renovate.json`, not a resurrected Dependabot
config.
