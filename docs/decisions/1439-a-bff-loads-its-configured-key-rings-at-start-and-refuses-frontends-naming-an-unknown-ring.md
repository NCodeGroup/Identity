# 1439. A BFF loads its configured key rings at start and refuses frontends naming an unknown ring

- **Status:** Accepted
- **Date:** 2026-11-06
- **Deciders:** NCode Group

## Context

A BFF frontend names the key ring that holds its client-authentication keys (`ClientAuthentication:KeyRing`) and the key
ring that holds its DPoP keys (`DPoP:KeyRing`). Each name resolves to a keyed `ISecretKeyCollectionProvider` in the
host's service collection, so a host registers one provider per key ring before its BFF can start.

A BFF replica keeps no state of its own
([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)), so it takes
its keys from a secret that the platform mounts as files. A host that only needs to read those files has no reason to
carry code that does so, and the files are the same shape for every host: a PEM-encoded private key.

The client-authentication key ring is the one that `GET /bff/jwks` publishes, and the DPoP key ring is never published
([ADR-1382](1382-the-bffs-browser-security-headers-and-its-client-key-endpoint-are-each-opt-in-for-the-host.md)). Both
rings hold private keys, so how a host loads them decides where the private key's bytes live in the process.

Four further facts shape how a misspelled ring name is found:

- Frontends follow their configuration at run time, and a change takes effect only when the whole set passes every check
  ([ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md)).
- A name that no key ring answers to fails only at the frontend's first token request or at `/bff/jwks`, long after the
  host started or the configuration changed.
- `IClientKeyRingProvider` is the seam that the protocol client resolves key rings through, and a host can replace it
  with a source that is not keyed dependency injection.
- Resolving a key ring loads its keys, which a check that runs on every configuration change must not do.

Issue [#614](https://github.com/NCodeGroup/identity/issues/614) describes the need.

## Decision

**A BFF loads every key ring that `Bff:KeyRings` names when the host starts and registers it as a keyed
`ISecretKeyCollectionProvider` named after the key ring. It refuses a set of frontends that names a key ring it does not
find. A host that needs another source registers the keyed provider in code.**

1. **The section.** Each `Bff:KeyRings:{name}` entry registers one keyed provider when `AddBffConfiguration` binds
   `Bff`. `{name}` is the key ring's name, and a frontend's `ClientAuthentication:KeyRing` or `DPoP:KeyRing` refers to
   it. A ring lists `PemFiles`, `RemoteKeys` or both. Remote keys that a key management service holds are described in
   [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md).
2. **The file formats.** Each PEM file holds one private key of one of the three standard encodings: PKCS#8, SEC1 (EC)
   or PKCS#1 (RSA). The loader reads a file as an EC key first and as an RSA key when that fails. A file that holds
   neither throws an `InvalidOperationException` that names the key ring and the path.
3. **The key id.** A key's `kid` is its file name without the extension, so `mgmt-client.pem` holds the key
   `mgmt-client`. A ring with several files holds several keys, and a rotation adds a file.
4. **Sensitive buffers.** The loader reads a file into a sensitive buffer, decodes it into a sensitive character buffer
   and hands that buffer to the secret-key factory, never an ordinary string or array.
5. **The path.** A relative path resolves against the host's content root, and an absolute path is used as it is.
6. **Startup validation.** The options validator fails startup with one message per problem: an unnamed ring, a ring
   with no PEM file or remote key, a blank or missing file, and a duplicate key id.
7. **Load at start.** `BffKeyRingLoader`, an `IHostedLifecycleService`, loads every ring in `StartingAsync`, and a
   failure stops the host. A ring changes only when the process restarts, so a ring that a configuration change adds
   needs a restart.
8. **Code registration is the seam.** A host that holds its keys elsewhere registers its own keyed
   `ISecretKeyCollectionProvider` under the key ring's name and configures no `Bff:KeyRings` entry for that name. The
   configuration is the built-in source, and keyed dependency injection is the open seam.
9. **The provider says whether a ring exists.** `IClientKeyRingProvider.HasKeyRing(keyRing)` says whether `GetKeyRing`
   finds the key ring, and loads no key. `DefaultClientKeyRingProvider` answers through `IServiceProviderIsKeyedService`.
10. **`BffKeyRingCheck` refuses an unknown ring.** The library registers it as an `IBffFrontendSetCheck`. It refuses a
    set in which a non-blank `ClientAuthentication:KeyRing` or `DPoP:KeyRing` names a ring that `HasKeyRing` does not
    find, and reports one failure for each such key. A blank name is left to the options validators. At start the
    failure stops the host; on a configuration change it is logged at `Error` as event 19012 and the last valid set
    stays.

## Options considered

- **The key's text in the configuration.** Rejected: a private key in a configuration value ends up in logs and in dumps
  of the configuration.
- **A JWK set file.** Rejected: PEM is what tooling and secret stores emit, and a host does not convert it to read it.
- **A `Source` vocabulary for key rings.** Rejected as unneeded: keyed dependency injection already is the open seam,
  and a host with another source registers it in code.
- **Query keyed dependency injection directly in the check.** Rejected: a host that replaces `IClientKeyRingProvider`
  with a store-backed source would be refused for key rings that its source finds.
- **Load the key ring in the check.** Rejected: it reads every file of the key ring on every configuration change, and a
  key ring that fails to load would refuse the set for a reason the name does not show.
- **Leave it to the first token request.** Rejected: a misspelled name stays hidden until a user signs in.
- **PEM files named by configuration, loaded at start, with the set checked against `HasKeyRing`.** Chosen: it matches
  how a mounted secret looks, keeps the private key out of the configuration's values, needs no code in a host, and
  finds a misspelled name before a user does.

## Consequences

- A host that mounts its keys as files configures `Bff:KeyRings` and writes no key-loading code.
- A key's id comes from its file name, so renaming a file changes the `kid` that a published JWKS carries and that a
  client's registration refers to.
- A key ring's files are read once, when the host starts, so a rotation takes effect when the process starts again.
- A host that registers a keyed provider in code for a name that `Bff:KeyRings` also configures has two registrations
  for one key; the host configures a name in one place only.
- The validator checks that each file exists and the loader checks that each file parses; a wrong key type stops the
  host when it starts.
- A misspelled key ring name never reaches a token request.
- Every `IClientKeyRingProvider` answers `HasKeyRing`.
- A key ring that a configuration change adds under `Bff:KeyRings` still needs a restart, so a frontend that names it is
  refused until then.

## References

- [ADR-1285](1285-the-protocol-client-is-a-framework-free-library-over-the-familys-jose-stack-and-named-key-rings.md),
  [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
  [ADR-1382](1382-the-bffs-browser-security-headers-and-its-client-key-endpoint-are-each-opt-in-for-the-host.md),
  [ADR-1442](1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md),
  [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md)
- `src/NCode.Identity.Bff/Secrets/BffPemKeyReader.cs`
- `src/NCode.Identity.Bff/Secrets/BffKeyRingLoader.cs`
- `src/NCode.Identity.Bff/Frontends/BffKeyRingCheck.cs`
- Issue [#614](https://github.com/NCodeGroup/identity/issues/614)
