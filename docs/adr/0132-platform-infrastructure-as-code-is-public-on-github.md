# 132. Platform infrastructure as code is public on GitHub; state and backups live in AWS

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

The hosted platform ([ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md)) is provisioned entirely
with OpenTofu. That code needs a home, a CI system to apply it, a state backend, a secret store for vendor API tokens,
and an off-vendor destination for database backups. Forces:

- **Transparency is part of the product.** An identity provider asks customers to trust how their tenants' data and
  keys are protected. Publishing how the platform is built is stronger evidence than asserting it, and a design that
  stays secure only while hidden is not secure (Kerckhoffs's principle). Most of what the code describes is already
  observable: DNS, certificate-transparency logs, IP ownership, and response headers.
- **Public infrastructure code has real risks**, none of which are about obscurity: committed secrets, untrusted pull
  requests executing with credentials (a `tofu plan` can exfiltrate through providers and data sources), public CI logs
  and artifacts (plan files contain secrets in plain text), and operator-specific details such as admin IP allowlists.
- **One VCS.** Source, packages, and images already live in the `NCodeGroup` GitHub organization, where the
  maintainers' tooling (Copilot, VS Code) and the commit → image → deployment trail are. A second platform for
  infrastructure doubles the accounts, CI systems, and secret stores to secure.
- **On public repositories GitHub provides the deployment controls for free** that private repositories only get on
  paid plans: environments with required reviewers, wait timers, deployment-branch restrictions, secret-scanning push
  protection, and unlimited standard-runner minutes.
- **State needs locking and history; backups need immutability and must survive losing the database vendor.** The
  managed Postgres keeps 14 days of point-in-time recovery, but only inside the same vendor account.

## Decision

**Platform infrastructure code lives in a separate public GitHub repository, `NCodeGroup/identity-platform`, applied by
GitHub Actions; OpenTofu state, vendor tokens, and off-vendor backups live in a dedicated AWS account reached through
OIDC.**

1. **Repository.** A dedicated public repository — not a folder in the library repository — so its settings,
   environments, rulesets, and code owners are its own, and library pull requests never touch deployment workflows.
   Reusable components and stacks live alongside the platform's own configuration (`live/`, `clusters/`), which consumes
   them only by released tag ([ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md)). Its README links to the
   hosting analysis and these ADRs.
2. **No long-lived secrets in GitHub.** Workflows authenticate to AWS with GitHub OIDC. The **apply** role trusts only
   the protected `production` environment of this repository; a separate **plan** role is read-only. Vendor API tokens
   (Cloudflare, the cloud provider) are SSM Parameter Store `SecureString`s, read at run time through those roles —
   read-only tokens for the plan role, scoped write tokens for the apply role.
3. **Guardrails for a public repository.**
   - Never commit secrets: secret scanning with push protection, plus a `gitleaks` pre-commit hook.
   - Untrusted code never runs with credentials: no secrets for fork pull requests, no `pull_request_target` that checks
     out pull-request code, and approval required before outside contributors' workflows run.
   - Nothing sensitive is published: values are marked `sensitive`, plans are never posted to pull requests or uploaded
     unencrypted, and OpenTofu's state **and plan** encryption is enabled.
   - Operator-specific values (admin IP allowlists, contacts) live in SSM, not in code.
   - Changes reach `main` only through pull requests with required checks (`tofu validate` / `plan`, configuration
     scanning); [`SECURITY.md`](../../SECURITY.md) covers the platform.
4. **State.** An S3 bucket with versioning, default encryption, public-access block, and a TLS-only policy; OpenTofu's
   S3 backend with native lockfile locking (`use_lockfile`), on top of client-side state encryption.
5. **Backups.** The managed database's point-in-time recovery is the primary restore path. A nightly `pg_dump`,
   encrypted client-side with `age` (it contains signing keys and the Data Protection key ring), is written to a
   separate S3 bucket with **Object Lock in compliance mode**, using write-only credentials; lifecycle rules move
   monthly copies to an archive tier. Restores are drilled before launch and quarterly.
6. **AWS account.** A dedicated member account under AWS Organizations, isolated from any other workload; root user
   MFA, no IAM users for automation.
7. **Bootstrap.** A one-time OpenTofu stack, run from the owner's machine with short-lived credentials, creates the state
   and backup buckets, the OIDC provider and roles, the DNS zone and DNSSEC, the scoped vendor tokens (written to SSM),
   and the repository's environments and rulesets; its local state is then migrated into the bucket it created.

## Options considered

- **Infrastructure on GitLab, source on GitHub** (GitLab-managed state, GitLab CI). Free versioned state and the
  maintainer's familiar homelab pattern, and it keeps production credentials off the open-source platform. Rejected:
  once backups need AWS anyway, S3 state costs cents with the same versioning and locking, and the split doubles the
  platforms to secure while moving infrastructure work away from the maintainers' primary tooling. The GitLab group
  remains a defensive name reservation.
- **Private GitHub repository.** Hides topology that is mostly observable anyway, and loses the free deployment controls
  above (required reviewers on private repositories need GitHub Enterprise). Rejected for a security product whose
  transparency is a feature.
- **A folder in the library repository.** Co-locates code and docs, but mixes contributor pull requests with deployment
  workflows and repository-wide settings. Rejected.
- **Cloudflare R2 for state and backups.** Cheaper per unit with free egress, but no object versioning for state
  history, and a less mature retention lock than S3 Object Lock. Not chosen; fine for bulk data later.
- **Encrypted state committed to Git.** Free, but no locking and a commit-back from CI. Rejected.

## Consequences

- Anyone can read how the platform is built; credentials, state, plans, backups, and operator-specific values remain
  private.
- GitHub holds no long-lived cloud credentials; compromising a workflow outside the protected environment yields at most
  read-only plan access.
- A misconfiguration is visible as soon as it merges, so configuration scanning and review are required checks, not
  optional ones.
- Cost: GitHub $0 (public repository); AWS cents per month at this size.
- `use_lockfile` and plan encryption require a recent OpenTofu; the version is pinned in the repository.

## References

- [Hosting analysis](../hosting-analysis.md) — §3 bootstrap, §11 delivery, §13 backups.
- [ADR-0131](0131-hosted-platform-environment-model-and-hostname-scheme.md) — environment model and hostnames.
- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — knowledge lives in the repository.
- GitHub: [deployment environments](https://docs.github.com/en/actions/reference/workflows-and-actions/deployments-and-environments)
  (protection-rule availability by plan).
