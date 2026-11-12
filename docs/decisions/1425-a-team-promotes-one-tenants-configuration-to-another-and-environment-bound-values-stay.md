# 1425. A team promotes one tenant's configuration to another, and environment-bound values stay

- **Status:** Accepted
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A customer's environments are the tenants of one team
([ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md) decision 1,
[ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md)). The configuration of
one environment, its settings, resource servers, clients and client grants, is the configuration of the next, and an
operator re-enters it in each tenant by hand.

Some of it belongs to one environment. A credential serves the environment that issued it, and secret material is
revealed once, at creation
([ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md)). A redirect URI, a logout URI, a key set address or a
mutual-TLS subject names the application of one environment, and a profile of another environment can refuse it.

A limit in a document is set by the authority above the scope whose document carries it
([ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md)), and a promoted
document is a settings write to the target. A client a host registers is changed by no management caller
([ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md)), and `IHostManagedClientPolicy`
names it.

## Decision

**A team promotes one tenant's configuration into another of its tenants, previewed and then applied. Credentials,
keys, principals and environment-bound values stay with their environment.**

1. **Routes and answers.** `POST /api/teams/{teamId}/promotions/preview` and `POST /api/teams/{teamId}/promotions`
   take `PromoteTenantRequest { sourceTenantId, targetTenantId, environmentSettings[] }`, between two distinct tenants
   of the team, in either direction: environment types have no order, because the vocabulary is open. Preview answers
   `200` with the report and writes nothing. Promote answers `200` with the applied report, or `409` with the report's
   errors and writes nothing.
2. **What is copied, and what is never copied.** The tenant's settings document, every non-system resource server (its
   name, `isDisabled`, settings and non-system scopes), every client no `IHostManagedClientPolicy` names in the source
   tenant (`isDisabled`, settings and the resource server it acts for), and every client grant of a promoted client are
   copied. The secrets and keys of any owner (each environment holds its own credentials, ADR-1050), master and root
   keys, user grants, consents, sessions, principals, local accounts, role assignments and ownership (`Owner`,
   `TeamAdmin`, `TenantAdmin`), usage, audit records, the tenant's metadata (issuer, mTLS alias, display name,
   `isDisabled`), system resource servers and system scopes, and host-managed clients with their grants are never
   copied.
3. **Host-managed clients.** A source client that `IHostManagedClientPolicy` names for the source tenant is left out
   with a `host_managed` note, and so are its grants. A target client the policy names for the target tenant is never
   written: a source client with its id is an error, as every management change to it answers `409`.
4. **Environment-bound settings never travel.** `SettingDescriptor.IsEnvironmentBound`, `false` by default, marks a
   setting whose value belongs to the environment its owner serves: `environment_type`, `environment_name`,
   `deletion_protection`, `redirect_uris`, `post_logout_redirect_uris`, `backchannel_logout_uri`,
   `frontchannel_logout_uri`, `jwks_uri`, `sector_identifier_uri`, `client_notification_endpoint`, the
   `tls_client_auth_*` subject, SAN and trust-anchor settings, and `pairwise_salt`. A promoted document holds the
   source's other settings, then the target's own environment-bound values, then any the request supplies in
   `environmentSettings` for that resource, which may carry only environment-bound settings. A package marks its own
   descriptors, and nothing lists the names in code
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)).
5. **The limit rule.** A limit is not environment-bound: it travels, under
   [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md). A promoted document
   that updates an existing target document and changes a limit, as
   `ISettingSerializer.GetChangedLimits(target document, promoted document)` names, needs `Operations.Update` at the
   node above: the target's team node for the tenant's document, the target tenant node for a client's or a resource
   server's. A created client or resource server needs no check, because its creator already holds that authority.
   Contributors record each document's changed limits and the node above on the context while planning. After
   planning, and before the handler answers or writes, it refuses with the `403` a settings `PATCH` answers, naming
   the limits, for preview and promote alike.
6. **Identifiers and refusals.** A promoted client keeps its `clientId` and is matched by it. A resource server is
   matched by its audience `identifier`, a created one keeps the source `resourceServerId`, and the promotion maps
   source to target resource-server ids for clients and grants. A client grant is matched by client and mapped
   resource server. Promotion creates and updates, and never deletes, scopes included. An error per item refuses: a
   system target resource server of that identifier; a target resource server holding the source id under another
   identifier; a target client bound to another resource server; a target client a policy names as host-managed; a
   promoted client document naming a `jwks_uri` while the target client holds public keys; a client or grant whose
   resource server is neither promoted nor a target system one of that identifier; a grant to a system resource server
   naming a scope it lacks; a document the serializer rejects; a promoted client the coherence rules refuse under the
   promoted tenant settings; a target client those settings make incoherent; an `environmentSettings` entry with a
   non-environment-bound setting, no promoted resource, or repeated.
