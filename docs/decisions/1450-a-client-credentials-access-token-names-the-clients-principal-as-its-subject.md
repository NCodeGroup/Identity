# 1450. A client-credentials access token names the client's principal as its subject

- **Status:** Accepted (amends
  [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md) decision 2 and
  [ADR-1044](1044-management-api-authentication-and-globaladmin-bootstrap.md);
  amended by [ADR-1470](1470-a-callers-acting-client-is-resolved-once-per-request.md))
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

[RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §2.2 requires `sub` in every JWT access token and, when the token has
no resource owner, requires it to identify the client. A client acting for itself is a principal with a server-generated
`PrincipalId`
([ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md)). Resource servers and the
management API need a subject to name the caller.

## Decision

**A `client_credentials` access token's `sub` is the client's `PrincipalId`, and `IPrincipalResolver` knows a client
acting for itself.**

1. **The subject is the client's principal.** A `client_credentials` access token carries the client's `PrincipalId` as
   `sub`, beside its `client_id`. The subject is never the `client_id` and never pairwise-projected.
2. **A subject names a client acting for itself when it equals the client's principal.** A subject names a client acting
   for itself when it equals the `PrincipalId` of the client its `client_id` claim names in the issuing tenant.
   `IClientPrincipalResolver` decides it.
3. **`DefaultPrincipalResolver` resolves it.** It resolves such a subject to that principal while the client is
   enabled, and to none when the client is disabled. It never provisions a federated principal for it and never reads
   it as an upstream identity.
4. **Endpoints that serve an end-user refuse it.** Token exchange answers `invalid_grant` for such a `subject_token`,
   and UserInfo answers `invalid_token`. It is a valid `actor_token`.
5. **The management API resolves every caller through `IPrincipalResolver`.** The audit records a client acting for
   itself as `AuditActor.Client(clientId)`.

## Options considered

- **`sub` = `client_id`.** Rejected: it shares a namespace with principals, and a host-configured client id is chosen by
  an operator ([RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §5).
- **`<client_id>@clients`.** Rejected: it is a second namespace that no role assignment names.
- **Omitting `sub`.** Rejected: [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) §2.2 requires it.
- **A grant-type claim.** Rejected: it is non-standard, and the server decides from its data.

## Consequences

- Every access token has a subject.
- A client-credentials token's subject is a principal that role assignments name.
- A resource server tells a client's own token by comparing `sub` with the client it knows.

## References

- [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068), [RFC 8693](https://www.rfc-editor.org/rfc/rfc8693)
- [ADR-1035](1035-federated-principals-and-identity-resolution.md),
  [ADR-1044](1044-management-api-authentication-and-globaladmin-bootstrap.md),
  [ADR-1419](1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md)
- Issue [#587](https://github.com/NCodeGroup/Identity/issues/587)
