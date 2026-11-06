# 1446. A set of frontends that names an unregistered key ring is refused

- **Status:** Accepted (amends
  [ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md)
  decision 4)
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A key ring is a keyed `ISecretKeyCollectionProvider` registration, which `AddBffConfiguration` adds for each
`Bff:KeyRings` entry present when it is called and which a host can add itself
([ADR-1439](1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md)). A frontend names a key ring in
`ClientAuthentication:KeyRing` and in `DPoP:KeyRing`. Four facts shape how a misspelled name is found:

- Frontends follow their configuration at run time, and a change takes effect only when the whole set passes every check
  ([ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md)).
- A name that no key ring answers to fails only at the frontend's first token request or at `/bff/jwks`, long after the
  host started or the configuration changed.
- `IClientKeyRingProvider` is the seam that the protocol client resolves key rings through, and a host can replace it
  with a source that is not keyed dependency injection.
- Resolving a key ring loads its keys, which a check that runs on every configuration change must not do.

Issue [#614](https://github.com/NCodeGroup/Identity/issues/614) describes the need.

## Decision

**`IClientKeyRingProvider` says whether it finds a key ring without loading its keys, and the BFF refuses a set of
frontends that names a key ring it does not find.**

1. **The provider answers whether a key ring exists.** `IClientKeyRingProvider.HasKeyRing(keyRing)` says whether
   `GetKeyRing` finds the key ring, and loads no key. `DefaultClientKeyRingProvider` answers through
   `IServiceProviderIsKeyedService` for a keyed `ISecretKeyCollectionProvider`.
2. **The BFF registers `BffKeyRingCheck`.** It refuses a set in which a non-blank `ClientAuthentication:KeyRing` or
   `DPoP:KeyRing` names a key ring that `HasKeyRing` does not find, and reports one failure for each such key. A blank
   name is left to the options validators.
3. **It runs with every set check.** The host refuses to start on a failure. A configuration change that fails is
   logged at `Error` as event 19012, and the last valid set stays.

## Options considered

- **Query keyed dependency injection directly.** Rejected: a host that replaces `IClientKeyRingProvider` with a
  store-backed source would be refused for key rings that its source finds.
- **Load the key ring in the check.** Rejected: it reads every file of the key ring on every configuration change, and a
  key ring that fails to load would refuse the set for a reason the name does not show.
- **Leave it to the first token request.** Rejected: a misspelled name stays hidden until a user signs in.

## Consequences

- A misspelled key ring name never reaches a token request.
- Every `IClientKeyRingProvider` answers `HasKeyRing`.
- A key ring that a configuration change adds under `Bff:KeyRings` still needs a restart, so a frontend that names it is
  refused until then.

## References

- [ADR-1270](1270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md),
  [ADR-1439](1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md),
  [ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md)
- `src/NCode.Identity.Bff/Frontends/BffKeyRingCheck.cs`
- Issue [#614](https://github.com/NCodeGroup/Identity/issues/614)
