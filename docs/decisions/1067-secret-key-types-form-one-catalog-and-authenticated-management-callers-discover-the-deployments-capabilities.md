# 1067. Secret key types form one catalog, and authenticated management callers discover the deployment's capabilities

- **Status:** Accepted
- **Date:** 2026-11-09
- **Deciders:** NCode Group

## Context

The management API serves clients that build key forms and gate key actions: the administration console and the tools
that sit beside it. They need to know what the deployment can do: which secret types exist, which algorithms it
accepts with which key sizes and curves, and which remote key providers it runs. A client that hard-codes those lists
drifts from the server, and the server has no other surface that tells a client.

The forces:

- **The knowledge has one source.** Key types are registered descriptors
  ([ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md)), so the server already holds what each type can do,
  and a client reads it instead of keeping a copy.
- **The set is open.** A sibling package adds a key type, an algorithm or a remote key provider by registering it, so
  the document that describes them is composed from registered parts, like the OpenID Connect discovery document.
- **The document describes the deployment.** It lists the deployment's secret types, algorithms, curves and registered
  key management services. A caller that reads it learns which key services the deployment runs and which algorithms
  and key sizes it accepts.
- **Its clients are management clients.** They read it after they sign in, so every consumer already holds a
  management access token.
- **OpenID discovery is public for a reason that does not apply here.** Relying parties read the discovery document
  before they hold a token, because they need it to find the token endpoint. No client needs the capabilities document
  before it holds a management access token.

## Decision

**The management API publishes one composable capabilities document, and only an authenticated management caller
reads it.**

1. **The document.** `GET /api/capabilities` returns `ManagementCapabilitiesResource`, a `[JsonExtensionData]`
   document. It is composed, like OpenID discovery, from registered
   `ICommandHandler<DiscoverManagementCapabilitiesCommand>` providers that the context's mediator dispatches. A new
   section is a registered provider, never an edit to the endpoint or the resource type.
2. **The built-in sections.**
   - `secretTypes`, from `SecretTypeCapabilitiesProvider`.
   - `algorithms`, from `AlgorithmCapabilitiesProvider`; each algorithm lists its compatible secret types with the
     key sizes and curves legal for each
     ([ADR-1389](1389-one-algorithm-name-serves-every-key-type-and-curve-it-declares-and-ecdh-es-agreements-are-registered.md)).
   - `eccCurves`, from `EccCurveCapabilitiesProvider`.
   - `remoteKeyProviders`, from `RemoteKeyProviderCapabilitiesProvider`.
3. **Key types are registered descriptors.** The `secretTypes` section reads the key-type catalog of
   [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md), so a new key type is a registered descriptor and
   appears in the document without any other edit.
4. **No client hard-codes secret types.** The management console reads the document to gate public-key export and to
   drive its key forms.
5. **Authentication is the whole requirement.** The handler answers when `IManagementCallerFeature` is present. It
   asks for no role, because tenant administrators and team administrators drive key forms from the document too.
6. **An unauthenticated request gets the authentication-required problem.** The handler otherwise returns
   `ManagementProblems.AuthenticationRequired()`, the same `401` problem document every other management route
   answers when the caller carries no valid access token.
7. **The route declares `401` and no `403`.** The route has no authority check, so it cannot answer `403`. The host
   walk in `ManagementRouteProblemDeclarationTests` names `api/capabilities/get` among the authenticated-caller routes.

The catalog is a strategy collection and the document is a mediator fan-out
([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)).

## Options considered

- **Per-type lists duplicated across the generator, validator, converter and console.** Rejected: each copy can
  drift from the others, and a client cannot discover server behavior.
- **A public document.** Rejected: an anonymous caller could survey the deployment's key management services,
  algorithms and secret types, which is reconnaissance, and no anonymous consumer needs the document.
- **Require the platform `Read` authority.** Rejected: tenant administrators and team administrators drive key forms
  from the document and do not hold a platform authority, so the requirement would lock out the callers the document
  serves.
- **One composable document behind authentication.** Chosen: the document is open to extension by registration and
  reaches exactly the callers it serves.

## Consequences

- Clients discover server behavior instead of hard-coding it, and divergence between the server and the console is
  structurally impossible.
- The document is open for extension (algorithms, curves, remote key providers and future sections) without modifying
  the endpoint.
- An anonymous request to `GET /api/capabilities` is answered with a `401` problem document.
- A management client with any valid management access token reads the document.
- The route's OpenAPI document lists `401` and no `403`, and the host walk keeps that declaration true.

## References

- `GET /api/capabilities`, `ManagementCapabilitiesResource`, `DiscoverManagementCapabilitiesCommand`
- `SecretTypeCapabilitiesProvider`, `AlgorithmCapabilitiesProvider`, `EccCurveCapabilitiesProvider`,
  `RemoteKeyProviderCapabilitiesProvider`
- `ManagementProblems.AuthenticationRequired()`, `IManagementCallerFeature`, `ManagementRouteProblemDeclarationTests`
- [ADR-1104](1104-a-key-type-is-one-registered-descriptor-and-public-keys-are-imported-and-stored-by-encoding.md): the key-type catalog.
- [ADR-1389](1389-one-algorithm-name-serves-every-key-type-and-curve-it-declares-and-ecdh-es-agreements-are-registered.md): the algorithms section.
- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): strategy
  collection and mediator.
- [#707](https://github.com/NCodeGroup/identity/issues/707)
