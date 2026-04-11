# Security Policy

## Supported Versions

Security fixes are applied to the **latest release** of each component. Older releases do not receive backported security patches.

| Component | Supported |
|-----------|-----------|
| Engine Libraries (latest `engine/v*`) | ✅ |
| RealmForge Tooling (latest `tooling/v*`) | ✅ |
| Veldrath Server (latest `server/v*`) | ✅ |
| Veldrath Client (latest `client/v*`) | ✅ |
| Any previous release | ❌ |

## Reporting a Vulnerability

**Please do not open a public GitHub issue for security vulnerabilities.**

Use GitHub's private **[Security Advisory](https://github.com/KungRaseri/RealmEngine/security/advisories/new)** feature to report vulnerabilities confidentially:

1. Go to the [Security Advisories page](https://github.com/KungRaseri/RealmEngine/security/advisories/new).
2. Click **"New draft security advisory"**.
3. Fill in the affected component(s), severity, and a clear description of the issue and its potential impact.
4. Submit the draft — only repository maintainers can see it.

If you are unable to use GitHub's advisory system, you may contact the maintainer directly via the email address listed on the [KungRaseri GitHub profile](https://github.com/KungRaseri).

## What to Include in Your Report

To help us triage quickly, please include:

- A description of the vulnerability and its potential impact.
- The affected component(s) (Engine, Server, Client, RealmForge) and version(s).
- Steps to reproduce or a proof-of-concept (if safe to share).
- Any suggested mitigations you have identified.

## Response Timeline

| Stage | Target |
|-------|--------|
| Initial acknowledgement | Within **3 business days** |
| Severity assessment & triage | Within **7 business days** |
| Fix or mitigation plan communicated | Within **14 business days** |
| Public disclosure (coordinated) | After a fix is released |

We follow a **coordinated disclosure** model. Once a fix is available we will publish a GitHub Security Advisory and credit the reporter (unless anonymity is requested).

## Scope

The following are considered in-scope vulnerabilities:

- Remote code execution (RCE) or privilege escalation in the **Veldrath Server** or any engine library consumed by it.
- Authentication or authorization bypasses in the server.
- Injection vulnerabilities (SQL, command, path traversal, etc.) in any component.
- Insecure deserialization of untrusted game-data JSON files.
- Sensitive data exposure (credentials, personal data, save-game data).

The following are generally **out of scope**:

- Bugs that require local, authenticated access to a machine already controlled by the attacker.
- Denial-of-service issues caused by extremely large or malformed input files in a single-player or offline context.
- Vulnerabilities in third-party dependencies — please report those directly to the dependency maintainer.
- Issues in documentation, tooling scripts, or the RealmForge data editor that do not affect a networked or multi-user deployment.

## Responsible Disclosure

We ask that you:

- Give us a reasonable amount of time to investigate and remediate before any public disclosure.
- Avoid accessing, modifying, or deleting data that does not belong to you.
- Not exploit a vulnerability beyond what is necessary to confirm its existence.

We commit to:

- Acknowledging your report promptly.
- Keeping you informed of progress.
- Crediting you in the published advisory (unless you prefer to remain anonymous).
- Not pursuing legal action against good-faith security researchers.
