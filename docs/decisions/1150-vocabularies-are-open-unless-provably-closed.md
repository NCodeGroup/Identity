# 1150. Vocabularies are open unless provably closed

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

The family exists to be extended easily and without restriction. Its extension points are registrations: a package
adds a strategy to a collection resolved from DI
([ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md)),
contributes descriptors through `ICollectionDataSource<T>` (settings,
[ADR-1139](1139-setting-names-are-a-closed-vocabulary.md)), or registers a named implementation (security profiles,
client-authentication methods, PKCE transformations). The Auth0-parity-plus posture
([ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)) asks
for data-driven, flexible controls, which only holds when a deployment or sibling package can add what the library did
not foresee.

A convention that leaves the choice between an `enum` and open names to the author invites the wrong question: "does
this package own today's members?". That question misleads. Setting scopes modeled as a `[Flags]` enum of the server,
tenant, client and resource server, with their cascade hard-coded in a `switch`, would close the settings framework to
every extension that stores settings for an entity of its own (an organization, a connection), and nothing short of
editing the abstractions package could open it again.

An enum closes a vocabulary in several ways at once: its members are fixed at compile time; a `[Flags]` enum also fixes
the bit space and the meaning of every combination; and code that `switch`es over the members, or encodes their
relationships in a mask or a chain, closes the behavior and the topology even when the names themselves are strings.

## Decision

**Every vocabulary is open by default. A closed construct is allowed only when the vocabulary is provably closed.**

1. **An open vocabulary is names plus registrations.** Its members are `public const string` names in a static class,
   compared ordinally and documented as the known values, so a consumer, deployment or sibling package extends it by
   registering, never by editing the library. When a member carries behavior or relationships, it is registered data or
   a strategy, contributed through `ICollectionDataSource<T>` or a DI strategy collection and resolved by name through
   a catalog that validates the whole set when it is composed (duplicate names, dangling references, cycles). Input
   naming an unregistered member is rejected, not ignored.
2. **An `enum` needs a three-part justification.** The reviewer can state that:
    1. the members are fixed by something that cannot grow without this library changing: a finite enumeration of an
       external specification the library implements in full, the outcome set of a library-owned operation, or a
       mirror of a framework type;
    2. no package other than this one could ever need another member, the question being "could a package that is not
       this one ever need to add one?"; and
    3. a new member would require a change to this library anyway.
3. **Relationships and behavior are never encoded over a vocabulary.** No `[Flags]` over a vocabulary that may grow; no
   `switch`, mask or hard-coded chain for its hierarchy, compatibility or order; no exhaustive `switch` to choose
   behavior. The catalog derives relationships from registered data, and behavior dispatches to the registered
   strategy.
4. **A persisted or wire value is its name.** Never an ordinal, so a node that meets an unknown member fails closed by
   name instead of misreading a number.

### Open vocabularies

These are open, and each stays open:

- `SessionTerminationReasons`: an extension ends a session for its own reason
  ([ADR-1098](1098-session-lifecycle-termination-seam.md)).
- `SecretKeyImportFormats`: each key type declares the formats it parses, because a registered key type parses its own
  formats ([ADR-1104](1104-import-public-asymmetric-secret-material.md)).
- The framework's `HashAlgorithmName` and the `BinaryEncodings` names of registered encoders
  ([ADR-1190](1190-hashing-names-the-framework-algorithm-and-encodings-are-registered.md)).
- Setting scopes: registered `SettingScopeDescriptor` data whose cascade a catalog derives
  ([ADR-1149](1149-settings-declare-the-scopes-they-apply-to.md)), so the settings framework serves any entity an
  extension adds.

### Public enums that pass

Each passes for one reason:

- `AlgorithmType` mirrors the four JOSE algorithm registries of RFC 7518: signature, key management, content encryption
  and compression. The algorithms inside each registry are open names.
- `JoseSerializationFormat` lists the serializations that RFC 7515 and RFC 7516 define.
- `RequestObjectSource` lists the two parameters that RFC 9101 defines for a request object, `request` and
  `request_uri`.
- `CoseMessageType` and `CoseAlgorithmKind` mirror RFC 9052.
- `SerializationFormat` and `CoseKeyParameterEncoding` choose between the library's own converter representations.
- `TenantPlane` follows from the topology: the root tenant is the control plane, and every other tenant is a workload
  tenant.
- `ObjectJsonDateStringHandling`, `ObjectJsonIntegerHandling` and `ObjectJsonRealHandling` are options of the library's
  `ObjectJsonConverter`, and each member is a branch of that converter.
- `PersistedGrantStatus` is never stored. The grant service derives it on read from a grant's revocation and expiry
  instants; a grant type's own lifecycle, such as a pending device code, lives in its payload.

The outcome sets of one library-owned operation each also pass: `PasswordVerificationResult`,
`SecretVerificationResult`, `OutboundHttpReadResult`, `OwnerRemovalResult` and `ClientCertificateRevocationStatus`.

## Options considered

- **Ask "does this package own today's members?".** Rejected: it produced the closed setting-scope design, which shut
  the settings framework to every entity an extension adds.
- **Ban enums from the public surface.** Rejected: a genuinely closed set, such as the outcomes of one operation or a
  mirror of a framework option, gains exhaustiveness checking and type safety from an enum, and modeling it as strings
  would add validation with no extensibility to show for it.
- **Typed name wrappers (`readonly record struct Scope(string Name)`) as the default.** Rejected as a default: they add
  a type per vocabulary without adding openness, and the family's open vocabularies are `const string` names. A wrapper
  remains available where a signature would otherwise confuse two name parameters.
- **Open by default, with closing as a justified exception.** Chosen: it keeps every vocabulary extensible by
  registration and spends an enum only where the three-part test holds.

## Consequences

- A new vocabulary starts open; closing it is the exception a reviewer must be able to justify in three sentences.
- The settings framework is open to new entity types, because setting scopes are registered data with their cascade
  derived by a catalog.
- A vocabulary of registered values is closed to unregistered input, which is a different thing from a closed type: an
  open vocabulary still rejects a name nobody registered.
- Persisted and wire values are names, so a node that meets a member it does not know rejects it by name.
- The `/audit-conventions` sweep checks every `enum`, every `[Flags]` and every `switch` that encodes relationships
  among a vocabulary's members.
- A smell that the rule was chosen wrong: an enum member added for a package other than this one, or a `switch` over
  names that grows with each extension.

## References

- [ADR-1001](1001-behavior-is-a-service-a-strategy-collection-or-a-mediator-seam-chosen-by-one-ladder.md): services,
  strategy collections and mediators, the registration seams an open vocabulary uses.
- [ADR-1025](1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md): the
  pre-release, Auth0-parity-plus posture.
- [ADR-1139](1139-setting-names-are-a-closed-vocabulary.md): registered setting names reject unregistered input.
- [ADR-1149](1149-settings-declare-the-scopes-they-apply-to.md): the setting scopes, the case that motivated this rule.
- [ADR-1098](1098-session-lifecycle-termination-seam.md), [ADR-1104](1104-import-public-asymmetric-secret-material.md)
  and [ADR-1190](1190-hashing-names-the-framework-algorithm-and-encodings-are-registered.md): the open vocabularies they
  hold.
- RFC 7515, RFC 7516, RFC 7518, RFC 9052 and RFC 9101: the specifications behind the enums that pass.
- Convention: [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §2.
