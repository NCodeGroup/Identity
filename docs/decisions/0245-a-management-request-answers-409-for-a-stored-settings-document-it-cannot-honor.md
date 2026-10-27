# 245. A management request answers 409 for a stored settings document it cannot honor

- **Status:** Accepted
- **Date:** 2026-10-24
- **Deciders:** NCode Group

## Context

A stored settings document can name a setting, a value or a scope that the registered settings cannot honor: an
unregistered setting, a value outside its domain, or a setting outside its scope
([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)). A settings write passes such a document and logs it
([ADR-0241](0241-a-settings-write-is-not-refused-for-a-stored-document-it-cannot-read.md)), and the incoherent-client
report lists it with its `DocumentError`
([ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md)).

A request that needs the effective settings of the document's owner cannot do its work without it: the read of the
effective settings of a tenant, a client or a resource server, the creation of a client, the report, and the judgment
of a password's policy. The resolver throws a `JsonException` for it, and an exception that escapes answers `500`, which
tells the caller that the server failed, not which document to replace. While the server's own document cannot be
honored, every request that resolves the server's settings answers `500` as well (issue [#301](https://github.com/NCodeGroup/Identity/issues/301)).

## Decision

**A management request that needs a stored settings document the settings vocabulary cannot honor answers
`409 Conflict` with a problem that names the document, after authorization, from one endpoint filter on the management
route group.**

1. **The answer is `409` with a problem.** RFC 9110 §15.5.10 defines `409 Conflict` as a conflict with the current
   state of the resource that the user might be able to resolve, and a settings write repairs the document
   ([ADR-0241](0241-a-settings-write-is-not-refused-for-a-stored-document-it-cannot-read.md)). The body is an RFC 9457
   problem, `application/problem+json`. Its `detail` is the message of the exception, which names the scope, the owner
   and the error of the settings vocabulary, followed by "Replace the document with a settings write.". Its extensions
   are `settingsScope`, the registered scope name (`server`, `tenant`, `client` or `resource_server`), `ownerId`, the
   identifier of the server, tenant, client or resource server that owns the document, and `documentError`, the message
   of the error that the vocabulary reported. A `500` means an unexpected condition and a `422` concerns the content of
   the request, and neither fits.
2. **`UnreadableSettingsException` names the document.** It is a public sealed class in
   `NCode.Identity.OpenId.Abstractions`, namespace `NCode.Identity.OpenId.Settings`, that derives from `JsonException`
   and carries `ScopeName`, `OwnerId` and `DocumentError`, with the original exception as its inner exception. Every
   `catch (JsonException)`, the skips of ADR-0241 among them, keeps catching it, and the resolver's contract stays that
   a document it cannot honor throws a `JsonException`.
3. **The resolver names a stored document.** `IEffectiveSettingsResolver.ResolveStored(parentSettings, scopeName,
   ownerId, settings)` merges the document that a store holds for an owner of a scope onto its parent's effective
   settings and throws `UnreadableSettingsException`. `ResolveServerAsync(CancellationToken)` resolves the server's
   committed document and throws it naming the server. `Resolve` is for a proposed document and for the sites that skip
   an unreadable one, and a plain `JsonException` from a proposed document stays a `500`.
4. **One filter answers it.** `UnreadableSettingsEndpointFilter` is installed once on the management route group,
   beside `StoreConcurrencyEndpointFilter`
   ([ADR-0214](0214-a-management-write-that-loses-a-race-is-answered-412.md)), so the nested route groups, such as the
   local accounts', are covered. It logs `StoredSettingsUnreadable`, a `Warning` message of
   `NCode.Identity.OpenId.Management.Logging.Log` with the event ID 3009, which carries the scope, the owner and the
   error, and it returns the problem. No handler catches the exception, so the answers cannot drift apart.
