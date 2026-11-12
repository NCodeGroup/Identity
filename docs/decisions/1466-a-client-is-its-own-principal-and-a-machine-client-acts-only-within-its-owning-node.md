# 1466. A client is its own principal, and a machine client acts only within its owning node

- **Status:** Accepted
- **Date:** 2026-11-07
- **Deciders:** NCode Group

## Context

Automation, such as a provisioner that manages teams and tenants, calls the management API with `client_credentials`
tokens. Authority is a role assignment to a principal
([ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)),
and only a subject resolves to one. Without a subject, a machine caller's only authority is the bootstrap `GlobalAdmin`
claim
([ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md)),
which names no node of the resource hierarchy, so automation that manages one team holds the authority of the whole
platform. That fails least privilege.

[RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §2.2 requires `sub` in every JWT access token and, when the token has
no resource owner, requires it to identify the client. Resource servers and the management API need a subject to name
the caller.

The management API accepts tokens only from the control-plane tenant
([ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md)).
Creating a client there needs creation authority at that tenant, which a `TeamAdmin` lacks. A team needs automation that
acts only within it, yet a client principal that is granted a role anywhere has no boundary, and a control-plane client
with an interactive grant could collect the tokens of platform administrators.

Whoever can set a client's credentials acts with every role the client holds, so custody of those credentials has to
follow the roles.

One request asks whether a caller is a client acting for itself several times: authorization resolves the caller's
principal, the audit recorder asks for the actor of every change it records, and token exchange and UserInfo refuse a
client's own token. The recorder asks after the change is committed
([ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)), so a machine client that
deletes itself is gone from the store by then and its record would name an administrator. A host that registers its own
resolver must get one answer at every one of those places.

## Decision

**A client is its own principal, a `client_credentials` token names that principal as its subject, and a machine client
is a control-plane-tenant client that the platform or one team owns, that obtains tokens only for itself and that holds
roles only at its owning node or beneath it.**

1. **The client row carries its principal.** `PersistedClient.PrincipalId` is a 22-character Base64Url identifier of 16
   random bytes, unique per tenant. The client store generates it when it adds the client, overwrites any value the
   caller supplies and never changes it. No caller can pick another actor's principal, and a client recreated with the
   same `client_id` holds a fresh principal and none of the earlier client's authority. Removing a client removes every
   role assignment its principal holds.
2. **The token's subject is the client's principal.** A `client_credentials` access token carries the client's
   `PrincipalId` as `sub`, beside its `client_id`. The subject is never the `client_id` and never pairwise-projected.
3. **A subject names a client acting for itself when it equals the client's principal.** `IClientPrincipalResolver`
   decides whether a caller is a client acting for itself: its subject equals the `PrincipalId` of the client its
   `client_id` claim names in the issuing tenant. `DefaultPrincipalResolver` resolves such a subject to that principal
   while the client is enabled and to none when it is disabled, so disabling a client withdraws its authority at once. It
   never provisions a federated principal for it and never reads it as an upstream identity
   ([ADR-1035](1035-federated-principals-and-identity-resolution.md)).
4. **The resolver answers each caller once per request.** A caller is the context's tenant, its subject and its
   `client_id`. The first answer, a client or none, holds for the rest of the request.
   `DefaultClientPrincipalResolver` keeps it in `HttpContext.Items` (`ActingClientMemo`) and neither reads the store nor
   opens a store manager for a caller it has answered.
5. **The management API resolves the acting client when it authenticates the caller.**
   `ManagementAuthenticationEndpointFilter` asks the resolver as soon as the bearer token authenticates, before the
   endpoint runs. Authorization and the audit recorder reuse the answer, so a change a client makes for itself,
   deleting itself included, is audited as `AuditActor.Client(clientId)`.
6. **Every refusal of a client acting for itself asks the resolver.** Token exchange answers `invalid_grant` for such a
   `subject_token`, and a client's own token is a valid `actor_token`. UserInfo answers `invalid_token`. No endpoint
   compares a subject with a client's principal itself.
7. **A machine client has one owning node.** It is a control-plane-tenant client owned by the platform or by one team,
   recorded as `OwningNodeType` and `OwningNodeId` on `PersistedClient` and as `owningNodeType` and `owningNodeId` on
   `ClientResource`. The owning node is set when the client is created and never changes. Its creator is assigned no
   `Owner` role.
8. **Its creation and listing routes are authorized at the owning node.** `POST` and `GET /api/platform/clients` and
   `/api/teams/{teamId}/clients` create and list the platform's and a team's machine clients with Create or Read
   authority at the owning node, each authorized from the route before any read
   ([ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md)).
   Otherwise the control-plane tenant's client routes manage a machine client
   ([ADR-1257](1257-the-management-console-addresses-every-owners-secrets-settings-and-roles-through-one-owner-value.md)).
9. **It obtains tokens only for itself.** Its stored `grant_types_supported` is exactly `["client_credentials"]`,
   written when it is created. A settings write that leaves anything else, or that names a resource server for it,
   answers `400`.
10. **Its authority chain includes its owning node.** The chain of a machine client's node holds its owning node and
    every node above it, read from the client row as a tenant's team is read from the tenant row, so the owner's
    administrators manage it. An unknown client has no owning node, and a caller without authority gets `403` whether
    or not the client exists. The node above a machine client's settings is its owning node, where their limits are set
    ([ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)).
11. **Only a machine client's principal holds roles, and only at its owning node or beneath it.** The ownership service
    refuses any other grant (`RoleAssignmentGrant.IsRefused`), every grant route then answers `409` and records no
    role-assignment event
    ([ADR-1465](1465-role-lists-share-one-audited-grant-and-revoke-flow-and-a-caller-discovers-its-own-assignments.md)),
    and an ordinary client holds no roles. Changing an ordinary client needs only the authority at its node.
12. **Custody of a machine client needs creation authority at its owning node.** Updating or deleting it, and changing
    its settings, owners or secrets, also needs `Operations.Create` at its owning node. The check is a row check after
    the caller's authorization at the client's node and the read, and it answers `403` after the host-managed `409`
    ([ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md)).
    It does not depend on when a credential was set: every role a machine client can hold lies within its owning node,
    so whoever can set its credentials could grant every role it holds or will hold. A grant neither revokes nor
    re-establishes credentials.
13. **A team that owns a machine client cannot be deleted** and answers `409`, as it does for a team that has tenants.
    A promotion leaves machine clients alone.

## Options considered

- **A separate `ServicePrincipal` entity.** Rejected: it is a second lifecycle beside the client's, and an administrator
  cannot name it before the client's first call.
- **The raw `client_id` as the principal, or `<client_id>@clients` as the subject.** Rejected: the first shares a
  namespace with other principal identifiers, a host-configured client id is chosen by an operator, and a client
  recreated with the same `client_id` inherits the earlier client's authority; the second is a namespace that no role
  assignment names.
- **Omitting `sub`, or a grant-type claim.** Rejected: [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §2.2 requires
  `sub`, and a grant-type claim is non-standard where the server decides from its data.
- **A role claim on the token.** Rejected: it is stale until the token expires and names no node of the resource
  hierarchy.
- **Accepting a team's tenants' tokens at the management API.** Rejected: the management API accepts only the
  control-plane tenant's tokens.
- **Letting a team administrator create any control-plane client.** Rejected: it has no boundary, and an interactive
  control-plane client could phish platform administrators.
- **Revoking the client's credentials at a grant.** Rejected: it breaks running automation, and credentials borne by
  settings (`jwks`, `jwks_uri`, a TLS subject) are not secrets to revoke.
- **Recording who set each credential, or leaving custody to the operator.** Rejected: a settings-borne credential has
  no setter record, the check would follow every later grant, and a duty left to the operator fails open.
- **Resolving the actor in each handler that changes a client, or in the audit recorder.** Rejected: the next handler
  that forgets records the wrong actor, and the recorder's first read can follow the commit.
- **UserInfo comparing the subject with its resolved client's principal.** Rejected: a host's own resolver would give
  another answer at token exchange than at UserInfo.

## Consequences

- Every access token has a subject, and a client-credentials token's subject is a principal that role assignments name.
  A resource server tells a client's own token by comparing `sub` with the client it knows.
- A machine client can hold `TeamAdmin` at one team and manage that team's tenants without the bootstrap credential,
  and a team provisions its own automation without platform authority.
- The control-plane tenant's settings, quotas and usage govern machine clients. Its administrators can read a team's
  machine clients but cannot set their credentials unless they hold creation authority at the owner, while a team's
  administrators manage them.
- A request reads each caller's client at most once, and a self-deleting machine client is audited as the `client`
  actor. Every authenticated management request reads its caller's client once, also when it records nothing, and
  UserInfo reads the client once more for a token whose client does not act for itself.
- An implementation of `IClientPrincipalResolver` answers each caller once per request.
- Machine clients that call the management API live in the control-plane tenant, where the resolver reads them.

## References

- [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068), [RFC 8693](https://www.rfc-editor.org/rfc/rfc8693)
- [ADR-1035](1035-federated-principals-and-identity-resolution.md),
  [ADR-1044](1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md),
  [ADR-1191](1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md),
  [ADR-1257](1257-the-management-console-addresses-every-owners-secrets-settings-and-roles-through-one-owner-value.md),
  [ADR-1351](1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md),
  [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md),
  [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md),
  [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md),
  [ADR-1465](1465-role-lists-share-one-audited-grant-and-revoke-flow-and-a-caller-discovers-its-own-assignments.md)
- `src/NCode.Identity.OpenId.Management/Authorization/DefaultCallerPrincipalResolver.cs`,
  `src/NCode.Identity.OpenId.Core/PrincipalResolution/DefaultPrincipalResolver.cs`,
  `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Stores/ClientStore.cs`,
  `IClientPrincipalResolver.cs`, `DefaultClientPrincipalResolver.cs`, `ActingClientMemo.cs`,
  `ManagementAuthenticationEndpointFilter.cs`, `DefaultManagementAuditRecorder.cs`,
  `DefaultUserInfoEndpointProvider.cs`
- Issues [#557](https://github.com/NCodeGroup/identity/issues/557), [#587](https://github.com/NCodeGroup/identity/issues/587),
  [#588](https://github.com/NCodeGroup/identity/issues/588), [#589](https://github.com/NCodeGroup/identity/issues/589),
  [#632](https://github.com/NCodeGroup/identity/issues/632), [#633](https://github.com/NCodeGroup/identity/issues/633),
  [#634](https://github.com/NCodeGroup/identity/issues/634)
