# Contributing to RealmEngine

Contribution guidelines, code conventions, and the PR process are documented in the **[Contributing wiki page](https://git.kungraseri.cloud/Veldrath/Veldrath/wiki/Contributing)**.

## Quick Links

- **[Getting Started](https://git.kungraseri.cloud/Veldrath/Veldrath/wiki/Getting-Started)** — clone, build, and run the tests
- **[FAQ](https://git.kungraseri.cloud/Veldrath/Veldrath/wiki/FAQ)** — architecture decisions and common questions
- **[Open Issues](https://git.kungraseri.cloud/Veldrath/Veldrath/issues)** — good first issues are tagged `good first issue`
- **[Releases](https://git.kungraseri.cloud/Veldrath/Veldrath/releases)** — component artifacts and docker image tags
- **[CI guide](docs/ci-forgejo.md)** — how the pipeline runs and which actions are allowed

## The Short Version

1. Fork the repo and create a feature branch off `main`
2. `dotnet build Realm.Full.slnx` — make sure it builds
3. `dotnet test Realm.Full.slnx` — all ~3,860 .NET tests must pass (plus `cd web && npm run test` for the web suites)
4. `dotnet format Realm.Full.slnx` — the root [`.editorconfig`](.editorconfig) is enforced by CI
5. Engine libraries (`RealmEngine.Core`, `.Shared`, `.Data`) must never take a UI framework dependency
6. Open a pull request — the CI suite will run automatically
