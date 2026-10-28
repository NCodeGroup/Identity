# Audit records

Every audited operation produces one audit record: a single JSON object with a fixed, versioned, snake_case envelope.
It is the reviewed contract that log pipelines, trace backends and incident runbooks are built against: adding or
renaming a field or an event changes this document in the same change.

## Overview

An operation that is audited (a token issued, a client that failed to authenticate, a configuration change) publishes an
audit event through the event seam ([ADR-0047](decisions/0047-auditing-and-events-observation-seam.md)). Which
operations are audited, and their outcomes, are the same whatever sink receives the event.

One record shape serves every sink:

- **The JSON record** is what `AuditRecordWriter` writes for an `IAuditEvent`. A sink that persists or forwards the
  record calls `AuditRecordWriter.Write`, which writes into a `Utf8JsonWriter`, or into any `IBufferWriter<byte>`, ref
  structs included, through a pooled staging buffer.
- **The log entry** carries the record as flattened `audit.*` key/value pairs, so a collector indexes the same fields.
- **The span event** on the current activity carries the same `audit.*` tags.
- **The metric attributes** carry the bounded fields only: `event_name`, `event_category` and `outcome`.

The field names are the same everywhere (`event_name` in the record, `audit.event_name` in the log state and the span
tags, `ncode.identity.audit.event_name` on the metric), so an operator pivots between a log entry, its trace and the
counters without translating names. `AuditRecordFields` declares the record's field names as constants.

A record carries identifiers and metadata only. It never carries a token, an authorization code, a credential or a query
string.

## Example

