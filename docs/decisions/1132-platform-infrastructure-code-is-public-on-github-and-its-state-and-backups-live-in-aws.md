# 1132. Platform infrastructure code is public on GitHub, and its state and backups live in AWS

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

The hosted platform ([ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md)) is provisioned entirely
with OpenTofu. That code needs a CI system to apply it, a state backend, a secret store for vendor API tokens and an
off-vendor destination for database backups. Where the code lives is
[ADR-1502](1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md)'s:
the public `NCodeGroup/identity-platform` repository. Forces:

- **Transparency is part of the product.** An identity provider asks customers to trust how their tenants' data and
  keys are protected. Publishing how the platform is built is stronger evidence than asserting it, and a design that
  stays secure only while hidden is not secure (Kerckhoffs's principle). Most of what the code describes is observable
  anyway: DNS, certificate-transparency logs, IP ownership and response headers.
- **Public infrastructure code has real risks**, none of them about obscurity: committed secrets, untrusted pull
  requests that execute with credentials (a `tofu plan` can exfiltrate through providers and data sources), public CI
  logs and artifacts (plan files hold secrets in plain text), and operator-specific details such as admin IP
  allowlists.
- **One VCS.** Source, packages and images live in the `NCodeGroup` GitHub organization, with the maintainers' tooling
  and the commit, image and deployment trail. A second platform for infrastructure doubles the accounts, CI systems and
  secret stores to secure.
- **On public repositories GitHub provides the deployment controls for free** that private repositories get only on
  paid plans: environments with required reviewers, wait timers and deployment-branch restrictions, secret-scanning
  push protection and unlimited standard-runner minutes.
- **State needs locking and history; backups need immutability and must survive losing the database vendor.** The
  managed Postgres keeps 14 days of point-in-time recovery, but only inside the same vendor account.

## Decision

**The platform's OpenTofu code lives in its own public repository, `NCodeGroup/identity-platform`, and GitHub Actions
applies it; OpenTofu state, vendor tokens and off-vendor backups live in a dedicated AWS account reached through
OIDC.**

1. **Repository.** The code is not a folder of the library repository, so its environments, rulesets and code owners
   are its own and library pull requests never touch deployment workflows. Where the repository sits among the
   product's repositories is
   [ADR-1502](1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md)'s.
2. **No long-lived secrets in GitHub.** Workflows assume AWS roles through GitHub OIDC. The **apply** role trusts only
   the protected `production` environment; a separate **plan** role is read-only. Vendor API tokens are SSM Parameter
   Store `SecureString`s, read at run time through those roles: read-only tokens for the plan role, scoped write
   tokens for the apply role.
3. **Guardrails for a public repository.**
   - Secrets are never committed: secret scanning with push protection, plus a `gitleaks` pre-commit hook.
   - Untrusted code never runs with credentials: no secrets for fork pull requests, no `pull_request_target` that
     checks out pull-request code, and approval before an outside contributor's workflows run.
   - Nothing sensitive is published: values are marked `sensitive`, plans are never posted to pull requests or
     uploaded unencrypted, and OpenTofu state and plan encryption are on.
   - Operator-specific values (admin IP allowlists, contacts) live in SSM, not in code.
   - Changes reach `main` only through pull requests with required checks (`tofu validate` and `plan`, and
     configuration scanning), and [`SECURITY.md`](../../SECURITY.md) covers the platform.
4. **State.** An S3 bucket with versioning, default encryption, a public-access block and a TLS-only policy, used
   through OpenTofu's S3 backend with native lockfile locking (`use_lockfile`), on top of client-side state
   encryption.
5. **Backups.** The managed database's point-in-time recovery is the primary restore path. A nightly `pg_dump`,
   encrypted client-side with `age` (it holds signing keys and the Data Protection key ring), goes to a separate S3
   bucket with Object Lock in compliance mode, through write-only credentials. Lifecycle rules archive monthly copies.
   Restores are drilled before launch and quarterly.
6. **AWS account.** A dedicated member account under AWS Organizations, isolated from any other workload, holds root
   MFA and no IAM users for automation.
7. **Bootstrap.** A one-time OpenTofu stack, run by the owner with short-lived credentials, creates the state and
   backup buckets, the OIDC provider and roles, every DNS zone
   [ADR-1495](1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md)
   names with DNSSEC, the scoped vendor tokens (written to SSM) and the repository's environments and rulesets. Its
   local state then moves into the bucket it created.

## Options considered

- **Infrastructure on GitLab, source on GitHub** (GitLab-managed state, GitLab CI). Free versioned state, and it keeps
  production credentials off the open-source platform. Rejected: backups need AWS anyway, S3 state costs cents with
  the same versioning and locking, and the split doubles the platforms to secure while moving infrastructure work away
  from the maintainers' primary tooling.
- **A private repository.** Hides topology that is mostly observable anyway, and loses the free deployment controls
  above (required reviewers on private repositories need GitHub Enterprise). Rejected for a security product whose
  transparency is a feature.
- **A folder in the library repository.** Co-locates code and docs, but mixes contributor pull requests with
  deployment workflows and repository-wide settings. Rejected.
- **Cloudflare R2 for state and backups.** Cheaper, with free egress, but it has no object versioning for state
  history and a less mature retention lock than S3 Object Lock. Rejected.
- **Encrypted state committed to Git.** Free, but it has no locking and needs a commit-back from CI. Rejected.
- **Chosen:** a public GitHub repository applied by GitHub Actions through OIDC, with S3 state and Object Lock backups
  in a dedicated AWS account.

## Consequences

- Anyone can read how the platform is built; credentials, state, plans, backups and operator-specific values stay
  private.
- GitHub holds no long-lived cloud credentials; compromising a workflow outside the protected environment yields at
  most read-only plan access.
- A misconfiguration is visible as soon as it merges, so configuration scanning and review are required checks.
- GitHub costs nothing for a public repository; AWS costs cents per month at this size.
- `use_lockfile` and plan encryption need a recent OpenTofu, so the repository pins its version.

## References

- [Hosting analysis](../hosting-analysis.md): bootstrap, delivery and backups.
- [ADR-1131](1131-hosted-platform-environment-model-and-hostname-scheme.md): the environment model and hostnames.
- [ADR-1502](1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md):
  repository placement.
- [ADR-1495](1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md):
  the platform's domains.
- [project-template ADR-0001](project-template/0001-conventions-and-lessons-live-in-the-repository.md): knowledge lives
  in the repository.
- GitHub: [deployment environments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments)
  (protection-rule availability by plan).
