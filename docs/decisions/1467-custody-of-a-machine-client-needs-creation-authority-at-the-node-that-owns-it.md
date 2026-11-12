# 1467. Custody of a machine client needs creation authority at the node that owns it

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Supersedes:** [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md) decision 7
- **Refines:** [ADR-1466](1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md),
  [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md) decision 7

## Context

Whoever can set a client's credentials acts with every role the client holds. Every role a machine client can hold lies
within its owning node, so custody of a machine client needs creation authority at that node, whenever its credentials
were set. Whoever can set its credentials could then grant every role it holds or will hold, and a credential set
before a grant never lends authority the custodian lacks. Cloud IAM leaves the custody
of a service account's keys to the operator (Google Cloud `iam.serviceAccountKeys.create`, Microsoft Entra application
owners), and Microsoft Entra protects role-assignable groups by restricting who manages them.

## Decision

**Custody of a machine client needs creation authority at its owning node, and its principal holds roles only within
that node.**

1. **Changing a machine client needs `Operations.Create` at its owning node.** Updating or deleting it, and changing its
   settings, owners or secrets, is a row check after the caller's authorization at the client's node and the read
   ([ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md) decision 7). It answers `403`,
   after the host-managed refusal (`409`).
2. **A role is granted to a client's principal only within the machine client's owning node.** The client must be a
   machine client and the node must be its owning node or a node beneath it. The ownership service answers a refused
   `RoleAssignmentGrant` (`IsRefused`), every grant route answers `409` and records no role-assignment event
   ([ADR-1449](1449-every-role-assignment-the-management-api-grants-or-revokes-is-audited.md)), and a creator that
   cannot hold `Owner` at what it creates is assigned none.
3. **Custody does not depend on when a credential was set.** Every role a machine client can hold lies within its owning
   node, and custody needs creation authority there, so whoever can set its credentials could grant every role it holds
   or will hold, whenever the credentials were set. A grant neither revokes nor re-establishes them.
4. **An ordinary client holds no roles.** Changing it needs only the authority at its node.

## Options considered

- **Revoke the client's credentials at a grant.** Rejected: it breaks running automation, and credentials borne by
  settings (`jwks`, `jwks_uri`, a TLS subject) are not secrets to revoke.
- **Require the granter's custody.** Rejected: the granter vouches for credentials that others set.
- **Record it as the operator's duty.** Rejected: it fails open.
- **Record who set each credential.** Rejected: a settings-borne credential has no setter record, and the check would
  follow every later grant.

## Consequences

- The control-plane tenant's administrators cannot set a machine client's credentials unless they hold creation
  authority at its owner.
- A team's administrators manage their machine clients.
- A client's owners manage it only when it is an ordinary client.

## References

- [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md),
  [ADR-1449](1449-every-role-assignment-the-management-api-grants-or-revokes-is-audited.md),
  [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md),
  [ADR-1466](1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md)
- PR review <https://github.com/NCodeGroup/identity/pull/585#discussion_r4485655771>
- Issue [#589](https://github.com/NCodeGroup/identity/issues/589)
