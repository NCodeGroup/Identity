# 25. Pre-release posture: greenfield on `dev`, and an Auth0-parity-plus design philosophy

- **Status:** Accepted
- **Date:** 2026-09-30
- **Deciders:** NCode Group

## Context

The family is a greenfield **OpenID Connect + OAuth 2.0** server built from the ground up. It is pre-1.0 and has not
had a stable release; all work lands on the `dev` branch until the feature set is complete. Two recurring forces shape
day-to-day design decisions and are worth recording once so each feature need not re-litigate them:

1. **How much backward compatibility is owed while the product is unreleased.** The public-API surface is tracked and,
   after release, evolves compatibly ([ADR-0005](0005-public-api-surface-is-tracked-and-evolves-compatibly.md)). The
   open question is what that implies _before_ the first stable tag, when there are no external consumers to protect.
2. **How closely to follow Auth0.** Auth0's vocabulary and shapes (resource servers, scopes/permissions, client grants,
   tenants) are well understood and lower the onboarding cost ([ADR-0023](0023-scopes-and-resources-management-model.md)
   adopts them deliberately). The open question is whether to clone Auth0 exactly or to improve on it.

## Decision

Two standing stances govern design while the family is pre-release.

**1. Greenfield pre-release posture.** Until the first stable release, a **breaking change is acceptable on `dev`** when
it yields a cleaner, more coherent design. Prefer a **clean cutover over compatibility shims or dual code paths**: there
are no released consumers to protect, so a deprecation cycle buys nothing and a half-migrated "two sources of truth"
state is the worse outcome. Public-API tracking still runs, so expect **frequent `PublicAPI.Unshipped.txt` churn** — that
is the normal, accepted signal of pre-release evolution, not a smell. This latitude **ends at the first stable tag**,
after which [ADR-0005](0005-public-api-surface-is-tracked-and-evolves-compatibly.md) governs in full.

**2. Auth0-parity-plus.** Model concepts after Auth0's familiar vocabulary and shapes for recognizability and a
well-trodden security model, but deliberately offer **more flexibility and finer-grained control where it is cheap and
principled**. Favor **data-driven, general mechanisms over hardcoded special cases**. The litmus test for a deviation:
it must preserve the mental model an Auth0 user already has while giving an operator a control Auth0 does not — at little
added complexity. For example, a system resource server (`IsSystem`) is implicitly available to every client in its
tenant, which delivers Auth0's "OIDC scopes just work" behavior through a **general resource-server property** rather
than a hardcoded OIDC special case — so the same mechanism can later bless any operator-chosen resource server.

## Options considered

- **Maintain backward compatibility from day one.** Rejected: premature. It forces deprecation cycles and compatibility
  shims that slow the clean design while no consumer exists to benefit, and it normalizes long-lived transitional code.
- **Clone Auth0 exactly.** Rejected: forgoes improvements that are cheap to make now and hard to retrofit later; a
  faithful clone inherits Auth0's limitations without reason.
- **Diverge freely from Auth0.** Rejected: discards the familiarity and the battle-tested model that make the system
  approachable and auditable; novelty for its own sake raises onboarding and review cost.
- **Record both stances once as an ADR (chosen).** A single durable reference each feature can cite, instead of
  re-deciding per pull request.

## Consequences

- Breaking changes on `dev` (renames, removed settings, non-nullable tightening, reshaped contracts) are expected and
  need no migration path until the first stable release; `PublicAPI.Unshipped.txt` churn is routine.
- Once a stable version is tagged, this latitude is spent and
  [ADR-0005](0005-public-api-surface-is-tracked-and-evolves-compatibly.md) applies without exception; a note in this ADR
  should mark that transition.
- Feature work cites Auth0 as the baseline and justifies each deliberate deviation as "parity-plus" — a concrete control
  Auth0 lacks, added at low complexity — which keeps divergence intentional rather than accidental.
- Reviewers evaluate a breaking change on `dev` by design quality alone, not by compatibility cost.

## References

- [ADR-0005](0005-public-api-surface-is-tracked-and-evolves-compatibly.md) — the post-release compatibility regime this
  posture defers to once the family ships.
- [ADR-0008](0008-conventions-and-lessons-live-in-the-repository.md) — durable conventions and stances live in the
  repository, which is why these are recorded here rather than left implicit.
- [ADR-0023](0023-scopes-and-resources-management-model.md) — the Auth0-style resource-server / scope / client-grant
  model that this philosophy extends with implicit system-resource-server grants.
- [`AGENTS.md`](../../AGENTS.md) — the contributor/agent guide that points here for the pre-release workflow.
