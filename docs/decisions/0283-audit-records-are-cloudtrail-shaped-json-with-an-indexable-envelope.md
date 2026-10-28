# 283. Audit records are CloudTrail-shaped JSON with an indexable envelope

- **Status:** Accepted
- **Date:** 2026-10-28
- **Deciders:** NCode Group

## Context

[ADR-0047](0047-auditing-and-events-observation-seam.md) gives every audit event a common envelope: a unique event id,
a UTC timestamp, the tenant, the actor, the client, an action, an outcome and a schema version. An envelope of flat,
ad-hoc members gives a sink that wants the event as data no single shape to write: each sink picks members and
invents names.

An audit log entry rendered from a text template reaches a collector as one sentence. The collector cannot filter on
the subject, the client, the outcome or the trace without parsing prose, and the keys it would index are whatever the
template placeholders happen to be. If the same event reaches a span and a counter under yet other names, an operator
cannot pivot from a log entry to its trace or its metric.

An incident investigation needs more than such an envelope carries. It needs the request context: a trace id in the
W3C form, the source address, the User-Agent and the endpoint. It needs a failure that is a machine-readable code, not
free text, so that "every failed client authentication for one error" has a field to group on. It needs the entities an
event acted on, and the caller, stated in the record rather than implied by the event type.

Operators of identity providers already know two record shapes. AWS CloudTrail describes an event as the identity of the
caller, a request context, the resources acted on, an error and event-specific request parameters. Auth0's log events
use OAuth-vocabulary snake_case names and a stable event type code. An audit record that follows both is familiar to
those operators and to the tools built around them ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).

## Decision

**An audit event is written as one JSON record with a fixed, versioned, snake_case, CloudTrail-modelled envelope. One
writer, `AuditRecordWriter`, produces it, and the log entry, the span event and the metric attributes all use the
record's field names.** The writer writes into a `Utf8JsonWriter` or a caller-supplied `IBufferWriter<byte>` and never
allocates a `byte[]` ([ADR-0116](0116-memory-allocations-and-buffer-operations.md)).

1. **Ingestion is unchanged.** `IEventPublisher`, hierarchical dispatch, fault isolation, background delivery, the
   recorder facades and every publishing site's choice of event and outcome are as
   [ADR-0047](0047-auditing-and-events-observation-seam.md) and
   [ADR-0049](0049-openid-errors-audited-from-single-funnel.md) describe. Only the record's shape and its log, trace and
   metric projections are decided here.
2. **The record is CloudTrail-modelled and snake_case.** Field names follow the OAuth and OpenID Connect wire
   vocabulary and Auth0 logs; the structure follows CloudTrail: the identity of the caller, a request context, the
   resources acted on, an error, and event-specific details. C# member names mirror the JSON (`EventName` is
   `event_name`, `EventTime` is `event_time`), and the constants that name the protocol's audit events are
   `OpenIdAuditEventNames`. `AuditRecordFields` declares every field name once.
3. **The primary lookup keys are flat; the context is grouped.** `tenant_id`, `subject_id` and `client_id` are
   top-level scalars. `actor`, `request` and `error` are objects. `resources` is an array of `{type, id}`. `details`
   holds the fields specific to the event. A member without a value is omitted, and so is an empty `resources`.
4. **`schema_version` is one string for the whole record, `"1.0"`** (`AuditRecordWriter.SchemaVersion`). A field added
   to the record bumps the minor version. An event type carries no schema version of its own.
5. **`event_id` is a UUIDv7** (`Guid.CreateVersion7`), so identifiers are time-ordered and index and paginate well.
   `event_time` is the UTC time of the operation in RFC 3339 form with a `Z` suffix.
6. **The record carries no correlation id.** `request.trace_id` and `request.span_id` are the W3C identifiers of the
   current `Activity`, and `request.request_id` is `HttpContext.TraceIdentifier`, so a record pivots straight to traces
   and to the host's HTTP logs.
7. **`request` captures a fixed set of members.** It holds the request id, the trace id, the span id, the source
   address (`Connection.RemoteIpAddress`, which the host has already corrected for a trusted forwarding proxy), the
   User-Agent cut at 512 characters, the HTTP method, `PathBase` plus `Path`, and the endpoint name. It never holds the
   query string, which can carry codes or tokens. One public factory, `AuditRequestFactory.Create(HttpContext)`, builds
   it for both recorders and reads the endpoint from the request's endpoint metadata: its name, else its display name.
   It lives in `NCode.Identity.OpenId.Authentication.Abstractions`, so `NCode.Identity.Events.Abstractions` stays free
   of HTTP types. The request context is captured when the event is published, so a sink that runs in the background
   after the request has completed still sees it.
