# Architecture Decision Records

This directory records the **architecturally significant decisions** made for the
**NCode.Identity** library family and for the hosted platform's infrastructure in
[`NCodeGroup/identity-platform`](https://github.com/NCodeGroup/identity-platform)
([ADR-1502](./1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md)), using lightweight
[Architecture Decision Records (ADRs)](https://adr.github.io/).
The private `NCodeGroup/identity-cloud` keeps its own decisions, and a decision about a public contract it relies on is
recorded here ([ADR-1502](./1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md)).

An ADR captures a single decision as it holds in the code: the context that forces it, the decision itself, the
options weighed and the consequences. When a decision changes, its record is rewritten in place, and git history
keeps what it said before; no record here supersedes, amends or overrides another, and each relates only to the
project template's records
([ADR-1503](./1503-a-decision-record-is-rewritten-when-its-decision-changes-and-relates-only-to-template-records.md)).

An ADR records a decision **we** made; it is **not** a source of external authority. Where an
authoritative standard governs — the OpenID Foundation specifications, the IETF OAuth/OIDC RFCs and
the Security BCP ([RFC 9700](https://datatracker.ietf.org/doc/html/rfc9700)), and the OpenID
Foundation conformance suite's pass/warn verdicts — **that external guidance is the authority**, and
an ADR exists to record how we _apply_ it, not to override it. A self-authored ADR must never be
cited as justification to deviate from current authoritative best practice: when an ADR and the
authoritative guidance conflict, re-evaluate and **rewrite the ADR to align with the standard**
rather than treating the ADR as having settled the question. This is a direct corollary of the
greenfield posture ([ADR-1025](./1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)) — our own
docs, code, and ADRs are point-in-time artifacts that may be wrong and are subordinate to the
standards.

## Conventions

- Files are named `NNNN-title-in-kebab-case.md`, where `NNNN` is a four-digit, monotonically increasing number from
  `1000`. Numbers below 1000 are the project template's, in [`project-template/`](./project-template/README.md)
  ([project-template ADR-0012](./project-template/0012-the-templates-records-are-numbered-below-1000-a-projects-from-1000.md)),
  so a bare ADR number names one record across both folders. Take the next number above the highest ADR on a freshly
  fetched `origin/main` and any a change in flight elsewhere has claimed; re-check it when you rebase, and renumber
  yours (file name, title, index row, and links) if it was taken in the meantime. The `adr index` stage of
  `./build/dod.ps1` (`build/adr-index.ps1`) fails when a file is numbered below 1000, when two files share a number,
  when the index misses a file or links a missing one, or when a row's number is not its file's, so a collision that
  slips past a textual merge still fails the merge queue. It also fails when a tracked file names a `NNNN-<slug>.md`
  record that is in neither this folder nor `project-template/`, or points a `docs/decisions/0NNN` path at this
  folder, so a link that a rename or renumber breaks fails the gate too.
- Start a new ADR by copying [`0000-template.md`](./0000-template.md).
- **Status** is `Proposed` or `Accepted`, with nothing after it. A record that builds on one of the project template's
  decisions says so on a metadata line under `**Deciders:**` (see [The template's decisions](#the-templates-decisions));
  a record has no other metadata lines, and cites another record here only in its text, where that record holds a rule
  this one relies on.
- When two records come to decide one thing, they become one record that keeps the more-cited number; the other number
  is retired, never taken again, and every citation of it names the record that holds its decision.
- Keep ADRs short, concrete, and link to real code so future readers can verify the decision
  against the current source.
- Write each ADR as a **timeless, present-tense record** of the decision and its rationale — never a
  narrative of the development process. Avoid development-timeline language (_"shipped"_, _"reversed
  before release"_, _"the first cut"_, _"we built"_, _"now/originally/today"_): describe what the
  decision **is** and why, so the record reads the same a year later. A changed decision is a rewrite of its record,
  which reads as if the decision had always been so.

## The template's decisions

The project template's general decisions are in [`project-template/`](./project-template/README.md); template updates
replace that folder. They are the source of truth for every rule they cover: cite one as `project-template ADR-NNNN`,
linking `project-template/NNNN-<slug>.md`. A record here never restates one. When it adds NCode.Identity-specific
decisions to one, it says `- **Refines:** project-template ADR-NNNN`, and where the two differ the template's holds.
When it contradicts one, it says `- **Conflicts with:** project-template ADR-NNNN` with the issue that tracks bringing
the two into line; until then the record describes what the code does. These three lines are the only relations a
record here carries. To decide otherwise for good, write a record
that says `- **Supersedes:** project-template ADR-NNNN` and list the override in [`AGENTS.md`](../../AGENTS.md).

## Index

The records are grouped by area; within an area they are in number order.

### Engineering, build and repository

| ADR | Title | Status |
| --- | --- | --- |
| [1001](./1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md) | Behavior is a service, a strategy collection or a mediator seam, chosen by one ladder | Accepted |
| [1003](./1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md) | The family versions in lockstep from one version.json and ships source-linked symbol packages | Accepted |
| [1012](./1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) | Concurrency tokens are interceptor-managed, and a lost race surfaces as a store-neutral exception | Accepted |
| [1025](./1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md) | The family is greenfield and pre-release, tracks its public API, and models Auth0-parity-plus | Accepted |
| [1052](./1052-entity-framework-persistence-is-a-reusable-framework-that-domain-slices-plug-into.md) | Entity Framework persistence is a reusable framework that domain slices plug into | Accepted |
| [1069](./1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md) | DI registration proves every prerequisite at compile time through marker builders | Accepted |
| [1080](./1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md) | Filtered reads are intent-named store methods, and growing criteria are init-only DTOs | Accepted |
| [1127](./1127-every-project-builds-with-zero-warnings-and-the-familys-own-banned-apis-bind-shipping-code.md) | Every project builds with zero warnings, and the family's own banned APIs bind shipping code | Accepted |
| [1137](./1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md) | Configuration binds strictly, and every option, timer period and retention is validated at startup | Accepted |
| [1143](./1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md) | CI presents test results and hand-written-code coverage through job summaries, annotations and Codecov | Accepted |
| [1150](./1150-vocabularies-are-open-unless-provably-closed.md) | Vocabularies are open unless provably closed | Accepted |
| [1170](./1170-agent-work-is-planned-on-claude-opus-and-executed-step-by-step-on-claude-sonnet.md) | Agent work is planned on Claude Opus and executed step by step on Claude Sonnet | Accepted |
| [1190](./1190-hashing-names-the-framework-algorithm-and-binary-encodings-are-registered-strategies.md) | Hashing names the framework algorithm, and binary encodings are registered strategies | Accepted |
| [1197](./1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md) | Telemetry has one owner per signal, bounded attributes, and spans that mirror their histograms | Accepted |
| [1261](./1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md) | The main branch takes only reviewed pull requests, squash-merged by the merge queue past required checks | Accepted |
| [1262](./1262-minimal-api-request-delegates-are-generated-at-compile-time.md) | Minimal-API request delegates are generated at compile time | Accepted |
| [1263](./1263-branches-and-pull-requests-run-an-affected-gate-and-the-merge-queue-runs-the-full-gate.md) | Branches and pull requests run an affected gate, and the merge queue runs the full gate | Accepted |
| [1266](./1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md) | Binary data uses BufferFactory rentals with explicit sensitivity, and text only strict encodings | Accepted |
| [1302](./1302-the-main-branch-is-the-trunk-release-lines-service-shipped-versions-and-prereleases-use-trusted-publishing.md) | The main branch is the trunk, release lines service shipped versions, and prereleases use trusted publishing | Accepted |
| [1306](./1306-pending-work-is-tracked-as-github-issues-linked-by-address-in-repository-files.md) | Pending work is tracked as GitHub issues, linked by address in repository files | Accepted |
| [1309](./1309-c-files-carry-a-two-line-spdx-header-that-the-build-checks.md) | C# files carry a two-line SPDX header that the build checks | Accepted |
| [1385](./1385-no-shipping-package-replaces-its-consumers-logging-generator-or-carries-microsoft-extensions-http-resilience.md) | No shipping package replaces its consumers' logging generator or carries Microsoft.Extensions.Http.Resilience | Accepted |
| [1386](./1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md) | Primitives are benchmarked in a project the build compiles and nothing runs | Accepted |
| [1428](./1428-a-package-translates-the-exceptions-it-owns-into-protocol-errors-through-registered-translators.md) | A package translates the exceptions it owns into protocol errors through registered translators | Accepted |
| [1486](./1486-the-repositorys-own-analyzer-project-fails-the-build-on-a-field-an-auto-property-could-replace.md) | The repository's own analyzer project fails the build on a field an auto-property could replace | Accepted |
| [1493](./1493-global-json-requires-the-newest-net-sdk-release-past-the-cooldown.md) | global.json requires the newest .NET SDK release past the cooldown | Accepted |
| [1496](./1496-an-analyzer-reports-an-own-async-method-that-returns-task-or-lacks-a-trailing-cancellation-token.md) | An analyzer reports an own async method that returns Task or lacks a trailing cancellation token | Accepted |
| [1500](./1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md) | Every package names one capability under the NCode.Identity prefix, as an abstractions and implementation pair | Accepted |
| [1501](./1501-a-projects-top-level-folder-and-name-say-whether-it-ships-and-what-it-is.md) | A project's top-level folder and name say whether it ships and what it is | Accepted |
| [1502](./1502-code-a-self-hoster-could-use-is-public-and-identity-holds-the-products-and-platforms-decisions.md) | Code a self-hoster could use is public, and identity holds the product's and platform's decisions | Accepted |
| [1503](./1503-a-decision-record-is-rewritten-when-its-decision-changes-and-relates-only-to-template-records.md) | A decision record is rewritten when its decision changes and relates only to template records | Accepted |

### Deployment and local development

| ADR | Title | Status |
| --- | --- | --- |
| [1071](./1071-the-aspire-apphost-runs-the-local-stack-in-health-gated-order-after-checking-the-frontend-toolchain.md) | The Aspire AppHost runs the local stack in health-gated order after checking the frontend toolchain | Accepted |
| [1073](./1073-one-postgresql-database-holds-the-hosts-schema-and-key-ring-migrated-by-a-worker-and-drift-checked.md) | One PostgreSQL database holds the host's schema and key ring, migrated by a worker and drift-checked | Accepted |
| [1132](./1132-platform-infrastructure-code-is-public-on-github-and-its-state-and-backups-live-in-aws.md) | Platform infrastructure code is public on GitHub, and its state and backups live in AWS | Accepted |
| [1133](./1133-the-hosted-platform-runs-only-the-published-artifacts-it-offers-self-hosters.md) | The hosted platform runs only the published artifacts it offers self-hosters | Accepted |
| [1134](./1134-the-platform-and-the-host-follow-written-cloud-native-operating-principles.md) | The platform and the host follow written cloud-native operating principles | Accepted |
| [1135](./1135-platform-resources-carry-one-stable-tag-set-and-provenance-comes-from-attestations-and-audit-logs.md) | Platform resources carry one stable tag set, and provenance comes from attestations and audit logs | Accepted |
| [1180](./1180-each-host-contexts-migrations-run-before-the-rollout-and-only-expand-the-schema.md) | Each host context's migrations run before the rollout and only expand the schema | Accepted |
| [1247](./1247-the-platform-master-key-protects-the-data-protection-ring-and-an-unreadable-key-stops-the-host.md) | The platform master key protects the Data Protection ring, and an unreadable key stops the host | Accepted |
| [1248](./1248-the-host-and-console-images-are-chiseled-non-root-multi-arch-digest-pinned-and-log-one-line-json.md) | The host and console images are chiseled, non-root, multi-arch, digest-pinned and log one-line JSON | Accepted |
| [1370](./1370-development-conveniences-are-inert-unless-configured-and-a-host-always-names-its-root-key-provider.md) | Development conveniences are inert unless configured, and a host always names its root key provider | Accepted |
| [1430](./1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md) | End-to-end suites run the BFF in real browsers against a containerized Keycloak in their own gate | Accepted |
| [1456](./1456-the-hosts-map-their-health-probes-through-one-general-helper.md) | The hosts map their health probes through one general helper | Accepted |
| [1458](./1458-the-host-chart-deploys-the-server-and-an-opt-in-management-console-and-refuses-unsafe-releases.md) | The host chart deploys the server and an opt-in management console and refuses unsafe releases | Accepted |
| [1485](./1485-the-host-chart-enables-server-kms-vendors-and-holds-console-key-rings-under-the-consoles-identity.md) | The host chart enables server KMS vendors and holds console key rings under the console's identity | Accepted |
| [1495](./1495-the-hosted-platform-runs-customer-environments-as-tenants-on-production-under-the-ncodeid-net-platform-domain.md) | The hosted platform runs customer environments as tenants on production under the ncodeid.net platform domain | Accepted |
| [1499](./1499-the-host-accepts-only-tls-1-2-and-1-3-with-aead-cipher-suites.md) | The host accepts only TLS 1.2 and 1.3 with AEAD cipher suites | Accepted |

### Settings

| ADR | Title | Status |
| --- | --- | --- |
| [1010](./1010-settings-merge-as-ceilings-and-floors-unset-lists-restrict-nothing-and-a-secure-baseline-supplies-defaults.md) | Settings merge as ceilings and floors, unset lists restrict nothing, and a secure baseline supplies defaults | Accepted |
| [1139](./1139-setting-names-are-a-closed-vocabulary-changed-outright-and-settings-the-server-cannot-honor-fail-loudly.md) | Setting names are a closed vocabulary changed outright, and settings the server cannot honor fail loudly | Accepted |
| [1140](./1140-setting-values-are-validated-against-their-domain-not-only-their-type.md) | Setting values are validated against their domain, not only their type | Accepted |
| [1148](./1148-a-scope-selects-security-profiles-that-accumulate-down-the-cascade-as-ceilings-defaults-and-requirements.md) | A scope selects security profiles that accumulate down the cascade as ceilings, defaults and requirements | Accepted |
| [1149](./1149-settings-declare-the-scopes-they-apply-to-and-the-scopes-are-a-registered-cascade.md) | Settings declare the scopes they apply to, and the scopes are a registered cascade | Accepted |
| [1202](./1202-fapi-1-0-advanced-is-a-registered-profile-and-only-a-request-objects-parameters-count.md) | FAPI 1.0 Advanced is a registered profile, and only a request object's parameters count | Accepted |
| [1204](./1204-a-scopes-effective-settings-are-resolved-by-one-service-and-previewed-through-the-management-api.md) | A scope's effective settings are resolved by one service and previewed through the management API | Accepted |
| [1206](./1206-a-clients-settings-are-checked-by-registered-coherence-rules-wherever-they-are-written-or-bounded.md) | A client's settings are checked by registered coherence rules wherever they are written or bounded | Accepted |
| [1245](./1245-management-requests-list-incoherent-clients-name-unreadable-stored-settings-and-let-a-write-repair-them.md) | Management requests list incoherent clients, name unreadable stored settings, and let a write repair them | Accepted |
| [1289](./1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md) | A tenant declares its environment type, which selects its guardrails | Accepted |
| [1412](./1412-a-settings-change-reaches-every-replica-at-once-and-is-reconciled-per-team-against-what-changed.md) | A settings change reaches every replica at once and is reconciled per team against what changed | Accepted |
| [1416](./1416-a-replica-that-may-have-missed-announcements-evicts-only-teams-and-tenants-whose-row-changed.md) | A replica that may have missed announcements evicts only teams and tenants whose row changed | Accepted |
| [1418](./1418-limits-are-set-by-the-authority-above-their-scope-and-readers-learn-which-they-may-change.md) | Limits are set by the authority above their scope, and readers learn which they may change | Accepted |

### Tenancy, persistence and replicas

| ADR | Title | Status |
| --- | --- | --- |
| [1018](./1018-tenant-selection-is-shared-and-tenant-bound-rows-are-scoped-at-the-persistence-layer.md) | Tenant selection is shared and tenant-bound rows are scoped at the persistence layer | Accepted |
| [1036](./1036-one-factory-materializes-every-openid-context-for-a-request-an-addressed-tenant-or-a-job.md) | One factory materializes every OpenID context, for a request, an addressed tenant or a job | Accepted |
| [1053](./1053-a-host-configured-behavioral-hook-is-declared-on-the-openid-environment-and-combined-with-tenant-data.md) | A host-configured behavioral hook is declared on the OpenID environment and combined with tenant data | Accepted |
| [1160](./1160-a-tenant-is-named-by-its-creator-validated-by-its-strategy-and-never-reissued.md) | A tenant is named by its creator, validated by its strategy, and never reissued | Accepted |
| [1164](./1164-replicas-share-expiring-keys-values-and-indexes-through-ports-with-in-process-and-valkey-adapters.md) | Replicas share expiring keys, values and indexes through ports with in-process and Valkey adapters | Accepted |
| [1165](./1165-a-tenants-stored-issuer-is-its-single-origin-and-its-mtls-alias-a-routed-second-host.md) | A tenant's stored issuer is its single origin, and its mTLS alias a routed second host | Accepted |
| [1166](./1166-a-tenant-is-created-and-seeded-through-one-provisioning-pipeline-under-every-strategy.md) | A tenant is created and seeded through one provisioning pipeline under every strategy | Accepted |
| [1198](./1198-a-daily-quota-counts-a-utc-day-and-a-tier-may-admit-a-bounded-billed-overage.md) | A daily quota counts a UTC day and a tier may admit a bounded billed overage | Accepted |
| [1199](./1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to-once-and-prune-in-batches.md) | Tenant usage is a daily ledger that replicas add to once and prune in batches | Accepted |
| [1205](./1205-replicas-announce-changes-to-each-other-through-a-best-effort-broadcast-port.md) | Replicas announce changes to each other through a best-effort broadcast port | Accepted |
| [1242](./1242-deferred-work-runs-through-a-replaceable-job-dispatch-port-with-a-concurrent-in-process-default.md) | Deferred work runs through a replaceable job-dispatch port with a concurrent in-process default | Accepted |
| [1246](./1246-rate-limits-are-registered-descriptors-counted-in-a-shared-expiring-counter-set.md) | Rate limits are registered descriptors counted in a shared expiring counter set | Accepted |
| [1351](./1351-work-that-follows-a-committed-change-runs-on-its-own-bounded-token.md) | Work that follows a committed change runs on its own bounded token | Accepted |
| [1411](./1411-the-platform-is-the-deployment-root-a-team-groups-tenants-and-tenants-share-one-database.md) | The platform is the deployment root, a team groups tenants, and tenants share one database | Accepted |
| [1425](./1425-a-team-promotes-one-tenants-configuration-to-another-and-environment-bound-values-stay.md) | A team promotes one tenant's configuration to another, and environment-bound values stay | Accepted |
| [1426](./1426-a-team-can-nest-beneath-a-parent-team-and-its-subtree-is-walked-depth-first.md) | A team can nest beneath a parent team, and its subtree is walked depth first | Accepted |

### Management API

| ADR | Title | Status |
| --- | --- | --- |
| [1014](./1014-management-resources-carry-opaque-server-identifiers-and-list-one-keyset-page-at-a-time.md) | Management resources carry opaque server identifiers and list one keyset page at a time | Accepted |
| [1020](./1020-a-tenants-grants-are-listed-inspected-and-revoked-by-opaque-identifier-never-by-their-key.md) | A tenant's grants are listed, inspected and revoked by opaque identifier, never by their key | Accepted |
| [1044](./1044-the-management-api-authenticates-its-own-access-tokens-and-a-configured-client-bootstraps-the-first-globaladmin.md) | The management API authenticates its own access tokens and a configured client bootstraps the first GlobalAdmin | Accepted |
| [1067](./1067-secret-key-types-form-one-catalog-and-authenticated-management-callers-discover-the-deployments-capabilities.md) | Secret key types form one catalog, and authenticated management callers discover the deployment's capabilities | Accepted |
| [1086](./1086-endpoints-compose-as-a-declarative-group-tree-and-a-protocol-endpoint-is-a-thin-mediator-driven-provider.md) | Endpoints compose as a declarative group tree, and a protocol endpoint is a thin mediator-driven provider | Accepted |
| [1191](./1191-a-management-request-is-authenticated-by-the-control-plane-tenant-and-scoped-by-its-routes-tenant.md) | A management request is authenticated by the control-plane tenant and scoped by its route's tenant | Accepted |
| [1257](./1257-the-management-console-addresses-every-owners-secrets-settings-and-roles-through-one-owner-value.md) | The management console addresses every owner's secrets, settings and roles through one owner value | Accepted |
| [1417](./1417-management-authority-is-a-role-assignment-at-a-node-of-the-platform-team-and-tenant-hierarchy.md) | Management authority is a role assignment at a node of the platform, team and tenant hierarchy | Accepted |
| [1424](./1424-a-management-route-authorizes-at-the-node-it-names-before-it-reads-through-replaceable-validators.md) | A management route authorizes at the node it names before it reads, through replaceable validators | Accepted |
| [1465](./1465-role-lists-share-one-audited-grant-and-revoke-flow-and-a-caller-discovers-its-own-assignments.md) | Role lists share one audited grant and revoke flow, and a caller discovers its own assignments | Accepted |
| [1466](./1466-a-client-is-its-own-principal-and-a-machine-client-acts-only-within-its-owning-node.md) | A client is its own principal, and a machine client acts only within its owning node | Accepted |
| [1481](./1481-the-composition-layer-serves-the-api-reference-and-each-management-problem-says-when-it-occurs.md) | The composition layer serves the API reference, and each management problem says when it occurs | Accepted |
| [1483](./1483-every-management-error-is-a-problem-document-that-its-route-declares.md) | Every management error is a problem document that its route declares | Accepted |

### Protocol endpoints, grants and tokens

| ADR | Title | Status |
| --- | --- | --- |
| [1026](./1026-resource-servers-own-scopes-settings-and-keys-and-decide-a-clients-scopes-and-token-audiences.md) | Resource servers own scopes, settings and keys, and decide a client's scopes and token audiences | Accepted |
| [1028](./1028-the-revocation-endpoint-revokes-refresh-tokens-and-their-families-and-always-answers-200.md) | The revocation endpoint revokes refresh tokens and their families, and always answers 200 | Accepted |
| [1039](./1039-requested-claims-acr-and-auth-time-follow-the-request-and-the-authentication-event.md) | Requested claims, acr and auth_time follow the request and the authentication event | Accepted |
| [1059](./1059-the-device-authorization-grant-is-a-pending-request-in-the-grant-store-approved-out-of-band.md) | The device authorization grant is a pending request in the grant store, approved out of band | Accepted |
| [1081](./1081-consent-is-a-durable-record-of-decided-and-granted-scopes-enforced-at-the-authorization-endpoint.md) | Consent is a durable record of decided and granted scopes, enforced at the authorization endpoint | Accepted |
| [1085](./1085-ciba-is-a-pending-request-in-the-grant-store-delivered-by-poll-ping-or-push.md) | CIBA is a pending request in the grant store, delivered by poll, ping or push | Accepted |
| [1088](./1088-front-channel-logout-and-session-management-are-available-but-disabled-by-default.md) | Front-channel logout and session management are available but disabled by default | Accepted |
| [1089](./1089-a-pushed-authorization-request-is-a-single-use-client-bound-grant-expanded-before-anything-is-trusted.md) | A pushed authorization request is a single-use, client-bound grant expanded before anything is trusted | Accepted |
| [1093](./1093-a-pairwise-subject-is-a-one-way-projection-of-the-principal-over-a-verified-sector.md) | A pairwise subject is a one-way projection of the principal over a verified sector | Accepted |
| [1097](./1097-access-tokens-are-sender-constrained-by-dpop-or-mutual-tls-through-one-proof-of-possession-seam.md) | Access tokens are sender-constrained by DPoP or mutual TLS through one proof-of-possession seam | Accepted |
| [1101](./1101-token-exchange-issues-delegation-and-impersonation-tokens-from-tokens-the-tenant-issued.md) | Token exchange issues delegation and impersonation tokens from tokens the tenant issued | Accepted |
| [1112](./1112-every-end-of-a-session-runs-one-termination-seam-once-revoking-the-grants-issued-under-it.md) | Every end of a session runs one termination seam, once, revoking the grants issued under it | Accepted |
| [1114](./1114-an-openid-response-is-written-by-a-content-negotiated-response-formatter-json-by-default.md) | An OpenID response is written by a content-negotiated response formatter, JSON by default | Accepted |
| [1141](./1141-a-response-type-is-a-whole-value-and-the-listed-values-alone-control-front-channel-tokens.md) | A response type is a whole value, and the listed values alone control front-channel tokens | Accepted |
| [1144](./1144-authorization-codes-are-bound-to-an-s256-pkce-challenge-unless-an-attested-client-uses-the-nonce.md) | Authorization codes are bound to an S256 PKCE challenge unless an attested client uses the nonce | Accepted |
| [1158](./1158-the-tenant-signs-an-access-token-and-its-client-and-targeted-resource-servers-bound-it.md) | The tenant signs an access token, and its client and targeted resource servers bound it | Accepted |
| [1167](./1167-the-server-validates-the-tokens-it-issued-under-one-policy.md) | The server validates the tokens it issued under one policy | Accepted |
| [1168](./1168-introspection-reports-a-token-active-only-to-the-party-that-consumes-it.md) | Introspection reports a token active only to the party that consumes it | Accepted |
| [1187](./1187-a-client-signed-jwt-is-short-lived-its-lifetime-capped-from-its-earliest-asserted-start.md) | A client-signed JWT is short-lived, its lifetime capped from its earliest asserted start | Accepted |
| [1192](./1192-dynamic-client-registration-rides-the-client-and-grant-stores-authorized-by-single-use-initial-access-tokens.md) | Dynamic client registration rides the client and grant stores, authorized by single-use initial access tokens | Accepted |
| [1201](./1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md) | Authorization responses are delivered by registered response-mode handlers | Accepted |
| [1203](./1203-redirect-uris-are-matched-exactly-as-written-and-their-kinds-are-narrowed-by-settings.md) | Redirect URIs are matched exactly, as written, and their kinds are narrowed by settings | Accepted |
| [1212](./1212-deleting-a-client-resource-server-principal-or-tenant-removes-what-it-owns.md) | Deleting a client, resource server, principal or tenant removes what it owns | Accepted |
| [1216](./1216-a-refresh-token-rotates-unless-it-is-bound-and-rotation-spends-it-with-its-successor-atomically.md) | A refresh token rotates unless it is bound, and rotation spends it with its successor atomically | Accepted |
| [1217](./1217-a-reused-refresh-token-revokes-its-family-unless-it-is-a-retry-within-an-opt-in-interval.md) | A reused refresh token revokes its family unless it is a retry within an opt-in interval | Accepted |
| [1218](./1218-ended-grants-are-removed-after-a-retention-period-in-bounded-batches.md) | Ended grants are removed after a retention period in bounded batches | Accepted |
| [1220](./1220-an-end-user-session-is-a-grant-keyed-by-its-sid-bounded-by-idle-and-absolute-lifetimes.md) | An end-user session is a grant keyed by its sid, bounded by idle and absolute lifetimes | Accepted |
| [1222](./1222-a-grant-is-changed-by-a-read-modify-write-and-a-revocation-that-retry-a-lost-race.md) | A grant is changed by a read-modify-write and a revocation that retry a lost race | Accepted |
| [1223](./1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) | A session that ends unused is terminated once, in the background | Accepted |
| [1225](./1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md) | Every grant that issues tokens for a subject validates it | Accepted |
| [1297](./1297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md) | A refresh token family expires at a fixed horizon that the token response announces | Accepted |
| [1314](./1314-protocol-endpoints-keep-the-http-methods-their-specifications-require.md) | Protocol endpoints keep the HTTP methods their specifications require | Accepted |
| [1380](./1380-logout-ends-the-session-found-in-server-state-confirmed-when-unsure-and-notifies-relying-parties.md) | Logout ends the session found in server state, confirmed when unsure, and notifies relying parties | Accepted |
| [1395](./1395-grant-payloads-have-one-versioned-serializer-and-a-stored-record-missing-written-members-fails-closed.md) | Grant payloads have one versioned serializer, and a stored record missing written members fails closed | Accepted |
| [1401](./1401-userinfo-authenticates-an-access-token-returns-enricher-built-claims-and-audits-disclosures-on-opt-in.md) | UserInfo authenticates an access token, returns enricher-built claims, and audits disclosures on opt-in | Accepted |
| [1498](./1498-protocol-endpoints-answer-the-x-fapi-interaction-id-header.md) | Protocol endpoints answer the x-fapi-interaction-id header | Accepted |

### Client authentication and outbound calls

| ADR | Title | Status |
| --- | --- | --- |
| [1102](./1102-a-client-authenticates-with-a-single-use-jwt-assertion-signed-by-its-key-or-its-secret.md) | A client authenticates with a single-use JWT assertion signed by its key or its secret | Accepted |
| [1106](./1106-a-clients-jwks-uri-key-set-is-fetched-when-needed-shared-between-callers-and-never-served-stale.md) | A client's jwks_uri key set is fetched when needed, shared between callers, and never served stale | Accepted |
| [1145](./1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md) | A client authenticates only with its registered method, which a registered descriptor names | Accepted |
| [1155](./1155-jwts-exchanged-with-a-client-follow-its-registered-metadata-within-its-effective-ceilings.md) | JWTs exchanged with a client follow its registered metadata within its effective ceilings | Accepted |
| [1161](./1161-every-owners-public-keys-have-one-source-and-are-imported-under-one-key-policy.md) | Every owner's public keys have one source and are imported under one key policy | Accepted |
| [1172](./1172-a-forwarded-client-certificate-is-decoded-by-the-decoder-registered-for-its-format.md) | A forwarded client certificate is decoded by the decoder registered for its format | Accepted |
| [1173](./1173-a-mutual-tls-client-is-authenticated-by-its-registered-certificate-chained-to-configured-trust-anchors.md) | A mutual-TLS client is authenticated by its registered certificate, chained to configured trust anchors | Accepted |
| [1176](./1176-dpop-proofs-are-created-and-validated-by-a-jose-service.md) | DPoP proofs are created and validated by a JOSE service | Accepted |
| [1259](./1259-the-built-in-revocation-sources-check-ocsp-and-every-crl-form-rfc-5280-defines.md) | The built-in revocation sources check OCSP and every CRL form RFC 5280 defines | Accepted |
| [1315](./1315-calls-to-caller-supplied-urls-share-one-guarded-bounded-client-and-one-single-flight-helper.md) | Calls to caller-supplied URLs share one guarded, bounded client and one single-flight helper | Accepted |
| [1365](./1365-no-url-the-server-publishes-or-compares-takes-its-origin-from-the-requests-host.md) | No URL the server publishes or compares takes its origin from the request's Host | Accepted |
| [1399](./1399-failed-client-authentications-are-limited-per-remote-address-by-default.md) | Failed client authentications are limited per remote address by default | Accepted |
| [1400](./1400-client-certificate-revocation-is-checked-by-configured-sources-under-the-clients-revocation-mode.md) | Client certificate revocation is checked by configured sources under the client's revocation mode | Accepted |

### Secrets and keys

| ADR | Title | Status |
| --- | --- | --- |
| [1050](./1050-a-secret-is-generated-by-the-server-revealed-once-and-never-cached.md) | A secret is generated by the server, revealed once and never cached | Accepted |
| [1068](./1068-elliptic-curves-are-one-extensible-catalog-and-an-imported-point-is-checked-against-its-curve.md) | Elliptic curves are one extensible catalog, and an imported point is checked against its curve | Accepted |
| [1104](./1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md) | A key type is one registered descriptor, and public keys are imported and stored by encoding | Accepted |
| [1169](./1169-every-tenant-is-provisioned-with-a-persisted-key-for-each-configured-algorithm-it-uses.md) | Every tenant is provisioned with a persisted key for each configured algorithm it uses | Accepted |
| [1235](./1235-stored-secrets-and-grant-payloads-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) | Stored secrets and grant payloads are protected by a key hierarchy under an external root key | Accepted |
| [1260](./1260-a-key-wrapper-is-one-open-contract-and-each-key-management-service-has-one-adapter-package.md) | A key wrapper is one open contract, and each key management service has one adapter package | Accepted |
| [1268](./1268-an-asymmetric-secrets-public-key-is-stored-beside-it-and-read-without-its-private-key.md) | An asymmetric secret's public key is stored beside it and read without its private key | Accepted |
| [1320](./1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md) | A rotated client secret overlaps its replacement until it expires or is deleted | Accepted |
| [1391](./1391-a-client-secret-is-a-one-way-verifier-unless-its-method-keys-from-its-utf-8-octets.md) | A client secret is a one-way verifier unless its method keys from its UTF-8 octets | Accepted |
| [1403](./1403-a-stored-secret-has-an-active-window-inside-its-validity-window-honored-on-every-use.md) | A stored secret has an active window inside its validity window, honored on every use | Accepted |
| [1404](./1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md) | A tenant's provisioned keys roll over ahead of retirement and expire after a grace | Accepted |
| [1407](./1407-a-master-key-rotates-its-data-keys-are-rewrapped-and-it-is-destroyed-once-unnamed.md) | A master key rotates, its data keys are rewrapped, and it is destroyed once unnamed | Accepted |
| [1408](./1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md) | An offline host command rewraps every master key under the configured root and recovery keys | Accepted |
| [1414](./1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md) | A tenant can wrap its master keys under a root key it controls | Accepted |
| [1471](./1471-a-tenants-signing-and-encryption-keys-can-be-held-in-a-key-management-service.md) | A tenant's signing and encryption keys can be held in a key management service | Accepted |
| [1472](./1472-signing-and-content-key-unwrapping-are-asynchronous-from-the-jose-algorithm-to-every-caller.md) | Signing and content-key unwrapping are asynchronous from the JOSE algorithm to every caller | Accepted |
| [1474](./1474-a-remote-key-provider-is-described-by-data-and-reads-signs-and-decrypts-by-key-identifier.md) | A remote key provider is described by data and reads, signs and decrypts by key identifier | Accepted |
| [1476](./1476-the-management-api-creates-a-tenants-remote-key-and-binds-one-only-after-proving-it.md) | The management API creates a tenant's remote key and binds one only after proving it | Accepted |
| [1478](./1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md) | A background job destroys the key management service keys that no stored secret names | Accepted |
| [1479](./1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md) | A protocol client's and a BFF's keys can be held in a key management service | Accepted |

### JOSE, JWT, COSE and the protocol client

| ADR | Title | Status |
| --- | --- | --- |
| [1118](./1118-ml-dsa-post-quantum-signatures-ship-in-the-core-libraries-over-the-bcl-mldsa-type.md) | ML-DSA post-quantum signatures ship in the core libraries over the BCL MLDsa type | Accepted |
| [1119](./1119-a-di-free-jwk-package-models-keys-sets-and-thumbprints-and-key-types-register-their-rules.md) | A DI-free JWK package models keys, sets and thumbprints, and key types register their rules | Accepted |
| [1122](./1122-jwt-claim-values-map-to-and-from-json-by-their-declared-type-alone.md) | JWT claim values map to and from JSON by their declared type alone | Accepted |
| [1123](./1123-a-numericdate-and-a-whole-number-a-remote-party-sends-are-read-strictly-through-shared-readers.md) | A NumericDate and a whole number a remote party sends are read strictly through shared readers | Accepted |
| [1126](./1126-the-ecdh-es-concat-kdf-digest-is-sha-256-and-a-test-oracle-must-conform-to-the-rfc.md) | The ECDH-ES Concat KDF digest is SHA-256, and a test oracle must conform to the RFC | Accepted |
| [1154](./1154-jwts-are-validated-only-as-signed-tokens-by-requirements-and-handlers-under-the-callers-stated-policy.md) | JWTs are validated only as signed tokens, by requirements and handlers under the caller's stated policy | Accepted |
| [1273](./1273-the-protocol-client-validates-id-tokens-and-their-token-hashes-under-openid-connect-core.md) | The protocol client validates ID tokens and their token hashes under OpenID Connect Core | Accepted |
| [1274](./1274-the-protocol-client-validates-logout-tokens-and-records-each-only-after-the-client-handles-it.md) | The protocol client validates logout tokens and records each only after the client handles it | Accepted |
| [1275](./1275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md) | The protocol client pushes authorization requests whenever the provider offers PAR | Accepted |
| [1276](./1276-the-oauth-and-openid-connect-wire-vocabulary-lives-in-a-dependency-free-package.md) | The OAuth and OpenID Connect wire vocabulary lives in a dependency-free package | Accepted |
| [1278](./1278-token-validation-failures-carry-a-machine-readable-code-set-where-the-failure-is-detected.md) | Token validation failures carry a machine-readable code set where the failure is detected | Accepted |
| [1280](./1280-the-protocol-client-validates-and-decrypts-every-provider-issued-jwt-through-one-core-under-registered-algorithms.md) | The protocol client validates and decrypts every provider-issued JWT through one core under registered algorithms | Accepted |
| [1282](./1282-the-jose-library-implements-neither-none-nor-rsa1-5-and-a-failed-jwe-decryption-reveals-no-cause.md) | The JOSE library implements neither none nor RSA1_5, and a failed JWE decryption reveals no cause | Accepted |
| [1285](./1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md) | The protocol client is a framework-free library over the family's JOSE stack and named key rings | Accepted |
| [1286](./1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md) | The protocol client binds tokens with DPoP whenever the provider supports it | Accepted |
| [1300](./1300-the-protocol-client-authenticates-with-a-key-rings-tls-client-certificate-and-its-issuers-certificates.md) | The protocol client authenticates with a key ring's TLS client certificate and its issuers' certificates | Accepted |
| [1301](./1301-the-protocol-client-reads-userinfo-as-json-or-a-registered-jwt-under-the-familys-claim-mapping.md) | The protocol client reads UserInfo as JSON or a registered JWT under the family's claim mapping | Accepted |
| [1336](./1336-the-protocol-client-bounds-every-call-with-a-polly-core-pipeline-and-retries-only-safe-requests.md) | The protocol client bounds every call with a Polly.Core pipeline and retries only safe requests | Accepted |
| [1389](./1389-one-algorithm-name-serves-every-key-type-and-curve-it-declares-and-ecdh-es-agreements-are-registered.md) | One algorithm name serves every key type and curve it declares, and ECDH-ES agreements are registered | Accepted |
| [1390](./1390-the-opt-in-eddsa-package-implements-ed25519-ed448-x25519-and-x448-clean-room-over-okp-keys.md) | The opt-in EdDSA package implements Ed25519, Ed448, X25519 and X448 clean-room over OKP keys | Accepted |
| [1392](./1392-ecdsa-and-eddsa-algorithms-are-fully-specified-and-bound-to-their-curve-and-es256k-is-opt-in.md) | ECDSA and EdDSA algorithms are fully specified and bound to their curve, and ES256K is opt-in | Accepted |
| [1398](./1398-a-key-is-generated-on-one-named-curve-and-only-symmetric-keys-are-checked-by-size.md) | A key is generated on one named curve, and only symmetric keys are checked by size | Accepted |
| [1405](./1405-selective-disclosure-jwts-are-an-opt-in-package-that-issues-presents-and-verifies-over-the-jwt-validator.md) | Selective Disclosure JWTs are an opt-in package that issues, presents and verifies over the JWT validator | Accepted |
| [1409](./1409-cose-messages-are-an-opt-in-package-family-over-system-formats-cbor-and-the-familys-keys-and-algorithms.md) | COSE messages are an opt-in package family over System.Formats.Cbor and the family's keys and algorithms | Accepted |
| [1410](./1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md) | A CWT is validated fail-closed, and its claims are read only after its COSE layer verifies | Accepted |

### Accounts, interactive login and audit

| ADR | Title | Status |
| --- | --- | --- |
| [1035](./1035-an-end-user-is-one-tenant-scoped-principal-that-every-authentication-entry-point-resolves-to.md) | An end user is one tenant-scoped principal that every authentication entry point resolves to | Accepted |
| [1047](./1047-observation-is-a-fault-isolated-event-seam-that-audit-specializes-and-protocol-errors-reach-once.md) | Observation is a fault-isolated event seam that audit specializes and protocol errors reach once | Accepted |
| [1051](./1051-a-local-account-is-a-self-issued-connection-behind-one-pluggable-store-seam.md) | A local account is a self-issued connection behind one pluggable store seam | Accepted |
| [1055](./1055-subject-metadata-lives-on-the-principal-and-projects-into-tokens-as-nested-claims-at-issuance.md) | Subject metadata lives on the principal and projects into tokens as nested claims at issuance | Accepted |
| [1057](./1057-local-account-management-is-an-opt-in-capability-that-provisions-the-principal-eagerly-and-authorizes-per-tenant.md) | Local-account management is an opt-in capability that provisions the principal eagerly and authorizes per tenant | Accepted |
| [1062](./1062-the-op-serves-an-opt-in-interactive-sign-in-and-consent-surface-with-a-pluggable-credential-verifier.md) | The OP serves an opt-in interactive sign-in and consent surface with a pluggable credential verifier | Accepted |
| [1182](./1182-the-frontend-bootstraps-issuer-is-the-serving-tenants-and-its-api-base-is-root-relative.md) | The frontend bootstrap's issuer is the serving tenant's and its API base is root-relative | Accepted |
| [1193](./1193-the-interactive-surface-uses-the-tenant-route-host-only-cookies-and-one-shared-single-page-bundle-helper.md) | The interactive surface uses the tenant route, host-only cookies and one shared single-page bundle helper | Accepted |
| [1194](./1194-a-tenant-brands-its-interactive-surface-through-settings.md) | A tenant brands its interactive surface through settings | Accepted |
| [1229](./1229-a-blocked-disabled-or-deleted-end-user-loses-its-sessions-and-is-refused-with-access-denied.md) | A blocked, disabled or deleted end user loses its sessions and is refused with access_denied | Accepted |
| [1230](./1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md) | An end user changes their password and keeps only the current session | Accepted |
| [1236](./1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md) | A password is judged by one policy wherever it is set | Accepted |
| [1237](./1237-failed-password-attempts-are-limited-per-account.md) | Failed password attempts are limited per account | Accepted |
| [1238](./1238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md) | A password is judged, hashed and verified in its NFC form | Accepted |
| [1239](./1239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) | A password is verified in the same time whether or not its account exists | Accepted |
| [1243](./1243-every-aspnet-core-identity-path-hashes-and-verifies-the-nfc-form-of-a-password.md) | Every ASP.NET Core Identity path hashes and verifies the NFC form of a password | Accepted |
| [1244](./1244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md) | A password refused at the attempt limit costs what a wrong one costs | Accepted |
| [1253](./1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md) | The interactive bundle mounts at once over the page the host rendered | Accepted |
| [1283](./1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) | Audit records are CloudTrail-shaped JSON with an indexable envelope | Accepted |
| [1303](./1303-audit-records-are-kept-in-an-indexed-store-that-each-replica-writes-in-batches.md) | Audit records are kept in an indexed store that each replica writes in batches | Accepted |
| [1402](./1402-the-interactive-surface-renders-in-the-locale-the-request-prefers-among-the-registered-ui-locales.md) | The interactive surface renders in the locale the request prefers among the registered UI locales | Accepted |
| [1494](./1494-a-tenant-sets-its-sign-in-tabs-icon-as-a-branding-setting-the-hosts-link-in-place-of-the-familys.md) | A tenant sets its sign-in tab's icon as a branding setting the hosts link in place of the family's | Accepted |

### Frontend, BFF and browser security

| ADR | Title | Status |
| --- | --- | --- |
| [1064](./1064-frontend-presentation-platform.md) | A single cohesive frontend presentation platform | Accepted |
| [1082](./1082-playground-and-conformance-surfaces.md) | Playground flow explorer, conformance posture dashboard, and surface-aware bootstrap | Accepted |
| [1084](./1084-spec-coverage-conformance-harness.md) | Spec-coverage conformance harness (full protocol catalog + browser/probe-runner boundary) | Accepted |
| [1147](./1147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md) | The frontend toolchain follows the pinned runtime and supported upstreams | Accepted |
| [1151](./1151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md) | Frontend tests treat warnings as errors and hold coverage floors | Accepted |
| [1152](./1152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md) | The frontend's Node and pnpm are each pinned once and provisioned without Corepack | Accepted |
| [1153](./1153-one-release-age-window-gates-frontend-dependencies.md) | One release-age window gates frontend dependencies for people and Dependabot | Accepted |
| [1156](./1156-shared-app-shell-containers-live-in-ncode-shell.md) | Shared app-shell containers live in `@ncode/shell` | Accepted |
| [1157](./1157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md) | Every frontend workspace builds and tests from one Vite preset | Accepted |
| [1174](./1174-the-management-consoles-url-is-its-navigation-state.md) | The management console's URL is its navigation state | Accepted |
| [1175](./1175-bundles-split-into-vendor-and-surface-chunks-and-an-oversized-chunk-fails-the-build.md) | Bundles split into vendor and surface chunks, and an oversized chunk fails the build | Accepted |
| [1255](./1255-each-tenant-surface-of-the-management-console-has-its-own-address.md) | Each tenant surface of the management console has its own address | Accepted |
| [1256](./1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md) | Every page the server writes has a strict Content-Security-Policy and is framed only by design | Accepted |
| [1258](./1258-the-server-sends-strict-transport-security-outside-development.md) | The server sends Strict-Transport-Security outside Development | Accepted |
| [1290](./1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md) | The BFF keeps each session as an encrypted server-side record under a cookie handle | Accepted |
| [1291](./1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md) | The BFF refreshes a session single-flight under a lease on its refresh token | Accepted |
| [1292](./1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md) | The BFF accepts its cookie only with a static header and Fetch Metadata | Accepted |
| [1293](./1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md) | The BFF ends sessions by guarded logout and by back-channel logout through indexes | Accepted |
| [1294](./1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md) | A BFF client is registered with explicit metadata, not a bff security profile | Accepted |
| [1308](./1308-a-refresh-lease-is-released-only-by-the-owner-token-its-winner-holds.md) | A refresh lease is released only by the owner token its winner holds | Accepted |
| [1311](./1311-the-bff-logs-out-by-a-form-post-that-carries-the-sessions-logout-guard.md) | The BFF logs out by a form post that carries the session's logout guard | Accepted |
| [1381](./1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md) | The BFF serves its own and exchanged tokens without a lease and forwards remote APIs through YARP | Accepted |
| [1382](./1382-the-bff-sends-browser-security-headers-and-publishes-its-client-keys-only-when-the-host-opts-in.md) | The BFF sends browser security headers and publishes its client keys only when the host opts in | Accepted |
| [1388](./1388-the-html-page-security-model-and-the-front-channel-marker-are-general-aspnet-core-helpers.md) | The HTML page security model and the front-channel marker are general ASP.NET Core helpers | Accepted |
| [1431](./1431-the-bff-requests-offline-access-only-when-its-deployment-lists-it.md) | The BFF requests offline access only when its deployment lists it | Accepted |
| [1432](./1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md) | One BFF serves several frontends, each selected per request by an open rule | Accepted |
| [1433](./1433-a-bff-host-refuses-to-start-when-an-endpoints-bff-metadata-is-incomplete.md) | A BFF host refuses to start when an endpoint's BFF metadata is incomplete | Accepted |
| [1434](./1434-open-tabs-learn-that-a-bff-session-ended-from-its-next-answer-not-a-pushed-event.md) | Open tabs learn that a BFF session ended from its next answer, not a pushed event | Accepted |
| [1435](./1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md) | Server-rendered and Blazor hosts use the BFF session through pages, hubs and two opt-in Blazor packages | Accepted |
| [1436](./1436-a-bff-page-accepts-a-form-post-only-with-fetch-metadata-and-a-valid-antiforgery-token.md) | A BFF page accepts a form post only with Fetch Metadata and a valid antiforgery token | Accepted |
| [1437](./1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md) | The management console is a confidential client served by a BFF host of its own | Accepted |
| [1438](./1438-the-management-console-holds-no-token-and-reads-its-session-from-its-bff.md) | The management console holds no token and reads its session from its BFF | Accepted |
| [1439](./1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md) | A BFF loads the key rings its configuration names from PEM files | Accepted |
| [1440](./1440-the-names-a-bff-and-its-browser-clients-share-live-in-a-dependency-free-package.md) | The names a BFF and its browser clients share live in a dependency-free package | Accepted |
| [1441](./1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md) | A BFF connection ends with its session even when idle, and a Blazor Web App proves it | Accepted |
| [1442](./1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md) | The BFF's frontends follow their configuration, and a change takes effect only when the whole set is valid | Accepted |
| [1443](./1443-a-bff-session-ends-when-its-frontend-is-removed-or-registered-as-another-client.md) | A BFF session ends when its frontend is removed or registered as another client | Accepted |
| [1444](./1444-a-bff-page-reached-without-its-strict-session-cookie-reloads-itself-once.md) | A BFF page reached without its Strict session cookie reloads itself once | Accepted |
| [1445](./1445-a-self-revalidating-hub-keeps-its-connection-when-the-session-ends-and-every-watcher-learns-the-end-at-once.md) | A self-revalidating hub keeps its connection when the session ends, and every watcher learns the end at once | Accepted |
| [1446](./1446-a-set-of-frontends-that-names-an-unregistered-key-ring-is-refused.md) | A set of frontends that names an unregistered key ring is refused | Accepted |
| [1447](./1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md) | Forwarded-header trust and Strict-Transport-Security are general ASP.NET Core helpers each host composes | Accepted |
| [1487](./1487-the-bff-forwards-a-remote-api-through-a-named-http-client-a-host-can-configure.md) | The BFF forwards a remote API through a named HTTP client a host can configure | Accepted |
| [1488](./1488-the-management-console-reaches-the-management-api-at-its-authoritys-origin-and-its-host-serves-only-the-console.md) | The management console reaches the management API at its authority's origin, and its host serves only the console | Accepted |
| [1491](./1491-the-familys-logo-is-the-id-mark-between-the-ncode-braces-and-one-script-draws-every-file-of-it.md) | The family's logo is the {id} mark between the NCode braces, and one script draws every file of it | Accepted |
| [1492](./1492-every-web-app-shows-the-familys-icon-from-hashed-copies-its-host-serves.md) | Every web app shows the family's icon from hashed copies its host serves | Accepted |
