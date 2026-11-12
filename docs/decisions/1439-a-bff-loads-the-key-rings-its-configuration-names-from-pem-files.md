# 1439. A BFF loads the key rings its configuration names from PEM files

- **Status:** Accepted (amended by [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md): a key ring lists keys a key management service holds, and every key ring loads when the host starts)
- **Date:** 2026-11-05
- **Deciders:** NCode Group

## Context

A BFF frontend names the key ring that holds its client-authentication keys (`ClientAuthentication:KeyRing`) and the key
ring that holds its DPoP keys (`DPoP:KeyRing`). Each name resolves to a keyed `ISecretKeyCollectionProvider` in the
host's service collection, so a host registers one provider per key ring in code before its BFF can start.

A BFF replica keeps no state of its own
([ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)), so it takes
its keys from a secret that the platform mounts as files. A host that only needs to read those files has no reason to
carry code that does so, and the files are the same shape for every host: a PEM-encoded private key.

The client-authentication key ring is the one that `GET /bff/jwks` publishes, and the DPoP key ring is never published
([ADR-1382](1382-the-bffs-browser-security-headers-and-its-client-key-endpoint-are-each-opt-in-for-the-host.md)).
Both rings hold private keys, so how a host loads them decides where the private key's bytes live in the process.

## Decision

**A BFF loads every key ring that `Bff:KeyRings` names from PEM files and registers it as a keyed
`ISecretKeyCollectionProvider` named after the key ring. A host that needs another source registers the keyed provider
in code.**

1. **The section.** `Bff:KeyRings:{name}:PemFiles` is a list of file paths. `{name}` is the key ring's name, and a
   frontend's `ClientAuthentication:KeyRing` or `DPoP:KeyRing` refers to it. Every entry under `Bff:KeyRings` registers
   one keyed provider when `AddIdentityBff` binds the `Bff` section.
2. **The file formats.** Each file holds one PEM-encoded private key of one of the three standard encodings: PKCS#8,
   SEC1 (EC) or PKCS#1 (RSA). The loader reads a file as an EC key first and as an RSA key when that fails. A file that
   holds neither throws an `InvalidOperationException` that names the key ring and the path.
3. **The key id.** A key's `kid` is its file name without the extension, so `mgmt-client.pem` holds the key `mgmt-client`.
   A ring with several files holds several keys, and a rotation adds a file.
4. **Sensitive buffers.** The loader reads a file into a sensitive buffer, decodes it into a sensitive character buffer
   and hands that buffer to the secret-key factory. The PEM text and its bytes are not held in an ordinary string or
   array.
5. **The path.** A relative path resolves against the host's content root, and an absolute path is used as it is.
6. **Startup validation.** The options validator fails startup with one message per problem:
   - `Bff:KeyRings has a key ring without a name.`
   - `Bff:KeyRings:{name} must list at least one PEM file or remote key.`
   - `Bff:KeyRings:{name}:PemFiles:{i} must not be blank.`
   - `Bff:KeyRings:{name}:PemFiles:{i} names '{path}', which does not exist.`
   - `Bff:KeyRings:{name} holds more than one key with the id '{id}'.`

   The messages of a `RemoteKeys` entry are in
   [ADR-1479](1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service.md)
   rule 7. A key ring that fails to load stops the host when it starts.
7. **Code registration is the seam.** A host that holds its keys elsewhere, in another source, registers its own
   keyed `ISecretKeyCollectionProvider` under the key ring's name and configures no `Bff:KeyRings` entry for it. The
   configuration is the built-in source, and keyed dependency injection is the open seam.

## Options considered

- **The key's text in the configuration.** Rejected: a private key in a configuration value ends up in logs and in dumps
  of the configuration.
- **A JWK set file.** Rejected: PEM is what tooling and secret stores emit, and a host does not convert it to read it.
- **A `Source` vocabulary for key rings.** Rejected as unneeded: keyed dependency injection already is the open seam,
  and a host with another source registers it in code.
- **PEM files named by configuration.** Chosen: it matches how a mounted secret looks, keeps the private key out of the
  configuration's values, and needs no code in a host.

## Consequences

- A host that mounts its keys as files configures `Bff:KeyRings` and writes no key-loading code.
- A key's id comes from its file name, so renaming a file changes the `kid` that a published JWKS carries and that a
  client's registration refers to.
- A key ring's files are read once, when the host starts, so a rotation takes effect when the process starts again.
- A host that registers a keyed provider in code for a name that `Bff:KeyRings` also configures has two registrations
  for one key; the host configures a name in one place only.
- The validator checks that each file exists and the loader checks that each file parses; a wrong key type stops the
  host when it starts.

## References

- [ADR-1290](1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)
- [ADR-1382](1382-the-bffs-browser-security-headers-and-its-client-key-endpoint-are-each-opt-in-for-the-host.md)
- `src/NCode.Identity.Bff/Secrets/BffPemKeyReader.cs`
- `src/NCode.Identity.Bff/Secrets/BffKeyRingLoader.cs`
