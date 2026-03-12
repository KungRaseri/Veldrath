# Contributing to RealmEngine

Contribution guidelines, code conventions, and the PR process are documented in the **[Contributing wiki page](https://github.com/KungRaseri/RealmEngine/wiki/Contributing)**.

## Quick Links

- **[Getting Started](https://github.com/KungRaseri/RealmEngine/wiki/Getting-Started)** — clone, build, and run the tests
- **[FAQ](https://github.com/KungRaseri/RealmEngine/wiki/FAQ)** — architecture decisions and common questions
- **[Open Issues](https://github.com/KungRaseri/RealmEngine/issues)** — good first issues are tagged `good first issue`
- **[Discussions](https://github.com/KungRaseri/RealmEngine/discussions)** — design questions, ideas, and feedback
- **[Full Documentation](https://kungraseri.github.io/RealmEngine/)** — MkDocs reference docs

## The Short Version

1. Fork the repo and create a feature branch off `main`
2. `dotnet build Realm.Full.slnx` — make sure it builds
3. `dotnet test Realm.Full.slnx` — all 8,500+ tests must pass
4. Engine libraries (`RealmEngine.Core`, `.Shared`, `.Data`) must never take a UI framework dependency
5. Open a pull request — the CI suite will run automatically