8. **The actor is stated explicitly for each recorder method.** The actor types are an open vocabulary
   (`AuditActorTypes`): `end_user`, `client`, `administrator`, `system` and `anonymous`. Token endpoint and revocation
   events are performed by a `client`. Authorization, successful subject authentication and session creation are
   performed by an `end_user`, or by `anonymous` when there is no subject. A failed subject authentication and
   `openid.error` are `anonymous`. `session.terminated` is `system`, and its `details.termination_reason` says why.
   Every management event is an `administrator`.
9. **Category, source and severity are open vocabularies.** `event_category` (`authentication`, `authorization`,
   `token`, `session`, `protocol`, `management`) is declared for each event type, because `client.authentication` and
   `client.created` share a name prefix and a prefix cannot tell them apart. `event_source` is the plane that produced
   the event (`openid`, `management`), stamped by the recorder. `severity` (`info`, `warning`, `critical`) defaults from
   the outcome (`failure` is `warning`, anything else is `info`), and `token.refresh.replay` overrides it to `critical`.
   The log level follows the severity: `info` is Information, `warning` is Warning and `critical` is Critical.
10. **Errors are machine-readable.** `error.code` is required on an `AuditError`, and the event has no free-text
    reason. `authorization.denied` uses its error code, `access_denied` by default. `client.authentication` uses its
    code-like reason, `invalid_client` by default. `openid.error` maps the error's code, description and HTTP status to
    `error.code`, `error.message` and `error.status_code`. A failed `subject.authentication` carries
    `authentication_failed`, the bearer error's code, `subject_id_missing`, `invalid_credentials` or
    `attempt_limit_reached`, with the prose as `error.message`.
11. **Every entity an event acts on is a resource.** `Resources` is computed from the event's typed properties, so a
    recorder does not set it. A resource-change event lists its target first and then the entity that owns it: a secret
    lists its client, tenant, server or resource server, and a scope lists its resource server. A session is the shared
    resource type `AuditResourceTypes.Session` (`"session"`) in `NCode.Identity.Events.Abstractions`, because both the
    protocol and the management planes reference sessions.
12. **An event writes its own details.** An event that has event-specific fields implements
    `ISupportAuditDetails.WriteAuditDetails(Utf8JsonWriter)`, which mirrors `ISupportMetricAttributes`. There is no
    object bag ([ADR-0047](0047-auditing-and-events-observation-seam.md)), the writer is trimming- and AOT-safe, and a
    package that adds an event writes its own details. A resource-change event writes `details.change_type` and
    `details.resource_values`.
13. **The log entry carries the record as flattened attributes.** `DefaultAuditLoggingHandler` writes under the stable
    category `NCode.Identity.Audit`, with one event id, `14005` (`AuditRecorded`), and a state of `audit.<json path>`
    key/value pairs. Objects are flattened with dots, and numbers stay numbers. Arrays and nested `details` values are
    raw JSON text, which bounds the number of index fields an event can create. The host's JSON console formatter or an
    OpenTelemetry log exporter then emits indexable fields. The span event carries the same `audit.*` tags. The metric
    attributes are `ncode.identity.audit.event_name`, `ncode.identity.audit.event_category` and
    `ncode.identity.audit.outcome`, the bounded fields only
    ([ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md)). The event ids `14003`
    and `14004` are reserved and never reused.
14. **The audit handler calls `ILogger.Log` directly.** This is an exception to the `[LoggerMessage]`-only rule of the
    [production conventions](../../.github/instructions/csharp-production.instructions.md) (§11): a source-generated
    method has a fixed parameter list, and the `audit.*` keys of the state vary for each event. A `//` comment at the
    call cites this ADR.
15. **Typed event properties stay for in-process subscribers.** `TokenType`, `SessionId`, `ServerId`,
    `ResourceServerId`, `ResourceId`, `ChangeType`, `ResourceValues`, `TerminationReason`, `AuthenticationMethod`,
    `RevokedCount` and `GrantType` remain typed properties of their events. The error of `OpenIdErrorEvent` is its
    `Error`.
