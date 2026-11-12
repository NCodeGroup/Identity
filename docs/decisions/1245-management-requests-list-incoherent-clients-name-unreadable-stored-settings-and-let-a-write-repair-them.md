# 1245. Management requests list incoherent clients, name unreadable stored settings, and let a write repair them

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

A client's settings are coherent with the effective settings of its tenant when every registered coherence rule is
satisfied ([ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md)).
A management write is refused while it would make a stored client incoherent. A change of the platform's settings that
is not a write, a configuration reload or a deployment, cannot be refused, so the reconciliation logs each stored client
that the change makes incoherent ([ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md)).
An operator without the logs cannot find those clients, and each of them fails at its next request (issue
[#278](https://github.com/NCodeGroup/identity/issues/278)).

A stored settings document can also name a setting, a value or a scope that the registered settings cannot honor: an
unregistered setting, a value outside its domain, or a setting outside its scope
([ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md),
[ADR-1140](1140-setting-values-are-validated-against-their-domain-not-only-their-type.md),
[ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md)). Such a document
makes a client incoherent too: the validator throws a `JsonException` for it and cannot say which relation the client
breaks.

Three forces follow from one unreadable document:

- A settings write refuses only the violations that its change introduces. To find them, the walks over the stored
  tenants and clients throw the `JsonException` for the unreadable document, an exception that escapes fails the write
  with `500`, and one such document blocks every platform write. An unreadable document that a write replaces blocks the
  very write that would repair it (issue [#295](https://github.com/NCodeGroup/identity/issues/295)).
- A request that needs the effective settings of the document's owner cannot do its work without it: the read of the
  effective settings, the creation of a client, the report, and the judgment of a password's policy. The resolver throws
  a `JsonException`, and an exception that escapes answers `500`, which tells the caller that the server failed, not
  which document to replace (issue [#301](https://github.com/NCodeGroup/identity/issues/301)).
- The caller needs a way to list the incoherent clients without the logs.

## Decision

**A management request lists a tenant's incoherent clients on demand, answers `409 Conflict` with a problem that names
a stored settings document it needs and cannot honor, and a settings write logs such a document instead of refusing it,
so a write repairs it.**

### The unreadable document is named by one exception and one filter

1. **`UnreadableSettingsException` names the document.** It is a public sealed class in
   `NCode.Identity.OpenId.Abstractions`, namespace `NCode.Identity.OpenId.Settings`, that derives from `JsonException`
   and carries `ScopeName`, `OwnerId` and `DocumentError`, with the original exception as its inner exception.
   `ResolveStored` and the committed-document reads throw it, so every `catch (JsonException)` still catches it. A plain
   `JsonException` from a proposed document stays a `500`, and runtime protocol resolution keeps failing closed
   ([ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md)).
2. **One filter answers it.** `UnreadableSettingsEndpointFilter` is installed once on the management root group
   (`ManagementRootGroup`), beside `StoreConcurrencyEndpointFilter`
   ([ADR-1483](1483-a-management-request-whose-binding-fails-is-answered-as-a-problem-its-route-declares.md)), so the
   nested route groups, such as the local accounts', are covered. No handler catches the exception, so the answers
   cannot drift apart.
3. **The answer is `409` with a problem.** RFC 9110 §15.5.10 defines `409 Conflict` as a conflict with the current
   state of the resource that the user might be able to resolve, and a settings write repairs the document. The body is
   an RFC 9457 problem, `application/problem+json`. Its `detail` is the message of the exception, which names the scope,
   the owner and the error of the settings vocabulary, followed by "Replace the document with a settings write.". Its
   extensions are `settingsScope`, the registered scope name, `ownerId`, the identifier of the owner of the document,
   and `documentError`, the message of the error that the vocabulary reported. The filter logs
   `StoredSettingsUnreadable`, a `Warning` message of `NCode.Identity.OpenId.Management.Logging.Log` with the event ID
   3009, which carries the scope, the owner and the error.
4. **Each request answers for the documents it needs, its parents' included.** The table lists the stored documents
   that each request resolves:

   | Request                                                   | Stored documents it needs     | Answer                    |
   | --------------------------------------------------------- | ----------------------------- | ------------------------- |
   | Read the platform's effective settings                    | platform                      | `409` naming the platform |
   | Read a team's effective settings                          | platform, team                | `409` naming the document |
   | Read a tenant's effective settings                        | platform, team, tenant        | `409` naming the document |
   | Read a client's or a resource server's effective settings | platform, team, tenant, owner | `409` naming the document |
   | Create a client, update a client's settings               | platform, team, tenant        | `409` naming the document |
   | List a tenant's incoherent clients                        | platform, team, tenant        | `409` naming the document |
   | Create a local account, reset its password                | platform, team, tenant        | `409` naming the document |
   | Write a team's settings                                   | platform                      | `409` naming the platform |
   | Write a tenant's settings                                 | platform, team                | `409` naming the document |

   A write that replaces the document it cannot read repairs it, as item 8 says.
5. **Resolution happens after authorization.** An effective-settings read resolves inside the `mapperAsync` of
   `ProcessGetAsync`, which runs after the caller is authorized, so a caller who may not read the settings learns
   nothing about a document that cannot be honored
   ([ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md)). The read passes
   the stored document's `JsonElement`, not the persisted settings, so the answer carries no `ETag` of the raw
   document, whose token is not the effective view's. The creation, the update, the report and the local-account
   requests authorize before they resolve.
6. **The problem names any needed document, a parent's included.** A caller authorized for the request learns the
   scope, the owner's identifier and the vocabulary's error of the document that cannot be honored, also when it is the
   document of a parent scope that the caller may not read itself. The effective settings that the same caller reads
   already carry the values that the parent documents set, and the vocabulary's errors name a setting and the rule it
   breaks, never the value, so the answer tells the caller which document an operator replaces and nothing that the
   effective view hides.

### A settings write is not refused for a document it cannot read

7. **The walks log an unreadable document and continue.** Whether a document can be honored depends on its own names,
   values and scopes, not on its parent's settings, so such a document is unreadable before and after the change, and
   the change introduces nothing:
   - `DefaultTenantClientCoherenceChecker` catches the `JsonException` that `IClientCoherenceValidator.Validate`
     throws for a client, logs `ClientSettingsUnreadable` (3006) with the tenant, the client and the message of the
     exception, and continues with the next client. It takes an `ILogger<DefaultTenantClientCoherenceChecker>`, and
     `ITenantClientCoherenceChecker` keeps its signature.
   - The platform and team walks catch the `JsonException` of resolving a tenant's document onto the current settings
     and onto the settings with the change, log `TenantSettingsUnreadable` (3007) or `TeamSettingsUnreadable` (3012)
     with the owner and the message, and continue with the next tenant or team, because none of its clients is coherent
     under either settings.
8. **A write repairs the document it replaces.** When the document that a write replaces cannot be resolved, nothing
   was coherent under it, so the write checks no client, logs `PlatformSettingsUnreadable` (3008),
   `TeamSettingsUnreadable` or `TenantSettingsUnreadable`, and proceeds once the written document passes
   `ValidateSettings`. The written document is resolved first, so its failure still propagates. The answer of a write is
   `204` or a validation problem for the violations the change introduces, and `ITenantClientCoherenceChecker` and
   `IClientCoherenceValidator` keep their contracts: the validator throws `JsonException`, and each caller decides what
   an unreadable document means for it.
9. **The log carries what the report carries.** The unreadable-document messages are `Warning` messages of
   `NCode.Identity.OpenId.Management.Logging.Log`, each carrying the `JsonException.Message`, the text that
   `IncoherentClient.DocumentError` carries.

### The incoherent-client report lists what it finds

10. **One reporter finds a tenant's incoherent clients.** `IClientCoherenceReporter.FindIncoherentClientsAsync`
    (`DefaultClientCoherenceReporter`) checks one page of a tenant's stored clients against the tenant's effective
    settings through `IClientStore.GetSettingsPageAsync`, the untracked, secret-free projection of the clients'
    settings read under the tenant's scope, and returns the incoherent ones with the cursor of the tenant's next page of
    clients. It reads under the tenant's ambient scope, so a caller outside the tenant's request, such as the
    reconciliation, reads the tenant's clients. It checks each client with `IClientCoherenceValidator`. The reporter
    only reads.
11. **Both callers share the reporter.** `ClientCoherenceReportHandler` pages each tenant's clients through it and logs
    each incoherent client, and `ClientApiEndpointHandler` lists them, so the log and the API cannot disagree about what
    is incoherent.
12. **An incoherent client is a class, not a violation.** `IncoherentClient` holds `ClientId`, `Violations` and
    `DocumentError`. A document the settings cannot honor is reported with `DocumentError` set to the message of the
    `JsonException` and no violations, because a violation names a relation that a rule checked, with the client
    setting, the value and the governing setting.
13. **The route is tenant scoped.** `GET /api/tenants/{tenantId}/incoherent-clients` takes `cursor` and `limit`, is
    mapped on the tenant group with the tag `Clients` and the endpoint name `api/clients/incoherent/list`, and
    authorizes `Operations.List` at the addressed tenant.
14. **A page checks a page of clients.** A request checks up to the normalized limit of the tenant's clients, 50 by
    default and 100 at most, and lists the incoherent ones among them, so a page may be empty while `continuationToken`
    is set. A caller pages until `continuationToken` is `null`. A tenant without a settings row has no clients to check
    and yields an empty page.
15. **The response is a collection of `IncoherentClientResource`.** Each item carries `tenantId`, `clientId`,
    `violations` and `documentError`. A violation, `ClientCoherenceViolationResource`, carries `settingName`, `value`,
    `governingSettingName` and `description`. The report is computed on demand and nothing is stored: each request reads
    the tenant's settings and one page of its clients.

## Options considered

- **Answer `500`.** Rejected: it tells the caller that the server failed, while this condition is known and a settings
  write repairs it.
- **Answer `422`.** Rejected: the content of the request is fine, and the stored state is not.
- **Skip an unreadable document on a read.** Rejected: a read, a client write or a password policy cannot do its work
  without the document, and a silent skip would answer with settings that the owner does not have.
- **A `try`/`catch` in each handler.** Rejected: the answers would drift, and a handler added later would forget it.
- **Count an unreadable document as introduced by a write.** Rejected: one document would block every platform write,
  and a tenant's document could not be repaired through the API.
- **Return the documents in the answer of a write.** Rejected: a successful write answers `204` without a body, and the
  incoherent-client report is the API that lists them.
- **A validator that returns document errors.** Rejected: `IClientCoherenceValidator` keeps its contract, and each
  caller decides what an unreadable document means for it: the report lists it, and a write logs it and skips it.
- **Store the reconciliation's findings.** Rejected: they are stale after a write to a client or a tenant, and only a
  change of the platform's settings produces them.
- **A filter on the client list.** Rejected: whether a client is incoherent is computed by rules over the resolved
  settings, so the predicate cannot be pushed to the database
  ([ADR-1080](1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md)).
- **A route under `/clients`.** Rejected: it collides with `/clients/{clientId}`, which would answer it as a client
  identifier.
- **One response for every client.** Rejected: the work of a request would be proportional to the size of the tenant.
- **An unreadable document as a violation with empty names.** Rejected: a caller that reads the names would find none.

The chosen option is the typed exception, one filter, log-and-continue walks and the paged on-demand report.

## Consequences

- An operator learns which document to repair from the answer, and the log carries the same names.
- A caller who may not make a request learns nothing about a document it needs.
- Such a document does not fail a settings write, and each write that meets it logs a warning that names it.
- A repairing write checks no client, so the incoherent-client report, and for the platform the report of the
  reconciliation, finds the clients that the repaired settings make incoherent.
- An operator finds the incoherent clients of a tenant without the logs, and the answer is current when the request
  reads it. A page costs one read of the tenant's settings and one read of up to `limit` clients' settings, with no
  secrets, and a client of the API pages until `continuationToken` is `null`.
- A custom `IEffectiveSettingsResolver` implements `ResolveStored`, and it and the committed-document reads throw
  `UnreadableSettingsException` for a stored document.
- The tests cover the 409 answer per request, the logged walks, the repairing write and the report's paging.

## References

- [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110#name-409-conflict) §15.5.10 — 409 Conflict.
- [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) — problem details for HTTP APIs.
- Issues [#278](https://github.com/NCodeGroup/identity/issues/278),
  [#295](https://github.com/NCodeGroup/identity/issues/295) and
  [#301](https://github.com/NCodeGroup/identity/issues/301).
- [ADR-1080](1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md) — a computed
  predicate cannot be pushed to the database.
- [ADR-1139](1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md)
  — configuration the server cannot honor fails loudly.
- [ADR-1204](1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md) —
  the resolver.
- [ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md)
  — the coherence rules and the validator.
- [ADR-1412](1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md) — the reconciliation that
  reports the clients a change makes incoherent.
- [ADR-1424](1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md) — authorization
  before a read.
- [ADR-1483](1483-a-management-request-whose-binding-fails-is-answered-as-a-problem-its-route-declares.md) — the
  endpoint filter on the management root group.
- Code: `NCode.Identity.OpenId.Settings.UnreadableSettingsException`,
  `NCode.Identity.OpenId.Settings.IEffectiveSettingsResolver`,
  `NCode.Identity.OpenId.Management.Endpoints.UnreadableSettingsEndpointFilter`,
  `NCode.Identity.OpenId.Management.Endpoints.ManagementRootGroup`,
  `NCode.Identity.OpenId.Management.Endpoints.Clients.DefaultTenantClientCoherenceChecker`,
  `NCode.Identity.OpenId.Management.Logging.Log`,
  `NCode.Identity.OpenId.Authentication.Clients.IClientCoherenceReporter`,
  `NCode.Identity.OpenId.Authentication.Clients.DefaultClientCoherenceReporter`,
  `NCode.Identity.OpenId.Authentication.Clients.ClientCoherenceReportHandler`,
  `NCode.Identity.OpenId.Management.Endpoints.Clients.ClientApiEndpointHandler.ListIncoherentClientsAsync`.
