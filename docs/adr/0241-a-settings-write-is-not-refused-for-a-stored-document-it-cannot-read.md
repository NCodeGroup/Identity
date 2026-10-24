# 241. A settings write is not refused for a stored document it cannot read

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A tenant or server settings write refuses only the violations that its change introduces
([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)). To find them, the server write
resolves every stored tenant document, and both writes check the stored clients with
`IClientCoherenceValidator.Validate`. It throws a `JsonException` for a document that the current settings vocabulary
cannot honor: an unregistered setting, a value outside its domain, or a setting outside its scope
([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md),
[ADR-0140](0140-setting-values-are-validated-against-their-domain.md),
[ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md)).

An exception that escapes fails the write with `500`. One such document therefore blocks every server write, and an
unreadable document that a write replaces blocks the write that would repair it. The incoherent-client report lists
such a client with its `DocumentError`
([ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md)). Backlog `R104`.

## Decision

**A stored document that the settings vocabulary cannot honor is logged, not checked, and never a violation that the
change introduces.**

1. **The tenant walk skips an unreadable client.** `DefaultTenantClientCoherenceChecker` catches the `JsonException`
   that `IClientCoherenceValidator.Validate` throws for a client, logs `ClientSettingsUnreadable` with the tenant, the
   client and the message of the exception, and continues with the next client. Whether a document can be honored
   depends on its own names, values and scopes, not on its parent's settings, so such a client is incoherent before and
   after the change, and the change introduces nothing. The checker logs, so `ITenantClientCoherenceChecker` keeps its
   signature, and the checker takes an `ILogger<DefaultTenantClientCoherenceChecker>`.
2. **The server walk skips an unreadable tenant.** `ServerApiEndpointHandler.ValidateClientCoherenceAsync` catches the
   `JsonException` of resolving a tenant's document onto the server's current settings and onto its settings with the
   change, logs `TenantSettingsUnreadable` with the tenant and the message, and continues with the next tenant, because
   none of the tenant's clients is coherent under either settings.
3. **A write may repair the document it replaces.** When the document that a write replaces cannot be resolved, nothing
   was coherent under it, so the write checks no client, logs it, and proceeds after the written document passes
   `ValidateSettings`:
   - `ServerApiEndpointHandler.UpdateSettingsAsync` resolves the server's committed document through a helper that
     catches the `JsonException`, logs `ServerSettingsUnreadable` with the server and the message, and skips the walk.
   - `TenantApiEndpointHandler.ValidateClientCoherenceAsync` resolves the server's committed settings once and
     resolves the written document first, so its failure still propagates. It catches only the resolution of the
     replaced document, logs `TenantSettingsUnreadable`, and reports no violation.
4. **The log carries what the report carries.** `ClientSettingsUnreadable`, `TenantSettingsUnreadable` and
   `ServerSettingsUnreadable` are `Warning` messages of `NCode.Identity.OpenId.Management.Logging.Log`, with the event
   IDs 3006, 3007 and 3008. Each carries the `JsonException.Message`, the text that `IncoherentClient.DocumentError`
   carries. The answer of a write does not change: `204`, or a validation problem for the violations that the change
   introduces. The incoherent-client report still lists the client, and `IClientCoherenceValidator` keeps its throwing
   contract.

## Options considered

- **Count it as introduced.** Rejected: one document would block every server write, and a tenant's document could not
  be repaired through the API.
- **Return the documents in the answer of the write.** Rejected: a successful write answers `204` without a body, and
  the incoherent-client report is the API that lists them.
- **A validator that returns document errors.** Rejected: `IClientCoherenceValidator` keeps its contract
  ([ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md)), and each caller decides what an
  unreadable document means for it: the report lists it, and a write logs it and skips it.

## Consequences

- Such a document does not fail a settings write, and each write that meets it logs a warning that names it.
- An operator repairs a tenant's or the server's own document with a settings write.
- A repairing write checks no client, so the incoherent-client report, and for the server the report of the
  reconciliation ([ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md)), finds the
  clients that the repaired settings make incoherent.
- A management request that resolves a tenant document that cannot be honored answers `500`. While the server's own
  document cannot be honored, every request that resolves the server's settings answers `500` too, a tenant settings
  write included, until a server settings write repairs it. Backlog `R110` records both.
- This implements backlog `R104`.

## References

- [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md) — configuration the server cannot honor fails loudly.
- [ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md) — a settings write refuses the
  violations that it introduces.
- [ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md) — the reconciliation that
  reports the clients a change makes incoherent.
- [ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md) — the report that lists an unreadable
  document with its `DocumentError`.
- Code: `NCode.Identity.OpenId.Management.Endpoints.Clients.DefaultTenantClientCoherenceChecker`,
  `NCode.Identity.OpenId.Management.Endpoints.Servers.ServerApiEndpointHandler`,
  `NCode.Identity.OpenId.Management.Endpoints.Tenants.TenantApiEndpointHandler`,
  `NCode.Identity.OpenId.Management.Logging.Log`.
