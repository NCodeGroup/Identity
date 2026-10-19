# 146. A development seeder registers only the clients its configuration declares

- **Status:** Accepted (amends [ADR-0082](0082-playground-and-conformance-surfaces.md))
- **Date:** 2026-10-19
- **Deciders:** NCode Group

## Context

The developer interactive seed (`AddDeveloperInteractiveSeed`) provisions a public, PKCE-only client for each frontend
surface that signs in from its own dev-server origin. When the seed was generalized to a list
([ADR-0082](0082-playground-and-conformance-surfaces.md)), `DeveloperInteractiveSeedOptions.SpaClients` was initialized
with two entries: `mgmt-spa` (redirects on `localhost:5173`) and `playground-spa` (`localhost:5174`, consent
required). That shape had three problems.

- **Configuration could extend the list but never replace it.** The configuration binder appends configured entries to
  a list that already holds some, so `DeveloperInteractiveSeed:SpaClients` added clients beside the two built-in ones
  and could not remove or replace them (backlog `F59`). This is the same defect that was fixed for
  `ConformanceClientSeed`'s grant types, and it breaks the binding convention of
  [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md): a bound collection starts empty.
- **The seed was never inert.** `AddDevelopmentEnvironment` documents that each development convenience does nothing
  when its section is absent. But every non-Production host persisted the two clients, including the Conformance
  environment, which drives an entirely different set of clients. Seeded clients are stored in the database, so they
  outlive the process that created them (the Aspire graph runs the Development and Conformance OPs on one Postgres).
- **Each surface's sign-in coordinates were split across two layers.** A bundle learns its client id from the host's
  `Frontend` section; nothing is baked into the bundle ([ADR-0064](0064-frontend-presentation-platform.md),
  [ADR-0082](0082-playground-and-conformance-surfaces.md)). The registration that id must match was hard-coded in the
  published `NCode.Identity.Server` library. Renaming a surface's client meant editing both, and the library carried
  one host's client ids and dev-server ports.

## Decision

**The developer interactive seed has no built-in clients.** `DeveloperInteractiveSeedOptions.SpaClients` starts empty,
and the seeder registers exactly the clients configuration declares. With nothing configured it registers none,
which is the same shape as the conformance seed's `Clients`.

**The host declares its surfaces' clients beside the client ids it publishes.** The reference host declares
`mgmt-spa` and `playground-spa` under `DeveloperInteractiveSeed:SpaClients` in its Development overlay
(`appsettings.Development.json`), next to the `Frontend` section that publishes the same ids. One file holds both
halves of each surface's sign-in coordinates. An integration test checks that every client id the host's bootstrap
document publishes is one the seed registers, and that each seeded client resolves at the authorization endpoint with
each of its redirect URIs, so the two halves cannot drift apart.

**A host's identifiers are not a library default.** A collection option with a genuine default still starts empty and
applies that default only when nothing is configured (the [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md)
convention). When the "default" would be one host's identifiers, such as a seeded client's id and redirect URIs, the
library has no default; the host's environment overlay declares the entries.

## Options considered

- **Start the list empty and apply the two clients when none are configured.** This was the backlog's suggested fix,
  and it is the shape of the developer CORS default. Rejected: configuration could then replace the clients, but the
  seed would still persist clients in hosts that never asked for them. The client ids would stay split from the
  `Frontend` section that publishes them. And configuration could not ask for no clients at all, because an empty
  configured list binds as an absent one.
- **Apply the defaults in the handler when the list is empty.** This is the shape of `ConformanceClientSeed`'s grant
  types. Rejected for the same reasons. It would also make an options object that holds no clients seed two.
- **Keep the built-in clients and add an opt-out switch.** Rejected: it adds public surface to preserve a default that
  belongs to one host.
- **Also move the developer CORS default origins into the overlay.** Not done here. That default is not persisted,
  applies only in Development, already follows the start-empty convention, and covers an origin that needs no client
  (the conformance dashboard).

## Consequences

- A host that composes `AddDevelopmentEnvironment` and wants a frontend surface to sign in declares that surface's
  client under `DeveloperInteractiveSeed:SpaClients`. The reference host already does, so F5, the Aspire stack, and the
  integration tests behave as before.
- The Conformance environment no longer receives the two loopback SPA clients. It holds only the conformance clients
  its own overlay declares.
- The seeder is add-only and idempotent, so a development database keeps any client seeded earlier.
  `build/dev-db-reset.ps1` purges it.
- The public API is unchanged. Only the initial value of `SpaClients` changed.

## References

- Backlog: `F59` (archived in [`BACKLOG-COMPLETED.md`](../../BACKLOG-COMPLETED.md)).
- [ADR-0082](0082-playground-and-conformance-surfaces.md): the surfaces and the list-shaped seed this amends.
- [ADR-0137](0137-configuration-binds-strictly-and-is-validated-at-startup.md): strict binding, and why a bound
  collection starts empty.
- [ADR-0064](0064-frontend-presentation-platform.md): runtime-discovered client ids ("one bundle, every tier").
- Code: `NCode.Identity.Server.DevelopmentEnvironment.DeveloperInteractiveSeedOptions`,
  `NCode.Identity.Server.DevelopmentEnvironment.DeveloperInteractiveSeedHandler`,
  `src/NCode.Identity.OpenId.Host/appsettings.Development.json`,
  `tests/NCode.Identity.OpenId.IntegrationTests/Endpoints/DeveloperSpaClientSeedingTests.cs`.