A record with every member set. Its `event_name`, `details` and values are illustrative and are not an event in the
[catalog](#event-catalog).

```json
{
  "schema_version": "1.0",
  "event_id": "01890a5d-ac96-774b-bcce-b302099a8057",
  "event_time": "2026-10-28T14:03:12.345Z",
  "event_name": "test.detailed",
  "event_category": "authentication",
  "event_source": "openid",
  "outcome": "failure",
  "severity": "warning",
  "tenant_id": "tenant-1",
  "subject_id": "subject-1",
  "client_id": "client-1",
  "actor": {
    "type": "client",
    "id": "client-1"
  },
  "request": {
    "request_id": "req-1",
    "trace_id": "4bf92f3577b34da6a3ce929d0e0e4736",
    "span_id": "00f067aa0ba902b7",
    "source_ip": "203.0.113.5",
    "user_agent": "curl/8.0",
    "http_method": "POST",
    "path": "/oauth2/token",
    "endpoint": "token_endpoint"
  },
  "resources": [
    {
      "type": "session",
      "id": "sid-1"
    }
  ],
  "error": {
    "code": "invalid_client",
    "message": "bad secret",
    "status_code": 401
  },
  "details": {
    "token_type": "access_token"
  }
}
```

## Fields

A member that has no value is omitted, never written as `null`, and so is an empty `resources` array. The writer emits
the members in the order below.

| Field                 | Type              | Presence                                        | Meaning                                                                                                                                                                          |
| --------------------- | ----------------- | ----------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `schema_version`      | string            | Always                                          | The version of the record's schema, `1.0` (`AuditRecordWriter.SchemaVersion`). A field added to the record bumps the minor version.                                              |
| `event_id`            | string (UUID)     | Always                                          | The unique identifier of the event. A UUIDv7, so identifiers sort by time and index and paginate well.                                                                           |
| `event_time`          | string (RFC 3339) | Always                                          | When the audited operation occurred, in UTC with a `Z` suffix.                                                                                                                   |
| `event_name`          | string            | Always                                          | The audited operation, such as `token.issued`. See the [event catalog](#event-catalog).                                                                                          |
| `event_category`      | string            | Always                                          | The kind of operation: one of the [categories](#event-categories). It is declared per event type, because `client.authentication` and `client.created` share a prefix.           |
| `event_source`        | string            | Always                                          | The plane that produced the event: one of the [sources](#event-sources).                                                                                                         |
| `outcome`             | string            | Always                                          | How the operation ended: one of the [outcomes](#outcomes).                                                                                                                       |
| `severity`            | string            | Always                                          | How much attention the event warrants: one of the [severities](#severities).                                                                                                     |
| `tenant_id`           | string            | When the operation ran under a tenant           | The tenant the operation ran under.                                                                                                                                              |
| `subject_id`          | string            | When the operation concerns a subject           | The end user the operation concerns. On a failed authentication it is the attempted identifier when one is known; a failed password grant records the user name the client sent. |
| `client_id`           | string            | When the operation concerns a client            | The client the operation concerns.                                                                                                                                               |
| `actor`               | object            | Always                                          | Who performed the operation. See [actor types](#actor-types).                                                                                                                    |
| `actor.type`          | string            | Always                                          | The kind of caller: one of the actor types.                                                                                                                                      |
| `actor.id`            | string            | When the actor has an identifier                | The caller's identifier within its type: a subject id, a client id or an administrator's subject id.                                                                             |
| `request`             | object            | When the request context was captured           | The HTTP request that caused the operation. Every event the library publishes carries it.                                                                                        |
| `request.request_id`  | string            | When known                                      | The HTTP request identifier (`HttpContext.TraceIdentifier`), which pivots to the host's request logs.                                                                            |
| `request.trace_id`    | string            | When the current activity uses W3C identifiers  | The 32-character W3C trace identifier, which pivots to traces.                                                                                                                   |
| `request.span_id`     | string            | When the current activity uses W3C identifiers  | The 16-character W3C identifier of the span that was current when the event was published.                                                                                       |
| `request.source_ip`   | string            | When known                                      | The caller's IP address (`Connection.RemoteIpAddress`), already corrected by the host for any trusted forwarding proxy.                                                          |
| `request.user_agent`  | string            | When the request sent one                       | The `User-Agent` header, cut at 512 characters.                                                                                                                                  |
| `request.http_method` | string            | When known                                      | The HTTP method.                                                                                                                                                                 |
| `request.path`        | string            | When known                                      | `PathBase` plus `Path`. It never includes the query string, which can carry codes or tokens.                                                                                     |
| `request.endpoint`    | string            | When the endpoint is known                      | The name of the endpoint that handled the request, such as `token_endpoint`, or its display name when it has none.                                                               |
| `resources`           | array of objects  | When the operation acted on at least one entity | Every entity the operation acted on, the target first and then the entities that own it. Each item has a `type` and, when known, an `id`.                                        |
| `resources[].type`    | string            | Always, within an item                          | The resource type: one of the [resource types](#resource-types).                                                                                                                 |
| `resources[].id`      | string            | When known                                      | The resource's identifier.                                                                                                                                                       |
| `error`               | object            | On failure and denial events                    | Why the operation did not succeed.                                                                                                                                               |
| `error.code`          | string            | Always, within `error`                          | A stable, machine-readable code to filter and pivot on: an OAuth error code where one applies.                                                                                   |
| `error.message`       | string            | When there is text                              | Human-readable text. It never contains a secret.                                                                                                                                 |
| `error.status_code`   | integer           | When an HTTP response was returned              | The HTTP status code returned to the caller.                                                                                                                                     |
| `details`             | object            | On events that declare event-specific fields    | The fields specific to the event, written by the event itself (`ISupportAuditDetails`). The catalog lists them per event.                                                        |

## Vocabularies

Every vocabulary is an open set of lower-case string names, never an enumeration
([ADR-0150](decisions/0150-vocabularies-are-open-unless-provably-closed.md)): a package that adds an event adds its own
names without editing this library. The tables list the names the library defines.

### Outcomes

| `outcome` | Meaning                                                                                                 |
| --------- | ------------------------------------------------------------------------------------------------------- |
| `success` | The operation completed.                                                                                |
| `failure` | The operation failed.                                                                                   |
| `denied`  | The operation was refused, as opposed to an unexpected failure, such as a denied authorization request. |

### Severities

The severity defaults from the outcome: `warning` for `failure`, `info` otherwise. An event type overrides it when its
meaning is stronger. The log entry's level follows the severity.

| `severity` | Log level   | Meaning                                                              |
| ---------- | ----------- | -------------------------------------------------------------------- |
| `info`     | Information | A routine operation that needs no attention.                         |
| `warning`  | Warning     | An operation that failed and may warrant review.                     |
| `critical` | Critical    | A likely security incident. Only `token.refresh.replay` is critical. |

A severity the library does not define logs at Information.

### Event categories

| `event_category` | Covers                                                    |
| ---------------- | --------------------------------------------------------- |
| `authentication` | The authentication of a client or a subject.              |
| `authorization`  | The authorization of a request and the consent behind it. |
| `token`          | The issuance, refresh and revocation of tokens.           |
| `session`        | The lifecycle of a user session.                          |
| `protocol`       | Protocol-level errors returned to a caller.               |
| `management`     | Changes made through the management plane.                |

### Event sources

| `event_source` | Plane                                                           |
| -------------- | --------------------------------------------------------------- |
| `openid`       | The OpenID protocol endpoints (authorization, token and so on). |
| `management`   | The management API.                                             |

### Actor types

The recorder states the actor explicitly for each event, so the actor does not depend on how a request authenticated.

| `actor.type`    | `actor.id`                                 | Used by                                                                                                                                                    |
| --------------- | ------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `end_user`      | The subject id                             | `authorization.granted`, `authorization.denied`, `session.created`, and `subject.authentication` on success. With no subject these events use `anonymous`. |
| `client`        | The client id                              | `token.issued`, `token.revoked`, `token.refresh.replay`, `token.refresh.retry` and `client.authentication`.                                                |
| `administrator` | The administrator's subject id, when known | Every management event. The subject id is read with the server-default claim types, not the request tenant's setting.                                      |
| `system`        | None                                       | `session.terminated`; its `details.termination_reason` says why. No caller acted.                                                                          |
| `anonymous`     | None                                       | `subject.authentication` on failure and `openid.error`: the caller is not identified.                                                                      |

### Resource types

`session` is shared by the protocol and the management planes (`AuditResourceTypes.Session`). The management types are
`ManagementResourceTypes`, and the local-account type is `AccountManagementResourceTypes`.

| `resources[].type`       | Entity                                                              |
| ------------------------ | ------------------------------------------------------------------- |
| `session`                | A user session, identified by its `sid`.                            |
| `client`                 | An OpenID client.                                                   |
| `client.secret`          | A secret belonging to a client.                                     |
| `tenant`                 | An OpenID tenant.                                                   |
| `tenant.secret`          | A secret belonging to a tenant.                                     |
| `server`                 | An OpenID server.                                                   |
| `server.secret`          | A secret belonging to a server.                                     |
| `resource_server`        | A resource server (an API, in Auth0 terms).                         |
| `resource_server.scope`  | A scope belonging to a resource server.                             |
| `resource_server.secret` | A secret belonging to a resource server.                            |
| `initial_access_token`   | An initial access token (RFC 7591) for dynamic client registration. |
| `principal`              | An end user's federated principal.                                  |
| `local_account`          | A local account.                                                    |

## Event catalog

One row for each event the library publishes. In a cell, `none` means the member is absent from the record.

### Protocol plane

| `event_name`             | Category         | Source   | Outcomes             | Severity                                | Actor                                         | Resources             | `error.code`                                                                                                                                 | `details`               |
| ------------------------ | ---------------- | -------- | -------------------- | --------------------------------------- | --------------------------------------------- | --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------- |
| `token.issued`           | `token`          | `openid` | `success`            | `info`                                  | `client`                                      | none                  | none                                                                                                                                         | `token_type`            |
| `token.revoked`          | `token`          | `openid` | `success`            | `info`                                  | `client`                                      | none                  | none                                                                                                                                         | none                    |
| `token.refresh.replay`   | `token`          | `openid` | `denied`             | `critical`                              | `client`                                      | none                  | none                                                                                                                                         | none                    |
| `token.refresh.retry`    | `token`          | `openid` | `success`            | `info`                                  | `client`                                      | none                  | none                                                                                                                                         | none                    |
| `authorization.granted`  | `authorization`  | `openid` | `success`            | `info`                                  | `end_user`, or `anonymous` with no subject    | none                  | none                                                                                                                                         | none                    |
| `authorization.denied`   | `authorization`  | `openid` | `denied`             | `info`                                  | `end_user`, or `anonymous` with no subject    | none                  | The error code; `access_denied` by default                                                                                                   | none                    |
| `client.authentication`  | `authentication` | `openid` | `failure`            | `warning`                               | `client`                                      | none                  | The error code; `invalid_client` by default                                                                                                  | `authentication_method` |
| `subject.authentication` | `authentication` | `openid` | `success`, `failure` | `info` on success, `warning` on failure | `end_user` on success, `anonymous` on failure | none                  | Failure only: `authentication_failed`, the bearer token error's code, `subject_id_missing`, `invalid_credentials` or `attempt_limit_reached` | none                    |
| `openid.error`           | `protocol`       | `openid` | `failure`, `denied`  | `warning` on failure, `info` on denied  | `anonymous`                                   | none                  | The OAuth error code; `message` is its description and `status_code` the HTTP status                                                         | none                    |
| `session.created`        | `session`        | `openid` | `success`            | `info`                                  | `end_user`                                    | `session`             | none                                                                                                                                         | none                    |
| `session.terminated`     | `session`        | `openid` | `success`            | `info`                                  | `system`                                      | `session`, when known | none                                                                                                                                         | `termination_reason`    |

Notes on the protocol events:

- `token.refresh.replay` records a refresh token presented after its grant was revoked, a token-theft signal. A rotated
  token presented again within its reuse interval is answered as a retry and recorded as `token.refresh.retry`.
- `authorization.denied` and `client.authentication` use the error code the operation produced and fall back to the
  default shown. `details.authentication_method` is the registered client-authentication method the client attempted,
  such as `private_key_jwt`; no credential is written.
- `openid.error` is published once for each request that returns an OAuth or OpenID Connect error response. Its outcome
  is `denied` when the code is `access_denied` and `failure` otherwise.
- `subject.authentication` failure codes: `authentication_failed` (the session cookie could not be authenticated), the
  code of the bearer token error (such as `invalid_token`), `subject_id_missing` (the authenticated principal has no
  subject id), `invalid_credentials` (the password grant refused the credentials) and `attempt_limit_reached` (the
  account reached its limit of failed password attempts). The `error.message` carries the prose that describes the
  failure. A request that presents no credentials is not audited.
- `session.terminated` has no `session` resource for a subject-wide termination. `details.termination_reason` is a
  session termination reason or an extension's own name.
- `token.issued`'s `details.token_type` is the issued token's type, such as `access_token` or `id_token`; the token value
  is never written.

### Management plane

Every management event has `event_source` `management`, `event_category` `management`, the `administrator` actor, the
outcome `success` and the severity `info`. The `actor.id` is the administrator's subject id.

| `event_name`                    | Resources                                            | `error.code` | `details`                                         |
| ------------------------------- | ---------------------------------------------------- | ------------ | ------------------------------------------------- |
| `grant.revoked`                 | none                                                 | none         | `grant_type`, when known                          |
| `grant.revoked.bulk`            | `session`, when the revocation was scoped to one     | none         | `revoked_count`                                   |
| `session.revoked.admin`         | `session`, when one session was revoked              | none         | `revoked_count`                                   |
| `<resource_type>.<change_type>` | The changed resource, then the resource that owns it | none         | `change_type`, and `resource_values` when present |

On a revocation, `subject_id` and `client_id` carry the subject and client it applies to. `session.revoked.admin`
attributes the administrator's action; the per-session `session.terminated` events are raised independently by the
session-termination seam.

#### Resource-change events

A resource-change event is named `<resource_type>.<change_type>`, where `change_type` is `created`, `updated`, `deleted`
or `settings.updated`. `details.change_type` repeats the change type, and `details.resource_values` is a non-sensitive
snapshot of the resource, never secret material.

| Event name prefix (`resource_type`) | Owning resource listed second |
| ----------------------------------- | ----------------------------- |
| `client`                            | none                          |
| `client.secret`                     | `client`                      |
| `tenant`                            | none                          |
| `tenant.secret`                     | `tenant`                      |
| `server`                            | none                          |
| `server.secret`                     | `server`                      |
| `resource_server`                   | none                          |
| `resource_server.scope`             | `resource_server`             |
| `resource_server.secret`            | `resource_server`             |
| `initial_access_token`              | none                          |
| `principal`                         | none                          |
| `local_account`                     | none                          |

For example, `client.secret.created` lists the secret first and its client second. An initial access token is recorded as
`initial_access_token.created`; the record never carries the token itself.

## Where records go

### Logs

`DefaultAuditLoggingHandler` writes one log entry for each audit event to the category `NCode.Identity.Audit`, with the
event id `14005` (`AuditRecorded`). The message is `Audit '<event_name>' <outcome>.`, and its level follows the
severity (`info` is Information, `warning` is Warning, `critical` is Critical). A host routes the audit trail on its own
by the category. Because the level follows the severity, a category filter at `Warning` keeps failures and critical
events and drops routine successes.

The entry's state is the record flattened into `audit.<json path>` key/value pairs, so the host's JSON console formatter
or an OpenTelemetry log exporter emits indexable fields:

| Record member                   | State key                                                        | Value                                                                    |
| ------------------------------- | ---------------------------------------------------------------- | ------------------------------------------------------------------------ |
| A top-level scalar              | `audit.event_name`, `audit.outcome`, `audit.tenant_id`           | The member's value.                                                      |
| A member of an object           | `audit.actor.type`, `audit.request.trace_id`, `audit.error.code` | The object is flattened with dots.                                       |
| `resources`                     | `audit.resources`                                                | The array as raw JSON text, such as `[{"type":"session","id":"sid-1"}]`. |
| A scalar in `details`           | `audit.details.token_type`                                       | The details object is flattened one level.                               |
| An object or array in `details` | `audit.details.resource_values`                                  | The value as raw JSON text.                                              |
| A JSON number                   | `audit.error.status_code`, `audit.details.revoked_count`         | A number, not a string.                                                  |

Arrays and nested `details` values stay raw JSON text so the number of index fields an event can create is bounded. A
member that is `null` is omitted. The state also carries the original format
`Audit '{audit.event_name}' {audit.outcome}.`.

### Traces

When there is a current activity, `DefaultEventTracingHandler` adds a span event named after the `event_name`. Its tags
are `event.type` (the CLR type name of the event) and every `audit.*` key the log entry carries. Without a current
activity nothing is recorded.

### Metrics

The counter `ncode.identity.events.published` is tagged with `ncode.identity.audit.event_name`,
`ncode.identity.audit.event_category` and `ncode.identity.audit.outcome`. Only these bounded fields are metric
attributes; a tenant, client, subject or network address never is. See [metrics.md](metrics.md).

### Custom sinks

An audit sink is an `IEventHandler<IAuditEvent>`: register it with `AddEventHandler<IAuditEvent, THandler>()` on the
events library builder, and call `AuditRecordWriter.Write` (into a `Utf8JsonWriter`, or into any
`IBufferWriter<byte>`, ref structs included, through a pooled staging buffer) for the record. The request context is
captured when the event is published, so a sink that runs in the background after the request has completed still sees
it. A handler that throws neither fails the audited operation nor stops the other handlers
([ADR-0047](decisions/0047-auditing-and-events-observation-seam.md)).

## Indexing

A collector or a store that holds audit records indexes the fields an operator filters, groups and joins on. The names
below are the record's; the log entry carries each one with the `audit.` prefix (`audit.event_name`,
`audit.request.trace_id`).

- **Keyword** (exact match, filter and group by): `event_id`, `event_name`, `event_category`, `event_source`, `outcome`,
  `severity`, `tenant_id`, `subject_id`, `client_id`, `actor.type`, `actor.id`, `request.trace_id`,
  `request.request_id`, `request.source_ip` and `error.code`. None is free text, and every one is a bounded or
  identifier-like value.
- **Date**: `event_time`, so a range filter or a sort follows when the operation occurred, not when the entry was
  collected.
- **Nested array**: `resources`, as a nested array of `{type, id}` items with `type` and `id` as keywords, so a query
  that names a type and an id matches both on the same item. The log entry carries `audit.resources` as raw JSON text,
  so a log collector parses it before it indexes the items.
- **Not indexed**: `details`. Its members differ for each event and a package that adds an event adds its own, so leave
  the object unindexed or turn dynamic mapping off for it. A query that needs one member, such as
  `audit.details.termination_reason`, names that member in the mapping.

## Lookups

How a sink names the log entry's fields depends on the host's log formatter. With the .NET JSON console formatter the
state appears under `State`, so `audit.event_name` is `State.audit.event_name` in Elasticsearch, OpenSearch and
CloudWatch Logs, and `State_audit_event_name` after Loki's `| json` stage flattens it. The examples assume the JSON
console formatter; the log category is its `Category` member.

### Elasticsearch and OpenSearch (KQL)

Failed authentications:

```text
Category : "NCode.Identity.Audit" and State.audit.event_category : "authentication" and State.audit.outcome : "failure"
```

### Grafana Loki (LogQL)

Critical audit events:

```text
{app="identity"} | json | Category="NCode.Identity.Audit" | State_audit_severity="critical"
```

### CloudWatch Logs Insights

Every audit event of one request, oldest first:

```text
filter Category = "NCode.Identity.Audit" and State.audit.request.trace_id = "<trace id>" | sort @timestamp asc
```

### PostgreSQL (jsonb)

The library ships no audit store yet. A host that stores the `AuditRecordWriter` JSON in its own table, with the record
in a `record jsonb` column, queries the record's own member names, with no `audit.` prefix. A GIN `jsonb_path_ops` index
serves the containment (`@>`) test on `resources`:

```sql
CREATE INDEX audit_records_record_idx ON audit_records USING GIN (record jsonb_path_ops);
```

Every audit event of one session within a tenant, oldest first:

```sql
SELECT record
FROM audit_records
WHERE record->>'tenant_id' = $1
  AND record->'resources' @> '[{"type":"session","id":"<sid>"}]'
ORDER BY (record->>'event_time')::timestamptz;
```

## Incident runbook

Each entry names when to use it and gives one query. The queries are KQL over the log entries and filter on the category
`NCode.Identity.Audit` as the [lookups](#lookups) do, except the last, which needs the record. Group the results with
the tool's aggregation, and read the fields named for each entry from the matching entries.

### Follow one request

Use it to see every audited step of one request, from a trace id taken from a trace, a log line or the caller's
`traceparent` header. The `audit.request.request_id` on a result pivots to the host's request logs.

```text
Category : "NCode.Identity.Audit" and State.audit.request.trace_id : "<trace id>"
```

### Refresh-token replay

Use it when a critical event fires. `token.refresh.replay` is the only critical event: a refresh token was presented
after its grant was revoked, a token-theft signal. Read `audit.client_id` and `audit.subject_id` from the entry, then
revoke the subject's sessions through the management API; a `session.revoked.admin` event appears when that is done.

```text
Category : "NCode.Identity.Audit" and State.audit.severity : "critical" and State.audit.event_name : "token.refresh.replay"
```

### Credential stuffing

Use it to find a source that is trying many credentials. Group the failures by `audit.request.source_ip` and
`audit.error.code`; a source with many `invalid_credentials` or `attempt_limit_reached` failures across different
`audit.subject_id` values is guessing credentials.

```text
Category : "NCode.Identity.Audit" and State.audit.event_name : "subject.authentication" and State.audit.outcome : "failure"
```

### A misbehaving client

Use it to find a client that keeps failing to authenticate, such as one with a rotated or expired secret. Group by
`audit.client_id` and `audit.error.code`; `audit.details.authentication_method` says which method the client attempted.

```text
Category : "NCode.Identity.Audit" and State.audit.event_name : "client.authentication"
```

### Who changed configuration

Use it to attribute a configuration change. Read `audit.actor.id` (the administrator), `audit.event_name` (such as
`client.secret.created`) and `audit.resources` (the changed resource, then the resource that owns it).

```text
Category : "NCode.Identity.Audit" and State.audit.event_category : "management"
```

### Everything about a session

Use it to reconstruct a session from its `sid`: its creation, its termination and any administrator revocation. A
session is identified by a `resources` item whose `type` is `session`. In a store that maps `resources` as a nested
array, KQL matches the pair on one item:

```text
resources : { type : "session" and id : "<sid>" }
```

The log entry carries `audit.resources` as raw JSON text, so look a session up in a store that holds the record. The
PostgreSQL containment query in the [lookups](#postgresql-jsonb) does the same:

```sql
SELECT record
FROM audit_records
WHERE record->'resources' @> '[{"type":"session","id":"<sid>"}]'
ORDER BY (record->>'event_time')::timestamptz;
```
