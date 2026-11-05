# 1419. A client acting for itself is a principal that holds role assignments

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group
- **Amends:** [ADR-1033](1033-authorization-model-scopes-roles-and-ownership.md) (a client acting for itself is the
  client case of the abstract principal);
  [ADR-1034](1034-persisted-role-assignments-and-ownership.md) (an assignment's principal may be a client's, and
  removing a client removes the assignments it holds);
  [ADR-1035](1035-federated-principals-and-identity-resolution.md) (the service principal is the client itself,
  identified by a `PrincipalId` its row carries)

## Context

Automation, such as a provisioner that manages teams and tenants, calls the management API with `client_credentials`
tokens. Such a token carries a `client_id` and no `sub`. Authority is a role assignment to a principal, and only a
subject resolves to one, so a machine caller's only authority is the bootstrap `GlobalAdmin` claim. That claim names no
node of the resource hierarchy, so automation that manages one team holds the authority of the whole platform, which
fails least privilege.

## Decision

1. **The client row carries its principal.** `PersistedClient.PrincipalId` is a 22-character Base64Url identifier of 16
   random bytes, unique per tenant. A client and its principal are one-to-one within a tenant and share a lifecycle.
   The client store generates the identifier when it adds the client, overwrites any value the caller supplies, and
   never changes it. No caller can pick another actor's principal, and a client recreated with the same `client_id`
   holds a fresh principal and none of the earlier client's authority.
2. **A request without a subject resolves through its client.** `DefaultPrincipalResolver` reads the `client_id` claim
   when the token has no subject and returns that client's `PrincipalId` in the ambient tenant. A missing or disabled
   client resolves to no principal, so disabling a client withdraws its authority at once. The token's shape is
   unchanged, and the `IPrincipalResolver` seam is the extension point
   [ADR-1035](1035-federated-principals-and-identity-resolution.md) reserves.
3. **Grants use the existing endpoints.** `POST /api/teams/{teamId}/admins` and `POST /api/teams/{teamId}/owners` accept
   `ClientResource.principalId`. A client that creates a team or tenant becomes its owner, as a person does.
4. **Removing a client removes every assignment its principal holds.**
5. **A management change a client makes acting for itself is audited with `AuditActor.Client(clientId)`.**
6. **The bootstrap administrator stays the cold-start `GlobalAdmin` claim.** Durable administrators, human or machine,
   are assignments it provisions. `BootstrapAdminAccessTokenClaimsHandler` and the claim are unchanged.
7. **Custody of a client's credentials needs the authority to grant what the client holds.** Managing a client whose
   principal holds role assignments (updating it, deleting it, changing its settings or owners, or any of its secrets)
   also needs `Operations.Create` at every node where its principal holds an assignment. The check follows the
   caller's own authorization at the client and answers 403 when it fails.

## Options considered

- **A separate `ServicePrincipal` entity.** Rejected: it is a second lifecycle beside the client's, and an administrator
  cannot name it before the client's first call.
- **The raw `client_id` as the principal reference.** Rejected: it shares a namespace with other principal identifiers,
  and a recreated client with the same `client_id` inherits the earlier client's authority.
- **A role claim on the token.** Rejected: it is stale until the token expires and names no node of the resource
  hierarchy.

## Consequences

- A machine client can hold `TeamAdmin` at one team and manage that team's tenants without the bootstrap credential.
- A client's owners can rotate its secrets, so they can act with every role the client holds. A role is granted to a
  client only where its owners may hold it.
- An administrator of the control-plane tenant's clients cannot impersonate a machine client that administers a team it
  does not: minting a secret for that client, or changing its settings or owners, is refused.
- Machine clients that call the management API live in the control-plane tenant
  ([ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md)), where the resolver reads
  them.
- Authorization is unchanged: handlers evaluate the resolved principal's assignments as they do for a person.

## References

- [ADR-1033](1033-authorization-model-scopes-roles-and-ownership.md),
  [ADR-1034](1034-persisted-role-assignments-and-ownership.md),
  [ADR-1035](1035-federated-principals-and-identity-resolution.md),
  [ADR-1191](1191-the-management-api-authenticates-against-the-control-plane-tenant.md)
- `src/NCode.Identity.OpenId.Core/PrincipalResolution/DefaultPrincipalResolver.cs`,
  `src/NCode.Identity.OpenId.Persistence.EntityFramework.Core/Stores/ClientStore.cs`,
  `src/NCode.Identity.OpenId.Management/Auditing/DefaultManagementAuditRecorder.cs`
- Issue [#557](https://github.com/NCodeGroup/Identity/issues/557)
