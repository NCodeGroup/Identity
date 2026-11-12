# Architecture Decision Records

This directory records the **architecturally significant decisions** made for the
**NCode.Identity** library family and for the hosted platform's infrastructure in
[`NCodeGroup/identity-platform`](https://github.com/NCodeGroup/identity-platform)
([ADR-1307](./1307-identity-platform-records-its-decisions-and-pending-work-in-this-repository.md)), using lightweight
[Architecture Decision Records (ADRs)](https://adr.github.io/).
The private `NCodeGroup/identity-cloud` keeps its own decisions, and a decision about a public contract it relies on is
recorded here ([ADR-1502](./1502-code-a-self-hoster-could-use-is-public-and-the-products-repositories-and-artifacts-follow-one-naming-scheme.md)).

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
greenfield posture ([ADR-1025](./1025-pre-release-posture-and-auth0-parity-plus.md)) — our own
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
| [1005](./1005-public-api-surface-is-tracked-and-evolves-compatibly.md) | The public API surface is tracked and evolves backward-compatibly | Accepted |
| [1012](./1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md) | Concurrency tokens are interceptor-managed, and a lost race surfaces as a store-neutral exception | Accepted |
| [1016](./1016-implementation-packages-depend-only-on-abstractions.md) | Implementation packages depend only on abstractions; composition wires them | Accepted |
| [1025](./1025-pre-release-posture-and-auth0-parity-plus.md) | Pre-release posture: greenfield, and an Auth0-parity-plus design philosophy | Accepted |
| [1052](./1052-entity-framework-persistence-is-a-framework-plus-slices.md) | Entity Framework persistence is a reusable framework plus domain slices | Accepted |
| [1056](./1056-abstractions-split-into-framework-free-tiered-packages.md) | `NCode.Identity.Abstractions` is split into framework-free, correctly-tiered packages | Accepted |
| [1069](./1069-registration-library-compile-time-marker-nesting.md) | DI registration nests per package and folder through compile-time marker builders | Accepted |
| [1080](./1080-store-query-methods-push-filtering-to-dbms.md) | Store query methods push entity filtering into the DBMS | Accepted |
| [1099](./1099-forward-compatible-evolving-parameters.md) | Forward-compatible evolving parameters: filter/criteria DTOs | Accepted |
| [1116](./1116-memory-allocations-and-buffer-operations.md) | Memory allocations and buffer operations use spans, pooled writers, and secure lifetimes | Accepted |
| [1125](./1125-trunk-based-development-on-main.md) | Trunk-based development on a single `main` trunk | Superseded |
| [1127](./1127-warnings-are-errors-in-every-project.md) | Warnings are errors in every project, including tests | Accepted |
| [1130](./1130-distinct-names-for-host-local-dev-orchestrator-and-library.md) | The deployable host, the local-dev orchestrator, and the shipped library have distinct names | Accepted |
| [1137](./1137-configuration-binds-strictly-and-is-validated-at-startup.md) | Configuration binds strictly and is validated when the host starts | Accepted |
| [1143](./1143-ci-presents-test-results-and-coverage-in-github.md) | CI presents test results and coverage in GitHub, through job summaries, annotations, and Codecov | Accepted |
| [1150](./1150-vocabularies-are-open-unless-provably-closed.md) | Vocabularies are open unless provably closed | Accepted |
| [1170](./1170-agent-work-is-planned-on-a-frontier-model-and-executed-step-by-step-on-a-cheaper-one.md) | Agent work is planned on a frontier model and executed step by step on a cheaper one | Accepted |
| [1185](./1185-coverage-measures-hand-written-code-and-counts-a-line-that-runs-as-covered.md) | Coverage measures hand-written code and counts a line that runs as covered | Accepted |
| [1188](./1188-general-time-helpers-live-in-a-dependency-free-package.md) | General time helpers live in a dependency-free package | Superseded |
| [1190](./1190-hashing-names-the-framework-algorithm-and-encodings-are-registered.md) | Hashing names the framework algorithm, and binary encodings are registered strategies | Accepted |
| [1197](./1197-protocol-metrics-have-one-owner-per-signal-and-only-bounded-dimensions.md) | Protocol metrics have one owner per signal and only bounded dimensions | Accepted |
| [1221](./1221-intervals-and-retentions-are-configured-and-validated-when-the-host-starts.md) | Intervals and retentions are configured and validated when the host starts | Accepted |
| [1261](./1261-main-accepts-changes-only-through-pull-requests-that-pass-the-gate.md) | main accepts changes only through pull requests that pass the gate | Accepted |
| [1262](./1262-minimal-api-request-delegates-are-generated-at-compile-time.md) | Minimal-API request delegates are generated at compile time | Accepted |
| [1263](./1263-feature-branches-run-a-fast-gate-and-the-merge-queue-runs-the-full-gate.md) | Feature branches run a fast gate and the merge queue runs the full gate | Accepted |
| [1264](./1264-pending-work-is-tracked-as-github-issues.md) | Pending work is tracked as GitHub issues | Superseded |
| [1266](./1266-text-is-encoded-and-decoded-only-through-strict-encodings.md) | Text is encoded and decoded only through strict encodings | Accepted |
| [1269](./1269-a-pull-request-stays-a-draft-until-its-author-reviews-it.md) | A pull request stays a draft until its author reviews it | Accepted |
| [1298](./1298-a-package-traces-its-own-mechanisms-through-one-activity-source-named-like-its-meter.md) | A package traces its own mechanisms through one activity source named like its meter | Accepted |
| [1302](./1302-prerelease-packages-publish-from-a-prerelease-tag-or-a-manual-run-on-main-through-trusted-publishing.md) | Prerelease packages publish from a prerelease tag or a manual run on main, through trusted publishing | Accepted |
| [1304](./1304-pull-requests-into-main-merge-by-squash-through-the-merge-queue.md) | Pull requests into main merge by squash through the merge queue | Accepted |
| [1305](./1305-shipped-versions-are-serviced-from-release-lines-beside-the-trunk.md) | Shipped versions are serviced from release lines beside the trunk | Accepted |
| [1306](./1306-pending-work-is-tracked-as-github-issues-linked-by-address-in-files.md) | Pending work is tracked as GitHub issues, linked by address in repository files | Accepted |
| [1307](./1307-identity-platform-records-its-decisions-and-pending-work-in-this-repository.md) | identity-platform records its decisions and pending work in this repository | Accepted |
| [1309](./1309-csharp-files-carry-a-two-line-spdx-header-that-the-build-checks.md) | C# files carry a two-line SPDX header that the build checks | Accepted |
| [1355](./1355-general-aspnet-core-helpers-live-in-a-dependency-free-package.md) | General ASP.NET Core helpers live in a dependency-free package | Superseded |
| [1385](./1385-no-shipping-package-switches-off-its-consumers-logging-generator.md) | No shipping package switches off its consumers' logging generator | Accepted |
| [1386](./1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md) | Primitives are benchmarked in a project the build compiles and nothing runs | Accepted |
| [1397](./1397-before-the-first-stable-release-no-identifier-or-version-is-held-for-an-unreleased-state.md) | Before the first stable release, no identifier or version is held for an unreleased state | Accepted |
| [1428](./1428-a-package-translates-the-exceptions-it-owns-into-protocol-errors.md) | A package translates the exceptions it owns into protocol errors | Accepted |
| [1486](./1486-an-analyzer-in-every-project-reports-a-field-that-could-be-an-auto-property.md) | An analyzer in every project reports a field that could be an auto-property | Accepted |
| [1493](./1493-global-json-requires-the-newest-net-sdk-release-past-the-cooldown.md) | global.json requires the newest .NET SDK release past the cooldown | Accepted |
| [1496](./1496-an-analyzer-reports-an-own-async-method-that-returns-task-or-takes-no-trailing-cancellation-token.md) | An analyzer reports an own async method that returns Task or takes no trailing cancellation token | Accepted |
| [1500](./1500-every-package-names-one-capability-under-the-ncode-identity-prefix.md) | Every package names one capability under the NCode.Identity prefix | Accepted |
| [1501](./1501-a-projects-top-level-folder-says-whether-it-ships.md) | A project's top-level folder says whether it ships | Accepted |
| [1502](./1502-code-a-self-hoster-could-use-is-public-and-the-products-repositories-and-artifacts-follow-one-naming-scheme.md) | Code a self-hoster could use is public, and the product's repositories and artifacts follow one naming scheme | Accepted |
| [1503](./1503-a-decision-record-is-rewritten-when-its-decision-changes-and-relates-only-to-template-records.md) | A decision record is rewritten when its decision changes and relates only to template records | Accepted |

### Deployment and local development

| ADR | Title | Status |
| --- | --- | --- |
| [1002](./1002-ephemeral-development-keys.md) | Ephemeral development keys are an explicit opt-in, never a silent default | Superseded |
| [1021](./1021-developer-signing-keys-seeded-through-persistence.md) | Developer signing keys are seeded through the persistence layer, not injected at the read path | Accepted |
| [1041](./1041-host-owned-ef-migrations-deliberate-outside-development.md) | The reference host owns schema with EF Core migrations, applied deliberately outside Development | Accepted |
| [1071](./1071-aspire-local-dev-orchestrator.md) | Adopt .NET Aspire as the local development orchestrator | Accepted |
| [1072](./1072-self-healing-dev-database-and-model-drift-gate.md) | Self-healing local developer database + a Definition-of-Done model-drift gate | Superseded |
| [1073](./1073-postgres-dev-database-and-aspire-migration-worker.md) | PostgreSQL developer database with an Aspire migration-worker, and data-protection keys stored in the database | Accepted |
| [1131](./1131-hosted-platform-environment-model-and-hostname-scheme.md) | The hosted platform's environment model and hostname scheme | Accepted |
| [1132](./1132-platform-infrastructure-as-code-is-public-on-github.md) | Platform infrastructure as code is public on GitHub; state and backups live in AWS | Accepted |
| [1133](./1133-we-ship-and-run-the-same-deployment-artifacts.md) | We ship and run the same deployment artifacts | Accepted |
| [1134](./1134-cloud-native-operating-principles.md) | Cloud-native operating principles: cattle, immutable, stateless, redundant | Accepted |
| [1135](./1135-one-resource-tagging-strategy-across-every-vendor.md) | One resource tagging strategy across every vendor | Accepted |
| [1136](./1136-provenance-is-attested-on-workloads-and-audited-for-infrastructure.md) | Provenance is attested on workloads and audited for infrastructure | Accepted |
| [1146](./1146-a-development-seeder-registers-only-the-clients-its-configuration-declares.md) | A development seeder registers only the clients its configuration declares | Accepted |
| [1177](./1177-the-data-protection-key-ring-is-encrypted-at-rest-in-production.md) | The Data Protection key ring is encrypted at rest in Production | Superseded |
| [1178](./1178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md) | The host image is chiseled, non-root, multi-arch, and carries the login bundle | Accepted |
| [1179](./1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md) | The host chart references its secrets and refuses a release the host would refuse | Accepted |
| [1180](./1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md) | Migrations run before the rollout and keep the previous release working | Accepted |
| [1184](./1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md) | A migration only expands the schema unless it is marked a contract step | Accepted |
| [1247](./1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md) | A Data Protection key the server cannot decrypt stops the host | Accepted |
| [1248](./1248-the-host-image-writes-one-line-json-logs.md) | The host image writes one-line JSON logs | Accepted |
| [1249](./1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md) | The host image's base images are pinned by digest and tracked by Dependabot | Accepted |
| [1250](./1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md) | A migration classifies its data change, and a constraint added to an existing table contracts | Accepted |
| [1287](./1287-the-apphost-fails-fast-when-the-frontend-toolchain-is-stale.md) | The AppHost fails fast when the frontend toolchain is stale | Accepted |
| [1313](./1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md) | Development conveniences are inert unless configured and run through the production path | Accepted |
| [1370](./1370-a-host-names-its-root-key-provider-and-development-secrets-are-generated-per-machine.md) | A host names its root key provider, and development secrets are generated per machine | Accepted |
| [1394](./1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md) | Until the first stable release, each host database context has one initial migration and no migration rewrites stored data | Accepted |
| [1413](./1413-the-platform-master-key-protects-the-data-protection-key-ring-and-the-hosts-own-data.md) | The platform master key protects the Data Protection key ring and the host's own data | Accepted |
| [1423](./1423-developer-key-pruning-starts-the-platform-over-when-its-master-keys-cannot-be-unwrapped.md) | Developer key pruning starts the platform over when its master keys cannot be unwrapped | Accepted |
| [1430](./1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md) | End-to-end suites run the BFF in real browsers against a containerized Keycloak in their own gate | Accepted |
| [1456](./1456-the-hosts-map-their-health-probes-through-one-general-helper.md) | The hosts map their health probes through one general helper | Accepted |
| [1458](./1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md) | The management console ships as its own image and an opt-in component of the host chart | Accepted |
| [1485](./1485-the-host-chart-holds-the-consoles-key-rings-in-a-key-management-service-under-the-consoles-own-identity.md) | The host chart holds the console's key rings in a key management service under the console's own identity | Accepted |
| [1490](./1490-the-host-chart-registers-each-server-remote-key-provider-the-operator-enables-and-its-location-is-optional.md) | The host chart registers each server remote key provider the operator enables, and its location is optional | Accepted |
| [1495](./1495-the-hosted-platforms-hosts-live-on-a-platform-domain-of-their-own-and-a-brand-domain-carries-the-site-docs-and-mail.md) | The hosted platform's hosts live on a platform domain of their own, and a brand domain carries the site, docs and mail | Accepted |
| [1499](./1499-the-host-accepts-only-tls-1-2-and-1-3-with-aead-cipher-suites.md) | The host accepts only TLS 1.2 and 1.3 with AEAD cipher suites | Accepted |

### Settings

| ADR | Title | Status |
| --- | --- | --- |
| [1010](./1010-supported-settings-unset-means-unrestricted.md) | `*_supported` settings: unset means unrestricted; a replaceable baseline supplies defaults | Accepted |
| [1038](./1038-setting-merge-lattice-duals.md) | Setting merge is a policy of lattice duals — ceiling, floor, or override | Accepted |
| [1040](./1040-effective-settings-preview-endpoints.md) | Effective-settings preview endpoints for tenant, client, and server | Accepted |
| [1083](./1083-secure-by-default-discovery-metadata.md) | Secure-by-default discovery metadata, overridable by operators | Accepted |
| [1108](./1108-fapi-security-profile-merge-lattice-ceiling-injection.md) | FAPI security profile via merge-lattice ceiling injection | Accepted |
| [1109](./1109-fapi-behavioral-rules-and-discovery-validation-details.md) | FAPI 2.0 behavioral rules and discovery/validation details | Accepted |
| [1139](./1139-setting-names-are-a-closed-vocabulary.md) | Setting names are a closed vocabulary, and configuration the server cannot honor fails loudly | Accepted |
| [1140](./1140-setting-values-are-validated-against-their-domain.md) | Setting values are validated against their domain, not only their type | Accepted |
| [1148](./1148-security-profiles-accumulate-down-the-settings-cascade.md) | Security profiles accumulate down the settings cascade | Accepted |
| [1149](./1149-settings-declare-the-scopes-they-apply-to.md) | Settings declare the scopes they apply to | Accepted |
| [1202](./1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md) | FAPI 1.0 Advanced is a second registered security profile | Accepted |
| [1204](./1204-a-scopes-effective-settings-are-resolved-by-one-service.md) | A scope's effective settings are resolved by one service | Accepted |
| [1206](./1206-a-clients-settings-are-checked-by-registered-coherence-rules.md) | A client's settings are checked by registered coherence rules | Accepted |
| [1219](./1219-server-settings-converge-on-every-replica-through-one-reconciliation.md) | Server settings converge on every replica through one reconciliation | Accepted |
| [1233](./1233-the-management-api-lists-a-tenants-incoherent-clients.md) | The management API lists a tenant's incoherent clients | Accepted |
| [1234](./1234-a-server-settings-reconciliation-reaches-only-the-tenants-its-change-affects.md) | A server settings reconciliation reaches only the tenants its change affects | Accepted |
| [1240](./1240-a-client-value-that-a-ceiling-lowers-is-incoherent.md) | A client value that a ceiling lowers is incoherent | Accepted |
| [1241](./1241-a-settings-write-is-not-refused-for-a-stored-document-it-cannot-read.md) | A settings write is not refused for a stored document it cannot read | Accepted |
| [1245](./1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md) | A management request answers 409 for a stored settings document it cannot honor | Accepted |
| [1251](./1251-a-retired-setting-stays-registered-until-a-later-release-removes-it.md) | A retired setting stays registered until a later release removes it | Superseded |
| [1289](./1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md) | A tenant declares its environment type, which selects its guardrails | Accepted |
| [1396](./1396-until-the-first-stable-release-a-setting-is-renamed-removed-or-narrowed-outright.md) | Until the first stable release, a setting is renamed, removed or narrowed outright | Accepted |
| [1412](./1412-settings-are-reconciled-per-team.md) | Settings are reconciled per team | Accepted |
| [1416](./1416-a-replica-that-may-have-missed-a-team-announcement-evicts-only-the-teams-whose-row-changed.md) | A replica that may have missed a team announcement evicts only the teams whose row changed | Accepted |
| [1418](./1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md) | A limit is set by the authority above the scope whose document carries it | Accepted |
| [1448](./1448-a-settings-reader-learns-which-limits-it-may-change.md) | A settings reader learns which limits the settings carry and whether it may change them | Accepted |
| [1454](./1454-a-replica-that-may-have-missed-a-tenant-announcement-evicts-only-the-tenants-whose-row-changed.md) | A replica that may have missed a tenant announcement evicts only the tenants whose row changed | Accepted |

### Tenancy, persistence and replicas

| ADR | Title | Status |
| --- | --- | --- |
| [1017](./1017-tenant-resolution-shared-abstraction-and-management-boundary.md) | Tenant resolution is a shared abstraction; the management API has an optional tenant boundary | Superseded |
| [1018](./1018-tenant-scoped-data-access-at-the-persistence-layer.md) | Tenant selection is shared; tenant-bound data access is scoped at the persistence layer | Accepted |
| [1024](./1024-control-plane-and-per-tenant-planes.md) | Deployment model: one control plane with logical tenant isolation; physical isolation is a separate deployment | Accepted |
| [1036](./1036-unified-openid-request-environment.md) | A single OpenID request environment for every HTTP surface | Accepted |
| [1037](./1037-tenant-cache-probe-before-store-load.md) | The tenant cache is probed before the store is read on the resolution hot path | Accepted |
| [1045](./1045-tenant-provisioning-and-seeding-pipeline.md) | Tenant provisioning and seeding are one mediator-driven pipeline | Accepted |
| [1048](./1048-durable-audit-delivery-outbox.md) | Durable audit delivery: an outbox with at-least-once, publish-after-commit semantics | Proposed |
| [1053](./1053-host-configured-hooks-on-the-openid-environment.md) | Host-configured behavioral hooks are surfaced on the OpenID environment | Accepted |
| [1090](./1090-durable-outbound-delivery-dispatch-adapter.md) | Durable outbound delivery via a generic job-dispatch adapter (BYO orchestration) | Accepted |
| [1100](./1100-explicit-tenant-context-materialization.md) | Explicit-tenant OpenID context materialization for admin operations | Accepted |
| [1129](./1129-the-tenant-issuer-is-the-single-authoritative-origin.md) | The tenant's issuer is the single authoritative origin | Accepted |
| [1138](./1138-protocol-endpoints-are-served-under-the-tenant-route.md) | The protocol endpoints are served under the tenant route, and an issuer is its tenant's base address | Accepted |
| [1159](./1159-a-tenant-is-named-by-its-creator.md) | A tenant is named by its creator | Accepted |
| [1160](./1160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md) | A tenant's address is validated by the strategy that serves it, wherever it is written | Accepted |
| [1164](./1164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md) | Replicas share single-use state through an atomic expiring key set, backed by Valkey | Accepted |
| [1165](./1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md) | Every served tenant has an issuer, and its strategy supplies the default | Accepted |
| [1166](./1166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md) | A tenant is provisioned through one pipeline under every strategy | Accepted |
| [1189](./1189-a-retired-tenant-identifier-or-issuer-is-never-reissued.md) | A retired tenant identifier or issuer is never reissued | Accepted |
| [1195](./1195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md) | A tenant's mTLS endpoint alias is a routed tenant property | Accepted |
| [1196](./1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md) | Tenants are rate limited through a shared expiring counter set | Accepted |
| [1198](./1198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md) | Plan-tier quotas are daily UTC counts enforced at the quota | Accepted |
| [1199](./1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md) | Tenant usage is a daily ledger that replicas add to | Accepted |
| [1200](./1200-a-tier-admits-a-bounded-overage-past-its-quota-and-bills-it-from-the-ledger.md) | A tier admits a bounded overage past its quota and bills it from the ledger | Accepted |
| [1205](./1205-replicas-announce-changes-through-a-broadcast-port.md) | Replicas announce changes through a broadcast port | Accepted |
| [1209](./1209-a-replica-adds-each-usage-batch-to-the-ledger-once.md) | A replica adds each usage batch to the ledger once | Accepted |
| [1213](./1213-the-usage-ledger-is-pruned-in-bounded-batches.md) | The usage ledger is pruned in bounded batches that every provider supports | Accepted |
| [1232](./1232-the-in-process-dispatcher-runs-jobs-concurrently-up-to-a-limit.md) | The in-process dispatcher runs jobs concurrently up to a limit | Accepted |
| [1242](./1242-the-in-process-dispatcher-is-configured-under-the-openid-section.md) | The in-process dispatcher is configured under the OpenID section | Accepted |
| [1246](./1246-rate-limits-are-registered-descriptors-judged-in-one-round-trip-per-stage.md) | Rate limits are registered descriptors judged in one round trip per stage | Accepted |
| [1267](./1267-replicas-share-versioned-expiring-values-and-membership-indexes.md) | Replicas share versioned expiring values and membership indexes | Accepted |
| [1351](./1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md) | A committed change is applied and announced on its own bounded token | Accepted |
| [1411](./1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md) | The platform is the deployment root and a team groups a customer's tenants | Accepted |
| [1425](./1425-a-team-promotes-one-tenants-configuration-to-another-and-environment-bound-values-stay.md) | A team promotes one tenant's configuration to another, and environment-bound values stay | Accepted |
| [1426](./1426-a-team-can-nest-beneath-a-parent-team.md) | A team can nest beneath a parent team | Accepted |
| [1455](./1455-a-teams-subtree-is-walked-depth-first-one-page-of-child-teams-per-level.md) | A team's subtree is walked depth first, one page of child teams per level | Accepted |
| [1463](./1463-a-seeded-tenant-is-materialized-from-the-row-its-seed-committed.md) | A seeded tenant is materialized from the row its seed committed | Accepted |
| [1464](./1464-a-management-audit-record-runs-on-its-own-bounded-token-and-takes-no-request-token.md) | A management audit record runs on its own bounded token and takes no request token | Accepted |
| [1468](./1468-a-security-effect-that-follows-a-committed-change-runs-on-its-own-bounded-token.md) | A security effect that follows a committed change runs on its own bounded token | Accepted |

### Management API

| ADR | Title | Status |
| --- | --- | --- |
| [1009](./1009-endpoint-families-own-their-route-group.md) | Endpoint families own their route group and cross-cutting endpoint filters | Accepted |
| [1013](./1013-management-precondition-mediator-pipeline.md) | Management preconditions are a mediator validation pipeline | Superseded |
| [1014](./1014-server-generated-opaque-resource-ids.md) | Resource identifiers are server-generated and opaque | Accepted |
| [1015](./1015-management-core-validation-via-validators.md) | Management core validation via replaceable validators | Accepted |
| [1019](./1019-collection-endpoints-keyset-pagination.md) | Collection endpoints use keyset (cursor) pagination, not a query protocol | Accepted |
| [1020](./1020-grant-management-read-surface.md) | Grant administration is a tenant-scoped, metadata-only read surface with opaque ids | Accepted |
| [1022](./1022-grant-filters-and-bulk-revocation.md) | Grant administration gains specific subject/client filters and bulk revocation | Accepted |
| [1031](./1031-control-plane-management-resource-server-and-root-tenant-seeding.md) | The control-plane management resource server is a planed system resource server seeded only into the root tenant | Accepted |
| [1033](./1033-authorization-model-scopes-roles-and-ownership.md) | Authorization model: granular scopes, roles as bundles, and resource-scoped assignments (ownership) | Accepted |
| [1034](./1034-persisted-role-assignments-and-ownership.md) | Persisted resource-scoped role assignments and ownership | Accepted |
| [1042](./1042-management-api-route-driven-tenant-scope.md) | The management API is plane-aware; tenant-bound resources nest under the tenant and drive the ambient scope from the route | Accepted |
| [1043](./1043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md) | The hierarchical endpoint-group mechanism is a cross-cutting package layered on the registration builder | Accepted |
| [1044](./1044-management-api-authentication-and-globaladmin-bootstrap.md) | The management API authenticates self-issued bearer tokens, and the first GlobalAdmin is bootstrapped from configuration | Accepted |
| [1046](./1046-api-reference-openapi-document-and-scalar.md) | The API reference (OpenAPI document + Scalar) is owned by the composition layer and turnkey for every environment | Accepted |
| [1067](./1067-secret-type-capability-catalog-and-capabilities-discovery.md) | Secret-type capability catalog and composable management capabilities discovery | Accepted |
| [1086](./1086-canonical-openid-endpoint-shape.md) | Canonical OpenID endpoint shape: a thin endpoint over a strongly-typed message and a mediator pipeline | Accepted |
| [1191](./1191-the-management-api-authenticates-against-the-control-plane-tenant.md) | The management API authenticates against the control-plane tenant | Accepted |
| [1214](./1214-a-management-write-that-loses-a-race-is-answered-412.md) | A management write that loses a concurrency race is answered 412 | Accepted |
| [1257](./1257-the-management-console-reaches-every-owners-secrets-and-settings-through-one-endpoint-per-operation.md) | The management console reaches every owner's secrets and settings through one endpoint per operation | Accepted |
| [1417](./1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md) | A tenant administrator is a role assignment at the tenant's node | Accepted |
| [1419](./1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md) | A client acting for itself is a principal that holds role assignments | Accepted |
| [1420](./1420-a-callers-teams-and-tenants-are-discovered-from-its-role-assignments.md) | A caller's teams and tenants are discovered from its role assignments | Accepted |
| [1421](./1421-the-management-console-addresses-a-client-and-a-resource-server-through-their-tenant.md) | The management console addresses a client and a resource server through their tenant | Accepted |
| [1422](./1422-a-management-problem-that-asks-the-caller-to-wait-carries-the-wait-in-retry-after-seconds.md) | A management problem that asks the caller to wait carries the wait in retryAfterSeconds | Accepted |
| [1424](./1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md) | A management route authorizes the caller at the node it names before it reads | Accepted |
| [1449](./1449-every-role-assignment-the-management-api-grants-or-revokes-is-audited.md) | Every role assignment the management API grants or revokes is audited | Accepted |
| [1450](./1450-a-client-credentials-access-token-names-the-clients-principal-as-its-subject.md) | A client-credentials access token names the client's principal as its subject | Accepted |
| [1451](./1451-every-management-route-authorizes-at-its-node-before-it-reads.md) | Every management route authorizes at its node before it reads | Accepted |
| [1465](./1465-a-role-list-tells-its-reader-whether-it-may-grant-and-revoke-the-role.md) | A role list tells its reader whether it may grant and revoke the role | Accepted |
| [1466](./1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md) | A machine client belongs to the platform or a team and acts only within it | Accepted |
| [1467](./1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md) | Custody of a machine client needs creation authority at the node that owns it | Accepted |
| [1469](./1469-every-role-list-shares-role-neutral-contracts-and-one-grant-and-revoke-flow.md) | Every role list shares role-neutral contracts and one grant and revoke flow | Accepted |
| [1470](./1470-a-callers-acting-client-is-resolved-once-per-request.md) | A caller's acting client is resolved once per request | Accepted |
| [1480](./1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md) | A management route answers from what it read and declares every problem it answers | Accepted |
| [1481](./1481-a-management-routes-problem-responses-say-when-each-occurs.md) | A management route's problem responses say when each occurs | Accepted |
| [1482](./1482-only-an-authenticated-management-caller-reads-the-management-capabilities.md) | Only an authenticated management caller reads the management capabilities | Accepted |
| [1483](./1483-a-management-request-whose-binding-fails-is-answered-as-a-problem-its-route-declares.md) | A management request whose binding fails is answered as a problem its route declares | Accepted |
| [1484](./1484-a-management-routes-caller-is-the-principal-its-management-access-token-authenticates.md) | A management route's caller is the principal its management access token authenticates | Accepted |

### Protocol endpoints, grants and tokens

| ADR | Title | Status |
| --- | --- | --- |
| [1023](./1023-scopes-and-resources-management-model.md) | Scopes and resources: an Auth0-style Resource Server owns its scopes; a Client Grant authorizes a client | Accepted |
| [1026](./1026-scope-enforcement-via-resource-servers-and-client-grants.md) | Scope enforcement is driven by resource servers and client grants, not a `scopes_supported` setting | Accepted |
| [1027](./1027-access-token-audience-from-resource-servers.md) | An access token's audience is the resource servers that own its scopes | Accepted |
| [1028](./1028-token-revocation-refresh-tokens-only.md) | Token revocation revokes refresh tokens only; stateless access tokens expire | Accepted |
| [1029](./1029-token-introspection-endpoint.md) | Token introspection resolves JWT access tokens and refresh-token grants | Accepted |
| [1030](./1030-userinfo-endpoint-and-subject-authentication-seam.md) | UserInfo is backed by a reusable subject-authentication seam; claims come from enrichers | Accepted |
| [1039](./1039-claims-request-parameter.md) | The OpenID Connect `claims` request parameter is honored end-to-end | Accepted |
| [1059](./1059-device-authorization-grant.md) | Device Authorization Grant (RFC 8628) over the persisted-grant store | Accepted |
| [1063](./1063-dynamic-client-registration.md) | Dynamic Client Registration (RFC 7591/7592) over the persisted-grant store | Accepted |
| [1066](./1066-resource-server-settings-and-secret-owner.md) | Resource servers are first-class settings and secret owners | Accepted |
| [1074](./1074-session-management-and-logout.md) | End-user sessions, the `sid` claim, and RP-Initiated + Back-Channel Logout | Accepted |
| [1075](./1075-runtime-consumption-of-resource-server-secrets.md) | Runtime consumption of resource-server secrets and settings at access-token issuance | Accepted |
| [1076](./1076-consent-persistence-and-enforcement.md) | Durable consent — a first-class persisted aggregate, enforced at authorize | Accepted |
| [1077](./1077-session-scoped-grant-revocation-on-logout.md) | Logout revokes the terminating session's grants, not the subject's | Accepted |
| [1078](./1078-resource-indicators-rfc-8707.md) | Resource indicators (RFC 8707) bind and narrow the access-token audience | Accepted |
| [1079](./1079-extensible-pkce-code-challenge-methods.md) | Extensible PKCE `code_challenge_method` transformations | Accepted |
| [1081](./1081-granular-consent-and-authorized-applications.md) | Granular per-scope consent and the self-service authorized-applications surface | Accepted |
| [1085](./1085-ciba-grant.md) | CIBA grant (OpenID Connect Client-Initiated Backchannel Authentication) over the persisted-grant store | Accepted |
| [1087](./1087-refresh-token-reuse-detection-and-family-revocation.md) | Refresh-token reuse detection revokes the whole rotation family | Accepted |
| [1088](./1088-opt-in-front-channel-logout-and-session-management.md) | Opt-in Front-Channel Logout and Session Management (available, disabled by default) | Accepted |
| [1089](./1089-pushed-authorization-requests.md) | Pushed Authorization Requests (PAR, RFC 9126) | Accepted |
| [1091](./1091-acr-emission-and-conditional-auth-time.md) | `acr` emission via a replaceable resolver, and conditional `auth_time` | Accepted |
| [1093](./1093-pairwise-subject-identifiers.md) | Pairwise subject identifiers via a stateless, deterministic projection | Accepted |
| [1095](./1095-sector-identifier-uri-fetch-and-ssrf-guard.md) | Registration-time `sector_identifier_uri` fetch and validation with an SSRF-guarded client | Accepted |
| [1096](./1096-end-session-subject-from-server-state.md) | End-session resolves the stable subject from server-owned state, never by reversing a pairwise `sub` | Accepted |
| [1097](./1097-sender-constrained-access-tokens-dpop-and-mtls.md) | Sender-constrained access tokens (DPoP — RFC 9449, mutual-TLS — RFC 8705) | Accepted |
| [1098](./1098-session-lifecycle-termination-seam.md) | Session lifecycle as an observable termination seam | Accepted |
| [1101](./1101-oauth-token-exchange-rfc8693.md) | OAuth 2.0 Token Exchange (RFC 8693) over the extension-grant seam, self-issued-token only | Accepted |
| [1112](./1112-authorization-code-replay-revokes-session.md) | Authorization-code replay revokes the end-user session; first-party resource endpoints enforce it | Accepted |
| [1113](./1113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md) | At-rest grant payloads are schema-versioned, upcastable, and preserve unknown members | Accepted |
| [1114](./1114-non-redirectable-authorization-errors-are-content-negotiated.md) | Non-redirectable authorization errors are content-negotiated via a response-formatter seam | Accepted |
| [1124](./1124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md) | JWT lifetime is capped from its earliest asserted start; client assertions default to five minutes | Accepted |
| [1141](./1141-response-types-are-whole-values.md) | Response types are whole values, enforced exactly and consistent with grant types | Accepted |
| [1142](./1142-listed-response-types-are-the-only-front-channel-token-control.md) | The listed response types are the only control over the tokens the authorization endpoint returns | Accepted |
| [1144](./1144-authorization-codes-are-bound-to-pkce-by-default.md) | Authorization codes are bound to PKCE by default | Accepted |
| [1158](./1158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md) | An access token is bound by its client and every resource server it targets | Accepted |
| [1167](./1167-the-server-validates-the-tokens-it-issued-under-one-policy.md) | The server validates the tokens it issued under one policy | Accepted |
| [1168](./1168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md) | Introspection discloses a token only to the party that consumes it | Accepted |
| [1187](./1187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md) | A signed request object is short-lived and addressed to the issuer | Accepted |
| [1192](./1192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md) | Dynamic registration binds a client to the resource server its initial access token names | Accepted |
| [1201](./1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md) | Authorization responses are delivered by registered response-mode handlers | Accepted |
| [1203](./1203-redirect-uris-are-matched-exactly.md) | Redirect URIs are matched exactly | Accepted |
| [1211](./1211-a-rotated-refresh-token-is-spent-with-its-successor.md) | A rotated refresh token is spent with its successor, and a lost spend is a reuse | Accepted |
| [1212](./1212-deleting-an-owner-removes-what-it-owns.md) | Deleting a client or a tenant removes what it owns, its end users' data included | Accepted |
| [1215](./1215-a-revocation-wins-a-concurrency-race.md) | A revocation wins a concurrency race | Accepted |
| [1216](./1216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md) | A refresh token is rotated unless it is sender-constrained | Accepted |
| [1217](./1217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md) | A rotated refresh token may be retried within a reuse interval | Accepted |
| [1218](./1218-ended-grants-are-removed-after-a-retention-period.md) | Ended grants are removed after a retention period | Accepted |
| [1220](./1220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md) | An end-user session always ends, and its grant expires when it does | Accepted |
| [1222](./1222-a-grants-payload-is-changed-by-a-read-modify-write.md) | A grant's payload is changed by a read-modify-write that loses no concurrent write | Accepted |
| [1223](./1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md) | A session that ends unused is terminated once, in the background | Accepted |
| [1225](./1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md) | Every grant that issues tokens for a subject validates it | Accepted |
| [1227](./1227-a-session-is-terminated-at-most-once-on-every-path.md) | A session is terminated at most once on every path | Accepted |
| [1228](./1228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md) | A session is terminated with the clients it lists when claimed, and a replayed code terminates it | Accepted |
| [1231](./1231-a-ping-or-push-client-is-told-when-its-ciba-request-is-denied.md) | A ping or push client is told when its CIBA request is denied | Accepted |
| [1297](./1297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md) | A refresh token family expires at a fixed horizon that the token response announces | Accepted |
| [1314](./1314-protocol-endpoints-keep-the-http-methods-their-specifications-require.md) | Protocol endpoints keep the HTTP methods their specifications require | Accepted |
| [1380](./1380-a-logout-without-a-valid-id-token-hint-for-the-current-session-is-confirmed.md) | A logout without a valid `id_token_hint` for the current session is confirmed by the end-user | Accepted |
| [1395](./1395-a-stored-record-that-lacks-what-the-serving-code-writes-is-malformed-and-fails-closed.md) | A stored record that lacks what the serving code writes is malformed and fails closed | Accepted |
| [1401](./1401-userinfo-access-is-audited-when-a-tenant-or-client-opts-in.md) | UserInfo access is audited when a tenant or client opts in | Accepted |
| [1498](./1498-protocol-endpoints-answer-the-x-fapi-interaction-id-header.md) | Protocol endpoints answer the x-fapi-interaction-id header | Accepted |

### Client authentication and outbound calls

| ADR | Title | Status |
| --- | --- | --- |
| [1092](./1092-token-endpoint-401-www-authenticate-challenge.md) | Token-endpoint `401` + `WWW-Authenticate` challenge and the response-header seam | Accepted |
| [1102](./1102-jwt-bearer-client-authentication.md) | JWT-bearer client authentication (`private_key_jwt` and `client_secret_jwt`) | Accepted |
| [1106](./1106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md) | Runtime `jwks_uri` client-key resolution and a shared, extensible outbound-fetch SSRF guard | Accepted |
| [1107](./1107-mutual-tls-client-authentication.md) | Mutual-TLS client authentication (`tls_client_auth` and `self_signed_tls_client_auth`) | Accepted |
| [1111](./1111-complete-outbound-fetch-ssrf-coverage.md) | Complete outbound-fetch SSRF coverage and bound the `request_uri` fetch | Accepted |
| [1145](./1145-a-client-authenticates-only-with-its-registered-method.md) | A client authenticates only with its registered method | Accepted |
| [1155](./1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md) | JWTs exchanged with a client follow its registered metadata | Accepted |
| [1161](./1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md) | A client's public keys have one source and are imported under one policy | Accepted |
| [1172](./1172-forwarded-client-certificates-are-decoded-by-a-registered-format.md) | Forwarded client certificates are decoded by a registered format | Accepted |
| [1173](./1173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md) | `tls_client_auth` certificates chain to configured trust anchors | Accepted |
| [1176](./1176-dpop-proofs-are-a-jose-building-block.md) | DPoP proofs are a JOSE building block | Accepted |
| [1181](./1181-every-issuer-of-a-jwt-addressed-to-a-client-resolves-one-public-credential-selector.md) | Every issuer of a JWT addressed to a client resolves one public credential selector | Accepted |
| [1186](./1186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md) | Every owner's keys are imported and updated under the client-key policy | Accepted |
| [1207](./1207-client-authentication-methods-are-named-by-registered-descriptors.md) | Client-authentication methods are named by registered descriptors | Accepted |
| [1252](./1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md) | A revocation list retrieval is shared, a failed one is remembered, and soft-fail is opt-in | Accepted |
| [1259](./1259-revocation-is-checked-by-registered-sources-ocsp-before-every-crl-form.md) | Revocation is checked by registered sources, OCSP before every CRL form | Accepted |
| [1315](./1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md) | Outbound calls to caller-supplied URLs share one guarded, bounded client | Accepted |
| [1335](./1335-a-clients-jwks-uri-is-fetched-single-flight-and-a-failed-fetch-is-remembered.md) | A client's `jwks_uri` is fetched single-flight and a failed fetch is remembered | Accepted |
| [1350](./1350-a-shared-fetch-is-single-flight-through-one-helper.md) | A shared fetch is single-flight through one helper | Accepted |
| [1365](./1365-the-bff-and-the-login-redirect-take-no-url-from-the-requests-host.md) | The BFF and the login redirect take no URL from the request's Host | Accepted |
| [1366](./1366-the-revocation-retrieval-checks-its-caches-again-inside-the-shared-retrieval.md) | The revocation retrieval checks its caches again inside the shared retrieval | Accepted |
| [1387](./1387-the-client-assertion-audience-and-the-dpop-htu-are-compared-with-the-tenants-published-urls.md) | The client-assertion audience and the DPoP `htu` are compared with the tenant's published URLs | Accepted |
| [1399](./1399-failed-client-authentications-are-limited-per-remote-address-by-default.md) | Failed client authentications are limited per remote address by default | Accepted |
| [1400](./1400-the-revocation-sources-are-described-and-configuration-selects-and-orders-them.md) | The revocation sources are described, and configuration selects and orders them | Accepted |
| [1497](./1497-a-registered-key-set-names-a-key-without-a-kid-by-its-thumbprint.md) | A registered key set names a key without a kid by its thumbprint | Accepted |

### Secrets and keys

| ADR | Title | Status |
| --- | --- | --- |
| [1011](./1011-secret-management-api.md) | Secret management: server-side key generation, no material on the surface | Accepted |
| [1050](./1050-secret-material-revealed-once-on-create.md) | Secret material is revealed once, only by the client-secret create endpoint | Accepted |
| [1058](./1058-asymmetric-public-key-material-read-path.md) | Asymmetric public key material is exposed through a dedicated read endpoint (JWK + PEM) | Accepted |
| [1068](./1068-unified-extensible-elliptic-curve-catalog.md) | Unified, extensible elliptic-curve catalog | Accepted |
| [1103](./1103-public-only-asymmetric-secret-keys.md) | Public-only (verify-only) asymmetric secret keys | Accepted |
| [1104](./1104-import-public-asymmetric-secret-material.md) | Import of caller-supplied public asymmetric secret material (BYOK) | Accepted |
| [1105](./1105-secret-key-type-descriptor-first-class-seam.md) | The secret-key-type descriptor is the first-class seam for every key operation | Accepted |
| [1110](./1110-shared-secret-vs-cryptographic-key-storage.md) | A shared secret is stored as the UTF-8 bytes of its opaque string, distinct from a cryptographic key | Accepted |
| [1115](./1115-asymmetric-keys-model-der-material-via-buffer-writers.md) | Asymmetric secret keys model public material as DER via buffer writers (PQC-ready) | Accepted |
| [1169](./1169-every-tenant-is-provisioned-with-persisted-keys.md) | Every tenant is provisioned with persisted keys | Accepted |
| [1183](./1183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md) | A client secret is rotated by replacing it in one change | Superseded |
| [1235](./1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md) | Stored secrets are protected by a key hierarchy under an external root key | Accepted |
| [1260](./1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md) | Azure Key Vault and Google Cloud KMS are root key providers | Accepted |
| [1265](./1265-grant-payloads-are-protected-under-the-tenant-master-key.md) | Grant payloads are protected under the tenant master key | Accepted |
| [1268](./1268-a-generated-private-key-stores-its-public-key-beside-it.md) | A generated private key stores its public key beside it | Accepted |
| [1279](./1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md) | Key wrapping is a secrets abstraction shared by the root and recovery wrappers | Accepted |
| [1320](./1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md) | A rotated client secret overlaps its replacement until it expires or is deleted | Accepted |
| [1345](./1345-a-response-that-reveals-a-secret-is-marked-uncacheable-by-its-endpoint.md) | A response that reveals a secret is marked uncacheable by its endpoint | Accepted |
| [1391](./1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md) | A client secret that is only compared is stored as a one-way verifier | Accepted |
| [1403](./1403-a-stored-secret-has-an-active-window-inside-its-validity-window-and-every-use-of-a-key-honors-them.md) | A stored secret has an active window inside its validity window, and every use of a key honors them | Accepted |
| [1404](./1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md) | A tenant's provisioned keys roll over ahead of retirement and expire after a grace | Accepted |
| [1407](./1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md) | A master key rotates, its data keys are rewrapped in the background, and it is destroyed once nothing names it | Accepted |
| [1408](./1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md) | An offline host command rewraps every master key under the configured root and recovery keys | Accepted |
| [1414](./1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md) | A tenant can wrap its master keys under a root key it controls | Accepted |
| [1415](./1415-a-key-wrapper-can-wrap-under-a-key-the-caller-names.md) | A key wrapper can wrap under a key the caller names | Accepted |
| [1427](./1427-an-imported-elliptic-curve-point-is-checked-against-its-curve-before-the-platform-imports-it.md) | An imported elliptic-curve point is checked against its curve before the platform imports it | Accepted |
| [1429](./1429-a-tenant-root-key-can-be-reached-through-an-identity-the-tenant-grants.md) | A tenant root key can be reached through an identity the tenant grants | Accepted |
| [1471](./1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md) | A tenant's signing keys can be held in a key management service that signs every token | Accepted |
| [1472](./1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md) | Signing is asynchronous from the signature algorithm to every caller | Accepted |
| [1473](./1473-a-key-management-service-adapter-package-is-named-for-its-service.md) | A key management service's adapter package is named for its service | Accepted |
| [1474](./1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md) | A remote key provider is described by data, creates a key once and signs into the caller's buffer | Accepted |
| [1475](./1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md) | The management API creates a tenant's key in a key management service and binds one that proves it signs | Accepted |
| [1476](./1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md) | A bound KMS key's public half is read from its service, and the console offers each provider's algorithms | Accepted |
| [1477](./1477-a-tenants-encryption-keys-can-be-held-in-a-key-management-service-that-decrypts-every-content-key.md) | A tenant's encryption keys can be held in a key management service that decrypts every content key | Accepted |
| [1478](./1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md) | A background job destroys the key management service keys that no stored secret names | Accepted |
| [1479](./1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md) | A protocol client's and a BFF's keys can be held in a key management service, read and proved at start | Accepted |
| [1489](./1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md) | A remote key provider registers without a key-creation location, and only one that has one creates and sweeps keys | Accepted |

### JOSE, JWT, COSE and the protocol client

| ADR | Title | Status |
| --- | --- | --- |
| [1117](./1117-eddsa-clean-room-implementation-opt-in-package.md) | EdDSA (Ed25519 / Ed448) is a clean-room implementation in an opt-in package | Accepted |
| [1118](./1118-ml-dsa-post-quantum-signatures-in-core.md) | ML-DSA (post-quantum) signatures ship in the core libraries via the BCL | Accepted |
| [1119](./1119-first-class-jwk-jwks-and-thumbprint.md) | First-class JWK / JWKS / thumbprint live in a dedicated logic-bearing package | Accepted |
| [1120](./1120-jwt-validation-requirements-and-handlers.md) | JWT validation is modeled as ASP.NET Core-style requirements and handlers | Accepted |
| [1121](./1121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md) | Claim dates parse only by declared type, through the shared `DateCoercion` policy | Accepted |
| [1122](./1122-jwt-claim-values-map-to-json-by-declared-type.md) | JWT claim values map to and from JSON by declared type, in one symmetric mapper | Accepted |
| [1123](./1123-numeric-date-is-read-strictly-and-range-guarded.md) | RFC 7519 NumericDate is read strictly, range-guarded, through `DateCoercion` | Accepted |
| [1126](./1126-ecdh-es-concat-kdf-digest-is-sha-256-and-oracles-must-be-rfc-conforming.md) | The ECDH-ES Concat KDF digest is SHA-256, and third-party oracles must be RFC-conforming | Accepted |
| [1154](./1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md) | JWTs are validated as signed tokens under explicit algorithm and key policy | Accepted |
| [1270](./1270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md) | The OpenID protocol client uses the family's JOSE stack and named key rings | Accepted |
| [1271](./1271-a-key-type-registers-its-json-web-key-rules.md) | A key type registers its JSON Web Key rules | Accepted |
| [1272](./1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md) | Signature algorithms are fully specified and bound to their curve | Accepted |
| [1273](./1273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md) | The protocol client validates ID tokens under an asymmetric algorithm policy | Accepted |
| [1274](./1274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md) | The protocol client validates logout tokens and rejects replays | Accepted |
| [1275](./1275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md) | The protocol client pushes authorization requests whenever the provider offers PAR | Accepted |
| [1276](./1276-the-oauth-and-openid-connect-wire-vocabulary-lives-in-a-dependency-free-package.md) | The OAuth and OpenID Connect wire vocabulary lives in a dependency-free package | Accepted |
| [1277](./1277-a-whole-number-json-value-is-accepted-with-a-zero-fraction.md) | A whole-number JSON value is accepted with a zero fraction | Accepted |
| [1278](./1278-token-validation-failures-carry-a-machine-readable-code.md) | Token validation failures carry a machine-readable code | Accepted |
| [1280](./1280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md) | The protocol client decrypts ID and logout tokens with its key ring | Accepted |
| [1281](./1281-the-jwk-package-takes-contributed-key-types-and-hash-names-from-its-caller.md) | The JWK package takes contributed key types and hash names from its caller | Accepted |
| [1282](./1282-the-jose-library-does-not-implement-rsa1-5.md) | The JOSE library does not implement RSA1_5 | Accepted |
| [1284](./1284-a-logout-token-is-recorded-only-after-the-client-handles-it.md) | A logout token is recorded only after the client handles it | Accepted |
| [1285](./1285-the-openid-protocol-client-needs-no-aspnet-core-runtime.md) | The OpenID protocol client needs no ASP.NET Core runtime | Accepted |
| [1286](./1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md) | The protocol client binds tokens with DPoP whenever the provider supports it | Accepted |
| [1288](./1288-the-protocol-client-authenticates-with-a-tls-client-certificate-from-its-key-ring.md) | The protocol client authenticates with a TLS client certificate from its key ring | Accepted |
| [1295](./1295-the-jose-library-does-not-implement-the-none-algorithm.md) | The JOSE library does not implement the none algorithm | Accepted |
| [1296](./1296-a-failed-jwe-key-unwrap-is-indistinguishable-from-a-failed-authentication-tag.md) | A failed JWE key unwrap is indistinguishable from a failed authentication tag | Accepted |
| [1299](./1299-userinfo-is-read-as-json-under-the-familys-claim-mapping-and-merged-once-at-login.md) | UserInfo is read as JSON under the family's claim mapping and merged once at login | Accepted |
| [1300](./1300-an-asymmetric-key-carries-its-issuers-certificates-and-a-tls-client-presents-them.md) | An asymmetric key carries its issuers' certificates, and a TLS client presents them | Accepted |
| [1301](./1301-a-signed-userinfo-response-is-validated-like-an-id-token-under-the-clients-registered-algorithms.md) | A signed UserInfo response is validated like an ID token under the client's registered algorithms | Accepted |
| [1336](./1336-the-protocol-client-bounds-its-calls-with-a-polly-core-pipeline.md) | The protocol client bounds its calls with a Polly.Core pipeline | Accepted |
| [1389](./1389-one-algorithm-name-serves-every-key-type-it-declares.md) | One algorithm name serves every key type it declares | Accepted |
| [1390](./1390-x25519-and-x448-agree-keys-beside-the-okp-signing-keys-and-a-curves-use-separates-them.md) | X25519 and X448 agree keys beside the OKP signing keys, and a curve's use separates them | Accepted |
| [1392](./1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md) | ES256K is an opt-in package that registers a curve and an ECDSA algorithm as data | Accepted |
| [1398](./1398-a-key-size-selects-a-curve-only-when-no-other-curve-shares-it.md) | A key size selects a curve only when no other curve shares it, and only a symmetric key is checked by its type and size | Accepted |
| [1405](./1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md) | Selective Disclosure JWTs are an opt-in package that issues, presents and verifies over the JWT validator | Accepted |
| [1406](./1406-the-sd-jwt-vc-profile-is-data-the-issuer-and-the-verifier-apply.md) | The SD-JWT VC profile is data the issuer and the verifier apply | Accepted |
| [1409](./1409-cose-messages-are-an-opt-in-package-family-over-system-formats-cbor-and-the-familys-keys-and-algorithms.md) | COSE messages are an opt-in package family over System.Formats.Cbor and the family's keys and algorithms | Accepted |
| [1410](./1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md) | A CWT is validated fail-closed, and its claims are read only after its COSE layer verifies | Accepted |

### Accounts, interactive login and audit

| ADR | Title | Status |
| --- | --- | --- |
| [1035](./1035-federated-principals-and-identity-resolution.md) | Federated principals: one actor, many connection-identities, with a server-owned stable id | Accepted |
| [1047](./1047-auditing-and-events-observation-seam.md) | Auditing and events: a dedicated observation seam, distinct from the mediator | Accepted |
| [1049](./1049-openid-errors-audited-from-single-funnel.md) | OpenID protocol errors are audited once from a single endpoint-filter funnel | Accepted |
| [1051](./1051-local-accounts-behind-a-pluggable-account-source-seam.md) | Local accounts behind a pluggable account-source seam | Accepted |
| [1054](./1054-local-account-metadata-projection.md) | Local-account metadata projects into tokens through the subject-claims pipeline | Accepted |
| [1055](./1055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md) | Subject metadata is a principal-level concept, resolved at issuance | Accepted |
| [1057](./1057-local-account-eager-provisioning-and-management-seam.md) | Local accounts are provisioned eagerly with their principal, and the provisioner is the account write seam | Accepted |
| [1060](./1060-local-account-one-store-seam-and-dependency-inverted-authentication.md) | Local accounts: one store seam, dependency-inverted authentication capability, no owners | Accepted |
| [1061](./1061-local-account-management-is-an-opt-in-capability.md) | Local-account management is an opt-in capability that plugs into the Management slice | Accepted |
| [1062](./1062-opt-in-interactive-login-consent-ui.md) | An opt-in interactive login/consent UI for the OpenID Provider | Accepted |
| [1065](./1065-interactive-login-consent-op-hosted-auth-surface.md) | Interactive login/consent as the OP-hosted `apps/auth` surface | Accepted |
| [1070](./1070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md) | Interactive login/consent is a leaf capability package with a pluggable credential verifier | Accepted |
| [1094](./1094-resolve-principal-at-every-authentication-entry-point.md) | Resolve the principal at every authentication entry point; `SubjectAuthentication.PrincipalId` | Accepted |
| [1182](./1182-the-frontend-bootstrap-issuer-is-the-serving-tenants-not-the-requests.md) | The frontend bootstrap issuer is the serving tenant's, not the request's | Accepted |
| [1193](./1193-the-interactive-surface-is-served-under-the-tenant-route.md) | The interactive surface is served under the tenant route, and its session is scoped to the tenant's path | Accepted |
| [1194](./1194-a-tenant-brands-its-interactive-surface-through-settings.md) | A tenant brands its interactive surface through settings | Accepted |
| [1224](./1224-deleting-or-disabling-an-account-ends-its-sessions.md) | Deleting or disabling an account ends its sessions, and every token request checks the account | Accepted |
| [1226](./1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md) | An administrator can block any end user, and a password reset ends the account's sessions | Accepted |
| [1229](./1229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md) | A refused end user cannot sign in, and the client is answered access_denied | Accepted |
| [1230](./1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md) | An end user changes their password and keeps only the current session | Accepted |
| [1236](./1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md) | A password is judged by one policy wherever it is set | Accepted |
| [1237](./1237-failed-password-attempts-are-limited-per-account.md) | Failed password attempts are limited per account | Accepted |
| [1238](./1238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md) | A password is judged, hashed and verified in its NFC form | Accepted |
| [1239](./1239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md) | A password is verified in the same time whether or not its account exists | Accepted |
| [1243](./1243-every-aspnet-core-identity-path-hashes-and-verifies-the-nfc-form-of-a-password.md) | Every ASP.NET Core Identity path hashes and verifies the NFC form of a password | Accepted |
| [1244](./1244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md) | A password refused at the attempt limit costs what a wrong one costs | Accepted |
| [1253](./1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md) | The interactive bundle mounts at once over the page the host rendered | Accepted |
| [1254](./1254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md) | The interactive bundle's hashed files are served immutable without resolving the tenant | Accepted |
| [1283](./1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md) | Audit records are CloudTrail-shaped JSON with an indexable envelope | Accepted |
| [1303](./1303-audit-records-are-kept-in-an-indexed-store-that-each-replica-writes-in-batches.md) | Audit records are kept in an indexed store that each replica writes in batches | Accepted |
| [1310](./1310-the-interactive-surfaces-cookies-are-host-only-and-a-tenants-session-is-named-for-its-path.md) | The interactive surface's cookies are host-only, and a tenant's session is named for its path | Accepted |
| [1312](./1312-the-frontend-bootstrap-api-base-is-root-relative-unless-configured.md) | The frontend bootstrap API base is root-relative unless configured | Accepted |
| [1402](./1402-the-interactive-surface-renders-in-the-locale-the-request-prefers-among-the-registered-ui-locales.md) | The interactive surface renders in the locale the request prefers among the registered UI locales | Accepted |
| [1457](./1457-a-single-page-bundle-is-read-and-served-through-one-general-helper.md) | A single-page bundle is read and served through one general helper | Accepted |
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
