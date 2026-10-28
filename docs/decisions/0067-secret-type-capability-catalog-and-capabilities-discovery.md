# 0067. Secret-type capability catalog and composable management capabilities discovery

- **Status:** Accepted
- **Date:** 2026-10-11
- **Deciders:** NCode.Identity maintainers

## Context

The knowledge of "what secret types exist and what each can do" was duplicated across the stack as
hard-coded, drift-prone lists:

- `DefaultSecretGenerator` switched on the secret type to generate key material (and mapped ECC sizes to
  curves inline);
- `DefaultSecretValidator` held a `FrozenDictionary` of the valid types and a parallel per-type
  key-size switch that had to be kept in lockstep with the generator;
- `DefaultSecretPublicKeyConverter` switched on the asymmetric key type to export PEM, and decided
  "has a public key" from a CLR type check;
- the management console (frontend) carried its **own** hard-coded `Set(['rsa','ecc','certificate'])`
  to decide which rows could export a public key — a fourth copy that had already drifted (the persisted
  discriminator is `x509`, not `certificate`).

Adding or changing a secret type meant editing four places in two languages, with no single source of
truth and no way for a client to discover server behavior. There was also no management discovery
surface at all, unlike the OpenID Connect discovery document the authorization server already publishes.

## Decision

Introduce one **secret key-type catalog** as the single source of truth, and expose server behavior
through a **composable capabilities discovery document** modeled on OpenID discovery.

**Catalog (core `NCode.Identity.Secrets`).** `ISecretKeyTypeDescriptor` describes a single key type
(keyed by the runtime `SecretKey.KeyType`, i.e. `SecretKeyTypes`): its CLR type, whether it is
asymmetric, whether it exposes an exportable public key, its key-size validation, and its key
generation (`GenerateKeyMaterial`). `ISecretKeyTypeCatalog` aggregates the registered descriptors;
built-in `symmetric`/`rsa`/`ecc` descriptors are registered via `TryAddEnumerable` in
`AddSecretsLibrary`, so a new type is added by registering a descriptor. The generator, validator, and
public-key converter all consult the catalog; the per-type switches are deleted. PEM export moves into
the domain as `AsymmetricSecretKey.ExportAsymmetricAlgorithm()` + a base
`ExportSubjectPublicKeyInfoPem()`, removing the converter's switch entirely.

The catalog lives in the **core** secrets abstractions, not the persistence (DAL) slice: it is
domain/capability logic consumed by secret services **and** by management, whereas the secrets DAL is
restricted to secret services plus management's direct persistence needs. The persisted `x509`
discriminator stays a persistence concern (`SecretTypes`, which now references `SecretKeyTypes` for its
shared values); a certificate secret resolves to an `rsa`/`ecc` runtime key, so the public-key converter
keys capability off the deserialized key's `KeyType` and handles `x509` without a catalog entry for it.

**Discovery (`GET /api/capabilities`).** `ManagementCapabilitiesResource` is a `[JsonExtensionData]`
document composed — exactly like the OpenID discovery document — from registered
`ICommandHandler<DiscoverManagementCapabilitiesCommand>` providers dispatched through the ambient
`openIdContext.Mediator`. The built-in `SecretTypeCapabilitiesProvider` contributes the `secretTypes`
section. A new capability is published by registering a provider, never by editing the endpoint or the
resource type. The management console consumes the document to gate the public-key export action (and
the create form), deleting its hard-coded list.

## Consequences

- One place defines each secret type's taxonomy, validation, generation, and export capability;
  divergence between generator, validator, converter, and UI is structurally impossible.
- Clients discover server behavior instead of hard-coding it; the capabilities document is open for
  extension (algorithms, curves, and future sections) without modifying the endpoint.
- Public API churn is expected and accepted per [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md):
  new `ISecretKeyTypeDescriptor`/`ISecretKeyTypeCatalog`/`SecretKeyGenerationParameters` in the secrets
  abstractions, new capability contracts in the management abstractions, and the removed per-type
  switches.
- The capability/strategy-collection model is consistent with
  [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md): the catalog is a strategy
  collection; the discovery composition is a mediator fan-out.
