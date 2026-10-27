# 186. Every owner's keys are imported and updated under the client-key policy

- **Status:** Accepted
- **Date:** 2026-10-20
- **Deciders:** NCode Group

## Context

[ADR-0161](0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md) put a client's public keys
under one policy, applied by `IClientKeyImporter`. A key is a public key of a registered key type. Its `use`,
`key_ops`, and `alg` agree with each other and with the key. Its `kid` is unique among the client's keys. The management
API's client endpoints imported and updated keys under that policy.

The tenant, server, and resource-server secret endpoints kept their earlier checks:

- an import went through `ISecretImporter` directly, so its `use` and `alg` were not checked against each other or the
  key, and a malformed or refused key failed with a generic message;
- a JSON Web Key's `kid` was discarded, so the key was selected by its secret identifier rather than the name its
  publisher gave it;
- an update could give a key an `alg` its type does not accept, and it dropped the key's `KeyId`.

The management `SecretResource` reported only the secret identifier, so neither an API caller nor the admin console
could see the `kid` a relying party selects the key by.

## Decision

**Every owner's keys are imported and updated under the client-key policy, and every key reports its `kid`.**

1. **One import policy for every owner.** The tenant, server, resource-server, and client import endpoints all call
   `IClientKeyImporter.TryImportKey`. A refused key is a `400 Bad Request` whose detail says why. The policy says
   nothing that is specific to clients, so its documentation and error messages are written for any owner.
2. **A `kid` is unique among one owner's keys.** Keys are selected by `kid`, so an import whose `kid` matches one of
   the owner's existing keys is a `409 Conflict`. A key without its own `kid` is selected by its secret identifier, and
   the check compares that identifier too. `BaseApiEndpointHandler.KeyIdConflictOrDefault` applies the check for every
   owner.
3. **One update policy for every owner.** `ISecretValidator.ValidateUpdate` checks an update's `use` and `alg` against
   the stored key's type and size, with the same `IAlgorithmCollection.ValidateKeyAlgorithm` rule an import uses. Every
   owner's `UpdateSecretAsync` calls it before writing and keeps the key's `KeyId`.
4. **Every key reports its `kid`.** `SecretResource` and `CreatedSecretResource` carry a required `KeyId`: the `kid`
   the key was imported with, or its secret identifier when it has none. The `@ncode/api` contract mirrors it, and the
   console's secret table shows it in a **Key ID** column.

## Options considered

- **Keep per-owner checks in each handler.** Rejected. The four handlers drifted apart once already, and a key's
  validity does not depend on who owns it.
- **Rename `IClientKeyImporter` to an owner-neutral name.** Deferred. The service and its policy already apply to any
  owner, and its documentation says so. A rename would touch the dynamic-registration and `jwks_uri` paths as well.
  It can happen with the next change to those paths.
- **Report `KeyId` only when it differs from the secret identifier.** Rejected. A nullable field makes every caller
  repeat the fallback, and the key is selected by a `kid` either way.

## Consequences

- A tenant, server, or resource-server key imported as a JSON Web Key keeps its `kid`, so a token signed with that key
  carries the `kid` its publisher chose.
- A key whose metadata contradicts it is refused at import and at update, whoever owns it.
- `KeyId` is a required member of the management secret contracts. The console shows it, and callers can select a key by
  it.

## References

- [ADR-0161](0161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md): a client's public keys
  have one source and are imported under one policy.
- [RFC 7517 §4.5](https://www.rfc-editor.org/rfc/rfc7517#section-4.5): the `kid` parameter.
