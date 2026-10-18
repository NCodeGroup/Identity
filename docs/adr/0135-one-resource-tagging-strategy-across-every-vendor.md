# 135. One resource tagging strategy across every vendor

- **Status:** Accepted
- **Date:** 2026-10-18
- **Deciders:** NCode Group

## Context

The platform spans several vendors — AWS, Cloudflare, OCI, Akamai, GitHub, and Kubernetes
([ADR-0132](0132-platform-infrastructure-as-code-is-public-on-github.md)) — each with its own tagging model: AWS and
OCI use key/value maps, Akamai and Cloudflare DNS records use plain `key:value` strings, Kubernetes uses labels (no `@`
allowed in values) and annotations, and GitHub only has repository topics. Without one strategy, every component invents
its own keys and nobody can answer, from a resource alone, who owns it, which environment it serves, which code manages
it, or whether it may be deleted. Because the infrastructure code is public, tag values are public too.

## Decision

**Every taggable resource carries the same lowercase, snake_case keys, produced in one place and translated into each
vendor's format.**

| Key                   | Values                                              | Set by                       |
| --------------------- | --------------------------------------------------- | ---------------------------- |
| `application`         | `ncode-identity`                                    | live root (shared tags)      |
| `environment`         | exactly `shared` (cross-environment foundations), `lab`, `production`, or `ci` — never a customer's tenant word (`dev`, `test`, `staging`) | live root |
| `owner_email`         | a **role mailbox**, never a person — `platform@nidentity.io`        | live root          |
| `account`             | the vendor account or tenancy the resource lives in (for example `nidentity-platform`) | live root |
| `managed_by`          | `opentofu` (or `flux` in-cluster) — never edit by hand | shared tags module        |
| `source`              | the managing root, for example `NCodeGroup/identity-platform/live/bootstrap` | live root   |
| `component`           | the component that created it (for example `aws-state-backend`) | component              |
| `role`                | its purpose — `tofu-state`, `backups`, `secrets`, `ci-access`, `dns`, `database`, `kubernetes`, … | component |
| `data_classification` | `public`, `internal`, `confidential`, `restricted`  | component (overridable)      |
| `deletion_protection` | `true` / `false`, mirroring the component's `deletion_protection` input | component     |

Keys are snake_case; values keep their natural form (kebab-case names, emails, paths).

1. **One producer.** A provider-free `tags` component validates the shared keys and outputs them as a map (AWS, OCI)
   and as `key:value` strings (Akamai). On AWS the map is applied through the provider's `default_tags`, so nothing can
   be created untagged. Cloudflare's Free plan has no DNS record tags, so records carry a stable comment naming
   `managed_by` and `source` instead (comments are limited to 100 characters).
2. **Components add their own keys** (`component`, `role`, `data_classification`, `deletion_protection`) and merge them
   with the shared map; callers may add, but not remove, keys.
3. **Owner is a role mailbox.** `owner_email` names a routed address, so tags carry no personal data in public code and
   survive changes of people.
4. **Kubernetes** uses the standard `app.kubernetes.io/*` labels exactly as defined (`part-of: ncode-identity`,
   `component`, `managed-by`), plus our own `nidentity.io/environment` and `nidentity.io/role` labels; `owner_email` and
   `source` are annotations because label values can't contain `@` or `/`.
5. **GitHub** has no resource tags; repository topics describe the repository instead.
6. **Tags are stable** — a plan with no code change must show no tag change:
   - Values come only from static inputs: never timestamps, commit SHAs, run IDs, release versions, or IDs and ARNs
     known only after apply. Versions and commits belong on deployment objects (image digests, annotations), not tags.
   - Values are lowercase, because some vendors normalize tag case on read, which would show up as permanent drift.
   - The `key:value` list form is sorted, so its order never changes between plans.
   - On AWS, shared tags reach resources **only** through the provider's `default_tags`; components add only their own
     keys, so no key is set twice. Tags written by other systems are excluded with the provider's `ignore_tags`.
   - A tag changes only through a reviewed code change.

## Options considered

- **Each vendor's own conventions.** Natural per vendor, but no single query answers "what does production own". Rejected.
- **kebab-case keys** (`owner-email`). Every vendor accepts both styles, and kebab-case matches the Kubernetes standard
  labels and AWS's own tag keys. Rejected: snake_case keys are one word to editors and terminals (double-click selects
  the whole key), match Prometheus label names that tags become in metrics, and allow HCL attribute access
  (`tags.owner_email`). The Kubernetes standard labels keep their defined names.
- **A namespace prefix on every key** (`ncode:environment`). Avoids collisions with third-party tags, but costs length
  on vendors with tight limits (Akamai tags are at most 50 characters). Rejected; Kubernetes, which needs a prefix for
  custom labels, uses `nidentity.io/`.
- **A personal owner address.** Simple, but publishes personal data and breaks when ownership changes. Rejected.
- **Cost-center and project codes.** Useful in a multi-team company; here `application` and `environment` already
  separate cost. Not adopted; add a key with an ADR if that changes.

## Consequences

- From any resource you can tell who owns it, which environment and code manage it, how sensitive it is, and whether it
  may be deleted.
- `platform@nidentity.io` must exist as an inbound email route.
- Adding a required key is a change to the `tags` component and every root that calls it.

## References

- [ADR-0133](0133-we-ship-and-run-the-same-deployment-artifacts.md) — component rules.
- [ADR-0134](0134-cloud-native-operating-principles.md) — operating principles.
- [Kubernetes recommended labels](https://kubernetes.io/docs/concepts/overview/working-with-objects/common-labels/).