7. **Notes, not errors.** `credential_required` is noted for a promoted client whose `token_endpoint_auth_method` is
   not `none` and whose target holds no secret. `environment_setting_unset` is noted for each environment-bound setting
   the source sets and the promoted document lacks. `host_managed` is noted for each source client left out. The
   operator supplies the missing values in `environmentSettings` and previews again.
8. **Authority.** The caller needs `Read` at the source tenant node and `Create` at the target tenant node, checked
   before either tenant is read
   ([ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md)). Creating
   clients and resource servers is creation authority: `TeamAdmin` at the team, the target's `TenantAdmin`, or
   `GlobalAdmin`. Then both tenants must exist and belong to `{teamId}`, else `404`, and then the limit rule applies.
   Then a promotion that changes an existing target client whose principal holds role assignments needs `Create` at
   each node those assignments are held at, the authority any change to that client needs
   ([ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md)); without it, preview and
   promote answer `403` and nothing is written.
9. **Contributors.** The public strategy collection `ITenantPromotionContributor` (`Order`, `PlanAsync`, `ApplyAsync`)
   over `TenantPromotionContext` is open by registration
   ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md),
   [ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md)). The built-in contributors are the
   tenant settings at 100, resource servers at 200, clients at 300 and client grants at 400. The internal
   `DefaultTenantPromoter` plans every contributor, checks the `environmentSettings` entries, then applies one
   contributor at a time, saving and recording its audit events after each.
10. **Atomicity.** Every check runs in the plan before any write, so an apply fails only on a concurrent write (`412`)
    or a fault. A promotion converges: running it again completes one that stopped part way.
11. **Audit.** Each write records the resource-change event its own endpoint records (`tenant.settings.updated`,
    `resource_server.created` or `resource_server.updated`, `client.created` or `client.updated`; the endpoint of a
    client grant records none either), then one `tenant.promoted` event (`ResourceChangeTypes.Promoted = "promoted"`)
    on the target with the report as `resource_values`. The target's tenant change is announced with
    `TenantChangedCommand(target, settingsChanged: true)`.
12. **Console.** The team's tenants page has a "Promote" button that opens a dialog: pick the source and the target,
    preview, fill the environment-bound values the preview asks for, and promote.

## Options considered

- **Copying or generating secrets.** One credential would serve two environments, and a bulk response would carry
  secret material the platform reveals once. Rejected.
- **New client ids matched by name.** Clients have no unique name, and one id lets an application change only its
  issuer between environments. Rejected.
- **Carrying environment-bound values.** Production would accept a sandbox redirect URI, and its profile refuses some
  values. Rejected.
- **Keeping a target's limits as environment-bound.** A limit is configuration its authority promotes like any other,
  and ADR-1418 already names who may change it. Rejected.
- **Keyword templating.** A second language beside settings. Rejected.
- **One transaction.** Stores resolve references from the database, every check runs first, and a promotion converges.
  Rejected.
- **A closed list of families.** Rejected by [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md).
- **A plan of contributors that mark what travels and a per-setting environment flag.** The plan decides each family,
  the descriptor decides each value, and a package adds its own. Chosen.

## Consequences

- An application keeps its `client_id` across environments.
- A customer whose audiences differ per environment manages those resource servers per tenant.
- A tenant administrator of both tenants promotes everything but a limit change, which needs a team administrator.
- The work grows with the source tenant, and the promotion reads it in pages of 100.
- A package promotes its own configuration by registering a contributor.

## References

- [ADR-1050](1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md) — secret material is revealed once on create
- [ADR-1495](1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md) — the hosted platform's environment model
  and hostname scheme
- [ADR-1149](1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md) — settings declare the scopes they apply to
- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md) — vocabularies are open unless provably closed
- [ADR-1206](1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md) — a client's settings are checked by
  registered coherence rules
- [ADR-1483](1483-every-management-error-is-a-problem-document-that-its-route-declares.md) — a management write that loses a
  concurrency race is answered 412
- [ADR-1289](1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md) — a tenant declares its
  environment type, which selects its guardrails
- [ADR-1411](1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md) — the platform is the
  deployment root and a team groups a customer's tenants
- [ADR-1417](1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md) — a tenant administrator is a
  role assignment at the tenant's node
- [ADR-1418](1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) — a limit is set by
  the authority above the scope whose document carries it
- [ADR-1466](1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md) — a client acting for
  itself is a principal that holds role assignments; a machine client
  belongs to the platform or a team and acts only within it
- [ADR-1424](1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md) — a management
  route authorizes the caller at the node it names before it reads
