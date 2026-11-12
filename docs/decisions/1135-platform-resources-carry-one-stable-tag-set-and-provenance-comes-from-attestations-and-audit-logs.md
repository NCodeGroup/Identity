# 1135. Platform resources carry one stable tag set, and provenance comes from attestations and audit logs

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

The platform spans several vendors (AWS, Cloudflare, OCI, Akamai, GitHub and Kubernetes,
[ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md)), each with
its own tagging model: AWS and OCI use key/value maps, Akamai and Cloudflare DNS records use plain `key:value` strings,
Kubernetes uses labels (no `@` allowed in values) and annotations, and GitHub only has repository topics. Without one
strategy, every component invents its own keys and nobody can answer, from a resource alone, who owns it, which
environment it serves, which code manages it, or whether it may be deleted. Because the infrastructure code is public,
tag values are public too.

Every deployed thing should also be traceable to the reviewed code and the pipeline run that produced it. The obvious
place to record that, a `commit` or `pipeline_run` tag on every cloud resource, conflicts with stable tags: each apply
would rewrite tags on every resource, plans would never report "no changes", scheduled drift detection would lose its
signal, and a resource the apply did not otherwise touch would carry a SHA that says nothing about the code that
defines it. The platform deploys two kinds of things through different paths: workloads (container images and Helm
charts, rolled out by Flux) and infrastructure (cloud resources, applied by OpenTofu from GitHub Actions)
([ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md),
[ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md)).

## Decision

**Every taggable resource carries the same lowercase, snake_case keys, produced in one place and translated into each
vendor's format. Workloads carry verifiable provenance; infrastructure carries stable ownership tags and gets its
change provenance from audit trails.**

### The tag set

| Key                   | Values                                                                        | Set by                  |
| --------------------- | ----------------------------------------------------------------------------- | ----------------------- |
| `application`         | `ncode-identity`                                                              | live root (shared tags) |
| `environment`         | exactly `shared` (cross-environment foundations), `lab`, `production` or `ci` | live root               |
| `owner_email`         | a role mailbox, never a person: `platform@ncodeid.com`                        | live root               |
| `account`             | the vendor account or tenancy the resource lives in                           | live root               |
| `managed_by`          | `opentofu`, or `flux` in-cluster; never edit by hand                          | shared tags module      |
| `source`              | the managing root                                                             | live root               |
| `component`           | the component that created it                                                 | component               |
| `role`                | its purpose, such as `tofu-state`, `backups`, `secrets`, `dns` or `database`  | component               |
| `data_classification` | `public`, `internal`, `confidential` or `restricted`                          | component (overridable) |
| `deletion_protection` | `true` or `false`, mirroring the component's `deletion_protection` input      | component               |

Keys are snake_case; values keep their natural form (kebab-case names, emails, paths). The environment is never a
customer's tenant word such as `dev`, `test` or `staging`.

1. **One producer.** A provider-free `tags` component validates the shared keys and outputs them as a map (AWS, OCI)
   and as sorted `key:value` strings (Akamai). On AWS the map is applied only through the provider's `default_tags`, so
   nothing can be created untagged, and tags written by other systems are excluded with the provider's `ignore_tags`.
   Cloudflare's Free plan has no DNS record tags, so each record carries a comment of at most 100 characters naming
   `managed_by` and `source`.
2. **Components add their own keys** (`component`, `role`, `data_classification`, `deletion_protection`) and merge them
   with the shared map. Callers may add keys but never remove them, and no key is set twice.
3. **Owner is a role mailbox.** `owner_email` names a routed address, so tags carry no personal data in public code and
   survive changes of people.
4. **Kubernetes** uses the standard `app.kubernetes.io/*` labels exactly as defined, plus `ncodeid.net/environment` and
   `ncodeid.net/role` labels. `owner_email` and `source` are annotations, because label values cannot contain `@` or
   `/`. The platform domain is
   [ADR-1495](1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md)'s.
5. **GitHub** has no resource tags; repository topics describe the repository instead.
6. **Tags are stable.** A plan with no code change shows no tag change:
   - Values come only from static inputs: never timestamps, commit SHAs, run IDs, release versions, or IDs and ARNs
     known only after apply. Versions and commits belong on deployment objects (image digests, annotations), not tags.
   - Values are lowercase, because some vendors normalize tag case on read, which would show up as permanent drift.
   - A tag changes only through a reviewed code change.

