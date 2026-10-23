# 233. The management API lists a tenant's incoherent clients

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

A client's settings are coherent with the effective settings of its tenant when every registered coherence rule is
satisfied ([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)). A management write is
refused while it would make a stored client incoherent. A change of the server's settings that is not a write, a
configuration reload or a deployment, cannot be refused, so the reconciliation logs each stored client that the change
makes incoherent ([ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md)). An
operator without the logs cannot find those clients, and each of them fails at its next request (backlog `R87`).

A stored settings document that the current setting vocabulary cannot honor makes a client incoherent too: the
validator throws a `JsonException` for it, and it cannot say which relation the client breaks.

## Decision

**One public reporter finds a tenant's incoherent clients, the reconciliation logs what it finds, and the management
API lists it on demand.**

1. **One reporter.** `IClientCoherenceReporter.FindIncoherentClientsAsync` checks one page of a tenant's stored clients
   against the tenant's effective settings and returns the incoherent ones among them, with the cursor of the tenant's
   next page of clients. `DefaultClientCoherenceReporter` reads the page through `IClientStore.GetSettingsPageAsync`,
   the untracked projection of the clients' settings, which carries no secrets. It reads under the tenant's ambient
   scope, so a caller outside the tenant's request, such as the reconciliation, reads the tenant's clients. It checks
   each client with `IClientCoherenceValidator`, which keeps its contract and throws `JsonException` for a document it
   cannot read. The reporter only reads.
2. **An incoherent client is a class, not a violation.** `IncoherentClient` holds `ClientId`, `Violations`, and
   `DocumentError`. A document the settings cannot honor is reported with `DocumentError` set to the message of the
   `JsonException` and no violations. A violation names a relation that a rule checked, with the client setting, the
   value, and the governing setting, and an unreadable document breaks no such relation.
3. **Both callers share the reporter.** `ClientCoherenceReportHandler` pages each tenant's clients through it and logs
   each incoherent client, and `ClientApiEndpointHandler` lists them, so the log and the API cannot disagree about
   what is incoherent.
4. **The route is tenant scoped.** `GET /api/tenants/{tenantId}/incoherent-clients`, with the query parameters `cursor`
   and `limit`, is mapped on the tenant group with the tag `Clients` and the endpoint name
   `api/clients/incoherent/list`. It is authorized like the client list, with `Operations.List` on the scope of the
   addressed tenant.
5. **A page checks a page of clients.** A request checks up to the normalized limit of the tenant's clients, 50 by
   default and 100 at most, and lists the incoherent ones among them, so a page may be empty while `continuationToken`
   is set. A caller pages until `continuationToken` is `null`. A tenant without a settings row has no clients to check
   and yields an empty page.
6. **The response is a collection of `IncoherentClientResource`.** Each item carries `tenantId`, `clientId`,
   `violations`, and `documentError`. A violation, `ClientCoherenceViolationResource`, carries `settingName`, `value`,
   `governingSettingName`, and `description`.
7. **The report is computed on demand.** Nothing is stored: each request reads the tenant's settings and one page of
   its clients.

## Options considered

- **Store the reconciliation's findings.** Rejected: they are stale after a write to a client or a tenant, and only a
  change of the server's settings produces them.
- **A filter on the client list.** Rejected: whether a client is incoherent is computed by rules over the resolved
  settings, so the predicate cannot be pushed to the database
  ([ADR-0080](0080-store-query-methods-push-filtering-to-dbms.md)).
- **A route under `/clients`.** Rejected: it collides with `/clients/{clientId}`, which would answer it as a client
  identifier.
- **One response for every client.** Rejected: the work of a request would be proportional to the size of the tenant.
- **An unreadable document as a violation with empty names.** Rejected: a violation names a relation that a rule
  checked, and a caller that reads the names would find none.

## Consequences

- An operator finds the incoherent clients of a tenant without the logs, and the answer is current when the request
  reads it.
- A client of the API pages until `continuationToken` is `null`, and a page can be empty before the last.
- A page costs one read of the tenant's settings and one read of up to `limit` clients' settings, with no secrets.
- A write to the settings of the server, and a write to the settings of a tenant that changes a setting a coherence
  rule reads, still fails when a stored document cannot be honored, because it checks the stored documents with the
  validator that throws; backlog `R104` records it.
- This implements backlog `R87`.

## References

- [ADR-0080](0080-store-query-methods-push-filtering-to-dbms.md) — a computed predicate cannot be pushed to the
  database.
- [ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md) — the coherence rules and the
  validator.
- [ADR-0219](0219-server-settings-converge-on-every-replica-through-one-reconciliation.md) — the reconciliation that
  reports the clients a change makes incoherent.
- Code: `NCode.Identity.OpenId.Authentication.Clients.IClientCoherenceReporter`,
  `NCode.Identity.OpenId.Authentication.Clients.DefaultClientCoherenceReporter`,
  `NCode.Identity.OpenId.Authentication.Clients.ClientCoherenceReportHandler`,
  `NCode.Identity.OpenId.Management.Endpoints.Clients.ClientApiEndpointHandler.ListIncoherentClientsAsync`.
