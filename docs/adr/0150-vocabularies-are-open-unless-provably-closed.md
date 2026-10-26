# 150. Vocabularies are open unless provably closed

- **Status:** Accepted
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

The family exists to be extended easily and without restriction. Its extension points are registrations: a package
adds a strategy to a collection resolved from DI ([ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md)),
contributes descriptors through `ICollectionDataSource<T>` (settings, [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md)),
or registers a named implementation (security profiles, client-authentication methods, PKCE transformations). The
Auth0-parity-plus posture ([ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md)) asks for data-driven,
flexible controls, which only holds when a deployment or sibling package can add what the library did not foresee.

The convention for vocabularies said: an `enum` for a genuinely closed set, `public const string` values for one a
consumer or sibling package must extend. It left the judgment to the author, and the question authors asked was "does
this package own today's members?". That question misleads. Setting scopes were modeled as a `[Flags]` enum of the
server, tenant, client, and resource server, with their cascade hard-coded in a `switch`, because the library owned all
four entities. The enum and the switch then closed the settings framework — which until then had been usable for any
entity — to every extension that stores settings for an entity of its own (an organization, a connection), and nothing
short of editing the abstractions package could open it again.

An enum closes a vocabulary in several ways at once: its members are fixed at compile time; a `[Flags]` enum also fixes
the bit space and the meaning of every combination; and code that `switch`es over the members, or encodes their
relationships in a mask or a chain, closes the behavior and the topology even when the names themselves are strings.

## Decision

**Every vocabulary is open by default. A closed construct is allowed only when the vocabulary is provably closed.**

1. **An open vocabulary is names plus registrations.** Its members are `public const string` names in a static class,
   compared ordinally and documented as the known values. When a member carries behavior or relationships, it is
   registered data or a strategy — contributed through `ICollectionDataSource<T>` or a DI strategy collection and
   resolved by name through a catalog that validates the whole set when it is composed (duplicate names, dangling
   references, cycles). Input naming an unregistered member is rejected, not ignored.
2. **An `enum` needs a three-part justification.** The reviewer can state that (1) the members are fixed by something
   that cannot grow without this library changing — a finite enumeration of an external specification the library
   implements in full, the outcome set of a library-owned operation's contract, or a mirror of a framework type; (2) no
   consumer, deployment, or sibling package could reasonably need another member — the question is "could a package that
   is not this one ever need to add one?"; and (3) a new member would require a change to this library anyway.
3. **Relationships and behavior are never encoded over a vocabulary.** No `[Flags]` for a vocabulary that may grow; no
   `switch`, mask, or hard-coded chain describing its hierarchy, compatibility, or order; no exhaustive `switch` over an
   open vocabulary to choose behavior — the catalog derives relationships from registered data, and behavior is
   dispatched to the registered strategy.
4. **A persisted or wire value is its name.** Never an ordinal, so an older node that meets a newer member fails closed
   by name instead of misreading a number.

## Options considered

- **Keep the previous wording.** Rejected: it named the right outcome but not the test, and the ownership heuristic it
  invited produced a closed design that had to be reworked.
- **Ban enums from the public surface.** Rejected: a genuinely closed set — the outcomes of one operation, a mirror of a
  framework option — gains exhaustiveness checking and type safety from an enum, and modeling it as strings would add
  validation with no extensibility to show for it.
- **Typed name wrappers (`readonly record struct Scope(string Name)`) as the default.** Rejected as a default: they add a
  type per vocabulary without adding openness, and the family's existing open vocabularies are `const string` names. A
  wrapper remains available where a signature would otherwise confuse two name parameters.

## Consequences

- A new vocabulary starts open; closing it is the exception a reviewer must be able to justify in three sentences.
- The settings framework is open to new entity types: setting scopes are registered `SettingScopeDescriptor` data with
  their cascade derived by a catalog ([ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md)).
- The existing public enums were re-evaluated against the test (issue [#35](https://github.com/NCodeGroup/Identity/issues/35)). Four failed it and became names:
    - `SessionTerminationReason` became the `SessionTerminationReasons` names, because an extension ends a session for
      its own reason ([ADR-0098](0098-session-lifecycle-termination-seam.md)).
    - `SecretKeyImportFormat` became the `SecretKeyImportFormats` names, which each key type declares, because a
      registered key type parses its own formats ([ADR-0104](0104-import-public-asymmetric-secret-material.md)).
    - `HashAlgorithmType` became the framework's `HashAlgorithmName`, and `BinaryEncodingType` became the
      `BinaryEncodings` names of registered encoders
      ([ADR-0190](0190-hashing-names-the-framework-algorithm-and-encodings-are-registered.md)).

  The rest pass, each for the reason given:
    - `AlgorithmType` mirrors the four JOSE algorithm registries of RFC 7518: signature, key management, content
      encryption, and compression. The algorithms inside each registry are open names.
    - `JoseSerializationFormat` lists the three serializations that RFC 7515 and RFC 7516 define.
    - `RequestObjectSource` lists the two parameters that RFC 9101 defines for a request object, `request` and
      `request_uri`.
    - `SerializationFormat` chooses between the two representations of the library's own message converter.
    - `TenantPlane` follows from the topology: the root tenant is the control plane, and every other tenant is a
      workload tenant.
    - `DeveloperKeyMode` selects among the development key behaviors the server implements. A deployment that needs
      other keys selects `None` and provisions them like production keys.
    - `ObjectJsonDateStringHandling`, `ObjectJsonIntegerHandling`, and `ObjectJsonRealHandling` are options of the
      library's `ObjectJsonConverter`, and each member is a branch of that converter.
    - `PersistedGrantStatus` is never stored. The grant service derives it on read from a grant's revocation and expiry
      instants. A grant type's own lifecycle, such as a pending device code, lives in its payload.
    - `PasswordVerificationResult`, `OutboundHttpReadResult`, and `OwnerRemovalResult` are the outcome sets of one
      library-owned operation each.
- The audit-conventions sweep checks every `enum`, every `[Flags]`, and every `switch` that encodes relationships among a
  vocabulary's members.

## References

- [ADR-0001](0001-mediator-vs-dependency-injection-logic-classes.md) — services, strategy collections, and mediators,
  the registration seams an open vocabulary uses.
- [ADR-0025](0025-pre-release-posture-and-auth0-parity-plus.md) — the pre-release, Auth0-parity-plus posture.
- [ADR-0139](0139-setting-names-are-a-closed-vocabulary.md) — a vocabulary of registered values is closed to unregistered
  input, which is a different thing from a closed type.
- [ADR-0149](0149-settings-declare-the-scopes-they-apply-to.md) — the setting scopes, the case that motivated this rule.
- Convention: [`csharp-production.instructions.md`](../../.github/instructions/csharp-production.instructions.md) §2.