### Provenance

7. **Workloads.** Every image, chart and module artifact is built in GitHub Actions with a build-provenance attestation
   and a keyless Sigstore signature, and is referenced by digest. Flux verifies the signature against the building
   workflow's identity before it applies a release, and the cluster admits only signed images from the platform's
   registry. The provenance travels with the artifact, so it never causes drift.
8. **Infrastructure: what owns it.** Static tags only: `source` and `managed_by` name the code that defines a resource,
   and release refs (`?ref=vX.Y.Z`) name the component version. No commit, run or timestamp tags.
9. **Infrastructure: who changed it, when, from which commit.** This comes from audit trails:
   - CI assumes its AWS role with the session name `gh-<run_id>-<run_attempt>`, so CloudTrail attributes every API call
     to the exact workflow run, and through it the commit and approver. The role trust policies require that form with
     the `sts:RoleSessionName` condition, so CI cannot obtain credentials without naming its run.
   - Each CI scope uses its own named Cloudflare token, so Cloudflare's audit log attributes each change to a pipeline
     scope.
   - Every apply job runs in a GitHub environment, so GitHub records a deployment with the commit, run and approval,
     which answers "which commit is live in production".
   - Bootstrap runs are attributed to the owner's IAM Identity Center session.

## Options considered

- **Each vendor's own conventions.** Natural per vendor, but no single query answers "what does production own".
  Rejected.
- **kebab-case keys** (`owner-email`). Every vendor accepts both styles, and kebab-case matches the Kubernetes standard
  labels. Rejected: snake_case keys are one word to editors and terminals (double-click selects the whole key), match
  Prometheus label names that tags become in metrics, and allow HCL attribute access (`tags.owner_email`). The
  Kubernetes standard labels keep their defined names.
- **A namespace prefix on every key** (`ncode:environment`). Avoids collisions with third-party tags, but costs length
  on vendors with tight limits (Akamai tags are at most 50 characters). Rejected; Kubernetes, which needs a prefix for
  custom labels, uses `ncodeid.net/`.
- **A personal owner address.** Simple, but publishes personal data and breaks when ownership changes. Rejected.
- **Cost-center and project codes.** Useful in a multi-team company; here `application` and `environment` already
  separate cost. Not adopted; a new key needs a decision record.
- **Commit and run tags on every resource.** Visible on the resource itself, but rewrites every resource on every apply
  and blinds drift detection. Rejected.
- **A "last deployed" record managed by OpenTofu** (an SSM parameter holding the commit). Always shows a diff, for the
  same reason. Rejected; GitHub deployments already answer it.
- **Provenance only by convention** (trusting that `main` is what runs). No verification at the point of deployment.
  Rejected for workloads.
- **One tag set, stable values, attestations for workloads and audit trails for infrastructure.** Chosen.

## Consequences

- From any resource you can tell who owns it, which environment it serves, which code manages it, how sensitive it is
  and whether it may be deleted.
- Plans stay quiet unless the code or the real world changed, so a non-empty scheduled plan always means drift.
- Tracing an infrastructure change goes resource, then CloudTrail or the Cloudflare audit log, then the workflow run,
  then the commit, instead of reading a tag.
- `platform@ncodeid.com` must exist as an inbound Email Routing route.
- A new required key is a change to the `tags` component and every root that calls it.
- The workload build pipeline needs attestation, signing and Flux verification before the first production rollout;
  the `aws-github-oidc` component carries the `sts:RoleSessionName` condition, and the deploy workflow sets the session
  name.

## References

- [ADR-1132](1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md): the
  public infrastructure code and its vendors.
- [ADR-1133](1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md): component rules,
  signed OCI artifacts and the member rule.
- [ADR-1134](1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md): operating principles.
- [Kubernetes recommended labels](https://kubernetes.io/docs/concepts/overview/working-with-objects/common-labels/).
- [GitHub artifact attestations](https://docs.github.com/actions/security-for-github-actions/using-artifact-attestations).
- [Flux OCI artifact verification](https://fluxcd.io/flux/components/source/ocirepositories/#verification).