5. **Each request answers for the documents it needs.**

   | Request                                      | Stored documents it needs       | Answer                    |
   | -------------------------------------------- | ------------------------------- | ------------------------- |
   | Read the server's effective settings         | server                          | `409` naming the server   |
   | Read a tenant's effective settings           | server, tenant                  | `409` naming the document |
   | Read a client's effective settings           | server, tenant, client          | `409` naming the document |
   | Read a resource server's effective settings  | server, tenant, resource server | `409` naming the document |
   | Create a client, update a client's settings  | server, tenant                  | `409` naming the document |
   | List a tenant's incoherent clients           | server, tenant                  | `409` naming the document |
   | Create a local account, reset its password   | server, tenant                  | `409` naming the document |
   | Write a tenant's settings                    | server                          | `409` naming the server   |

   A write that replaces the document it cannot read keeps the skip of ADR-0241: a tenant settings write whose own
   document is unreadable repairs it, and a server settings write skips each stored document it cannot honor. A client
   document that the report cannot read keeps its `documentError` row
   ([ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md)).
6. **A read resolves only after authorization.** An effective-settings read resolves inside the `mapperAsync` of
   `ProcessGetAsync`, which runs after the caller is authorized, so a caller who may not read the settings learns
   nothing about a document that cannot be honored. The read passes the stored document's `JsonElement`, not the
   persisted settings, so the answer carries no `ETag` of the raw document, whose token is not the effective view's. The
   creation, the update, the report and the local-account requests authorize before they resolve.
7. **The problem names every document that the request needs, a parent scope's included.** A caller authorized for the
   request learns the scope, the owner's identifier and the vocabulary's error of the document that cannot be honored,
   also when it is the document of a parent scope that the caller may not read itself, such as the server's for a
   tenant's administrator or the tenant's for a client's owner. The effective settings that the same caller reads
   already carry the values that the parent documents set, and the vocabulary's errors name a setting and the rule it
   breaks, not its value, so the answer tells the caller which document an operator replaces and nothing that the
   effective view hides.

## Options considered

- **`500`.** Rejected: it tells the caller that the server failed, while this condition is known and a settings write
  repairs it.
- **`422`.** Rejected: the content of the request is fine, and the stored state is not.
- **Skipping the document as ADR-0241 does.** Rejected: a read, a client write or a password policy cannot do its work
  without the document, and a silent skip would answer with settings that the owner does not have.
- **A `try`/`catch` in each handler.** Rejected: the answers would drift, and a handler added later would forget it.

## Consequences

- An operator learns which document to repair from the answer, and the log carries the same names.
- A caller who may not make a request learns nothing about a document it needs; a caller who may learns the names and
  the error of every document the request needs, a parent scope's included (item 7).
- A plain `JsonException` of a proposed document stays a `500`.
- Resolution at runtime, in the protocol endpoints, keeps failing closed
  ([ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)); this answer concerns the management API only.
- **Breaking:** `IEffectiveSettingsResolver` gains `ResolveStored`, which a custom resolver implements, and it and
  `ResolveServerAsync(CancellationToken)` throw `UnreadableSettingsException` for a stored document.
- This implements issue [#301](https://github.com/NCodeGroup/Identity/issues/301).

## References

- [RFC 9110](https://www.rfc-editor.org/rfc/rfc9110#name-409-conflict) §15.5.10 — 409 Conflict.
- [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) — problem details for HTTP APIs.
- [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md) — configuration the server cannot honor fails loudly.
- [ADR-0204](0204-a-scopes-effective-settings-are-resolved-by-one-service.md) — the resolver.
- [ADR-0214](0214-a-management-write-that-loses-a-race-is-answered-412.md) — the endpoint filter on the management route
  group.
- [ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md) — the incoherent-client report.
- [ADR-0241](0241-a-settings-write-is-not-refused-for-a-stored-document-it-cannot-read.md) — a settings write skips such
  a document.
- Code: `NCode.Identity.OpenId.Settings.UnreadableSettingsException`,
  `NCode.Identity.OpenId.Settings.IEffectiveSettingsResolver`,
  `NCode.Identity.OpenId.Management.Endpoints.UnreadableSettingsEndpointFilter`,
  `NCode.Identity.OpenId.Management.Endpoints.ManagementRootGroup`.