16. **The record is the shape a persisted store and a lookup API index.** The library fixes the record and the fields
    an operator indexes on; a store and a management lookup API build on them and are decided separately.

## Options considered

- **Keep the flat envelope.** It needs no change, but it gives a sink no shape to write, no request context and no
  machine-readable failure, and a text-template log entry cannot be indexed by field. Rejected.
- **Adopt OpenTelemetry or Elastic Common Schema names verbatim.** Their dotted, nested names are a good fit for a
  generic telemetry pipeline, but the operators of an identity provider expect the CloudTrail and Auth0 shape and the
  OAuth snake_case names, and those names match the protocol's own vocabulary. The dotted `audit.*` keys of the log
  state and the span tags still map onto OpenTelemetry attributes, so a pipeline that wants them carries them as they
  are. Rejected.
- **Log the record's JSON as one string.** A collector receives one opaque property, so it cannot index a field without
  parsing the string first, and a sink that wants individual fields must parse it again. Rejected: the record is not
  indexable.
- **A dynamic details bag** (`IDictionary<string, object>` or a `[JsonExtensionData]` member). It lets any value reach
  a persisted record, including a secret, and it invites magic-string keys instead of reviewed typed fields
  ([ADR-0047](0047-auditing-and-events-observation-seam.md)). Rejected in favor of
  `ISupportAuditDetails`, where each event writes only the fields it declares.
- **A closed polymorphic `JsonDerivedType` model of the record.** Every event type would be listed on the base type, so
  a package that adds an event would edit the library, against the rule that vocabularies are open
  ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)). Rejected.

## Consequences

- An operator filters, groups and pivots on one set of names in the record, the log entry, the span event and the
  metrics, and follows one request across them with `request.trace_id`.
- An event declares its name, category, source and actor, writes its own details and, on a failure, names an error
  code. The audit-records document lists each event in its event catalog, and adding or renaming an event is a reviewed
  change to that catalog.
- The change is breaking for public API and for log and metric names: `Action` is `EventName`, `Timestamp` is
  `EventTime`, `OpenIdAuditActions` is `OpenIdAuditEventNames`, and the metric attribute `ncode.identity.audit.action`
  is `ncode.identity.audit.event_name`. This is acceptable under the pre-release posture
  ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)).
- A refresh-token replay is logged at Critical, because its severity is `critical`.
- A record contains personal data: the source address and the subject id. How long records are kept, and who can read
  them, is the host's decision; the library writes the record and keeps none.
- A record never contains a secret, a token, an authorization code or a query string. `details` holds only the fields
  its event writes, and `resource_values` is a non-sensitive snapshot.
- The audit log state is not a `[LoggerMessage]` method, so the source generator does not check its keys. They come from
  `AuditRecordFields`, and the audit-records document lists them.
- The record is the persisted shape that an audit store and a lookup API index: `event_time`, `event_id`,
  `event_name`, `event_category`, `outcome`, `severity`, `tenant_id`, `subject_id`, `client_id`, `actor_id`,
  `trace_id` and `source_ip`, plus `resources`. `details` stays unindexed.
- **"You chose wrong" smells.** _Leak smell:_ a record has to carry a secret, a token or a query string to be useful;
  put an identifier or a hash on the event instead. _Index smell:_ an operator filters on a `details` member in every
  query; promote it to a typed envelope member with a minor schema bump, or index that one member by name.

## References

- `src/NCode.Identity.Events.Abstractions/Audit/AuditRecordWriter.cs`
- [Audit records](../audit-records.md) — the fields, vocabularies, event catalog, indexing and incident lookups
- [Metrics](../metrics.md) — the audit metric attributes
- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — the service, strategy-collection and mediator
  shapes; an audit sink stays an event handler
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — breaking changes are acceptable before release
- [ADR-0047](0047-auditing-and-events-observation-seam.md) — the event seam and the envelope this amends
- [ADR-0049](0049-openid-errors-audited-from-single-funnel.md) — the single funnel that records `openid.error`
- [ADR-0116](0116-memory-allocations-and-buffer-operations.md) — the writer uses buffer writers and allocates no
  `byte[]`
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — open vocabularies
- [ADR-0197](0197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) — bounded metric
  dimensions
- AWS CloudTrail record contents — the identity, request, resources and error structure
- Auth0 log event schema — the snake_case names and the stable event type
