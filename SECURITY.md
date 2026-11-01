# Security Policy

FindRomCover is a cross-platform desktop application that reads local ROM and image
folders, downloads images, and optionally talks to third-party AI providers. We take
security reports seriously and appreciate responsible disclosure.

## Supported versions

Security fixes are provided for the latest published release. Older releases may not
receive fixes.

| Version | Supported |
| --- | --- |
| Latest release (3.4.x) | Yes |
| Older releases | No |

## Reporting a vulnerability

**Please do not open a public issue, discussion, or pull request for a security
vulnerability.**

Use GitHub's private vulnerability reporting:

- **Report a vulnerability:** <https://github.com/purelogiccode/FindRomCover/security/advisories/new>
- **Security overview:** <https://github.com/purelogiccode/FindRomCover/security>

Include as much of the following as possible:

- The affected version and platform (Windows, Linux, or macOS).
- A clear description of the vulnerability and its impact.
- Steps to reproduce, including a minimal proof of concept when practical.
- Relevant logs or screenshots with API keys, tokens, personal paths, and other
  sensitive data removed.
- Whether the issue affects the application, a release archive, the documentation,
  or a CI/CD workflow.

## What to expect

- We aim to acknowledge a report within **3 business days**.
- We aim to complete initial triage within **7 business days**.
- Fix timelines depend on severity and complexity. We will keep you informed of
  progress through the private advisory.
- We will credit reporters in the advisory when they want to be credited.

Please allow us reasonable time to investigate and release a fix before disclosing
the issue publicly. We aim to coordinate disclosure within **90 days** of the report,
or sooner once a fix is available.

## Scope

In scope:

- The FindRomCover application and its release archives.
- Authentication, secret storage, file handling, image processing, URL handling, and
  network request code in this repository.
- The documentation site, wiki content, and GitHub Actions workflows in this
  repository.

Out of scope:

- Vulnerabilities in third-party services or providers (for example Google, Bing,
  GitHub, OpenRouter, OpenAI, Anthropic, Gemini, GLM, or a local model server).
- Issues that require an already-compromised device, administrator/root access, or
  physical access to the user's machine.
- Social engineering, spam, or denial-of-service testing against third-party services.
- Reports that only affect unsupported versions.

## Safe harbor

We support good-faith security research. If you make a reasonable effort to avoid
privacy violations, data loss, service disruption, and access to data that is not
yours, we will not pursue legal action for your research. Do not test against other
users' installations, accounts, or data.

## Security practices

This repository uses GitHub security features including Dependabot alerts and security
updates, secret scanning with push protection, private vulnerability reporting, and
CodeQL analysis. Please report suspected secrets or credentials immediately through
the private reporting link above.
