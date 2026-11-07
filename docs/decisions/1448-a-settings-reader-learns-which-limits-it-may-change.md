# 1448. A settings reader learns which limits the settings carry and whether it may change them

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group
- **Refines:** [ADR-1418](1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md),
  [ADR-1257](1257-the-management-console-reaches-every-owners-secrets-and-settings-through-one-endpoint-per-operation.md)

## Context

A limit in a scope's settings document is written only by the authority above that scope
([ADR-1418](1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md)). The node's own
administrators read the whole document and may edit all of it, but a write that adds, changes or removes a limit is
refused with `403` unless the caller holds `Update` at the node above.

A console that edits the whole document offers limits the caller cannot change, and the caller learns so only from the
`403` after saving. The settings document resource cannot carry the answer: it is the audit snapshot of the document and
carries the document's ETag, while whether a caller may change a limit depends on the caller.

## Decision

**A settings reader asks a read-only sub-resource which limits a scope's settings may carry and whether it may change
them.**

1. **`GET …/settings/limits` answers `SettingLimitsResource { names, canChange }`.** The route exists on the platform,
   team, tenant, client and resource server, beside each one's settings document.
2. **`names` are the registered limits a document at that scope may carry.** They are listed in ordinal order from
   `ISettingSerializer.GetLimits`.
3. **`canChange` is whether the caller holds `Update` at the node above.** The node above is the platform for a
   top-level team, the parent team for a child team, the team for a tenant, and the tenant for a client or resource
   server. For the platform's own document it is the platform node itself.
4. **The route authorizes `Read` at the node.** It answers `404` for a missing resource and carries no ETag, because the
   answer is specific to the caller.
5. **The console shows a caller without `canChange` the document's limits read-only and keeps them out of its patch.**
   It edits the rest of the document, refuses editor text that names a limit, and saves the edited settings merged with
   the unchanged limit entries. A caller with `canChange` edits the whole document, and the console names the limits in
   a caption.

## Options considered

- **Fields on the settings document resource.** The document is the audit snapshot and carries its ETag, and the answer
  is specific to the caller. Rejected.
- **A setting catalog in the capabilities document, with authority computed in the browser.** It duplicates the
  authority chain, nested teams included. Rejected.
- **Hiding the limits from a caller who may not change them.** The reader needs the values that bound its scope.
  Rejected.

## Consequences

- The console shows a limit as read-only before the caller edits, rather than after a refused save.
- The authority chain stays in the server: the browser reads one boolean and never computes who is above a node.
- The settings document resource and its ETag are unchanged by the caller's authority, so the audit snapshot stays a
  pure representation of the document.
- Each answer is cached per owner and invalidated when a role assignment changes, since authority above can change.

## References

- [ADR-1418](1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md) — a limit is set by the
  authority above the scope whose document carries it
- [ADR-1257](1257-the-management-console-reaches-every-owners-secrets-and-settings-through-one-endpoint-per-operation.md)
  — the management console reaches every owner's secrets and settings through one endpoint per operation
- Issue #581
