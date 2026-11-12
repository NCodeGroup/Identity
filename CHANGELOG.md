# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to adhere to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html). Versioning is stamped in lockstep across the family by
Nerdbank.GitVersioning ([ADR-1003](docs/decisions/1003-the-family-versions-in-lockstep-from-one-version-json-and-ships-source-linked-symbol-packages.md)); from the
first stable release, a breaking change to the public API is a **major** version bump
([project-template ADR-0015](docs/decisions/project-template/0015-a-pre-release-project-describes-the-product-as-it-is.md)).

Until the first stable release, the [Unreleased] section describes the product as it is, not its changes.

## [Unreleased]

### Added

- **Every OpenID protocol endpoint answers the `x-fapi-interaction-id` header.** A request that carries a UUID gets the
  same value back, and a request that carries none, or a value that is not a UUID, gets a new UUID, on success and
  error responses alike, as FAPI 1.0 Part 1 §6.2.1-11 requires
  ([ADR-1498](docs/decisions/1498-protocol-endpoints-answer-the-x-fapi-interaction-id-header.md)).

- **Every registration prerequisite is checked at compile time through marker builders.** `IServiceBuilder<TMarker>` is
  the one way a capability attaches, so adding a capability to a host that lacks its prerequisite library is a compile
  error, not a runtime failure. A registration with one prerequisite extends `IServiceBuilder<TMarker>`; one with a
  second prerequisite takes it as a required `IServiceBuilder<...>` parameter (the builder that prerequisite's own
  registration returned) and returns its receiver builder, so the chain continues on the primary library. Markers are
  empty `readonly struct`s implementing `IRegistrationMarker<TMarker>`, whose type identity is the whole signal, and
  `IsRegistered<TMarker>()` answers whether a marker is registered. Chain roots and generic utility helpers are bare
  `IServiceCollection` extensions, and cross-library helpers invoked from several library builders
  (`AddMessageFactory<TMessage>`, `AddEndpointProvider<T>`, `AddEndpointGroup<T>`) take the non-generic
  `IServiceBuilder` receiver ([ADR-1069](docs/decisions/1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md),
  [ADR-1060](docs/decisions/1060-local-account-one-store-seam-and-dependency-inverted-authentication.md),
  [ADR-1061](docs/decisions/1061-local-account-management-is-an-opt-in-capability.md)).

- **The library chain composes off typed builders.** `AddJoseLibrary()` extends `IServiceBuilder<SecretsLibrary>` and
  `AddJwtLibrary()` extends `IServiceBuilder<JoseLibrary>`, each returning its own library builder, so the
  `AddIdentityServer()` composition root chains `AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary()` and exposes each
  library's builder (`SecretsLibraryBuilder`, `JoseLibraryBuilder`, `JwtLibraryBuilder`,
  `OpenIdAuthenticationLibraryBuilder`, `OpenIdManagementLibraryBuilder` and the rest).
  `AddOpenIdCoreLibrary()` extends `IServiceBuilder<SettingsLibrary>` and takes `IServiceBuilder<ClaimsLibrary>` and
  `IServiceBuilder<CryptographyLibrary>`, `AddOpenIdAuthenticationLibrary()` and `AddOpenIdManagementLibrary()` extend
  `IServiceBuilder<OpenIdCoreLibrary>`, the first taking `IServiceBuilder<JoseLibrary>`, and
  `AddInteractiveAuthentication(...)` extends
  `IServiceBuilder<OpenIdAuthenticationLibrary>`. Every library marker lives in its abstractions package, such as
  `JwtLibrary` in `NCode.Identity.Jwt.Abstractions`, `SettingsLibrary`, `ClaimsLibrary` and `CryptographyLibrary` in
  their abstractions packages, `OpenIdCoreLibrary` in `NCode.Identity.OpenId.Abstractions`,
  `OpenIdManagementLibrary` in `NCode.Identity.OpenId.Management.Abstractions`, `LocalAccountLibrary` in
  `NCode.Identity.OpenId.Accounts.Abstractions` and `StoreManagerLibrary` in `NCode.Identity.Persistence.Abstractions`. The
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core` registration class is `DefaultRegistration`
  ([ADR-1069](docs/decisions/1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md)).

- **A local-account capability cannot be written without a store.** Every local-account store registration
  (`AddLocalAccountEntityFramework()`, `AddAspNetIdentityLocalAccountStore<TUser>()` or a custom store) receives
  `IServiceBuilder<StoreManagerLibrary>`, which `AddEntityFrameworkCorePersistence<TDbContext>()` mints, and returns
  `IServiceBuilder<LocalAccountLibrary>`. `AddLocalAccountAuthentication(IServiceBuilder<LocalAccountLibrary>)` and
  `AddLocalAccountInteractiveAuthentication(IServiceBuilder<LocalAccountLibrary>)` extend
  `IServiceBuilder<OpenIdAuthenticationLibrary>`, and `AddLocalAccountManagement(IServiceBuilder<LocalAccountLibrary>)`
  extends `IServiceBuilder<OpenIdManagementLibrary>`, each returning its receiver for chaining, for example
  `AddIdentityServer().OpenIdAuthenticationLibraryBuilder.AddLocalAccountAuthentication(localAccountStore)`
  ([ADR-1069](docs/decisions/1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md),
  [ADR-1060](docs/decisions/1060-local-account-one-store-seam-and-dependency-inverted-authentication.md)).

- **Strict configuration binding is public.** `NCode.Identity.Registration.StrictConfigurationBindingExtensions.BindStrictly`
  binds a section so that a key that maps to no option fails, so an extension binds its own section the way the family
  does ([ADR-1139](docs/decisions/1139-setting-names-are-a-closed-vocabulary.md)).

- **Endpoints register through declarative, hierarchical endpoint groups in the cross-cutting
  `NCode.Identity.Registration.AspNetCore` package.** The mechanism (`IEndpointGroup`, `IEndpointGroupBuilder`,
  `IEndpointProvider`, `IEndpointTreeRouteBuilder`, `MapEndpointGroups()` and the endpoint and operation disposition
  types) layers on `NCode.Identity.Registration` and the ASP.NET Core shared framework under the `NCode.Identity.Registration.AspNetCore`
  namespace. A named `IEndpointGroup` owns its contents: a `Name` and `Prefix`, an optional `ConfigureRoutes` that
  attaches shared route-group filters when routes are mapped, and a `ConfigureServices(IEndpointGroupBuilder)` that
  registers its endpoints (`AddEndpoint<T>()`), child groups (`AddGroup<T>()`) and the services those endpoints depend
  on, including `AddMessageFactory<T>()`, through one registration surface, because `IEndpointGroupBuilder` is an
  `IServiceBuilder`. Registering one root group (`AddEndpointGroup<TRoot>()`) registers its whole subtree; the route
  builder materializes each group with its prefix and filters, maps its endpoints and recurses into its children, and
  each endpoint family registers its own provider and services once, so no endpoint is listed twice. The management
  surface is the hierarchy `api` (`/api`) → `api/tenant` (`/tenants/{tenantId}`) → `api/tenant/client`
  (`/clients/{clientId}`), so tenant-bound families (clients, grants, resource servers, client grants) nest under the
  tenant and map only their relative sub-routes, and the protocol surface is a single `openid` group
  ([ADR-1043](docs/decisions/1043-endpoint-registration-is-a-cross-cutting-aspnetcore-package.md),
  [ADR-1042](docs/decisions/1042-management-api-route-driven-tenant-scope.md)).

- **Every package names one capability under the `NCode.Identity` prefix.** Each capability publishes its contracts in
  an `.Abstractions` package and its default implementation beside it, so a consumer implements, decorates or replaces
  any part: `NCode.Identity.Settings(.Abstractions)` (setting descriptors, scopes, collections and the serializer,
  `AddSettingsLibrary()`), `NCode.Identity.Claims(.Abstractions)` (the claims serializer and claims service,
  `AddClaimsLibrary()`) and `NCode.Identity.Cryptography(.Abstractions)` (random keys, hashing and the registered binary
  encoders, `AddCryptographyLibrary()`). Foundation packages hold the family's building blocks on one technology for the
  family and its consumers alike: `NCode.Identity.Json`, `NCode.Identity.Time` (with `TimerPeriod` and `TimePeriod`) and
  `NCode.Identity.AspNetCore`, whose contracts `NCode.Identity.AspNetCore.Abstractions` holds (the HTTP result
  contracts, `HttpResultException`, `UriDescriptor` and the front-channel endpoint metadata).
  `NCode.Identity.Persistence.Abstractions` holds every persistence contract, and `NCode.Identity.OpenId.Abstractions`
  references `NCode.Identity.Registration.AspNetCore` directly because it exposes `ReadOnlyEndpointDisposition` on its
  public surface ([#758](https://github.com/NCodeGroup/identity/issues/758),
  [ADR-1500](docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).

- **The `NCode.Identity.Json` foundation package holds the shared `JsonElement` helpers.** `JsonElements` (`EmptyObject`,
  `OrEmptyObject`, `IsNullOrUndefined`, `TryGetPropertyValue<T>`, namespace `NCode.Identity.Json`) is the single home for
  `JsonElement` helpers, so the persistence layer and any package beneath `NCode.Identity.*` reuse it without an ASP.NET
  Core framework reference. Detached JSON parsing uses `JsonElement.Parse(string)`
  ([ADR-1055](docs/decisions/1055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md),
  [#758](https://github.com/NCodeGroup/identity/issues/758),
  [ADR-1500](docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).

- **General ASP.NET Core helpers live in the `NCode.Identity.AspNetCore` foundation package.** `.WithNoStore()` and
  `HttpResponse.SetNoStore()` are in the `NCode.Identity.AspNetCore` namespace, because an Abstractions package holds contracts
  only ([#492](https://github.com/NCodeGroup/identity/issues/492),
  [#758](https://github.com/NCodeGroup/identity/issues/758),
  [ADR-1500](docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).

- **Minimal-API request delegates are generated at compile time, so a host starts fast.** Every package builds with the
  Request Delegate Generator, so a host does not compile each endpoint's expression trees before it answers its first
  request. `NCode.Identity.OpenId.Authentication.Interactive` binds its endpoints at runtime, because the generated form
  binding answers a form whose antiforgery validation failed with 500 instead of 400
  ([ADR-1262](docs/decisions/1262-minimal-api-request-delegates-are-generated-at-compile-time.md), issue
  [#186](https://github.com/NCodeGroup/identity/issues/186)).

- **A consumer of the server packages keeps its own logging generator.** `NCode.Identity.OpenId.Authentication`, and
  through it `NCode.Identity.Server`, does not depend on `Microsoft.Extensions.Http.Resilience`, whose
  `Microsoft.Extensions.Telemetry.Abstractions` dependency would switch every consuming project's `[LoggerMessage]`
  generator to `Microsoft.Gen.Logging`: the guarded outbound client bounds its calls with a `Polly.Core` pipeline,
  retries only a GET or HEAD without a body, and emits no Polly metrics or logs of its own, and the build fails a
  shipping package that would bring the replacement generator to its consumers. The two packages bring none of
  `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.Resilience`, `Microsoft.Extensions.Http.Diagnostics`,
  `Microsoft.Extensions.Telemetry`, `Microsoft.Extensions.Telemetry.Abstractions`, `Polly.Extensions` or
  `Polly.RateLimiting` to a consumer: a project that uses one of them, such as `AddStandardResilienceHandler`,
  references that package itself ([#471](https://github.com/NCodeGroup/identity/issues/471),
  [ADR-1385](docs/decisions/1385-no-shipping-package-replaces-its-consumers-logging-generator-or-carries-microsoft-extensions-http-resilience.md)).
- **Project template v0.84.3.** The repository's plan-and-execute workflow, agent instructions and checks come from project-template v0.84.3. They include the template's pre-release posture, which `- pre-release: untracked` in `AGENTS.md`'s Landing section selects, as project-template ADR-0015 records: until the first stable release every text describes the family as it is, each plan keeps `## [Unreleased]` a catalog under `### Added` and `### Security`, `/review-plan-diff` checks that a plan's texts describe the product as it is, and no change keeps compatibility, so a stored payload's shape changes in place at version 1 and a stored document that isn't a versioned envelope is refused (project-template ADR-0108); `build/Get-TestRuntimes.ps1`, which names the runtime of each older .NET a test project targets, and the `.github/actions/install-dotnet` composite action, through which CI, the end-to-end and frontend checks, the prerelease publish and the Copilot cloud agent's setup install the SDK `global.json` selects and those runtimes; `build/Install-Dotnet.ps1`, which installs the same SDK and runtimes on a developer's machine, each download checked against the SHA-512 in Microsoft's release metadata; the `/address-feedback` and `/run-feedback` commands, in Claude Code and Copilot, which fix or answer every review thread, top-level comment and review summary on a pull request and reply to each, `/run-feedback` repeating that every five minutes until the pull request merges or closes and asking NCode Review to review each push with a `/review` comment, and each resuming a run that stopped, whichever tool started it; `/run-issues`, which does the same for the pull requests it opens and turns a finding the issue doesn't ask for into a follow-up; project-template ADR-0014, under which every such discussion is remediated and replied to before the pull request merges, with the person who merges resolving each thread; the weekly `template-update` workflow, which keeps an issue open while a newer template version is out; the template's `Tag and publish release` workflow, `.github/workflows/tag-release.yml`, which runs `.template/Publish-Release.ps1` on each push to `main` and publishes nothing, because the Landing section says `release: none`; `/ship-plan`'s choice to act on a plan's follow-ups next, which files them as issues and has `/finish-plan` end with a `/plan-issue` command for each; Claude Code settings that allow every Bash and PowerShell command, ask before one that mentions `.env` or `secrets`, and deny reading `.env` files and `secrets/`, with a `SessionStart` hook, `.claude/hooks/session-start.sh`, that runs each `.claude/hooks/ensure-*.sh` in a Claude Code cloud session, among them `ensure-pwsh.sh`, which installs `pwsh` where it is missing; `code-review-auto-merge.yml` for NCode Review pin bumps, which turns on auto-merge with the first merge method `main` allows; the template's C# test conventions in `.github/instructions/csharp-tests.instructions.md`; and the template's general decision records in `docs/decisions/project-template/`, the source of truth for the rules they cover. The workflow commands that post to GitHub run through `gh api` REST routes in a session that refuses GitHub's GraphQL API, such as a Claude Code cloud session; `/ship-plan` and `/run-issues` delete agent attribution lines from the pull request title and description they write, and nothing checks commits or pull requests for attribution (project-template ADR-0018). The template's `file_header_template` is the two-line SPDX header [ADR-1309](docs/decisions/1309-c-files-carry-a-two-line-spdx-header-that-the-build-checks.md) enforces, and `build/dod.ps1` is this repository's own: the `project_owned_files` answer names it, so template updates leave it alone ([#516](https://github.com/NCodeGroup/identity/issues/516), [#536](https://github.com/NCodeGroup/identity/issues/536), [#542](https://github.com/NCodeGroup/identity/issues/542)).

- **The reference host, its local-dev orchestrator and the shipped library carry distinct names.**
  `NCode.Identity.OpenId.Host` is the deployable reference host, with its companions
  `NCode.Identity.OpenId.Host.Database` and `NCode.Identity.OpenId.Host.MigrationService` (and the test types
  `HostApiMarker`, `HostDatabase` and `HostApplicationFactory`); `NCode.Identity.OpenId.LocalDev` is the developer-only
  .NET Aspire orchestrator, named for its purpose rather than its technology (Aspire discovers it through
  `<IsAspireHost>` and its SDK); and `NCode.Identity.Server` is the package that ships. The host is named as the process
  that is deployed and faces the internet, so nothing invites leaving ephemeral keys, wildcard `AllowedHosts` or
  development seeding switched on there. The frontend `apps/playground` flow explorer and its `playground-spa` client
  are a genuine playground and carry that name. These projects are `IsPackable=false`
  ([ADR-1130](docs/decisions/1130-distinct-names-for-host-local-dev-orchestrator-and-library.md)).

- **The reference host is a minimal-hosting ASP.NET Core application with the first-party .NET 10 OpenAPI stack.**
  `NCode.Identity.OpenId.Host` boots from a single top-level `Program.cs` using `WebApplication.CreateBuilder(...)`, and
  the integration tests boot it through `WebApplicationFactory<HostApiMarker>`. `MapIdentityServerApiReference()` in
  `NCode.Identity.Server` maps the OpenAPI document that `Microsoft.AspNetCore.OpenApi` generates and a
  `Scalar.AspNetCore` API reference (`/scalar/v1`), each independently toggleable, with the OAuth 2.0 client-credentials
  scheme preferred and, in development, the bootstrap administrator's credentials pre-filled. `Microsoft.OpenApi` is on
  the 2.x line that stack targets, and the OpenID endpoint operation builders (`DefaultAuthorizationEndpointHandler`,
  `DefaultTokenEndpointProvider`) use the 2.x `OpenApiMediaType` model.

- **The reference hosts separate liveness from readiness through one helper.** `MapHealthProbes()`
  (`NCode.Identity.AspNetCore`) maps `/health/live`, which runs no checks and reports only that the process answers, so a
  dependency outage never makes Kubernetes restart a replica, and `/health/ready`, which runs every check tagged
  `HealthCheckTags.Readiness` and returns `503` while any of them fails, taking the replica out of rotation until its
  dependencies return; it returns their route group, so a host adds a convention such as `RequireHost` to both.
  `NCode.Identity.OpenId.Host` and `NCode.Identity.ManagementConsole.Host` call it. The server's readiness checks are the
  database (`AddDbContextCheck<OpenIdDbContext>`, registered with the `openiddb` connection string) and Valkey when
  configured. The Aspire orchestrator gates dependents on `/health/ready`
  ([ADR-1456](docs/decisions/1456-the-hosts-map-their-health-probes-through-one-general-helper.md), issue
  [#609](https://github.com/NCodeGroup/identity/issues/609)).

- **The reference hosts write logs to standard output only.** `NCode.Identity.OpenId.Host` and
  `NCode.Identity.OpenId.Host.MigrationService` register the console and EventSource logging providers on every
  platform, and not the debug provider or, on Windows, the Event Log provider that the default builders add. The console
  writes to standard output in the format that `Logging:Console:FormatterName` selects, and EventSource writes nothing
  until `dotnet-monitor` or `dotnet-trace` attaches. A reference host writes nothing to the Windows Event Log, and a
  deployment that needs another sink, such as the Event Log of a Windows service, adds it in its own composition root
  ([project-template ADR-0115](docs/decisions/project-template/0115-a-host-logs-to-standard-output.md), issue
  [#245](https://github.com/NCodeGroup/identity/issues/245)).

- **The reference host receives mutual-TLS client certificates when it terminates TLS itself.** Its HTTPS defaults
  accept any client certificate an endpoint requests (`Kestrel:EndpointDefaults:ClientCertificateMode`), and the OP
  validates it against the client it authenticates. It is inert while no endpoint requests a certificate. A host that
  terminates TLS itself and serves mutual-TLS clients does the same in its own `ConfigureHttpsDefaults`
  ([ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).

- **The reference host configures tenant selection under `OpenId:TenantResolution`** (`StrategyCode`, `StaticSingle:…`),
  distinct from the tenant materialization section `OpenId:Tenant`, and a tenant-selection shape under the wrong section
  stops the host. Each launch profile pins its issuer through `OpenId:TenantResolution:StaticSingle:Issuer`.

- **Opt-in developer CORS for the OpenID server (`AddDeveloperCors` / `UseDeveloperCors`).** A development-only
  registration in `NCode.Identity.Server` lets a local SPA dev server read discovery and call the management API
  cross-origin. Its allowed origins come from the `Cors:AllowedOrigins` configuration array, and the library carries no
  origins of its own: without a `Cors` section it registers nothing, and `UseDeveloperCors()` adds the CORS middleware
  only when the policy is registered, so a host without the section, such as the reference host in Production, runs
  without CORS services. The reference host's `appsettings.Development.json` lists its dev-server origins.
  `AddDeveloperCors` is a standalone CORS policy, so it is a bare `IServiceCollection` extension
  ([#441](https://github.com/NCodeGroup/identity/issues/441),
  [ADR-1313](docs/decisions/1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md)).

- **Development signing keys are the tenant's persisted keys, self-healing on opt-in.** Every tenant is provisioned with
  persisted signing keys through the tenant secret store, so the runtime, the JWKS endpoint and the management API share
  one source of truth that survives restarts. `DeveloperKeys:PruneUnreadableKeys` (which the reference host's
  `launchSettings.json` sets) registers `AddDeveloperKeyPruning()`, which removes a tenant key that cannot be read,
  because its master key or the root key wrapping it was lost or changed, when the tenant is seeded, and provisioning
  generates a fresh key in its place. Production refuses a `DeveloperKeys` section
  ([ADR-1021](docs/decisions/1021-developer-signing-keys-seeded-through-persistence.md),
  [ADR-1313](docs/decisions/1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md)).

- **Development seeding signs in end to end (`AddDeveloperInteractiveSeed`).** A development-only, idempotent seeder
  provisions public (PKCE-only) single-page-application clients, confidential BFF clients and a deterministic
  administrator user into the tenant
  `DeveloperInteractiveSeed:TenantId` names, so the authorization-code + PKCE flow and the management console sign in
  end to end. `DeveloperInteractiveSeedOptions.SpaClients` is empty by default and the seeder registers exactly the
  clients configured under `DeveloperInteractiveSeed:SpaClients` (`DeveloperSpaClientSeed`: `ClientId`, `RedirectUris`,
  `RequireConsent`) and exactly the BFF clients under `DeveloperInteractiveSeed:BffClients` (`DeveloperBffClientSeed`:
  `ClientId`, `Jwks` registered by value, `RedirectUris`, `PostLogoutRedirectUris`, `BackchannelLogoutUri`;
  private_key_jwt, PAR and DPoP-bound tokens), or none. An existing BFF client is reconciled with its configuration:
  a client whose settings and keys differ is replaced, and an entry that sets `ClientId` without `Jwks` or
  `RedirectUris` is skipped with a warning. The administrator password has no default. The reference host
  declares `playground-spa` (which requires consent) and the management console's BFF client `mgmt-console`, whose key
  set the AppHost supplies, in `appsettings.Development.json`, beside the `Frontend` section that
  publishes `playground-spa`, and an integration test fails if the bootstrap document publishes a client id the seed
  does not register (issues [#139](https://github.com/NCodeGroup/identity/issues/139),
  [#133](https://github.com/NCodeGroup/identity/issues/133),
  [ADR-1146](docs/decisions/1146-a-development-seeder-registers-only-the-clients-its-configuration-declares.md)).

- **`IIdentityServerBuilder.AddFrontendBootstrap(configuration)` publishes a surface-aware runtime frontend bootstrap
  document at `/config/bootstrap.json`** (issue [#136](https://github.com/NCodeGroup/identity/issues/136),
  [ADR-1064](docs/decisions/1064-frontend-presentation-platform.md),
  [ADR-1082](docs/decisions/1082-playground-and-conformance-surfaces.md)). It binds the `Frontend` configuration section
  into `FrontendBootstrapOptions` and registers the endpoint as an `IEndpointProvider`, so the host's single
  `MapEndpointGroups()` call maps it. The document carries the API base, issuer, client id, scopes, feature flags and
  branding, and falls back to the serving request's origin for the API base and issuer, so a single-origin host serves
  every tier from one bundle with no configuration. `@ncode/config`'s `loadRuntimeConfig` sends the bundle's
  `SurfaceKind` as `?surface=`, and `FrontendBootstrapOptions.Surfaces` maps each surface to a `FrontendSurfaceOptions`
  override: client id and scopes override the default, so a read-only surface publishes none; feature flags merge;
  branding falls back. The bootstrap types (`FrontendBootstrapOptions`, `FrontendSurfaceOptions`,
  `FrontendBrandingOptions`) are in the `NCode.Identity.Server.Frontend` namespace. The reference host's Development
  document publishes developer branding, `playground-spa` for the playground
  and no client for the conformance dashboard, and the Vite dev servers proxy `/config/bootstrap.json` to the host
  through Aspire service discovery (`WithReference(api)`), so local development consumes the runtime endpoint.

- **`AddConformanceSeed(IConfiguration)` seeds the clients and test user the OpenID Foundation conformance suite
  drives** (issues [#127](https://github.com/NCodeGroup/identity/issues/127),
  [#235](https://github.com/NCodeGroup/identity/issues/235)). Chained off `AddIdentityServer()`, it registers an
  idempotent `SeedTenantCommand` handler modeled on the bootstrap-admin seeder: inert unless the `ConformanceSeed`
  configuration section is supplied, never duplicating on re-run, and seeding only the tenant `ConformanceSeed:TenantId`
  names. `ConformanceSeedOptions.Clients` is an object keyed by client id (`ConformanceSeed:Clients:<client_id>:…`), so
  an environment override names its client rather than an array position, and strict binding refuses an entry member the
  seed does not define. Each `ConformanceClientSeed` registers one `TokenEndpointAuthMethod`, its
  `token_endpoint_auth_method` and the only method it may use, so it is a confidential client the way a dynamically
  registered one is, with a `Base64Url`-encoded `ClientSecret` that never lingers on the GC heap, a `Jwks` key set it
  registers by value, or both, and its `SecurityProfiles`; a client with a key set and no secret is seeded with exactly
  the keys the client-key policy imports, and a key set the policy refuses stops the seeding with an error naming the
  client. `appsettings.Conformance.json` seeds the three Basic-OP clients (`client` and `client2` with
  `client_secret_basic`, `client-secret-post` with `client_secret_post`, all `S256`), the FAPI clients and
  `conformance-user`. It must not be used in production
  ([ADR-1025](docs/decisions/1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md),
  [ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).

- **A `Conformance` launch profile runs the OpenID host as an externally reachable HTTPS process for the OpenID
  Foundation Conformance Suite** (issue [#126](https://github.com/NCodeGroup/identity/issues/126)). It binds all
  interfaces on stable ports (`https://0.0.0.0:5001;http://0.0.0.0:5000`) so the suite's containers reach it at
  `host.docker.internal:5001`, and pins the `conformance` tenant's issuer to that origin through
  `OpenId:TenantResolution:StaticSingle:Issuer`, so it byte-matches the suite's `server.discoveryUrl`. The profile uses
  the conformance seed ([#127](https://github.com/NCodeGroup/identity/issues/127)) and the interactive login and consent
  UI ([#125](https://github.com/NCodeGroup/identity/issues/125)). Run it with `dotnet run --project
  hosts/NCode.Identity.OpenId.Host --launch-profile Conformance` after
  `build/conformance/Get-ConformanceSeedCredentials.ps1 -SetEnvironment`; see
  [`docs/conformance/reachable-host.md`](docs/conformance/reachable-host.md).

- **The local-dev Aspire graph runs a dedicated conformance OP instance, `conformanceapi`, that opts into the
  capabilities the stock server keeps off** ([ADR-1071](docs/decisions/1071-aspire-local-dev-orchestrator.md),
  [ADR-1084](docs/decisions/1084-spec-coverage-conformance-harness.md),
  [ADR-1088](docs/decisions/1088-opt-in-front-channel-logout-and-session-management.md),
  [ADR-1144](docs/decisions/1144-authorization-codes-are-bound-to-pkce-by-default.md)). It is the same host project
  under the Conformance environment (the `ConformanceAspire` launch profile) on its own port (`https://localhost:5003`,
  which its issuer is pinned to), serving its own `conformance` tenant from the one shared Postgres, and the conformance
  dashboard reads its discovery. Its `appsettings.Conformance.json` sets the `OpenId:Platform:Settings` root ceiling to
  enable `session_management_supported`, `frontchannel_logout_supported` and `frontchannel_logout_session_supported` and
  to add `client_credentials` and token exchange to `grant_types_supported` (which merges by intersect, so only the root
  widens it), plus an explicit `Cors:AllowedOrigins` for the dashboard; a seeded confidential client holds the
  `client_credentials` grant so the capability is exercisable. It relaxes `require_pkce` and sets
  `pkce_nonce_exemption`, because the suite is a confidential relying party that validates the nonce but sends PKCE only
  in one module; the Basic OP module that sends neither PKCE nor a nonce
  (`oidcc-ensure-request-without-nonce-succeeds-for-code-flow`) is an expected failure. The default server keeps the
  secure defaults. Development and conformance convenience only
  ([ADR-1025](docs/decisions/1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)).

- **FAPI 1.0 Advanced conformance harness.** `build/conformance/run-conformance.ps1 -Fapi1Advanced` runs the conformance
  suite's `fapi1-advanced-final-test-plan` with `private_key_jwt` and `self_signed_tls_client_auth` clients, pushed
  authorization requests, and plain and JARM responses. Like the default run, it runs the in-house probe-runner
  pre-flight first (`-SkipPreflight` skips it). It generates each client's key and self-signed certificate on first use
  into a gitignored folder, so no private key is committed, supplies each key set through
  `ConformanceSeed__Clients__<client_id>__Jwks`, renders both FAPI configurations from one
  `fapi1-advanced.template.json`, and takes the OP's URLs from `-IssuerUrl`
  ([ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md),
  [#235](https://github.com/NCodeGroup/identity/issues/235)).

- **One .NET Aspire launch runs the full local stack**
  ([ADR-1071](docs/decisions/1071-aspire-local-dev-orchestrator.md), issue
  [#143](https://github.com/NCodeGroup/identity/issues/143)). The AppHost project `NCode.Identity.OpenId.LocalDev`
  orchestrates Postgres, Valkey, the migration worker, the OpenID host (`api`), the conformance OP (`conformanceapi`),
  the interactive-login build-watcher (`auth`), the management console host (`console`) and its build-watcher (`mgmt`),
  and the `playground` and `conformance` Vite dev servers as one
  Aspire resource graph behind the `Aspire: Full stack (F5)` launch. Aspire starts each UI resource only once its OP is
  ready (`WithHttpHealthCheck("/health/ready")` + `WaitFor`), and its dashboard shows ordered startup, logs and traces
  in one place. Ports are pinned (`api` on `https://localhost:5001`, `conformanceapi` on `https://localhost:5003`, and
  `console` on `https://localhost:5005`, and `playground` and `conformance` on `https://localhost:5174` and `5175`,
  un-proxied) so the seeded
  redirect URIs, the developer CORS origins and the browser bootstrap document stay valid. Every browser surface serves
  HTTPS with the ASP.NET Core development certificate the OP hosts use (`WithHttpsDeveloperCertificate`), so the browser
  trusts one certificate across the stack and signs in through the seeded `https://…/callback` redirect URIs; the
  AppHost has no `http` launch profile and never needs `ASPIRE_ALLOW_UNSECURED_TRANSPORT`. `apps/auth` is a
  build-watcher with no endpoint whose `dist` the host serves, so `/account/login` works on an F5 launch. Running the
  AppHost requires a container runtime (Docker or Podman), and the AppHost is never packed. `build/frontend-install.ps1`
  is a one-time pnpm-workspace bootstrap, and Aspire runs `pnpm dev`.

- **The host's local database is PostgreSQL run by the Aspire AppHost, migrated by a dedicated worker** (issue
  [#144](https://github.com/NCodeGroup/identity/issues/144),
  [ADR-1073](docs/decisions/1073-postgres-dev-database-and-aspire-migration-worker.md)). A stable named data volume
  persists data across restarts, and `build/dev-db-reset.ps1` (detecting Docker or Podman) purges it on demand. The
  `NCode.Identity.OpenId.Host.MigrationService` worker applies the EF Core migrations and exits, and the OP instances
  wait for its completion, so a migration failure shows as a failed dashboard resource that blocks them. The migrations
  and Npgsql wiring live in the host-owned `NCode.Identity.OpenId.Host.Database` project the hosts and the worker share,
  where each of its two contexts has one `InitialCreate` migration that creates its whole model
  ([ADR-1394](docs/decisions/1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md)).
  When a context's migrations history records a migration the project does not hold, the worker applies nothing and
  fails with a message that names the context and says to reset the local database with `build/dev-db-reset.ps1`.
  The host persists its Data Protection key ring in the same database (`PersistKeysToDbContext`), so one purge resets
  the OpenID data, the tenants' master keys and signing keys, and the ring together, with nothing on the filesystem; the
  per-machine development root key lives outside the database, so a purged database gets fresh master keys under it.

- **The LocalDev AppHost gives its OP instances one development root key.** It generates one development root key per
  machine (in local application data, never in the repository) and gives it to both OP instances that share the
  development database through the `local` root key provider; a Development host started any other way uses an ephemeral
  root key.

- **The AppHost fails fast when the frontend toolchain is stale.** Before any resource starts it requires a Node of the
  major pinned in `frontend/.node-version` and a `node_modules` that matches the pinned pnpm and `pnpm-lock.yaml`,
  and otherwise stops with one message that names `./build/frontend-install.ps1`
  ([ADR-1287](docs/decisions/1287-the-apphost-fails-fast-when-the-frontend-toolchain-is-stale.md), issue
  [#401](https://github.com/NCodeGroup/identity/issues/401)).

- **The standalone Vite surfaces serve HTTPS outside Aspire too.** `./build/frontend-install.ps1` exports the ASP.NET
  Core development certificate as PEM, and the `ncode:serve-over-https` preset plugin serves it when `pnpm dev` runs
  `playground` or `conformance` directly, so the seeded redirect URIs and developer CORS origins for ports
  5174–5175 are `https://` only (issue [#402](https://github.com/NCodeGroup/identity/issues/402)).

- **The host ships a production container image.** `hosts/NCode.Identity.OpenId.Host/Dockerfile` builds the host on the
  chiseled `aspnet:10.0-noble-chiseled-extra` runtime. The image has no shell, runs as the non-root `app` user (UID
  1654), and exposes only port 8080. It builds the `apps/auth` interactive bundle and serves it from `/app/auth`, so the
  container renders login and consent, and it carries the migration worker in `/app/migrations`. The SDK and Node stages
  cross-build on the build platform, so the image builds for `linux/amd64` and `linux/arm64` without emulation in one
  multi-architecture build, whose NuGet cache mount is locked so the two publishes take turns rather than reading
  packages the other is still extracting; the `Host image` CI workflow builds both. The build context is an allow-list
  ([ADR-1178](docs/decisions/1178-the-host-image-is-chiseled-non-root-multi-arch-and-carries-the-login-bundle.md)).

- **The host image writes one-line JSON logs.** The image sets `Logging__Console__FormatterName=json`,
  `Logging__Console__FormatterOptions__UseUtcTimestamp=true` and
  `Logging__Console__FormatterOptions__TimestampFormat=O`, so every entry, the migration worker's included, is one JSON
  object on one line with a UTC timestamp; `Logging__Console__FormatterName=simple` selects text
  ([ADR-1248](docs/decisions/1248-the-host-image-writes-one-line-json-logs.md), issue
  [#265](https://github.com/NCodeGroup/identity/issues/265)).

- **The management console host ships a production container image.**
  `hosts/NCode.Identity.ManagementConsole.Host/Dockerfile` builds the console host on the host image's pinned chiseled
  base images: no shell, the non-root `app` user, port 8080, the `apps/mgmt` bundle served from `/app/mgmt`, and
  one-line JSON logs; the `Console image` CI workflow builds it for `linux/amd64` and `linux/arm64`
  ([ADR-1458](docs/decisions/1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md),
  issue [#610](https://github.com/NCodeGroup/identity/issues/610)).

- **The host has a Helm chart.** `charts/ncode-identity-host` deploys `NCode.Identity.OpenId.Host` on Kubernetes 1.30 or
  later under the restricted Pod Security Standard: non-root, read-only root filesystem, no capabilities, and no default
  service-account token (a cloud root-key provider gets only the projected token its workload identity exchanges). The
  startup and liveness probes call `/health/live` and the readiness probe calls `/health/ready`, each sending an allowed
  `Host` header, and a `preStop` sleep drains each pod before shutdown. The chart ships a PodDisruptionBudget and an
  optional `Ingress`, and its server pods carry `app.kubernetes.io/component: server`, so the Service and
  PodDisruptionBudget never select the migration Job. The database and Valkey connection strings and the local root key
  are referenced as existing Secrets and the recovery public key as a ConfigMap, never passed as values. Rendering fails
  for a release the host would refuse to start (an empty or wildcard `allowedHosts`, a missing database Secret, a
  `StaticSingle` issuer that is not `https`, a missing or incomplete root key provider, `ephemeral` or a local provider
  without its Secret in Production, a Production release with no recovery decision), for more than one replica without
  Valkey, and for unknown values. Without Valkey the chart uses the `Recreate` deployment strategy, which waits for the
  old pod to terminate before starting the next, so it never runs two replicas at once. The `Host chart` CI workflow
  lints the chart, renders each example release, and checks that each unsafe release is refused
  ([ADR-1179](docs/decisions/1179-the-host-chart-references-its-secrets-and-refuses-a-release-the-host-would-refuse.md)).

- **The host chart deploys the management console on a host of its own.** With `managementConsole.enabled`,
  `charts/ncode-identity-host` runs the console host under the restricted Pod Security Standard with its own
  ServiceAccount without a token, probes `/health/live` and `/health/ready`, and
  routes every path of `managementConsole.host` to the console through an Ingress of its own. The console calls the
  management API at `/api` on its authority's origin, the URL the server compares its DPoP proofs with, and machine
  callers reach the API at the server's own host. Its `mgmt-client` and `mgmt-dpop` key rings,
  `managementConsole.keyRings.client` and `.dpop`, each hold a PEM key mounted read-only from a Secret (`pemSecret`),
  keys that AWS KMS, Azure Key Vault or Google Cloud KMS holds (`remoteKeys`, by key id), or both. The chart mounts the
  console's own workload identity for each service a remote key names (`managementConsole.secretProtection`), so the
  console's private keys can stay out of its pods. Rendering fails for a missing or wildcard console host, an authority
  that is not `https`, a key ring with no key, two keys of a key ring with one id, a remote key whose id is not its
  service's key form, a half-set console identity, or more than one console replica without the console's own
  Valkey
  ([ADR-1458](docs/decisions/1458-the-management-console-ships-as-its-own-image-and-an-opt-in-component-of-the-host-chart.md),
  [ADR-1485](docs/decisions/1485-the-host-chart-holds-the-consoles-key-rings-in-a-key-management-service-under-the-consoles-own-identity.md),
  [ADR-1488](docs/decisions/1488-the-management-console-reaches-the-management-api-at-its-authoritys-origin-and-its-host-serves-only-the-console.md),
  issues [#610](https://github.com/NCodeGroup/identity/issues/610),
  [#712](https://github.com/NCodeGroup/identity/issues/712) and
  [#720](https://github.com/NCodeGroup/identity/issues/720)).

- **The host's chart migrates the database before each rollout.** The chart runs the image's migration worker as a Helm
  `pre-install` and `pre-upgrade` Job, so a failed migration fails the release before any pod is replaced. The Job runs
  restricted like the server, with the host's `fsGroup` so its non-root user can read a mounted secret, and
  `migrations.database.existingSecret` gives it a connection with schema rights the server does not need;
  `migrations.enabled=false` leaves migrations to another mechanism. `extraEnv`, `extraEnvFrom`, `extraVolumes` and
  `extraVolumeMounts` reach both the host's pods and the Job, so a database connection that needs a CA bundle or SSL
  setting works at migration time, and both pod specs share their security contexts, image pull secrets and scheduling
  constraints through `_helpers.tpl`. With a long release name, the `-migrations`-suffixed Job name is shortened to fit
  the 63-character label-value limit. Every migration passes the expand-and-contract gate, so from the first stable
  release, when each context's `InitialCreate` becomes the baseline, a model change adds first and drops in a later
  release (issue [#85](https://github.com/NCodeGroup/identity/issues/85),
  [ADR-1180](docs/decisions/1180-migrations-run-before-the-rollout-and-keep-the-previous-release-working.md),
  [ADR-1394](docs/decisions/1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md)).

- **The family builds with repository-wide engineering conventions.** These are Central Package Management, lockstep
  Nerdbank.GitVersioning versioning, SourceLink-to-GitHub provenance with portable symbols, a scripted Definition of
  Done, CSharpier formatting, and scoped coding-convention instruction files. `global.json` requires .NET SDK 10.0.403
  or a later .NET 10 SDK, the newest release past the three-day cooldown, which Dependabot's `dotnet-sdk` stream keeps
  current, so an SDK whose compiler cannot build the family stops at once with the version it needs
  ([#738](https://github.com/NCodeGroup/identity/issues/738),
  [#746](https://github.com/NCodeGroup/identity/issues/746),
  [ADR-1493](docs/decisions/1493-global-json-requires-the-newest-net-sdk-release-past-the-cooldown.md)). See
  [`docs/decisions/`](docs/decisions/).

- **The family has a logo, and one script draws every file of it.** The mark is `{id}`, the letters i and d between the
  braces of the NCode Group's `{N}`, in the group's blue. `brand/` holds it as SVG masters and as the PNG and ICO files
  each place takes: a NuGet package icon, a favicon set, pictures for GitHub, GitLab and My Apps, badge logos, social
  cards, and lockups with the wordmark "NCode Identity". Every package carries the blue tile as its icon, the host
  chart names it as its own, and the README opens with the lockup. The four web apps show its icon in the browser tab,
  unless a tenant or the console's branding sets its own icon: each links the kit's ICO, SVG and iOS icon, its build
  gives them content-hashed names in the assets its host serves, and the `favicon copies` stage of `./build/dod.ps1`
  keeps every app's copies equal to the kit's. `brand/build.py` draws every file from one geometry, with a drawing of
  its own for each size from 16 to 48 pixels, and stops when a file breaks its platform's limit. `brand/outlines.py`
  converts the wordmark to outlines from a pinned release of Inter, whose license `THIRD-PARTY-NOTICES.md` records
  ([#737](https://github.com/NCodeGroup/identity/issues/737),
  [#745](https://github.com/NCodeGroup/identity/issues/745),
  [ADR-1491](docs/decisions/1491-the-familys-logo-is-the-id-mark-between-the-ncode-braces-and-one-script-draws-every-file-of-it.md),
  [ADR-1492](docs/decisions/1492-every-web-app-shows-the-familys-icon-from-hashed-copies-its-host-serves.md),
  [ADR-1494](docs/decisions/1494-a-tenant-sets-its-sign-in-tabs-icon-as-a-branding-setting-the-hosts-link-in-place-of-the-familys.md)).

- **Feature branches run a fast gate, and the merge queue runs the full gate.** `./build/dod.ps1 -Affected` (with
  `-Base`) runs only the test projects a change reaches through the project graph, and `-Filter` passes a `dotnet test
  --filter` expression through; test assemblies run in parallel in every mode. A pull request into `main` runs the
  affected tests, and the merge queue, `main` and the `release/**` lines run the full gate, so a branch need not be up
  to date with `main` before it merges
  ([ADR-1263](docs/decisions/1263-branches-and-pull-requests-run-an-affected-gate-and-the-merge-queue-runs-the-full-gate.md)).

- **CI runs once per commit of a pull request.** A push runs CI only on `main` and the `release/**` lines; any other
  branch is checked through its pull request or by running the workflow by hand, so each check appears once.

- **Coverage measures hand-written code, and the job summary and Codecov agree.** The full Definition of Done collects
  .NET coverage with `build/coverage.runsettings`, which leaves out source-generator output under `obj/`, the scaffolded
  EF Core migrations and `[GeneratedCode]` members, and keeps the reference host in scope because it ships.
  `codecov.yml` counts a partially covered line as a hit, as ReportGenerator does, so both report the same figure from
  the same lines
  ([ADR-1143](docs/decisions/1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md)).

- **Doc comments cite no decision records, in production code or tests.** No `///` comment under `src/` or `tests/`
  names an ADR or links into `docs/decisions/`, so each package's XML documentation (IntelliSense) and, for the
  registration and management contracts, the OpenAPI document carry no citation that means nothing to a consumer; a
  decision record is cited in a `//` comment, such as `// See`. The `doc comment citations` stage of `./build/dod.ps1`
  (`build/doc-comment-citations.ps1`) fails on any `///` line under `src/` or `tests/` that cites one (issues
  [#453](https://github.com/NCodeGroup/identity/issues/453), [#498](https://github.com/NCodeGroup/identity/issues/498)).

- **The frontend toolchain is current and `pnpm audit` is clean.** It builds with Vite 8, Vitest 5, Storybook 10, ESLint
  10, jsdom 30 and TypeScript 6.0, the newest version typescript-eslint supports (issue
  [#147](https://github.com/NCodeGroup/identity/issues/147) tracks TypeScript 7). Lint uses a flat
  `frontend/eslint.config.js` with `@eslint-react/eslint-plugin` and `eslint-plugin-react-hooks` 7's React Compiler
  checks, and presentational tables such as `GrantTable` and `SecretTable` take a `now` prop from their container rather
  than reading the clock during render. `@types/node` follows the Node runtime major, pinned workspace-wide by a pnpm
  override, `engines.node` states the toolchain's real floor, and Dependabot ignores TypeScript and `@types/node` majors
  so a grouped update brings no upgrade that cannot land
  ([ADR-1147](docs/decisions/1147-frontend-toolchain-follows-the-pinned-runtime-and-supported-upstreams.md)).

- **The frontend runs on Node 26, and its Node and pnpm versions are each set in one place.** Node 26 is in Active LTS
  and supported until 2029-04-30. `frontend/.node-version` is the only place the Node major is set: CI reads it
  (`node-version-file`), and so do the Aspire AppHost and `build/frontend-install.ps1`, which pick the NVS install by
  version, and the `Host image` workflow checks the Dockerfile's Node image against it. `engines.node` is `>=26.0.0` and
  `@types/node` is 26. `build/frontend-install.ps1` adds the pinned Node through NVS if it is missing and installs,
  without Corepack, the exact pnpm the `packageManager` field pins; CI uses `pnpm/action-setup`. pnpm is 10.34, the
  newest major Dependabot supports, because Dependabot cannot read the lockfile pnpm 12 writes (issue
  [#153](https://github.com/NCodeGroup/identity/issues/153)). Every pnpm setting lives in `pnpm-workspace.yaml`, and a
  dependency's install script fails `pnpm install` until it is allowed or denied there; esbuild's is denied because
  esbuild does not need it
  ([ADR-1152](docs/decisions/1152-frontend-node-and-pnpm-are-pinned-once-and-provisioned-without-corepack.md)).

- **Frontend tests fail on warnings, and frontend coverage holds a floor.** A test that logs `console.error` or
  `console.warn` fails, through a console guard in the test-only `@ncode/test-support` workspace (the usual culprit is a
  React "not wrapped in act(...)" warning, meaning the test asserted before an update settled). A test that makes no
  assertion fails, lint fails on any warning, and tests, stories and setup files are type-checked. Each workspace's
  coverage measures every source file and holds a floor at its level, enforced by `pnpm run test:report` in CI. The
  conformance runner's CLI logic lives in a tested `cli.ts`, and its `--help` documents exit code `2`
  ([ADR-1151](docs/decisions/1151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md)).

- **The JOSE serializer encodes and decodes the Compact, Flattened and General JSON serializations, with multiple
  signatures and recipients (RFC 7515 §7.2, RFC 7516 §7.2).** The JSON serializations carry multiple signatures (JWS) or
  multiple recipients (JWE), unprotected header members and JWE additional authenticated data (`aad`), the shape many
  verifiable-credential and multi-audience scenarios require. One encode-options model covers every serialization:
  `JoseSigningOptions` / `JoseEncryptionOptions` select a `JoseSerializationFormat` (`Compact` / `Flattened` /
  `General`), with signatures and recipients always expressed as collections (`JoseSignatureOptions` /
  `JoseRecipientOptions`); compact is the one-element case. The shared content-encryption (`enc`) and compression
  (`zip`) algorithms live on the operation, per-recipient key management is carried by `JoseKeyManagementCredentials`,
  and `KeyManagementAlgorithm.SupportsKeyWrapping` gates multi-recipient encodes (direct `dir` / `ECDH-ES` agreement is
  single-recipient). `IJoseSerializer.DecodeAsync` / `DeserializeAsync` auto-detect the JSON serialization and verify
  or decrypt against each signature or recipient until one matches the supplied key; a recipient the key does not
  decrypt is skipped, and any other failure, such as a key management service that cannot be reached, ends the
  decode. Decoding is asynchronous: a JWE's content key is unwrapped through `KeyManagementAlgorithm.UnwrapKeyAsync`,
  which a key held in a KMS unwraps through its service, and a JWS decodes synchronously behind its `ValueTask`.
  Encoding is asynchronous (`JoseEncoder.EncodeAsync`), and every signature goes through
  `SignatureAlgorithm.SignAsync`, which signs with a local key synchronously behind its `ValueTask` and verifies a
  remote key's signature before a token carries it
  ([#75](https://github.com/NCodeGroup/identity/issues/75),
  [ADR-1472](docs/decisions/1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md),
  [ADR-1477](docs/decisions/1477-a-tenants-encryption-keys-can-be-held-in-a-key-management-service-that-decrypts-every-content-key.md)).

- **`IJoseSerializer` works on a parsed `JoseToken`, defined in a contracts-only abstractions package.** `Parse(string)`
  returns a `JoseToken` once, exposing the JOSE header through `JoseToken.Header` before validation or decryption, and
  `DecodeAsync` / `DeserializeAsync<T>` / `VerifyJws` operate on that parsed token; one-shot overloads accept a raw token
  `string`. `JoseToken` is an abstract base in `NCode.Identity.Jose.Abstractions` with a `JoseSerializationFormat` and
  two sealed subclasses, `CompactJwt` (compact serialization) and `JsonJwt` (flattened/general JSON serialization), and
  `DecodedJwt` is constructed from a `JoseToken`. Typed `DeserializeAsync<T>` / `VerifyJws<T>` come as explicit
  no-options / with-options overload pairs, with no optional-parameter overloads, satisfying the public-API analyzer.
  `NCode.Identity.Jose.Abstractions` holds contracts only and references only `NCode.Identity.Secrets.Abstractions`, not
  `NCode.Identity.Json`, `Microsoft.Extensions.Primitives` or `NCode.Buffers`: `CompactJwt` exposes its segments as
  zero-allocation `ReadOnlyMemory<char>` slices that the implementation computes by span-splitting the token once,
  keeping the abstractions package free of logic and heavy transitive references.

- **The JOSE library creates and validates DPoP proofs.** `IDPoPProofService`, registered by `AddJoseLibrary()`, signs a
  `dpop+jwt` proof through `CreateProofAsync` (RFC 9449) with any asymmetric `JoseSigningCredentials`. The proof embeds the public `jwk` and
  carries the `htm`, `htu`, `jti`, `iat`, and optional `ath` and `nonce` claims. `TryValidateProof` checks the
  structure, the `typ`, the absence of a `crit` header, an allow-listed asymmetric `alg` that matches the `jwk` key
  type, the signature, the HTTP binding, `ath`, and `iat` freshness, and returns the `jkt` thumbprint, `jti`, and
  `nonce`. The `jwk` is imported through the registered key types and verified by the registered signature algorithms,
  so EdDSA proofs (Ed25519 and Ed448) work once the opt-in EdDSA package is registered. The OpenId DPoP validator
  consumes the service and keeps the server-nonce challenge and the replay cache. The DPoP vocabulary is
  `JoseTokenTypes.DPoPJwt` and `JoseClaimNames.Payload.Htm`, `.Htu`, and `.Ath`
  ([ADR-1176](docs/decisions/1176-dpop-proofs-are-a-jose-building-block.md),
  [ADR-1472](docs/decisions/1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md)).

- **Post-quantum ML-DSA signing (FIPS 204 / RFC 9964) is in the core libraries.** The JWA `alg` values `ML-DSA-44`,
  `ML-DSA-65` and `ML-DSA-87` are registered by default beside RSA and ECC, with no opt-in package, on the in-box .NET
  10 `System.Security.Cryptography.MLDsa` primitive with no third-party cryptographic dependency. A first-class `AKP`
  (Algorithm Key Pair) `AsymmetricSecretKey` (`AkpSecretKey`; JWK `kty` `AKP`, `alg` `ML-DSA-*`, raw `pub`, 32-byte-seed
  `priv`) and its `ISecretKeyTypeDescriptor` provide generate, JWK/PEM public import, JWK and SPKI-PEM public export,
  and PKCS#8 / SPKI deserialize, with the private key held data-protected in memory. The `ML-DSA-*` algorithms are
  advertised only where `MLDsa.IsSupported` (OpenSSL 3.5+ / recent Windows 11), so discovery and JWKS never offer an
  algorithm the running OS cannot fulfill; discovery lists them in the asymmetric token-signing metadata automatically
  (the signing-algorithm partition keys on `AsymmetricSecretKey`)
  ([ADR-1118](docs/decisions/1118-ml-dsa-post-quantum-signatures-in-core.md)).

- **EdDSA signing over Ed25519 and Ed448, and ECDH-ES over X25519 and X448, are available as the opt-in
  `NCode.Identity.Jose.EdDsa` package.** It registers the fully-specified JWA `alg` values `Ed25519` and `Ed448` (RFC
  9864) and, for interop, the polymorphic `EdDSA` (RFC 8037), which RFC 9864 deprecates; one `EdDsaSignatureAlgorithm`
  serves all three codes and, for `EdDSA`, selects the curve from the signing key's `crv`. It also registers X25519 and
  X448 (RFC 7748) as the `OKP` agreement of `ECDH-ES`, `ECDH-ES+A128KW`, `ECDH-ES+A192KW` and `ECDH-ES+A256KW` (RFC
  8037 §3.2). The algorithms are implemented clean-room from the specifications (RFC 8032 / 8037 / 7748 / 8410):
  constant-time field, scalar and group arithmetic composing only the BCL hash primitives (`SHA512`, `Shake256`), with
  zero third-party cryptographic dependency in shipping code. The package builds on `NCode.Identity.Jose.Abstractions`
  and `NCode.Identity.Secrets.Abstractions`, not on the JOSE implementation. It adds a first-class `OKP` (Octet Key
  Pair) `AsymmetricSecretKey` (`OkpSecretKey`; JWK `kty` `OKP`, `crv` `Ed25519` / `Ed448` / `X25519` / `X448`) with its
  `ISecretKeyTypeDescriptor`: generate, JWK/PEM public import, JWK and SPKI-PEM public export, and PKCS#8 / SPKI
  deserialize (RFC 8410) on every one of the four curves, with the private seed held data-protected in memory and
  exported only through `ExportPrivateKey`. The ID-token `at_hash`/`c_hash`/`s_hash` hash is SHA-512 for Ed25519 and
  SHAKE256 with 114 bytes of output for Ed448. A host enables the package with
  `AddSecretsLibrary().AddJoseLibrary().AddEdDsa()`, an extension of `IServiceBuilder<JoseLibrary>`; discovery then
  advertises its signature algorithms in the asymmetric token-signing metadata automatically, and the FAPI 2.0 signing
  ceiling is PS256, ES256, EdDSA and Ed25519
  ([ADR-1117](docs/decisions/1117-eddsa-clean-room-implementation-opt-in-package.md),
  [ADR-1272](docs/decisions/1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md),
  [#336](https://github.com/NCodeGroup/identity/issues/336),
  [ADR-1390](docs/decisions/1390-x25519-and-x448-agree-keys-beside-the-okp-signing-keys-and-a-curves-use-separates-them.md)).

- **Selective Disclosure for JWTs (SD-JWT, RFC 9901) is available as the opt-in `NCode.Identity.Jose.SdJwt` package.**
  `AddSdJwt()`, an extension of `IServiceBuilder<JwtLibrary>`, registers `ISdJwtIssuer`, `ISdJwtHolder` and
  `ISdJwtVerifier`. The issuer takes a claims object and the JSON Pointers (RFC 6901) of the claims the holder may
  disclose selectively, object members and array elements at any depth, and moves each into a salted disclosure
  whose digest it writes in its place, in the object's sorted `_sd` array or as a `{"...": digest}` array element,
  with decoy digests where asked, the `_sd_alg` algorithm, and the holder's public key in `cnf`; a nested pointer
  puts the inner digest in the outer disclosure's value. The holder parses the combined format
  `<Issuer-signed JWT>~<Disclosure 1>~...~<Disclosure N>~[<KB-JWT>]`, resolves each disclosure to the claim it
  discloses and the disclosure that carries its digest, and presents a chosen subset with the disclosures it depends
  on, binding the presentation with a `kb+jwt` Key Binding JWT over `iat`, `aud`, `nonce` and the `sd_hash` of the
  presentation when the verifier supplied an audience and a nonce. The verifier validates the issuer-signed JWT under
  the caller's `JwtValidationParameters`, resolves the disclosures into the payload, drops the decoys, verifies the
  Key Binding JWT against the `cnf` key, the verifier's audience and nonce, the presentation's digest and its
  maximum age, and returns one claims object with every disclosed value in place and the `_sd`, `_sd_alg` and `...`
  members removed, or the `SdJwtVerificationFailureCodes` code of the rule the presentation failed. The `_sd_alg`
  hash algorithms (`sha-256`, `sha-384`, `sha-512` built in) and the `cnf` confirmation methods (`jwk` built in) are
  registered data, and each `cnf` method has exactly one resolver, validated when the verifier is composed. The
  SD-JWT VC profile, `SdJwtProfiles.SdJwtVc`, types the issuer-signed JWT `dc+sd-jwt`, requires `vct`, keeps
  `iss`, `nbf`, `exp`, `cnf`, `vct`, `vct#integrity` and `status` in the signed payload, and passes `status`
  through to the verified claims; an application defines another `SdJwtProfile` as data, whose required claims
  are protected whether or not it lists them
  ([#73](https://github.com/NCodeGroup/identity/issues/73),
  [ADR-1405](docs/decisions/1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md),
  [ADR-1406](docs/decisions/1406-the-sd-jwt-vc-profile-is-data-the-issuer-and-the-verifier-apply.md)).

- **ES256K over secp256k1 is available as the opt-in `NCode.Identity.Jose.Secp256k1` package.** `AddSecp256k1()`, an
  extension of `IServiceBuilder<JoseLibrary>` as `AddEdDsa()` is, registers the secp256k1 curve (`crv` `secp256k1`,
  RFC 8812) in the elliptic-curve catalog, so `ecc` keys on it are generated, imported and published as JSON Web Keys,
  and the `ES256K` JWS algorithm (ECDSA over secp256k1 with SHA-256), bound to the curve. It registers nothing where the
  platform's cryptography lacks the curve, as on macOS ([#337](https://github.com/NCodeGroup/identity/issues/337),
  [ADR-1392](docs/decisions/1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md)).

- **CBOR Object Signing and Encryption (COSE, RFC 9052 / 9053) is available as the opt-in `NCode.Identity.Cose` package.**
  `AddCoseLibrary()`, an extension of `IServiceBuilder<JoseLibrary>`, registers `ICoseSerializer`, which signs
  asynchronously (`EncodeSign1Async`, `EncodeSignAsync`), encodes, verifies and decrypts `COSE_Sign1`, `COSE_Sign`, `COSE_Mac0`, `COSE_Mac`, `COSE_Encrypt0` and `COSE_Encrypt` messages
  with protected and unprotected header buckets, external additional data, detached content and an optional tag; the
  content of a message is readable only from `Verify` or `Decrypt`, and `Inspect` returns its type and headers alone.
  The algorithms are registered `CoseAlgorithm` data keyed by the IANA identifiers and bound to JOSE algorithms, so the
  JOSE implementations serve them: ES256, ES384, ES512, ESP256, ESP384, ESP512, ES256K, EdDSA, Ed25519, Ed448, PS256,
  PS384, PS512, ML-DSA-44, ML-DSA-65 and ML-DSA-87 signatures, HMAC 256/256, 384/384 and 512/512, AES-GCM, direct, AES
  key wrap and RSAES-OAEP with SHA-256 and SHA-512 recipients, with HMAC 256/64, RSAES-OAEP with SHA-1 and
  RSASSA-PKCS1-v1_5 offered as descriptors that `AddCoseAlgorithm` registers. `ICoseKeyConverter` reads and writes
  public `COSE_Key` and `COSE_KeySet` structures through registered key type and curve descriptors mapped onto JSON Web
  Keys and the registered secret key types, for `OKP`, `EC2`, `RSA` and `AKP` keys. A `COSE_Key` whose `key_ops` is empty or combines signature and
  encryption operations is refused, and its operations otherwise set the key's use. A `kid` is an opaque byte string
  that `CoseKeyIdentifiers` names as a key's `KeyId`: its UTF-8 text, or its CBOR diagnostic notation (`h'a1b2'`) when
  it is not text, and keys are matched by the bytes. Everything it writes is in the core
  deterministic encoding of RFC 8949 §4.2.1, and `CoseOptions` bounds the size, nesting, signatures and recipients of
  what it reads
  ([#76](https://github.com/NCodeGroup/identity/issues/76),
  [ADR-1409](docs/decisions/1409-cose-messages-are-an-opt-in-package-family-over-system-formats-cbor-and-the-familys-keys-and-algorithms.md),
  [ADR-1472](docs/decisions/1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md)).

- **CBOR Web Tokens (CWT, RFC 8392) are available as the opt-in `NCode.Identity.Cwt` package.** `AddCwtLibrary()`, an
  extension of `IServiceBuilder<CoseLibrary>`, registers `ICwtIssuer` (`IssueAsync`) and `ICwtValidator`. The issuer writes the
  registered claims, the confirmation key as a public `COSE_Key` and any further claims in the deterministic encoding,
  and protects them with a `COSE_Sign1` or `COSE_Mac0` message typed by the protected `typ` header when a token type is
  given, optionally inside a `COSE_Encrypt0` or `COSE_Encrypt` message and optionally under the CWT tag. The validator
  takes `CwtValidationParameters`: the permitted algorithms and issuers, the verification keys or a resolver of them,
  the audiences, the decryption keys, the token type, the clock skew, the lifetime requirements and the required
  claims; it verifies the COSE layer first, then reads the claims into a `CwtClaimsSet`, and returns the claims or a
  `CwtValidationFailureCodes` or `CoseFailureCodes` code
  ([#76](https://github.com/NCodeGroup/identity/issues/76),
  [ADR-1410](docs/decisions/1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md),
  [ADR-1472](docs/decisions/1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md)).

- **A package contributes an ECDSA algorithm as data.** An `EccSignatureAlgorithmSpecification` service (in
  `NCode.Identity.Jose.Abstractions`) names an `alg` code, its one curve and its hash, and the JOSE library serves an
  ECDSA algorithm for each registered one; `AddJoseLibrary()` registers `ES256`, `ES384` and `ES512` the same way, as
  `EccSignatureAlgorithmSpecification.Es256`, `Es384` and `Es512`. Building the algorithm collection fails on a null
  specification, an empty, whitespace or duplicate code, or a curve the elliptic-curve catalog does not hold by both
  `crv` and OID. `TryAddEnumerableInstance` in `NCode.Identity.Registration` registers such an instance once without dropping
  the other instances of its type ([#337](https://github.com/NCodeGroup/identity/issues/337),
  [ADR-1392](docs/decisions/1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md)).

- **A keyed algorithm declares the key types it accepts, and decides which keys fit it.** `KeyedAlgorithm.KeyTypes`
  lists `AlgorithmKeyType` values, in the algorithm's order of preference: the `SecretKey` CLR type (`KeyClrType`), the
  legal sizes and, for a key type on curves, the curves (`AlgorithmCurve`, a `crv` with its key size). `TryGetKeyType`
  matches a key type and `AcceptsOnly<TKey>()` checks a family. `AlgorithmKeyType.OnCurves` builds a key type on curves
  and takes its sizes from them, and `AlgorithmKeyType.IsKeyCompatible` checks a key's type, size and curve: an
  asymmetric key names its curve (`AsymmetricSecretKey.CurveName`, which an `ecc` key takes from the elliptic-curve
  catalog). `KeyedAlgorithm.IsKeyCompatible(SecretKey)` applies that one rule for the credential selector, DPoP proofs
  and every ECDH agreement, and `SignatureAlgorithm.GetTokenHashFunction` supplies the hash of the ID-token `at_hash`,
  `c_hash` and `s_hash` claims. `AuthenticatedEncryptionAlgorithm` derives from `Algorithm`, because its key is a
  content encryption key ([#327](https://github.com/NCodeGroup/identity/issues/327),
  [ADR-1272](docs/decisions/1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md),
  [ADR-1389](docs/decisions/1389-one-algorithm-name-serves-every-key-type-it-declares.md)).

- **Keys are resolved, checked and generated on their algorithm's curve.** `TryGetKeyedAlgorithm` on
  `IAlgorithmCollection` returns a registered signature or key-management algorithm with the use it implies.
  `TryResolveKeyGeneration` resolves a `KeyGenerationDeclaration` (CLR type, optional size, optional curve, use and
  algorithm) into a `ResolvedKeyGeneration`: for a key type on curves the key is generated on the named curve, which the
  algorithm must accept, or on the one curve it accepts that has the requested size (`AlgorithmKeyType.GetCurves`), or
  on its first curve when neither is given; a size that two of the algorithm's curves have, such as P-256 and a
  contributed Brainpool curve for `ECDH-ES`, is refused with the curves that have it, and a curve named for a key type
  the algorithm binds to no curve is refused. Tenant key provisioning and management secret creation share it, so
  `ES256K`, `Ed25519` and `Ed448` keys, and `OKP` keys for the polymorphic `EdDSA` and for `ECDH-ES`, are generated on
  the right curve. `ValidateKeyAlgorithm` checks a declared key itself: a `KeyAlgorithmDeclaration` carries the key,
  whose type, size and curve the algorithm checks with `IsKeyCompatible`. `ValidateKeySize` checks a held symmetric
  key, whose material is not at hand, by a `KeySizeDeclaration` of its type and size, and refuses an asymmetric key
  type, which is checked by its stored public half ([#327](https://github.com/NCodeGroup/identity/issues/327),
  [#337](https://github.com/NCodeGroup/identity/issues/337), [#534](https://github.com/NCodeGroup/identity/issues/534),
  [ADR-1389](docs/decisions/1389-one-algorithm-name-serves-every-key-type-it-declares.md),
  [ADR-1392](docs/decisions/1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md),
  [ADR-1398](docs/decisions/1398-a-key-size-selects-a-curve-only-when-no-other-curve-shares-it.md)).

- **A key type refuses a curve requested with another size, and a size two of its curves share.** The `ecc`, `okp`
  and `AKP` secret key types refuse generation parameters that name a curve, or an ML-DSA parameter set, whose keys
  are not the requested `SecretKeyGenerationParameters.KeySizeBits`, such as P-256 at 384 bits or Ed25519 at 456 bits.
  An `ecc` key requested without a curve is generated on the one curve of the elliptic-curve catalog whose keys are
  that size (`IEccCurveCatalog.GetByCurveSizeBits`) and refused, with the curves named, when several catalog curves
  share it, as P-256 and secp256k1 do with `AddSecp256k1()`; an `okp` key is generated only on a named curve.
  Management secret creation, tenant key provisioning and every other caller of the key types meet the same checks
  ([#534](https://github.com/NCodeGroup/identity/issues/534),
  [ADR-1398](docs/decisions/1398-a-key-size-selects-a-curve-only-when-no-other-curve-shares-it.md)).

- **The EdDsa primitives have benchmarks.** `benchmarks/NCode.Identity.Jose.EdDsa.Benchmarks` measures the field, scalar
  and group arithmetic and the Ed25519 and Ed448 signing and verification of `NCode.Identity.Jose.EdDsa` with
  BenchmarkDotNet, so an optimization of a primitive is justified by a measurement; the build compiles the project and
  nothing runs it ([#470](https://github.com/NCodeGroup/identity/issues/470),
  [ADR-1386](docs/decisions/1386-primitives-are-benchmarked-in-a-project-the-build-compiles-and-nothing-runs.md)).

- **ECDH-ES agrees keys of any registered key type.** `ECDH-ES`, `ECDH-ES+A128KW`, `ECDH-ES+A192KW` and
  `ECDH-ES+A256KW` serve every key type that has a registered `IEcdhKeyAgreement` (in
  `NCode.Identity.Jose.Abstractions`), so a package contributes ECDH-ES over its key type, such as the `OKP` X25519 and
  X448 keys of RFC 8037 §3.2, by registering an agreement beside its key type; the core registers the `EC` agreement
  over P-256, P-384 and P-521, and ECDH-ES accepts an `EC` key only on a curve a registered agreement serves. Several
  agreements may serve one key type, each over its own curves (`IEcdhKeyAgreement.AlgorithmKeyType`), so a package can
  also add ECDH-ES over another `EC` curve: a key is agreed by the one agreement whose key type accepts it, and a key
  two agreements accept is refused. The agreements of one key type all name their curves or all name sizes only; a mix
  fails building the algorithm collection. The algorithms list key types in the order their agreements are registered:
  the core's, through `AddJoseLibrary()`, then each package's in the order the host adds it. The `epk` header is
  written and read through the key type's `ISecretKeyTypeDescriptor` and JSON Web Key rules. `ECDH-ES+A*KW` generates a
  random content encryption key and wraps it under the key agreed with the `epk` (RFC 7518 §4.6)
  ([#327](https://github.com/NCodeGroup/identity/issues/327),
  [ADR-1389](docs/decisions/1389-one-algorithm-name-serves-every-key-type-it-declares.md)).

- **ECDH-ES key agreement uses SHA-256 as the Concat KDF digest on every curve, as [RFC 7518
  §4.6.2](https://datatracker.ietf.org/doc/html/rfc7518#section-4.6.2) requires.** The digest belongs to the JWE
  key-agreement algorithm, not to the key. `NCode.Identity.Jose.Algorithms.KeyManagement.EcdhKeyManagementAlgorithm`
  therefore derives, for `ECDH-ES` and `ECDH-ES+A{128,192,256}KW` on P-256, P-384 and P-521, the same content encryption
  key as any conforming JOSE implementation, and tokens interoperate in both directions. The cross-check tests use
  jose-jwt's RFC-conforming managed implementation (`EcdhKeyManagementUnix`, backed by the BCL `ECDiffieHellman`) as
  their oracle. They do not use the Windows CNG implementation (`EcdhKeyManagementWin` + `CngKey`), because its
  `SP800_56A_CONCAT` KDF derives a curve-dependent digest on P-384 and P-521.
  [ADR-1126](docs/decisions/1126-ecdh-es-concat-kdf-digest-is-sha-256-and-oracles-must-be-rfc-conforming.md) records the
  reasoning and the testing rule.

- **Hash names are open.** `HashAlgorithmName.GetHashSizeBits` and `GetHashFunction` serve every hash the platform
  supports, SHA-3 included, and refuse `MD5` by name ([#377](https://github.com/NCodeGroup/identity/issues/377)).

- **First-class `JSON Web Key` / `JSON Web Key Set` types with `JWK Thumbprint` (RFC 7517 / 7638 / 9278), in the
  `NCode.Identity.Jwk` package.** `JsonWebKey` and `JsonWebKeySet` are rich, immutable types backed by a detached
  `System.Text.Json.JsonElement` (no backing `JsonDocument`, no cloning). `JsonWebKey` round-trips any key type
  (`RSA`/`EC`/`OKP`/`oct`/`AKP`/…) with full member fidelity, surfaces the standard members as typed accessors, exposes
  key-type-specific members by name (`GetMember`), detects and strips private material (`ContainsPrivateKeyMaterial` /
  `ToPublicKey`), and computes the RFC 7638 thumbprint and RFC 9278 thumbprint URI (`ComputeThumbprint` /
  `ComputeThumbprintUri`), the `jkt` fingerprint DPoP and mutual-TLS bind tokens to.
  `NCode.Identity.Secrets.Abstractions` references the package, so JWK is first-class in the secrets and JOSE stacks
  without any `*.Abstractions` package carrying logic. Public `JsonWebKey(JsonElement)` / `JsonWebKey(JsonObject)`
  constructors take in-memory nodes and `JsonWebKey.Parse(string)` takes JSON text. The key type descriptor seam
  ([ADR-1105](docs/decisions/1105-secret-key-type-descriptor-first-class-seam.md)) owns per-key-type member production;
  `ExportedPublicKey.Jwk` is a `JsonWebKey`, the JWKS endpoint returns a `JsonWebKeySet`, and the DPoP proof validator
  parses the embedded key into a `JsonWebKey` and thumbprints it with `ComputeThumbprint()`
  ([ADR-1119](docs/decisions/1119-first-class-jwk-jwks-and-thumbprint.md)).

- **The JWK package names the JSON Web Key vocabulary.** `NCode.Identity.Jwk` has `JsonWebKeyUses`,
  `JsonWebKeyOperations`, `JsonWebKeyTypes`, `JsonWebKeySetParameterNames` and `JsonWebKeyParameterNames`.
  `JsonWebKeyParameterNames` names every RSA private member, `FirstPrimeFactor` (`p`), `SecondPrimeFactor` (`q`),
  `FirstFactorCrtExponent` (`dp`), `SecondFactorCrtExponent` (`dq`), `FirstCrtCoefficient` (`qi`) and `OtherPrimesInfo`
  (`oth`), which the RSA key type descriptor uses, and `D` names the `d` member, which is the RSA private exponent, the
  EC private key and the OKP private key. The protocol client, the OP's client key import and registration, and ECDH key
  management use the vocabulary, and `SecretKeyUses` takes its values from `JsonWebKeyUses`
  ([#357](https://github.com/NCodeGroup/identity/issues/357),
  [#364](https://github.com/NCodeGroup/identity/issues/364)).

- **A key type registers its JSON Web Key rules.** A `JsonWebKeyTypeDescriptor` carries a key type's thumbprint members
  and private members; `JsonWebKeyTypeDescriptors` holds the built-in IANA types, and `JsonWebKey.KeyTypeDescriptor`
  exposes the one a key uses, set through the `JsonWebKey(JsonElement, JsonWebKeyTypeDescriptor)` and
  `JsonWebKey(JsonObject, JsonWebKeyTypeDescriptor)` constructors. `ISecretKeyTypeDescriptor.JwkKeyType` is a
  `JsonWebKeyTypeDescriptor?`. DPoP proof validation, client key import and provider key import build typed keys, so a
  contributed key type's thumbprint and private members work
  ([ADR-1271](docs/decisions/1271-a-key-type-registers-its-json-web-key-rules.md)).

- **A JSON Web Key Set types the keys of contributed key types and skips a key without a usable `kty`.** The constructor
  `JsonWebKeySet(JsonElement, IEnumerable<JsonWebKeyTypeDescriptor>)` and `JsonWebKeySet.Parse(string,
  IEnumerable<JsonWebKeyTypeDescriptor>)` build each key whose `kty` names a supplied descriptor with that descriptor,
  so a contributed key read from a set is thumbprinted and screened for private members by its own rules; pass the
  `JwkKeyType` of each `ISecretKeyTypeCatalog.Descriptors` entry that has one (it is `null` for a key type with no JSON
  Web Key form). A set deserialized through `System.Text.Json` uses the standard descriptors only. Every constructor and
  `Parse` overload that reads JSON skips an array member that is not a JSON object or whose `kty` is the empty string,
  missing or not a string, and reads the rest of the set, so one malformed key cannot poison the whole set
  ([ADR-1281](docs/decisions/1281-the-jwk-package-takes-contributed-key-types-and-hash-names-from-its-caller.md),
  [#329](https://github.com/NCodeGroup/identity/issues/329)).

- **A JWK Thumbprint URI names every registered hash the framework computes.** `JsonWebKey.ComputeThumbprintUri` takes
  the hash's name from the IANA Named Information Hash Algorithm Registry (RFC 9278 §4) and covers SHA-256, SHA-384,
  SHA-512, SHA3-256, SHA3-384 and SHA3-512 (`sha-256` … `sha3-512`). `ComputeThumbprintUri(HashAlgorithmName, string)`
  takes the registry name of any other hash; a truncated hash name is never produced
  ([ADR-1281](docs/decisions/1281-the-jwk-package-takes-contributed-key-types-and-hash-names-from-its-caller.md),
  [#328](https://github.com/NCodeGroup/identity/issues/328)).

- **`NCode.Identity.Jwt(.Abstractions)` encodes and validates JWTs through segregated services and ASP.NET Core-style
  requirements and handlers.** The package pairs with the sibling `NCode.Identity.Jose(.Abstractions)` and the `Jwt*`
  type naming (`JwtValidationParameters`, `JwtValidationResult`, `JwtValidationContext`, `JwtEncodingParameters`,
  `JwtLibrary` / `AddJwtLibrary`, `Jwt*Exception`). `IJwtEncoder` (`EncodeAsync`) and `IJwtValidator` (`ValidateAsync`) are
  separate services, because validation-only and encode-only consumers are disjoint. Validators are pure-data
  `IJwtValidationRequirement` records (`ClaimValueRequirement`, with `ValidateIssuer`/`ValidateAudience` as sugar over
  it, plus `TokenTypeRequirement`, `TokenLifetimeRequirement`, `CertificateLifetimeRequirement` and the inline
  `AssertionRequirement`) carried on `JwtValidationParameters.Requirements` and executed by `IJwtValidationHandler` /
  `JwtValidationHandler<TRequirement>` strategies resolved from DI; external code contributes its own validators through
  `AddJwtValidationHandler<T>()`. Handlers vote on a shared `JwtValidationContext` (`Succeed`/`Fail`) rather than
  throwing, and the validator aggregates failures into a single `JwtValidationException` on the result
  (`result.IsValid`/`result.Exception`). All contracts, requirement records, fluent builders, exceptions and
  `JsonClaimValueTypes` live in the `.Abstractions` package under purpose namespaces (`…Jwt.Encoding`,
  `…Jwt.Validation`, `…Jwt.Claims`), and the evaluator, handlers, key resolver and claims factory in the implementation
  package; `NCode.Identity.OpenId.Authentication` references `NCode.Identity.Jwt.Abstractions` only
  ([#28](https://github.com/NCodeGroup/identity/issues/28),
  [ADR-1120](docs/decisions/1120-jwt-validation-requirements-and-handlers.md),
  [ADR-1472](docs/decisions/1472-signing-is-asynchronous-from-the-signature-algorithm-to-every-caller.md)).

- **A JWT validation failure carries a machine-readable code.** `JwtValidationFailureCodes` is the open vocabulary of
  the JWT validator's codes (`invalid_signature`, `invalid_issuer`, `invalid_audience`, `expired`, `not_yet_valid`,
  `algorithm_not_permitted`, …). A handler fails with `JwtValidationContext.Fail(failureCode, reason)`, and
  `ClaimValueRequirement.FailureCode` names a claim check's code. `JwtValidationException.FailureCode` and
  `JwtValidationResult.FailureCode` report the first failure's code
  ([ADR-1278](docs/decisions/1278-token-validation-failures-carry-a-machine-readable-code.md),
  [#343](https://github.com/NCodeGroup/identity/issues/343)).

- **JWT audience validation follows [RFC 7519 §4.1.3](https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.3).** An
  `aud` claim is valid when it contains at least one expected value, and extra audiences are allowed. A token minted for
  several audiences, such as `["urn:ncode:management", "urn:ncode:openid"]`, is accepted by each resource that expects
  one of them, so a signed-in management console calls the management API with its token. Issuer validation is
  single-valued.

- **A JWT's lifetime claims are required per token.** `JwtEncodingParameters.EnsureTokenLifetime` controls whether an
  encoded JWT must carry a lifetime, so a JWT that is not a credential can omit `exp`
  ([ADR-1168](docs/decisions/1168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)). On
  validation, `TokenLifetimeRequirement` and `ValidateTokenLifeTimeOptions` carry `RequireNotBefore` and
  `RequireIssuedAt` beside `RequireExpirationTime`
  ([ADR-1187](docs/decisions/1187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md)).

- **JWT claim values map to and from JSON by declared type, losslessly, in one symmetric mapper.** Both directions of
  the JWT claim-value mapping live in one internal `ClaimValueMapper`, guarded by a decode → encode round-trip test over
  every JSON scalar kind. Decoding, a JSON number keeps its exact literal and is typed most specific first:
  `ClaimValueTypes.Integer32`, `Integer64`, `UInteger64`, the unbounded `Integer` for larger integers, and `Double` only
  for a fraction or exponent, so integers above 2^53 round-trip exactly. Encoding, each declared type parses in its XML
  Schema lexical space (invariant culture, `NumberStyles.Integer` / `Float`, so `"1,000"` is not a number); `UInteger32`
  / `UInteger64` emit numbers; `Integer` is unbounded (beyond 64 bits it emits the exact JSON number); `JSON_NULL` emits
  JSON `null`; and `JSON` / `JSON_ARRAY` emit the parsed value only when well formed and of the declared kind. A value
  outside its declared type, including a `Double` of `NaN`/`Infinity` or malformed `JSON`, is emitted as its verbatim
  string. `IJwtEncoder` emits every entry of `JwtEncodingParameters.ExtraPayloadClaims`, including a lone one such as
  `azp` ([#34](https://github.com/NCodeGroup/identity/issues/34),
  [ADR-1122](docs/decisions/1122-jwt-claim-values-map-to-json-by-declared-type.md)).

- **Claim dates parse only by declared type, through the public `NCode.Identity.Json.DateCoercion`.** `DateCoercion` is the
  date/time policy shared by `ObjectJsonConverter`, `JsonElements` and `ObjectGraphExtensions`, and its
  `TryParseDateTimeOffset` and `TryParseDateTime` let every package parse date strings under one lenient,
  invariant-culture policy. The JWT encoder parses a claim typed `ClaimValueTypes.DateTime` through it; a value that
  does not parse, including an instant outside the representable `DateTimeOffset` range, is emitted as its verbatim
  string. The JWT claims-identity factory never sniffs JSON strings for dates: a JSON string always decodes verbatim as
  a `ClaimValueTypes.String` claim (an OpenID Connect `birthdate` of `"1990-03-15"` stays `"1990-03-15"`), and a caller
  parses it with `DateCoercion` where the claim is known to be a date
  ([#32](https://github.com/NCodeGroup/identity/issues/32),
  [ADR-1121](docs/decisions/1121-claim-dates-parse-by-declared-type-through-shared-date-coercion.md)).

- **Applications protect their own data under the secret key hierarchy.** `ISecretProtector` is buffer-first: it writes
  into caller-supplied `IBufferWriter<T>`s, sizes its output with `GetProtectedLength`, and offers a `ProtectToString`
  helper for text columns, so an application protects any sensitive value under a tenant's or the platform's master key
  with a namespaced purpose. `ISecretSerializer` and `ISecretGenerator` run under an `ISecretProtector`,
  `ISecretSerializer.DeserializePublicSecret` reads a public secret without one, and imported public keys (`spki`,
  `x509`) are stored as plain base64url
  ([ADR-1235](docs/decisions/1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).

- **Key wrapping is a shared secrets abstraction.** Every key wrapper implements `IKeyWrapper` (`Name`, `WrapAsync`, and
  `UnwrapAsync`, which writes the key into an `IBufferWriter<byte>`) and returns `WrappedKey` (`WrapperName`, `KeyId`,
  `Ciphertext`); both, with `KeyWrapperNames`, live in `NCode.Identity.Secrets.Abstractions` under
  `NCode.Identity.Secrets.KeyWrapping`, and the offline recovery wrapper implements the same contract. The local wrapper
  is configured through `LocalKeyWrapperOptions`, and the opt-in packages `NCode.Identity.Secrets.AwsKms`,
  `.AzureKeyVault` and `.GoogleCloudKms` register a wrapper with `Add<Vendor>KeyWrapper` and
  `<Vendor>KeyWrapperOptions`. `WrappedKeyCiphertext.GetByteCount` and `WrappedKeyCiphertext.Decode` decode a
  `WrappedKey.Ciphertext` stored as base64url into a caller-supplied span or `IBufferWriter<byte>` and throw
  `CryptographicException` for empty or malformed text; the AES-GCM, AWS KMS, Azure Key Vault and Google Cloud KMS
  wrappers decode through it, so every wrapper reports empty ciphertext as "The wrapped key is empty."
  `NCode.Identity.Secrets.Abstractions` references `NCode.Base64Url`
  ([ADR-1279](docs/decisions/1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md),
  [#373](https://github.com/NCodeGroup/identity/issues/373)).

- **`NCode.Identity.Secrets.AwsKms` wraps the root key with AWS KMS.** `AddAwsKmsKeyWrapper` (`aws-kms`,
  section `SecretProtection:RootKeyProviders:AwsKms`, a key or alias ARN) binds each wrap to its owner through the KMS
  encryption context and takes credentials from the AWS SDK's default chain, never from configuration. The reference
  host and the Helm chart wire it in; the chart federates the pod's service-account token with AWS
  (`secretProtection.awsKms`) only when `aws-kms` is selected, and refuses a malformed key ARN
  ([ADR-1279](docs/decisions/1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md)).

- **`NCode.Identity.Secrets.GoogleCloudKms` and `NCode.Identity.Secrets.AzureKeyVault` wrap the
  root key with Google Cloud KMS and Azure Key Vault.** The Google Cloud KMS wrapper (`google-cloud-kms`,
  `AddGoogleCloudKmsKeyWrapper`, crypto key resource name in `SecretProtection:RootKeyProviders:GoogleCloudKms:KeyName`)
  binds each wrap to its owner through the additional authenticated data and checks CRC32C checksums on every request
  and response. The Azure Key Vault wrapper (`azure-key-vault`, `AddAzureKeyVaultKeyWrapper`, key identifier in
  `SecretProtection:RootKeyProviders:AzureKeyVault:KeyId`) wraps with `RSA-OAEP-256` and refuses an RSA key under 3072
  bits. Both take keyless credentials from their SDK's default chain (Application Default Credentials,
  `DefaultAzureCredential`), never from configuration. The reference host and the Helm chart wire them in; the chart's
  `secretProtection.googleCloudKms` / `secretProtection.azureKeyVault` values mount workload identity credentials only
  for the selected provider and refuse a malformed key name, key identifier or workload identity pool provider
  ([ADR-1260](docs/decisions/1260-azure-key-vault-and-google-cloud-kms-are-root-key-providers.md),
  [ADR-1279](docs/decisions/1279-key-wrapping-is-a-secrets-abstraction-shared-by-the-root-and-recovery-wrappers.md),
  [#173](https://github.com/NCodeGroup/identity/issues/173)).

- **Every tenant is provisioned with persisted keys, so a production deployment can sign from its first request.** The
  JSON Web Key Set is never empty, and the bootstrap administrator's first management token — the only credential that
  can import a key — can be signed. Every tenant is given a persisted key, when it is seeded, for each configured
  algorithm it holds no usable key for: the root tenant on its first request, a tenant created through the management
  API before the create returns, and an existing tenant at its next seed. `OpenIdTenantOptions.KeyProvisioning`
  configures provisioning. The defaults are `RS256` and `PS256` (3072-bit RSA) and `ES256` (P-256).
  `OpenId:Tenant:KeyProvisioning:Keys` names any registered asymmetric signature or key-management algorithm, with an
  optional `KeySizeBits`, an optional `SecretType` that selects which of the algorithm's key types the key is generated
  as, and an optional `Curve` that selects which of the algorithm's curves it is generated on, such as a contributed
  curve that shares P-256's size for `ECDH-ES`. Unset, the type is the algorithm's first key type that has a registered
  asymmetric secret key type and accepts the configured curve and size, and that key type reports a configuration it
  refuses; the curve is the one the algorithm accepts that has the configured size, or the algorithm's first curve. A
  size that two of the algorithm's curves have is reported until `Curve` names one, a curve configured with another
  size is reported, a secret type the algorithm does not accept is reported with the ones it does, and a curve or size
  none of the algorithm's key types accepts is reported with the curves and sizes each of them accepts.
  Configuring `Keys` overrides the defaults, and it is validated when the host starts. A stored key satisfies a
  configured key only when it is on that key's curve and the algorithm accepts its stored public half, so a stored key
  whose public half is missing or unreadable does not suppress generation.
  `OpenId:Tenant:KeyProvisioning:Enabled=false` turns provisioning off for a deployment that brings its own keys. A key
  the tenant holds that serves an algorithm, or will, suppresses generation; a key an operator supplied is never
  overwritten or removed, and a key the tenant cannot unprotect fails the tenant with a message naming the tenant and
  the master key or root key to restore, rather than being regenerated
  ([ADR-1169](docs/decisions/1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [#327](https://github.com/NCodeGroup/identity/issues/327), [#534](https://github.com/NCodeGroup/identity/issues/534),
  [ADR-1389](docs/decisions/1389-one-algorithm-name-serves-every-key-type-it-declares.md),
  [ADR-1398](docs/decisions/1398-a-key-size-selects-a-curve-only-when-no-other-curve-shares-it.md)).

- **Every stored secret has an active window inside its validity window, and every use of a key honors them.** A
  `PersistedSecret` carries `CreatedWhen`, `ActivatesWhen`, `RetiresWhen` and `ExpiresWhen`, in that order
  (`SecretLifecycle`), and a key's `KeyMetadata` carries the same instants. A key signs or encrypts only in its active
  window: `ICredentialSelector` never selects a key published ahead of its activation, a retired key or an expired key.
  A key verifies, decrypts and is published only in its validity window: the JSON Web Token validator never tries an
  expired key, with the clock skew as tolerance, and the JSON Web Key Set publishes every key in its validity window,
  the upcoming and the retired ones included, and never an expired one. `GenerateSecretRequest`,
  `SecretKeyImportRequest` and the management `CreateSecretRequest`, `ImportSecretRequest` and `UpdateSecretRequest`
  name `activatesWhen` and `retiresWhen`, each optional, and a request out of order is refused (`400`); a shared secret
  activates when it is created and retires when it expires. A management update that omits an instant keeps the
  stored one, the retirement bounded by the new expiry, so a retired or upcoming key stays so unless the update names
  the instant; a rotation that stops the rotated secret at once collapses its active window with its expiry.
  `SecretResource` and `CreatedSecretResource` show both instants, `PersistedSecret.GetLifecycleState` names the
  state (`SecretLifecycleStates`: `upcoming`, `active`, `retired`, `expired`), and the management console's secrets
  table shows the instants and the state
  ([ADR-1403](docs/decisions/1403-a-stored-secret-has-an-active-window-inside-its-validity-window-and-every-use-of-a-key-honors-them.md),
  [#166](https://github.com/NCodeGroup/identity/issues/166)).

- **A tenant's provisioned keys roll over on their own, ahead of retirement and with a grace after it.** The tenant
  settings `tenant_key_lifetime` (90 days), `tenant_key_activation_delay` (two days) and `tenant_key_retirement_grace`
  (seven days, raised to the longest lifetime of the tokens the tenant protects with its keys plus its clock skew, the
  settings `OpenId:Tenant:KeyRollover:TokenLifetimeSettings` names, each checked at startup against the registered
  settings) shape a provisioned key's lifecycle. One planner decides what a tenant's keys need: the successors of a
  key that retires within the delay plus the pass interval, generated and published ahead and chained until the keys
  sign past that horizon; a key that activates at once when no key signs, or the activation of a key already
  published; the retirement, for the grace, of a key the rollover provisioned for an algorithm the tenant's settings
  no longer use; a later expiry for a key whose grace was raised since it was generated; and the removal of an expired
  key the rollover provisioned. A provisioned key names the rollover in `PersistedSecret.ManagedBy`
  (`SecretManagers.TenantKeyRollover`, shown as `managedBy`), and a key an operator generated or imported through the
  management API is never retired, extended or removed. The seed applies the plan whenever the tenant is
  materialized, and `TenantKeyRolloverService` applies it in the background every
  `OpenId:Tenant:KeyRollover:Interval` (one hour), one replica per interval under a lease on the shared-state port
  that lasts half the interval, walking each enabled team's tenants under that team's effective settings (a team that
  is disabled, or beneath a disabled team, is skipped, and a team that fails is logged before the next one runs), each
  tenant in its own unit of work under its optimistic concurrency, and announces every tenant it changed so every
  replica serves its keys as they are. A team's settings reconciliation seeds a tenant whose only key for an algorithm
  has retired like one that never held it. Every change is an audit event,
  `tenant.key.generated`, `tenant.key.activated`, `tenant.key.retired`, `tenant.key.extended` or
  `tenant.key.removed`, in the `key` category with the `system` actor, published once the change is committed: the
  pass publishes after its save, and a seed queues its events on `TenantSeedContext.RunAfterCommit`, whose steps the
  provisioner runs once the seed has committed
  ([ADR-1404](docs/decisions/1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md),
  [ADR-1412](docs/decisions/1412-settings-are-reconciled-per-team.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [docs/capabilities/key-rollover.md](docs/capabilities/key-rollover.md), [docs/audit-records.md](docs/audit-records.md),
  [#166](https://github.com/NCodeGroup/identity/issues/166)).

- **A tenant's signing and encryption keys can stay inside a key management service, which signs every token and unwraps
  every content key sent to the tenant.** The tenant setting `tenant_key_provider` names a registered remote key
  provider that creates keys — `aws-kms`, `azure-key-vault` or `google-cloud-kms`, which `AddAwsKmsRemoteKeyProvider`,
  `AddAzureKeyVaultRemoteKeyProvider` and `AddGoogleCloudKmsRemoteKeyProvider`
  register on the secrets library's builder from `SecretProtection:RemoteKeyProviders:<Vendor>` — and every key the
  tenant provisions, signature and encryption alike, is created in that KMS. The server stores each key's public half
  and a `remote` reference, publishes the stored public half in the JSON Web Key Set with its `use` without calling the
  KMS, signs through the KMS, sending a digest and turning an ECDSA DER signature into the `r‖s` form JWS uses, and
  unwraps a JWE's content key through it: `RSA-OAEP` and `RSA-OAEP-256` on all three services and `RSA-OAEP-512` on
  Google Cloud KMS through the service's decryption, and `ECDH-ES` and `ECDH-ES+A128KW`, `+A192KW` and `+A256KW` on AWS
  KMS through `DeriveSharedSecret`, with the Concat KDF and the AES key unwrap run locally. An RSA-OAEP encrypted key
  that is not the size of the key's modulus fails at the tag without a service call, and the JWT validator tries only
  the decryption keys whose id a JWE header's `kid` names, or every eligible key when no key has that id. Registered
  `IRemoteKeyDescriptorSource` instances describe each provider (`RemoteKeyProviderDescriptor`) and contribute the rows
  of the `RemoteKeyAlgorithm` vocabulary — `RemoteSigningAlgorithm` (`RS*`, `PS*` and `ES*`),
  `RemoteDecryptionAlgorithm` and `RemoteKeyAgreementAlgorithm` — that map each JOSE algorithm onto a provider's key
  spec and operation and give the key its use, so a provider holds only the keys its service can use, and a host whose
  provider cannot hold a provisioned key, or holds it for another use, does not start. A provider registered without its
  region, vault URI or key ring (`RemoteKeyProviderDescriptor.CreatesKeys` is `false`) binds a key a KMS already holds
  and reads, signs with, decrypts with and destroys keys by their identifiers, but creates and lists none: the setting
  cannot name it, the management API refuses to create a key in it, the sweep skips it, and
  `SecretProtection:RemoteKeys:Deployment` is required only when a provider creates keys. A change of custody hands each
  key over after the activation delay (`custody_changed`), a successor is a new version of its KMS key (a new tagged key
  on AWS KMS), a removed key the server created is destroyed in its KMS after the commit, each SDK retries a repeatable
  call with jittered exponential back-off, and a key creation is made once; the setting's domain and the provisioning
  check read the descriptors, never the providers, and a provider signs, decrypts and agrees into the caller's buffer.
  The management API creates a tenant's signing or encryption key in a registered provider
  (`POST /api/tenants/{tenantId}/secrets/remote`) and binds a key a KMS already holds by its identifier and algorithm,
  reading its public half from the KMS and storing it once the key proves it holds the private half — a challenge the
  key signs verifies with that half, or a challenge encrypted to that half decrypts through the key
  (`POST /api/tenants/{tenantId}/secrets/remote/import`, which needs update authority on the platform; the server never
  destroys a bound key); an update keeps a remote key's algorithm; `SecretResource`'s `remoteKeyProvider` and the
  console's secrets table name the provider; the capabilities response lists each registered provider, whether it
  creates keys (`createsKeys`), and the signature and key-management algorithms, uses, key types, sizes and curves it
  holds (`remoteKeyProviders`); a tenant's Secrets tab in the console creates a key in a provider that creates keys
  through the create-secret form's custody choice and binds a key a KMS holds through a bind dialog, each offering only
  the provider's algorithms, labelled signing or encryption, and showing each refusal's problem detail; the management
  API's validation and its capabilities read the descriptors as well. The Helm chart registers each provider that
  `secretProtection.remoteKeyProviders.<vendor>.enabled` turns on, renders its region, vault URI or key ring only when
  it is set, so a release binds keys a KMS already holds without creating any, refuses a location on a provider that is
  not enabled, and mounts the vendor's workload identity. `IRemoteKeyProvider`,
  `IRemoteKeyProviderCatalog`, `IRemoteKeyDescriptorCatalog`, `RemoteKeyAlgorithm`, `IRemoteSecretKey` and
  `IRemoteSecretKeyTypeDescriptor` are open, so a package adds a provider or a key type. The providers live in
  `NCode.Identity.Secrets.AwsKms`, `.AzureKeyVault` and `.GoogleCloudKms`, beside each service's key wrapper
  ([#165](https://github.com/NCodeGroup/identity/issues/165),
  [#648](https://github.com/NCodeGroup/identity/issues/648), [#649](https://github.com/NCodeGroup/identity/issues/649),
  [#650](https://github.com/NCodeGroup/identity/issues/650), [#652](https://github.com/NCodeGroup/identity/issues/652),
  [#653](https://github.com/NCodeGroup/identity/issues/653), [#655](https://github.com/NCodeGroup/identity/issues/655),
  [#657](https://github.com/NCodeGroup/identity/issues/657), [#687](https://github.com/NCodeGroup/identity/issues/687),
  [#690](https://github.com/NCodeGroup/identity/issues/690), [#691](https://github.com/NCodeGroup/identity/issues/691),
  [#721](https://github.com/NCodeGroup/identity/issues/721),
  [#725](https://github.com/NCodeGroup/identity/issues/725),
  [ADR-1471](docs/decisions/1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md),
  [ADR-1473](docs/decisions/1473-a-key-management-service-adapter-package-is-named-for-its-service.md),
  [ADR-1474](docs/decisions/1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md),
  [ADR-1475](docs/decisions/1475-the-management-api-creates-a-tenants-key-in-a-key-management-service-and-binds-one-that-proves-it-signs.md),
  [ADR-1476](docs/decisions/1476-a-bound-kms-keys-public-half-is-read-from-its-service-and-the-console-offers-each-providers-algorithms.md),
  [ADR-1477](docs/decisions/1477-a-tenants-encryption-keys-can-be-held-in-a-key-management-service-that-decrypts-every-content-key.md),
  [ADR-1489](docs/decisions/1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md),
  [ADR-1490](docs/decisions/1490-the-host-chart-registers-each-server-remote-key-provider-the-operator-enables-and-its-location-is-optional.md)).

- **A background job destroys the KMS keys that no stored secret names.** Every key the server creates in a KMS carries
  the deployment that `SecretProtection:RemoteKeys:Deployment` names
  (required when a remote key provider that creates keys is registered), and its group name hashes the deployment, so
  deployments that share an AWS account, a vault or a key ring, each under a name of its own, never share or sweep each
  other's keys. At start and every `SecretProtection:RemoteKeys:SweepInterval` (one day), one replica dispatches
  a `RemoteKeySweepJob` per provider that creates keys through the dispatch port, logging a provider whose dispatch
  fails and dispatching the others; the job lists the deployment's keys older than `SweepGracePeriod` (one day, between
  one hour and 365 days) through `IRemoteKeyProvider.ListKeysAsync`, then reads the references every stored secret holds
  (`IRemoteKeyReferenceStore`), and destroys each key none names through `IRemoteSecretDestroyer.DestroyKeyAsync`,
  recording `remote_key.destroyed` with the reason `orphaned` in the control-plane tenant; `SweepEnabled` turns it off.
  The Helm chart's `secretProtection.remoteKeys` sets the deployment and the sweep, and rendering fails for a release
  whose enabled remote key provider has its region, vault URI or key ring, and so creates keys, without a deployment
  ([#654](https://github.com/NCodeGroup/identity/issues/654), [#704](https://github.com/NCodeGroup/identity/issues/704),
  [#725](https://github.com/NCodeGroup/identity/issues/725),
  [ADR-1478](docs/decisions/1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md),
  [ADR-1490](docs/decisions/1490-the-host-chart-registers-each-server-remote-key-provider-the-operator-enables-and-its-location-is-optional.md)).

- **A management route answers from what it read and declares every problem it answers.** Every management route
  answers `404 Not Found` and `409 Conflict` from the rows it read before writing — a missing resource, a secret
  identifier, `kid` or scope value its owner already holds, a reserved system scope — never from a store's exception,
  so a store failure is never reported as a conflict or a missing resource, and a write that lost a concurrency race
  answers `412`. Every error it answers is a problem document, `404` included. Each route declares in the OpenAPI
  document every problem it answers and `412` when it writes, and each declared problem says when it occurs: a route's
  documentation describes its own answers, and the management route group describes `401`, `403`, `412`, `415`, the
  `409` for a stored settings document it cannot honor and the `503` for an unavailable root key on every route. A
  request whose body is not JSON, is not valid JSON for the operation, or whose parameter is missing or cannot be
  parsed is answered `400` or `415` as a problem its route declares, and every error under `/api` that carries no
  body, whichever endpoint answers it — a route's, a failed request binding's, and the router's `404`, `405` and
  `415` — is a problem document in every host that registers the management library
  ([#689](https://github.com/NCodeGroup/identity/issues/689), [#700](https://github.com/NCodeGroup/identity/issues/700),
  [#701](https://github.com/NCodeGroup/identity/issues/701), [#706](https://github.com/NCodeGroup/identity/issues/706),
  [#708](https://github.com/NCodeGroup/identity/issues/708),
  [ADR-1480](docs/decisions/1480-a-management-route-answers-from-what-it-read-and-declares-every-problem-it-answers.md),
  [ADR-1481](docs/decisions/1481-a-management-routes-problem-responses-say-when-each-occurs.md),
  [ADR-1483](docs/decisions/1483-a-management-request-whose-binding-fails-is-answered-as-a-problem-its-route-declares.md)).

- **A tenant is provisioned only with the keys its settings use.** The tenant key seed merges the tenant's persisted
  settings onto its team's and provisions a configured algorithm only when one of the tenant's effective algorithm
  lists names it: by default the ID token, UserInfo, introspection, JWT-secured authorization response (JARM,
  `authorization_signing_alg_values_supported`) and access-token signing lists and the request-object and access-token
  encryption lists. `OpenIdTenantKeyProvisioningOptions.AlgorithmSettings`
  (`OpenId:Tenant:KeyProvisioning:AlgorithmSettings`) names those lists, and a package that signs or decrypts with the
  tenant's keys under another setting adds its name. A `fapi2` tenant holds and publishes no RS256 key it never signs
  with, and a key the tenant already holds is kept. A platform or team settings change that permits a configured algorithm
  provisions its key in every running tenant without a restart: a background job re-seeds each tenant that names the
  algorithm ([ADR-1169](docs/decisions/1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [#241](https://github.com/NCodeGroup/identity/issues/241)).

- **A Development host can recover from keys it cannot read.** `DeveloperKeys:PruneUnreadableKeys`
  (`DeveloperKeysOptions`) removes a tenant key whose master key, or the root key that wraps it, was lost or changed
  when the tenant is seeded, and provisioning generates a fresh one. When the host starts and the platform's master keys
  cannot be unwrapped, it removes them, revokes every Data Protection key they protected, and the platform starts over
  with a new master key. The reference host's Development launch profiles set it, and Production refuses the
  `DeveloperKeys` section
  ([#569](https://github.com/NCodeGroup/identity/issues/569),
  [ADR-1313](docs/decisions/1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md),
  [ADR-1423](docs/decisions/1423-developer-key-pruning-starts-the-platform-over-when-its-master-keys-cannot-be-unwrapped.md)).

- **Asymmetric secret keys model their public half as `SubjectPublicKeyInfo` DER through a buffer writer.** The
  `AsymmetricSecretKey` base exposes `ExportSubjectPublicKeyInfo<TWriter>(ref TWriter destination)`, writing into a
  caller-supplied `IBufferWriter<byte>`, and derives `ExportSubjectPublicKeyInfoPem()` from it through a pooled
  non-sensitive writer, so the key model is not coupled to `System.Security.Cryptography.AsymmetricAlgorithm` and covers
  Edwards-curve (OKP) and post-quantum keys. RSA and ECC implement it from their family exports; OKP and PQC keys
  implement it from their own DER encoders. Callers that need a live classical algorithm use the family-specific
  `ExportRSA()` / `ExportECDsa()` accessors
  ([ADR-1115](docs/decisions/1115-asymmetric-keys-model-der-material-via-buffer-writers.md),
  [ADR-1266](docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).

- **Verify-only (public-only) asymmetric secret keys.** `ISecretKeyFactory.CreateRsaPublicKey` and `CreateEccPublicKey`
  produce `RsaSecretKey` / `EccSecretKey` instances that hold only the public half (a `SubjectPublicKeyInfo`, stored
  unprotected): they verify signatures but cannot sign. `Create(KeyMetadata, X509Certificate2)` accepts a public-only
  certificate and yields a verify-only key, so a relying party's registered public certificate (persisted as an `x509`
  secret) is a usable verification key with no separate secret type. This is the primitive that lets the OP hold a
  client's public key for `private_key_jwt`, JAR and `jwks_uri` / BYOK onboarding
  ([ADR-1103](docs/decisions/1103-public-only-asymmetric-secret-keys.md),
  [#54](https://github.com/NCodeGroup/identity/issues/54), [#161](https://github.com/NCodeGroup/identity/issues/161)).

- **An asymmetric key carries its issuers' certificates.** `AsymmetricSecretKey.IssuerCertificatesRawData` keeps the
  certificates of the authorities that issued a key's certificate, and `ExportIssuerCertificates()` materializes them.
  `ISecretKeyFactory.Create(KeyMetadata, X509Certificate2Collection)`, which every `ISecretKeyFactory` implements,
  builds a key from a collection such as a loaded PKCS#12 file: the one certificate with a private key is the key's, and
  the others are its issuers
  ([ADR-1300](docs/decisions/1300-an-asymmetric-key-carries-its-issuers-certificates-and-a-tls-client-presents-them.md),
  [#407](https://github.com/NCodeGroup/identity/issues/407)).

- **A generated private key stores its public key.** `PersistedSecret.PublicKey` holds the base64url SPKI of every
  generated asymmetric key, set from the required `GeneratedSecretKeyMaterial.PublicKey`, and
  `PersistedSecret.HasPublicKey()` tells whether a secret has one. `ISecretSerializer.DeserializePublicSecret` reads it,
  so the management public-key endpoints and the registration response read a public key without a protector, through
  `ISecretPublicKeyConverter.Convert(PersistedSecret)`. `TryDeserializePublicSecret` on `ISecretSerializer` reads a
  stored secret's public half and returns `false` with the reason for one it cannot read; the client key importer, the
  management secret validator and tenant key provisioning share it, so a stored public key that cannot be read is
  handled the same way on each path
  ([ADR-1268](docs/decisions/1268-a-generated-private-key-stores-its-public-key-beside-it.md),
  [#176](https://github.com/NCodeGroup/identity/issues/176), [#327](https://github.com/NCodeGroup/identity/issues/327)).

- **The secret-key-type descriptor is the seam for every key operation, and its catalog is the single source of truth
  for the key-type taxonomy.** `ISecretKeyTypeDescriptor` owns the full lifecycle of a key type (validate, generate,
  import, export through `ExportPublicKey` to JWK and PEM, and deserialize), so registering one descriptor adds a key
  type end to end; the persistence generator, importer and serializer are thin wrappers over `ISecretKeyTypeCatalog`,
  and key-size validation, key generation and public-key export consult it with no per-type `switch`. The JWKS `keys`
  array is JSON objects the descriptor produces, and the management public-key read path uses the same seam. The
  extension bases `SecretKeyTypeDescriptor` and `AsymmetricSecretKeyTypeDescriptor` are public, so an application adds a
  custom key type by deriving from them. Shared key state (metadata, key size, certificate handling) lives on
  `AsymmetricSecretKey`, and the `Default*` / `Public*` concretes hold only their key material
  ([ADR-1105](docs/decisions/1105-secret-key-type-descriptor-first-class-seam.md),
  [ADR-1067](docs/decisions/1067-secret-type-capability-catalog-and-capabilities-discovery.md)).

- **The secret-key types live in purpose-based namespaces.** `NCode.Identity.Secrets.KeyTypes` holds the secret-key type
  descriptors, the type catalog and the ECC-curve / ML-DSA reference data; `NCode.Identity.Secrets.Collections` holds
  the `ISecretKeyCollection` family and its ordering comparer; `NCode.Identity.Secrets.KeyMaterial` holds
  `ISecretKeyFactory`, the generate / import / export parameter and result DTOs, the material protector and the key-size
  / data-protector helpers; and `NCode.Identity.Jose.EdDsa.KeyTypes` holds `OkpSecretKeyTypeDescriptor` and its OKP
  curve support, matching the family's `…Keys` / `…Algorithms` house style.

- **A single extensible `IEccCurveCatalog` selects elliptic curves by identity (name or OID), not by size.** Same-size
  curves (such as `secp256k1` and `P-256`) are distinguished for both key generation and JWK `crv` naming:
  `CreateSecretRequest` takes an optional `curve`, generation flows through the growable
  `SecretKeyGenerationParameters`, the JWKS converter names `crv` from the key's actual curve, the capabilities document
  exposes an `eccCurves` section, and the console's create-secret form offers a server-driven curve picker. An
  application adds a curve by registering one `EccCurveSpecification`, and `EccCurveSpecification.P256`, `P384` and
  `P521` define the NIST curves once, for the curve catalog, the `ES*` algorithms and `ECDH-ES`. `GenerateSecretRequest`
  carries the whole `SecretKeyGenerationParameters` as `GenerationParameters`
  ([ADR-1068](docs/decisions/1068-unified-extensible-elliptic-curve-catalog.md),
  [#337](https://github.com/NCodeGroup/identity/issues/337)).

- **The secrets library registers logging.** `AddSecretsLibrary()` registers logging itself (`AddLogging()`), which its
  key-type descriptors write to, so the secrets and JOSE libraries compose without the host having registered it
  ([#327](https://github.com/NCodeGroup/identity/issues/327)).

- **Public-key import (BYOK) across the management API and dynamic client registration.** A relying party's public
  asymmetric key is imported and persisted as a first-class secret that round-trips and is used for client
  authentication and JAR verification. A persisted `SecretEncoding` axis (`symmetric_raw` / `pkcs8` / `spki` / `x509`,
  stored in its own column) records the stored form orthogonally to the crypto family, so a bare public key (the native
  JWKS shape) persists and deserializes unambiguously, and a certificate is a family plus the `x509` encoding. An import
  format is an open name that each key type declares: `SecretKeyImportFormats` holds the `const string` names `jwk`,
  `pem` and `certificate`, `SecretKeyImportParameters.Format` and `SecretKeyImportRequest.Format` are strings, and
  `ISecretKeyTypeDescriptor.PublicKeyImportFormats` lists the names a type imports, so a registered key type can bring a
  format such as PKCS #12; OKP and AKP keys accept `jwk` and `pem`. `ISecretImporter` with `SecretKeyImportRequest`
  imports at the persistence layer; every secret owner (tenant, client, resource server) has a
  `POST …/secrets/import` endpoint taking the `ImportSecretRequest` management contract and returning the material-free
  `SecretResource`, advertised by the capabilities response's `supportsPublicKeyImport`; and `ISecretKeyFactory` offers
  `CreateRsaSpki` / `CreateEccSpki`. The management endpoints, the secret importer and client-key import reject a format
  the secret type does not list, with a `400` message listing the type's formats; `format` compares ordinally, so `JWK`
  is rejected. Import is public-only by construction (a private or symmetric key is rejected), so the reveal-once model
  ([ADR-1050](docs/decisions/1050-secret-material-revealed-once-on-create.md)) is the sole path a symmetric value leaves
  the server. Dynamic client registration consumes an inline `jwks` set at registration
  ([ADR-1104](docs/decisions/1104-import-public-asymmetric-secret-material.md),
  [ADR-1150](docs/decisions/1150-vocabularies-are-open-unless-provably-closed.md),
  [#161](https://github.com/NCodeGroup/identity/issues/161), [#54](https://github.com/NCodeGroup/identity/issues/54)).

- **One public seam, `IClientKeyImporter`, imports a client key at every entry point.** Dynamic registration imports all
  or nothing, the `jwks_uri` fetch ignores keys it cannot use (RFC 7517 §5) and logs `ClientJwksKeyIgnored` (event
  2038), and the management import applies the same policy. Every key of a JSON Web Key Set has a `kid` unique within
  the set: a registered key keeps the `kid` it was registered with and a registered key without one is named by its
  RFC 7638 thumbprint, and a published key without one is ignored. The `kid` is stored in `KeyId` of `PersistedSecret`,
  `SecretKeyImportRequest` and `SecretEntity` (a nullable column; a key without one is selected by its secret
  identifier). Key types are matched by their JSON Web Key `kty` through `ISecretKeyTypeDescriptor.JwkKeyType` and
  `ISecretKeyTypeCatalog.TryGetByJwkKeyType`, so a client may register `OKP` (EdDSA) and `AKP` (ML-DSA) keys as well as
  RSA and EC, and the key-to-algorithm rule is `IAlgorithmCollection.ValidateKeyAlgorithm` (`KeyAlgorithmDeclaration`)
  ([ADR-1497](docs/decisions/1497-a-registered-key-set-names-a-key-without-a-kid-by-its-thumbprint.md),
  [ADR-1161](docs/decisions/1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)).

- **A client secret is stored as a one-way verifier or as the UTF-8 bytes of the string it reveals, and a cryptographic
  key as raw bytes.** A symmetric `PersistedSecret` serves three purposes with three storage shapes: a shared secret an
  external party presents and the server only compares (the `client_secret` of `client_secret_basic` or
  `client_secret_post`) is stored as its one-way verifier (`SecretEncodings.Verifier`); a shared secret the server also
  keys from (the `client_secret` of `client_secret_jwt`) is stored as the UTF-8 bytes of its opaque string
  (`SecretEncodings.SymmetricRaw`); and a cryptographic key the server itself uses (an `HS256` key, a symmetric JWE key)
  is stored as raw CSPRNG bytes and revealed `base64url`. `ISecretGenerator.GenerateSharedSecretVerifier` and
  `GenerateReversibleSharedSecret` generate a URL-safe random string and persist it in the first or the second shape;
  `ImportSharedSecretVerifier` and `ImportReversibleSharedSecret` persist a caller-supplied `client_secret`, given as the
  UTF-8 octets of its opaque string, in the same shapes, deriving the bit length from the value; all four take a
  `SharedSecretRequest`, and `ISecretGenerator.GenerateSecret` produces the third shape, for asymmetric and server-side
  cryptographic keys. `ConvertToSharedSecretVerifier` stores the verifier of a secret held as its octets. Every stored
  secret records its `PersistedSecret.Origin` (`SecretOrigins.Generated` or `SecretOrigins.Supplied`), which picks the
  verifier scheme. The distinction is made at the call site, per the no-flag-argument convention, so a client-secret
  issuer cannot pick the wrong representation. Verifier schemes are an open vocabulary: each is a registered
  `ISecretVerifierScheme`, looked up by name through `ISecretVerifierSchemeCatalog`, and `SharedSecretVerifierOptions`
  names the scheme for generated secrets (`GeneratedSecretScheme`, `SecretVerifierSchemes.Sha256` by default) and for
  supplied ones (`SuppliedSecretScheme`, `SecretVerifierSchemes.Pbkdf2Sha256`). Client authentication compares a
  presented secret with its verifier, and `client_secret_jwt` keys its HMAC from the stored bytes
  ([ADR-1110](docs/decisions/1110-shared-secret-vs-cryptographic-key-storage.md),
  [#69](https://github.com/NCodeGroup/identity/issues/69), [#163](https://github.com/NCodeGroup/identity/issues/163), [ADR-1391](docs/decisions/1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).

- **Hash algorithms and binary encodings are open vocabularies.** `ICryptoService.HashValue` takes the framework's
  `HashAlgorithmName` and computes the digest with `CryptographicOperations`, so SHA-384 and SHA-3 are available.
  Encodings are named: `BinaryEncodings` holds `base64url`, `base64` and `hex`, each the name of a registered
  `IBinaryEncoder`, and `EncodeBinary`, `GenerateKey` and `HashValue` take the name as a `string`. `AddCryptographyLibrary`
  registers the three built-in encoders, a package adds one, such as Base32, by registering another, and an unregistered
  or duplicate encoding name is rejected
  ([ADR-1190](docs/decisions/1190-hashing-names-the-framework-algorithm-and-binary-encodings-are-registered-strategies.md),
  [ADR-1150](docs/decisions/1150-vocabularies-are-open-unless-provably-closed.md)).

- **The OAuth and OpenID Connect wire vocabulary lives in a dependency-free package.**
  `NCode.Identity.OpenId.Protocol.Abstractions` holds `OpenIdProtocol`, shared by the OP and the protocol client, with
  the groups `Parameters`, `ErrorCodes`, `GrantTypes`, `ResponseTypes`, `ResponseModes`, `CodeChallengeMethods`,
  `TokenTypes`, `TokenTypeIdentifiers`, `TokenTypeHints`, `ClientAuthenticationMethods`, `ClientAssertionTypes`,
  `PromptTypes`, `DisplayTypes`, `ScopeTypes`, `SubjectTypes`, `BackchannelTokenDeliveryModes`, `DPoP`,
  `ConfirmationMethods`, `MediaTypes` (with `FormUrlEncoded`), `ClaimTypes`, `AuthenticationMethods`,
  `AuthenticationContextClassReferences`, `MetadataNames`, `WellKnownPaths` and `BackChannelLogout`. The back-channel
  logout `events` claim is `JoseClaimNames.Payload.Events`, and the OP's internal `$` parameters are in
  `OpenIdConstants.InternalParameters`
  ([ADR-1276](docs/decisions/1276-the-oauth-and-openid-connect-wire-vocabulary-lives-in-a-dependency-free-package.md),
  [#353](https://github.com/NCodeGroup/identity/issues/353)).

- **The OpenID protocol endpoints are served under the tenant route.** A tenant-resolution strategy's route prefixes
  every protocol endpoint (`/{tenantId}/oauth2/token`, `/identity/.well-known/openid-configuration`), so the path-based
  strategy resolves tenants and a configured path serves the issuer it publishes. A configured issuer is the tenant's
  base address as-is (a path tenant's `https://op.example/acme` stays `https://op.example/acme`); binding the tenant
  route keeps the request's route values and keeps an endpoint's parameters out of the base address; and published URLs
  carry the tenant route's values and no explicit default port the issuer does not.
  `IEndpointGroup.GetPrefix(IServiceProvider)` returns a group's `RoutePattern` when routes are mapped, so a group's
  prefix can come from configuration
  ([ADR-1138](docs/decisions/1138-protocol-endpoints-are-served-under-the-tenant-route.md)).

- **OpenID protocol errors map to HTTP statuses consistently.** An OpenID protocol error without a status code is
  `400 Bad Request`: a validation error such as an unsupported scope or response type or a missing `code_challenge`, at
  the pushed authorization request endpoint and elsewhere, is rendered as `400` (RFC 6749 §5.2, RFC 9126 §2.3,
  [ADR-1144](docs/decisions/1144-authorization-codes-are-bound-to-pkce-by-default.md)). A form body that is malformed or
  over the form limits answers `400 invalid_request` at the token, revocation, introspection, pushed authorization,
  device authorization, CIBA, authorization, end-session, device verification and UserInfo endpoints (UserInfo reads a
  form body for its access token) and at the BFF's back-channel logout, and device verification reads its form
  asynchronously ([#497](https://github.com/NCodeGroup/identity/issues/497)). When a client disconnects mid-request,
  `OpenIdExceptionEndpointFilter` rethrows the `OperationCanceledException` whose token is `HttpContext.RequestAborted`
  and ASP.NET Core abandons the request, so the OpenID exception handler writes no `server_error` and records no
  `openid.error` audit row.

- **A failed `Authorization`-header client authentication answers `401 Unauthorized` with a
  `WWW-Authenticate: Basic realm="<issuer>"` challenge**, as [RFC 6749
  §5.2](https://datatracker.ietf.org/doc/html/rfc6749#section-5.2) requires; body-based (`client_secret_post`) and
  assertion-based methods answer `400` (issue [#48](https://github.com/NCodeGroup/identity/issues/48),
  [ADR-1092](docs/decisions/1092-token-endpoint-401-www-authenticate-challenge.md)). The challenge rides the general
  response-header seam `ISupportResponseHeaders` (sibling of `ISupportStatusCode`), which `IOpenIdError` implements and
  `OpenIdResult<T>` renders, so it covers every client-authenticating endpoint (token, introspection, revocation, device
  authorization, CIBA, PAR) through the shared `IClientAuthenticationService`, and it carries the `WWW-Authenticate`
  challenges of sender-constrained tokens.

- **Non-redirectable authorization errors are content-negotiated: a browser gets a human-readable HTML error page,
  programmatic callers get JSON.** When the authorize endpoint refuses to redirect an error back to the client — an
  unregistered or invalid `redirect_uri` or an unresolvable `client_id` ([OIDC Core
  §3.1.2.4](https://openid.net/specs/openid-connect-core-1_0.html#AuthError)) — an interactive request
  (`Accept: text/html`) receives a styled, SPA-enhanced error page carrying a stable `authorization-error` element (and
  `data-error-code`) for automation, while wildcard-only (`*/*`) and programmatic callers receive the machine-readable
  JSON body. The representation is chosen by the open `IOpenIdResponseFormatter` strategy collection: each formatter
  declares the media types it emits and a `CanFormat(IOpenIdResponse)` predicate, so a representation for any response
  type is an additive registration. `OpenIdResult<T>` is a thin `IResult` that delegates to
  `IResultExecutor<IOpenIdResult>` (`DefaultOpenIdResultExecutor` in Core), which applies the response envelope and does
  the server-driven negotiation, keeping `NCode.Identity.OpenId.Abstractions` lean, as `OpenIdRedirectResult` delegates
  to `DefaultOpenIdRedirectResultExecutor`. The interactive surface (`AddInteractiveAuthentication`) registers the HTML
  page, written through `SecureEncoding.UTF8` like `DefaultAuthorizationResultExecutor`; a deployment without the
  interactive package answers with JSON
  ([ADR-1114](docs/decisions/1114-non-redirectable-authorization-errors-are-content-negotiated.md)).

- **Discovery metadata lists only standardized parameters.** The discovery endpoint does not list itself as
  `discovery_endpoint`, a parameter neither OpenID Connect Discovery 1.0 nor RFC 8414 defines, and because CIBA Core
  defines only signing-algorithm metadata for the backchannel authentication request, discovery advertises no encryption
  metadata for it.

- **The authorization endpoint's subject pipeline is a set of replaceable commands.** `AuthenticateCommand`,
  `AuthorizeCommand`/`AuthorizeDisposition` and `ChallengeCommand` (with their `Default*` handlers) run the authorize
  request's subject steps, and the cross-slice
  `ValidateSubjectAuthenticationCommand`/`DefaultValidateSubjectAuthenticationHandler` validate an existing login
  session's `SubjectAuthentication` (tenant, `max_age`, `acr`)
  ([ADR-1030](docs/decisions/1030-userinfo-endpoint-and-subject-authentication-seam.md)). Subject and resource-request
  authentication are separate commands: `AuthenticateSubjectCommand` authenticates only the end-user session, and
  `AuthenticateResourceRequestCommand` authenticates a bearer access token for a set of required audiences
  ([ADR-1155](docs/decisions/1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).

- **The authorization endpoint treats an invalid end-user session cookie as no session.** A cookie that is present but
  fails authentication (expired, tampered, or undecryptable after a data-protection key change) leads to interactive
  login, or to `login_required` under `prompt=none`, and a successful login issues a fresh cookie. Bearer-token subject
  authentication (UserInfo, token) answers an invalid token with `401`.

- **The OP enforces the `max_age` / `subject_max_age` authentication-age limit only at the authorization endpoint, with
  no clock-skew leeway (issue [#131](https://github.com/NCodeGroup/identity/issues/131)).** The check compares elapsed
  time inside the OP. The client's `clock_skew` applies to token time claims across parties, not to this limit, so
  `max_age=1` forces re-authentication. `ValidateSubjectAuthenticationCommand.EnforceAuthenticationAge` turns the check
  on (`true` at the authorization endpoint). It is `false` for the authorization-code, refresh-token and password
  grants, so a valid code or refresh exchange succeeds after the limit has passed.

- **`prompt=login`, `prompt=select_account` and `prompt=create` run their interactive step once per authorization
  request.** `DefaultAuthorizeHandler` skips them on the resumed continuation (`!IsContinuation`), as it does for
  `prompt=consent` ([ADR-1081](docs/decisions/1081-granular-consent-and-authorized-applications.md)). The authorization
  then completes, and the re-authenticated ID token carries a later `auth_time`.

- **Response types are whole values, enforced exactly and kept consistent with grant types.** `response_types_supported`
  lists whole `response_type` values (`code`, `id_token`, `token`, `code id_token`, `code token`, `id_token token`,
  `code id_token token`, all in `OpenIdConstants.ResponseTypes`) in canonical form (names in ordinal order), the shape
  OpenID Connect Discovery, RFC 8414 and RFC 7591 define; a non-canonical value such as `id_token code` is outside the
  domain. Discovery advertises the list as configured, and the authorization endpoint accepts exactly a listed value
  (its names in any order), so an operator enables one implicit or hybrid flow by listing it. A response type needs the
  grant types it uses (OpenID Connect Registration §2: `authorization_code` for `code`, `implicit` for `id_token` or
  `token`). The hybrid flow is authorized by the `authorization_code` and `implicit` grant types: a client registered
  with both uses hybrid response types, there is no `hybrid` grant type, and the token endpoint answers only
  `grant_type=authorization_code` for a code. A response type the tenant does not support is rejected with
  `unsupported_response_type` (the error factory's `UnsupportedResponseType`), and one the client may not use with
  `unauthorized_client`. Dynamic client registration registers combined values such as `code id_token`, stores and
  reports them in canonical form, defaults omitted `grant_types` to the grant types the registered response types use
  (and omitted `response_types` to `code` only for a client that uses the authorization endpoint), refuses with
  `invalid_client_metadata` a response type whose grant type is not registered or a grant type or response type the
  tenant does not support, and reports an empty list as `"response_types": []`
  ([ADR-1141](docs/decisions/1141-response-types-are-whole-values.md)).

- **Response modes are an open vocabulary, resolved through one provider from handlers that declare their traits.** The
  authorization result executor delivers each response through the `IAuthorizationResponseModeHandler` registered for
  its response mode, so a package adds a response mode by registering a handler. A handler declares
  `SecuredResponseMode`, the plain mode whose JWT-secured form it delivers, and `DeliversInQuery`, whether it returns
  the response in the query; neither has a default and nothing is inferred from a handler's name. The internal
  `IAuthorizationResponseModeProvider` is composed once and refuses to start with a duplicate `ResponseMode`, two
  handlers that secure one mode, a `SecuredResponseMode` that names no registered handler, or a handler named `jwt`.
  `jwt`, the one shorthand JARM defines, resolves to the handler that secures the response type's default mode, and a
  handler that `DeliversInQuery` is refused for a response type that returns a token. The `response_mode` setting rule
  accepts any whitespace-free name, and a request whose response mode no handler serves is refused with `400` and no
  redirect
  ([ADR-1201](docs/decisions/1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md), issue
  [#228](https://github.com/NCodeGroup/identity/issues/228)).

- **The authorization response is addressed through one public `AuthorizationResponseDelivery`.** The internal
  `IAuthorizationResponseDeliveryResolver` derives it once per request from the redirect URI, the response mode handler,
  the `state` and the credentials that secure a JWT response, and the authorization endpoint, the PAR endpoint, the
  continuation, validation, the security profiles and the result share it.
  `IAuthorizationRequestProcessor.ProcessRequestAsync` takes the delivery, `ValidateAuthorizationRequestCommand` and
  `AuthorizationRequestProfileContext` carry it as `Delivery`, and `AuthorizationResult` requires its `OpenIdContext`,
  `OpenIdClient` and delivery and is delivered by `result.Delivery.ResponseModeHandler`.
  `IAuthorizationRequest.ResponseMode` is the value the request asked for, possibly `null`
  ([ADR-1201](docs/decisions/1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md),
  [ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md), issues
  [#229](https://github.com/NCodeGroup/identity/issues/229), [#238](https://github.com/NCodeGroup/identity/issues/238)).

- **JWT-secured authorization responses (JARM).** The authorization endpoint serves the `query.jwt`, `fragment.jwt`,
  `form_post.jwt`, and `jwt` response modes, which deliver the response, an error included, as one JWT in the `response`
  parameter. The JWT is issued by the tenant and addressed to the client; it is signed with the client's
  `authorization_signed_response_alg` (default `RS256`, `PS256` under `fapi2`) and encrypted when the client registers
  `authorization_encrypted_response_alg`/`_enc`. Its `exp` follows the client-scoped `TimeSpan` setting
  `authorization_response_lifetime`, which defaults to 5 minutes, accepts 1 second to 10 minutes (JARM §2.1) and merges
  with `Min`, so a tenant can only lower it. Discovery advertises the modes and the
  `authorization_signing_alg_values_supported` and
  `authorization_encryption_alg_values_supported`/`_enc_values_supported` ceilings, and Dynamic Client Registration
  accepts and reports the client metadata. `jwt` resolves to `fragment.jwt` when the response type returns a token, and
  `query.jwt` is refused for such a response type. The credentials that sign the response are selected when the delivery
  is derived, before authentication and before a code is issued, so a client with no signing credential, or a selector
  that fails, is refused with `server_error` and a `500` that is not redirected
  ([ADR-1201](docs/decisions/1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md), issues
  [#214](https://github.com/NCodeGroup/identity/issues/214), [#238](https://github.com/NCodeGroup/identity/issues/238)).

- **A `form_post` response delivers each value exactly as sent.** Each hidden field's value, and the form's action, is
  HTML-encoded only, as the OAuth 2.0 Form Post Response Mode specification intends, so the browser's own
  form-urlencoding yields the original value (a `state` holding a space, `=`, `/` or `+` arrives intact), and a
  parameter with several values posts one field per value.

- **Every authorization response echoes the request's `state` when the client sent one, and leaves it out when the
  client did not (issue [#131](https://github.com/NCodeGroup/identity/issues/131)).** This covers the errors the
  authorization endpoint builds itself: `prompt=none` → `login_required` and no consent surface → `interaction_required`
  ([OIDC Core §3.1.2.6](https://openid.net/specs/openid-connect-core-1_0.html#AuthError)). It also covers success and
  error responses through `AuthorizationResult`, which accepts a request without `state` ([OAuth 2.0
  §4.1.2.1](https://datatracker.ietf.org/doc/html/rfc6749#section-4.1.2.1)).

- **Authorization responses carry the issuer and redirect with 303.** The OP advertises
  `authorization_response_iss_parameter_supported` (RFC 9207, default `true`), and the authorization response redirect
  uses 303 See Other (FAPI 2.0 §5.2.2, OAuth 2.1) for every client. The operator-configured `clock_skew` setting governs
  validation of signed request objects and client assertions, and the FAPI 2.0 profile caps it at 60 seconds through the
  merge lattice ([ADR-1109](docs/decisions/1109-fapi-behavioral-rules-and-discovery-validation-details.md)).

- **The OP serves Pushed Authorization Requests (RFC 9126) at `pushed_authorization_request_endpoint` (`/oauth2/par`)**
  (issue [#46](https://github.com/NCodeGroup/identity/issues/46),
  [ADR-1089](docs/decisions/1089-pushed-authorization-requests.md)). A client authenticates as at the token endpoint and
  POSTs its authorization parameters form-encoded; the request is loaded and validated exactly as the authorization
  endpoint would, persisted as a single-use grant and answered with `201 Created`, an opaque `request_uri`
  (`urn:ietf:params:oauth:request_uri:<reference>`) and `expires_in`. The client then calls the authorization endpoint
  with `client_id` and `request_uri`, and the pushed parameters are replayed from storage. The setting
  `require_pushed_authorization_requests` (opt-in, advertised in discovery) rejects a direct authorization request
  without a pushed `request_uri`, and `pushed_authorization_request_lifetime` defaults to 90 seconds. The endpoint is
  advertised in discovery.

- **Request objects carry numeric and boolean claims.** Standard JWT claims such as `iat`, `nbf` and `exp`, and
  parameters such as `max_age`, are read from their JSON number or boolean form as their string form, so a conformant
  signed request object is accepted.

- **The OP stores an authorization code as an authorization grant: the authorization request together with the
  authenticated subject.** This is the shape the token endpoint resumes when it exchanges the code.

- **The token response writes `expires_in` as a JSON number ([OAuth 2.0
  §5.1](https://datatracker.ietf.org/doc/html/rfc6749#section-5.1)).** `DurationSecondsParser`, exposed as
  `CommonParameterParsers.DurationSeconds`, reads and writes a duration in seconds as a JSON number in the OpenID wire
  format.

- **`client_credentials` and `password` token requests may omit `scope` ([RFC 6749 §3.3,
  §4.4](https://datatracker.ietf.org/doc/html/rfc6749#section-3.3)).** `DefaultClientCredentialsGrantHandler` and
  `DefaultPasswordGrantHandler` treat an omitted scope as an empty set and issue a token with no scopes. For the
  refresh-token and token-exchange grants, an omitted `scope` means the original grant's scopes.

- **The resource-owner password grant is an opt-in account capability.** It lives in
  `NCode.Identity.OpenId.Accounts.Authentication` (`AddLocalAccountAuthentication()`, with
  `CreatePasswordGrantSubjectCommand`), so `NCode.Identity.OpenId.Authentication` does not depend on the account
  packages; without that capability the base package answers the grant with `unsupported_grant_type`. The metadata
  projection claim types are in `NCode.Identity.OpenId.Principals.SubjectMetadataClaimTypes`
  ([ADR-1060](docs/decisions/1060-local-account-one-store-seam-and-dependency-inverted-authentication.md)). Its tickets
  carry the authentication's `AuthenticationProperties`: `DefaultPasswordGrantHandler` records the authentication
  instant once, at the start of the token request, passes it as `AuthenticationProperties.IssuedUtc` through
  `AuthenticatePasswordGrantCommand.AuthenticationProperties` and uses the same timestamp for the token's `CreatedWhen`,
  so the ticket's `auth_time` and the token's `iat` agree (issue
  [#62](https://github.com/NCodeGroup/identity/issues/62)).

- **Extension grants ([RFC 6749 §4.5](https://datatracker.ietf.org/doc/html/rfc6749#section-4.5)) are added by
  registering an `ITokenGrantHandler`** whose `GrantTypes` include the grant's absolute-URI identifier; the token
  endpoint needs no change ([ADR-1092](docs/decisions/1092-token-endpoint-401-www-authenticate-challenge.md)).

- **OAuth 2.0 Device Authorization Grant (RFC 8628).** Input-constrained clients (smart TVs, CLIs, IoT devices) obtain
  tokens by having the end user authorize on a second device. The `device_authorization_endpoint`
  (`POST /oauth2/device_authorization`) issues a `device_code`, a human-typable `user_code`, a `verification_uri` and a
  polling `interval`; the user-facing verification endpoint (`/oauth2/device`) authenticates the end user through the
  subject-authentication seam and records the approve or deny decision; and the
  `urn:ietf:params:oauth:grant-type:device_code` token grant polls the request, answering `authorization_pending`,
  `slow_down`, `expired_token` or `access_denied` until it is approved and redeemed for tokens exactly once. The pending
  request is a `PersistedGrant` payload keyed by `device_code` with a companion `user_code` pointer grant, updated
  through the general-purpose `IPersistedGrantService.UpdatePayloadAsync`. The settings `device_code_lifetime`,
  `device_code_polling_interval` and `user_code_length` tune the flow, `user_code` generation is the replaceable
  `IUserCodeGenerator` seam, and discovery advertises the endpoint and grant type
  ([ADR-1059](docs/decisions/1059-device-authorization-grant.md)). The endpoint follows the canonical endpoint shape:
  `DefaultDeviceAuthorizationEndpointHandler` is a thin orchestrator over the strongly typed
  `IDeviceAuthorizationRequest` / `DeviceAuthorizationRequest : OpenIdMessage<…>` and the mediator commands
  `ValidateDeviceAuthorizationRequestCommand` and `CreateDeviceAuthorizationRequestCommand` (returning
  `DeviceAuthorizationResult`), like the token, end-session and CIBA endpoints, so each protocol step is independently
  testable and replaceable (issue [#56](https://github.com/NCodeGroup/identity/issues/56),
  [ADR-1086](docs/decisions/1086-canonical-openid-endpoint-shape.md)).

- **The OP implements OpenID Connect Client-Initiated Backchannel Authentication (CIBA) with poll, ping and push
  delivery** (issues [#41](https://github.com/NCodeGroup/identity/issues/41) and
  [#55](https://github.com/NCodeGroup/identity/issues/55), [ADR-1085](docs/decisions/1085-ciba-grant.md)). The pending
  authorization is a persisted grant, as in the device flow, with no table of its own. The
  `backchannel_authentication_endpoint` (`/oauth2/bc-authorize`) accepts a strongly typed
  `BackchannelAuthenticationRequest` and orchestrates the mediator commands
  `LoadBackchannelAuthenticationRequestCommand`, `ValidateBackchannelAuthenticationRequestCommand`,
  `AuthenticateBackchannelRequestCommand` (returning `AuthenticateSubjectDisposition`) and
  `CreateBackchannelAuthenticationRequestCommand` (returning `BackchannelAuthenticationResult`); the
  `urn:openid:params:grant-type:ciba` token grant polls by `auth_req_id` with the device grant's
  `authorization_pending`, `slow_down`, `expired_token` and `access_denied` semantics. Login-hint resolution is a
  mediator seam whose default reports `unknown_user_id`; the authentication-device notification
  (`ICibaNotificationService.NotifyAuthenticationDeviceAsync`) and out-of-band approval (`ICibaRequestService`) are
  injectable services with safe defaults. Push mode issues the tokens on approval and delivers them to the client's
  `client_notification_endpoint`, and the token endpoint rejects a push-mode poll. `client_notification_endpoint`
  defaults to empty, so a ping or push client that registers none is rejected for the missing endpoint
  ([ADR-1140](docs/decisions/1140-setting-values-are-validated-against-their-domain.md)). A client may send its
  parameters as a signed `request` JWT validated against its keys. Discovery advertises the endpoint, the grant,
  `backchannel_token_delivery_modes_supported` (default `poll`, `ping`, `push`; `BackchannelTokenDeliveryModes`),
  `backchannel_user_code_parameter_supported` and `backchannel_authentication_request_signing_alg_values_supported`;
  `ciba_request_lifetime` and `ciba_polling_interval` set the request's lifetime and polling interval.

- **CIBA ping and push delivery and back-channel logout are delivered at least once through the dispatch port** (issue
  [#24](https://github.com/NCodeGroup/identity/issues/24),
  [ADR-1090](docs/decisions/1090-durable-outbound-delivery-dispatch-adapter.md)). On approval `ICibaRequestService`
  dispatches a CIBA delivery job whose handler performs the HTTP `POST` to the client and throws on failure so the
  dispatcher retries; the default `IBackChannelLogoutNotifier` builds the signed `logout_token` and dispatches its
  delivery the same way. The concrete delivery jobs are internal. A ping or push client is also told when its request is
  denied: a denial, also of an approval whose subject is refused, sends the ping notification, after which the token
  endpoint answers `access_denied`, or the push error payload with `error=access_denied`, and only the call that decided
  the request notifies the client
  ([ADR-1231](docs/decisions/1231-a-ping-or-push-client-is-told-when-its-ciba-request-is-denied.md), issue
  [#291](https://github.com/NCodeGroup/identity/issues/291)).

- **OAuth 2.0 Token Exchange (RFC 8693)** (issue [#61](https://github.com/NCodeGroup/identity/issues/61),
  [ADR-1101](docs/decisions/1101-oauth-token-exchange-rfc8693.md)). The token endpoint's
  `DefaultTokenExchangeGrantHandler` serves `urn:ietf:params:oauth:grant-type:token-exchange`
  (`OpenIdConstants.GrantTypes.TokenExchange`) through the `ITokenGrantHandler` extension-grant seam. A client presents
  a `subject_token` (and, for delegation, an `actor_token`) and receives an `access_token`, `refresh_token` or
  `id_token` output (`requested_token_type` in, `issued_token_type` out; `OpenIdConstants.TokenTypeIdentifiers.*`, the
  RFC 8693 `OpenIdConstants.Parameters.*` and `TokenTypes.NotApplicable`). Both impersonation and delegation are
  supported: a delegated exchange emits an `act` claim identifying the actor and nests any existing `act` to preserve
  the delegation chain, and requires the subject token to pre-authorize the actor through a `may_act` claim
  (`JoseClaimNames.Payload.MayAct`, RFC 8693 §4). Presented tokens are validated through the replaceable
  `ISubjectTokenValidator` seam (returning `SubjectTokenValidationResult`), whose default `DefaultSubjectTokenValidator`
  accepts only self-issued tokens, validated through `IIssuedTokenValidator` under the policy the `subject_token_type`
  selects; the requesting client must be confidential and be the client the token was issued to, or be named in its
  `aud`. The subject is resolved forward through `IPrincipalResolver`, never by reversing a pairwise `sub`; effective
  scopes and audiences are downscoped through the RFC 8707 `resource` and RFC 8693 `audience` parameters; and
  refresh-token issuance from an exchange is gated per client by `token_exchange_allow_refresh_token` (off by default).
  A token the exchange issues starts its lifetime when it is issued, so a token exchanged late in a long session is
  valid for its full lifetime, and its `auth_time` carries the subject's original authentication. The grant is secure by
  default: it is not in the baseline `grant_types_supported`, so an operator opts in (the conformance tenant does).
  `ITokenRequest` / `ITokenResponse` and `TokenExchangeGrant` carry the exchange. External and enterprise-IdP federation
  is out of scope and tracked as issue [#64](https://github.com/NCodeGroup/identity/issues/64); the
  `ISubjectTokenValidator` seam is its extension point.

- **`refresh_token_reuse_interval` lets a client retry a refresh with its rotated refresh token.** The interval is off
  by default and at most 60 seconds. A rotated refresh token that is presented within the interval is answered as the
  rotation was, and the retry receives another refresh token in the same rotation family, so a client that lost the
  response, or two tabs that refresh at once, keep their session.
  `IPersistedGrantService.AddToRefreshTokenLineageOrDefault`, `ITokenService.ReissueRefreshTokenAsync`, and
  `IGrantStore.GetActiveByRefreshTokenLineageAsync` store and read the branch (issue
  [#270](https://github.com/NCodeGroup/identity/issues/270)). When a retry is answered with another refresh token of its
  rotation family, `token.refresh.retry` (`RefreshTokenRetryAuditEvent`) is published with outcome `success`, so an
  operator can tell a client's retry from a rotation and from a reuse (`token.refresh.replay`); a refused retry
  publishes none, and a custom recorder implements `IAuditEventRecorder.RecordRefreshTokenRetryAsync` (issue
  [#275](https://github.com/NCodeGroup/identity/issues/275)). Concurrent refreshes that extend a sliding expiration both
  succeed, because the sliding-expiration update keeps the other request's change (issue
  [#260](https://github.com/NCodeGroup/identity/issues/260))
  ([ADR-1211](docs/decisions/1211-a-rotated-refresh-token-is-spent-with-its-successor.md),
  [ADR-1217](docs/decisions/1217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md)).

- **Client-authentication methods are named by registered descriptors.** `ClientAuthenticationMethodDescriptor` names a
  method, says whether it authenticates with a client secret (`RequiresClientSecret`) and whether it keys from that
  secret (`UsesClientSecretAsKey`), which decides the form the secret is stored in. A package contributes one through
  `ICollectionDataSource<ClientAuthenticationMethodDescriptor>`, and `IClientAuthenticationMethodCatalog` lists the
  registered names and finds a descriptor without constructing a handler. Every client-authentication handler is paired
  with a registered descriptor: the host does not compose when the registered handlers and descriptors differ, and
  options validation constructs no handler
  ([ADR-1207](docs/decisions/1207-client-authentication-methods-are-named-by-registered-descriptors.md), issue
  [#251](https://github.com/NCodeGroup/identity/issues/251), [ADR-1391](docs/decisions/1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).

- **JWT-bearer client authentication (`private_key_jwt` / `client_secret_jwt`)** (issue
  [#54](https://github.com/NCodeGroup/identity/issues/54),
  [ADR-1102](docs/decisions/1102-jwt-bearer-client-authentication.md)). Confidential clients authenticate with a signed
  `client_assertion` (`client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer`,
  `OpenIdConstants.ClientAssertionTypes.JwtBearer`; OpenID Connect Core §9, RFC 7523). Two handlers share one base, one
  for the asymmetric `private_key_jwt`, the FAPI-eligible method, and one for the HMAC `client_secret_jwt`, non-FAPI, so
  each assertion is handled once and each method is advertised independently in `token_endpoint_auth_methods_supported`.
  The client is identified from the assertion (`iss == sub == client_id`), the signature is verified against the
  client's registered keys, the `aud` must be the tenant issuer or the receiving endpoint's URL, `exp` is required, and
  `jti` is single-use through the `NCode.Identity.OpenId.Authentication.Clients.IClientAssertionReplayCache` seam, which
  records an assertion with one atomic add-if-absent.

- **`client_secret_basic` accepts a secret that contains `%` or `+`.** The handler form-urlencoding-decodes the client
  id and secret to exactly what the client sent, as RFC 6749 §2.3.1 defines (issue
  [#319](https://github.com/NCodeGroup/identity/issues/319)).

- **Mutual-TLS client authentication (`tls_client_auth` / `self_signed_tls_client_auth`)** (issue
  [#54](https://github.com/NCodeGroup/identity/issues/54),
  [ADR-1107](docs/decisions/1107-mutual-tls-client-authentication.md)). A confidential client can authenticate with a
  mutual-TLS client certificate (RFC 8705 §2), the second FAPI-permitted strong client-authentication mechanism beside
  `private_key_jwt`. `self_signed_tls_client_auth` matches the presented certificate against one the client registered
  by SHA-256 thumbprint; `tls_client_auth` matches its subject distinguished name or a subject alternative name
  (`dns`/`uri`/`ip`/`email`) against the client's registered `tls_client_auth_subject_dn` / `tls_client_auth_san_*`
  settings, and validates its chain. The handlers use `IClientCertificateResolver` (direct TLS or a trusted forwarded
  header), are gated by the client's registered `token_endpoint_auth_method`, and both methods are advertised in
  `token_endpoint_auth_methods_supported`. `AsymmetricSecretKey.CertificateRawData` gives raw DER access, so a
  thumbprint is computed without materializing a certificate. `self_signed_tls_client_auth` matches the certificates in
  a client's JSON Web Key Set: importing a JSON Web Key keeps the first certificate of its `x5c` (RFC 7517 §4.7) after
  checking that it carries the key's public key, so a client that registers its keys as `jwks` or publishes them at a
  `jwks_uri` authenticates with a certificate listed there (RFC 8705 §2.2), and a key whose `x5c` is empty, malformed,
  or carries another key is refused
  ([ADR-1161](docs/decisions/1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)).

- **`DistinguishedName` matches a distinguished name as RFC 4517 `distinguishedNameMatch` does.**
  `DistinguishedName.TryParse` reads an RFC 4514 string and what an operator pastes: whitespace around `=`, `,`, and
  `+`, RFC 2253 quoted values, `\` escapes, `\XX` UTF-8 hex escapes, and `#` hex BER values. An attribute type is a
  keyword from a closed table, a dotted OID, or `OID.` followed by a dotted OID. `DistinguishedName.FromX500` reads a
  certificate's name, and `Matches` compares two names: the RDN count and order are equal, a multi-valued RDN is a set,
  and a character-string value is compared after NFKC normalization, deletion of the characters that map to nothing,
  whitespace collapse, and case folding, while any other value is compared by its exact encoding. The
  `tls_client_auth_subject_dn` setting rule, the `tls_client_auth` handler, and the CRL issuer check use it, so a
  certificate subject written as a `UTF8String` or a `PrintableString`, in another case or with other spacing matches
  the registered `tls_client_auth_subject_dn`, and the setting rule accepts the RFC 4514 forms an operator pastes, such
  as `CN=client,O=Example`. A value written with `;` separators or in the `/C=US/O=Example` form does not parse, matches
  no certificate and is refused by the setting rule
  ([ADR-1173](docs/decisions/1173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md), issue
  [#209](https://github.com/NCodeGroup/identity/issues/209)).

- **A forwarded client certificate is decoded by a registered format, and every format reads the whole chain the client
  presented.** The default `IClientCertificateResolver` reads the mutual-TLS client certificate from the connection or,
  when `MutualTlsForwardingOptions.CertificateHeaderName` is configured (section `OpenId:MutualTls`), from the header a
  reverse proxy, load balancer or service mesh sets, with no host code. `Base64Der` reads a comma-separated list, the
  client's own certificate first, as Traefik's `passTLSClientCert` (`pem: true`) forwards it in
  `X-Forwarded-Tls-Client-Cert`; `Pem` and `UrlEncodedPem` read several PEM blocks; and `EnvoyXfcc` reads `Chain` as
  well as `Cert`, honoring quoted values. The resolver returns a `PresentedClientCertificate`: the client's certificate
  and the other certificates it sent, as untrusted `Intermediates` for chain building, resolved once per request.
  Formats are an open vocabulary: each is an `IClientCertificateHeaderDecoder` registered under a `Format` name (the
  known names are `ClientCertificateHeaderFormats`), `MutualTlsForwardingOptions.Format` is a string, and the host
  refuses to start when `OpenId:MutualTls:Format` names no registered decoder. `IClientCertificateResolver` and
  `MutualTlsForwardingOptions` live in `NCode.Identity.OpenId.Authentication.ClientCertificates`, registered by
  `AddClientCertificateServices()`, and `ResolveCertificateAsync` takes an `OpenIdContext`
  ([ADR-1097](docs/decisions/1097-sender-constrained-access-tokens-dpop-and-mtls.md),
  [ADR-1172](docs/decisions/1172-forwarded-client-certificates-are-decoded-by-a-registered-format.md)).

- **A client's public keys resolve at runtime from its `jwks_uri`** (issue
  [#54](https://github.com/NCodeGroup/identity/issues/54),
  [ADR-1106](docs/decisions/1106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md)). A FAPI client can
  authenticate with `private_key_jwt`, or present a signed request object (JAR), with keys it rotates without
  re-registering. `IClientKeyResolver` resolves a client's effective key collection, and the `client_assertion` handlers
  and the JAR loader verify against it. The fetch is SSRF-guarded, requires absolute `https`, and is bounded and cached
  by the per-client settings `jwks_uri`, `jwks_uri_allowed_hosts`, `jwks_uri_fetch_timeout`, `jwks_uri_max_size_bytes`
  and `jwks_uri_cache_duration`, mirroring the `sector_identifier_uri*` family. Concurrent token requests share one
  fetch of a client's key set, and a fetch that yields no key is not repeated for
  `OutboundHttpOptions.FailureCacheDuration` (`OpenId:OutboundHttp:FailureCacheDuration`, one minute), validated when
  the host starts ([#466](https://github.com/NCodeGroup/identity/issues/466),
  [ADR-1335](docs/decisions/1335-a-clients-jwks-uri-is-fetched-single-flight-and-a-failed-fetch-is-remembered.md)).

- **A `kid` is the client's own name for its key, unique among that client's keys.** Any number of clients, in any
  tenants, register keys with the same `kid`; the management import keeps a JSON Web Key's `kid`, so a client that signs
  with it matches its key; and a registration update (RFC 7592) that changes `jwks` sets the client's keys to the ones
  it lists ([ADR-1161](docs/decisions/1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)).

- **OAuth 2.0 Dynamic Client Registration (RFC 7591/7592).** The `registration_endpoint` (`POST /oauth2/register`),
  advertised in discovery, accepts a JSON client-metadata document and returns the assigned `client_id`, a one-time
  `client_secret` for a confidential client, and a `registration_access_token` with a `registration_client_uri`; the
  per-client management endpoint (`GET`/`PUT`/`DELETE /oauth2/register/{client_id}`, RFC 7592) lets the client read,
  update or delete its registration with that token. Metadata maps onto the client settings document and the
  `client_secret` is the standard one-time secret, issued in the form the client's method needs, so client
  authentication is the same as for any client; the
  `registration_access_token` and the RFC 7591 `initial_access_token` are hash-keyed persisted grants. The
  deployment-specific `IInitialAccessTokenValidator` seam decides what counts as a valid initial access token. The error
  codes `invalid_client_metadata` and `invalid_redirect_uri` are defined
  ([ADR-1063](docs/decisions/1063-dynamic-client-registration.md)). `client_name`, `client_uri`, `logo_uri`, `tos_uri`,
  `policy_uri`, `contacts`, `jwks`, `software_id` and `software_version` are stored as registered client settings; the
  requested `scope` is not stored, because the OpenID Connect scopes are available to every client, and the response's
  `scope` reports the scopes the client may request, taken from `IClientScopeService`, since scope entitlement comes
  from resource servers and client grants. Discovery publishes `scopes_supported`
  ([ADR-1139](docs/decisions/1139-setting-names-are-a-closed-vocabulary.md)). Registration checks relations only through
  the coherence rules — the checks of the supported metadata, the algorithms, and the backchannel metadata are coherence
  rules, so `ValidateCoherence` is its only relation check — and an error description names the client setting, such as
  `grant_types_supported` rather than `grant_types`, with the error code `invalid_redirect_uri` for `redirect_uris`,
  else `invalid_client_metadata`
  ([ADR-1206](docs/decisions/1206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issue
  [#253](https://github.com/NCodeGroup/identity/issues/253)).

- **The management API mints initial access tokens for protected Dynamic Client Registration, optionally scoped to a
  resource server.** A tenant-scoped `POST api/tenants/{tenantId}/grants/initial-access-tokens` creates a hash-keyed
  `initial_access_token` grant (optional `description` and `lifetimeSeconds`) and returns the one-time plaintext token,
  revealed once and never retrievable again, together with the grant id for revocation through the grant management
  endpoint, and records an `InitialAccessTokenCreatedAuditEvent`, so a deployment that wants protected registration
  needs no out-of-band grant seeding or custom `IInitialAccessTokenValidator`
  ([ADR-1063](docs/decisions/1063-dynamic-client-registration.md)). A `resourceServerId` scopes the token to a resource
  server, under the same checks as when the management API binds a client: the resource server exists in the tenant, is
  not a system resource server, and the caller may update it. A client registered with a scoped token is bound to that
  resource server, must be confidential, and cannot be made public by an RFC 7592 update. A bound client registers
  `introspection_signed_response_alg` and `introspection_encrypted_response_{alg,enc}` (RFC 9701 §6), which an ordinary
  client is refused, and the client information response reports the binding as `resource_server_id`.
  `IInitialAccessTokenValidator.ValidateAsync` returns the token's `InitialAccessTokenGrant`, or `null` for a token it
  refuses
  ([ADR-1192](docs/decisions/1192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md)).

- **Dynamic client registration accepts the strong client-authentication methods.** A self-registered client can
  register `private_key_jwt`, `client_secret_jwt`, `tls_client_auth`, or `self_signed_tls_client_auth`: registration
  accepts every method the tenant's effective `token_endpoint_auth_methods_supported` permits, so a `fapi2` tenant
  refuses `client_secret_*` when the client registers. `private_key_jwt` and `self_signed_tls_client_auth` require
  `jwks` or `jwks_uri` (a `jwks` by value with an `x5c` certificate for the latter); `tls_client_auth` requires exactly
  one `tls_client_auth_*` identity parameter, which the read and update responses report. Only the `client_secret_*`
  methods are issued a `client_secret`
  ([ADR-1145](docs/decisions/1145-a-client-authenticates-only-with-its-registered-method.md)). A client also registers
  `token_endpoint_auth_signing_alg` (OpenID Connect Dynamic Client Registration 1.0 §2), which narrows the client's own
  list to it under the tenant's ceiling, and the client information response reports it.

- **Dynamic client registration accepts the CIBA metadata.** A client registers `backchannel_token_delivery_mode`,
  `backchannel_client_notification_endpoint`, `backchannel_authentication_request_signing_alg`, and
  `backchannel_user_code_parameter` (CIBA Core 1.0 §4), and the client information response reports them. The signing
  algorithm narrows the client's own list to it, under the tenant's ceiling. A `ping` or `push` client must register an
  https notification endpoint, and a client may claim the `user_code` parameter only where the tenant supports it;
  otherwise registration answers `invalid_client_metadata`. `backchannel_user_code_parameter_supported` also applies to
  clients, where the registered flag is stored (issue [#105](https://github.com/NCodeGroup/identity/issues/105),
  [ADR-1155](docs/decisions/1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).

- **Dynamic client registration accepts every logout member, and logout follows what the tenant advertises.**
  `RegisterClientRequest` and `RegisterClientResponse` carry `BackchannelLogoutUri` and
  `BackchannelLogoutSessionRequired`, stored as the `backchannel_logout_uri` and `backchannel_logout_session_required`
  client settings, so a self-registered client receives logout tokens, and the URI must be an absolute https URI without
  a fragment ([ADR-1063](docs/decisions/1063-dynamic-client-registration.md), issue
  [#389](https://github.com/NCodeGroup/identity/issues/389)). They also carry `PostLogoutRedirectUris`,
  `FrontchannelLogoutUri` and `FrontchannelLogoutSessionRequired`, validated as RP-Initiated Logout 1.0 §3.1 and
  Front-Channel Logout 1.0 §2 require (issue [#399](https://github.com/NCodeGroup/identity/issues/399)). The
  back-channel logout notifier sends nothing while `backchannel_logout_supported` is false and omits `sid` while
  `backchannel_logout_session_supported` is false; the front-channel logout service appends `iss`/`sid` only while
  `frontchannel_logout_session_supported` is true. A client coherence rule refuses a `backchannel_logout_uri` or
  `frontchannel_logout_uri` the tenant does not support, and a `*_logout_session_required` the tenant cannot honor, so a
  tenant write that would strand such clients is refused too (issue
  [#398](https://github.com/NCodeGroup/identity/issues/398)).

- **Dynamic Client Registration accepts, validates, persists and returns the `subject_type` and `sector_identifier_uri`
  client metadata, and verifies the sector document** (issue [#58](https://github.com/NCodeGroup/identity/issues/58),
  [ADR-1095](docs/decisions/1095-sector-identifier-uri-fetch-and-ssrf-guard.md)). As OpenID Connect Core 1.0 §8.1
  requires, it fetches the JSON document the `sector_identifier_uri` references and checks that it lists every one of
  the client's `redirect_uris`, so a pairwise client shares a sector only when the party controlling that document
  vouches for its redirect URIs. The fetch is the replaceable `ISectorIdentifierValidator` seam. Registration requires
  an `https` `sector_identifier_uri`, and requires one for a pairwise client whose `redirect_uris` span several hosts.

- **UserInfo responses can be signed and encrypted, and a client registers how its ID tokens and UserInfo responses are
  protected.** The OpenID Connect registration metadata `id_token_signed_response_alg` (default `RS256`),
  `id_token_encrypted_response_alg` / `_enc`, `userinfo_signed_response_alg`, and `userinfo_encrypted_response_alg` /
  `_enc` (`enc` defaults to `A128CBC-HS256`) are client settings — the platform, a team or a tenant may set the default for its
  clients — and dynamic client registration accepts them, together with `request_object_signing_alg` and
  `request_object_encryption_alg` / `_enc`. A client that registers a UserInfo algorithm receives an `application/jwt`
  response carrying `iss` and `aud`; one that registers encryption receives an ID token, logout token, or UserInfo
  response encrypted to a public key from its `jwks` or `jwks_uri` (signed and encrypted is a Nested JWT with `cty`
  `JWT`). Each registered value must be in the matching `*_values_supported` list: registration refuses one that is not
  with `invalid_client_metadata`, and issuance fails with `server_error` rather than substitute another algorithm.
  Registering an encrypted response requires `jwks` or `jwks_uri`, and a client registers at most one of the two. A
  client that registers `id_token_encrypted_response_alg`, `userinfo_encrypted_response_alg`,
  `introspection_encrypted_response_alg` or `authorization_encrypted_response_alg` without the matching `enc` gets the
  default `enc` (the tenant's, normally `A128CBC-HS256`); when the tenant's supported `enc` values exclude that default,
  registration answers `invalid_client_metadata`, so no client is registered whose responses cannot be encrypted
  ([ADR-1155](docs/decisions/1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).

- **A custom token service can protect ID tokens as the client registered.** `IClientJwtCredentialSelector`,
  `ClientJwtCredentials`, and `ClientJwtProtectionSettings` are public in
  `NCode.Identity.OpenId.Authentication.Abstractions`. Every issuer of a JWT addressed to a client resolves the
  selector, and a deployment can register its own before `AddIdentityServer()` (issue
  [#108](https://github.com/NCodeGroup/identity/issues/108),
  [ADR-1181](docs/decisions/1181-every-issuer-of-a-jwt-addressed-to-a-client-resolves-one-public-credential-selector.md)).

- **The end-session endpoint accepts an `id_token_hint` the client re-encrypted to the server.** OpenID Connect
  RP-Initiated Logout 1.0 §2 lets a relying party that received an encrypted ID token re-encrypt the signed token to the
  OP. The hint is decrypted with the tenant's keys under the tenant's advertised `id_token_encryption_*` lists, and the
  enclosed ID token is validated as a signed hint is. A tenant without an encryption key accepts only a signed hint, and
  an ID token presented anywhere else is never decrypted (issue
  [#115](https://github.com/NCodeGroup/identity/issues/115),
  [ADR-1167](docs/decisions/1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)).

- **DPoP and mTLS binding are governed by client and tenant settings and advertised in discovery**
  ([ADR-1097](docs/decisions/1097-sender-constrained-access-tokens-dpop-and-mtls.md)).
  `require_dpop_bound_access_tokens` and `require_tls_client_certificate_bound_access_tokens` are a client's
  requirements that its access tokens be bound; they are named `require_` plus the RFC 9449 §5.2 client metadata member
  `dpop_bound_access_tokens` and the RFC 8705 §3.4 member `tls_client_certificate_bound_access_tokens`, apply to clients
  (the platform, a team or a tenant may set them as a default for every client, such as
  `OpenId:Platform:Settings:require_dpop_bound_access_tokens`), merge with `Or`, default to `false`, and are not
  advertised; `OpenIdSettingNames` and `OpenIdSettingKeys` carry them as `RequireDPoPBoundAccessTokens` and its
  certificate counterpart, and a configuration key that is not a registered setting cannot be honored. The advertised
  support is separate from the requirements: `dpop_signing_alg_values_supported` (RFC 9449 §5.1) and
  `tls_client_certificate_bound_access_tokens`, which keeps its RFC 8705 §3.3 discovery name, applies to tenants, merges
  with `Replace`, and does not force a client to bind its tokens, so a deployment that wants every client to bind sets
  `require_tls_client_certificate_bound_access_tokens` to `true`, and otherwise clients receive bearer tokens. The token
  endpoint binds the certificate only for a client whose requirement is `true`, and a client that requires binding and
  presents no certificate is refused with `invalid_request`. Dynamic client registration reads and reports the client
  metadata `dpop_bound_access_tokens` and `tls_client_certificate_bound_access_tokens` as the requirements.
  `DPoPBoundAccessTokenCoherenceRule` reports a client that requires DPoP-bound access tokens while its effective
  `dpop_signing_alg_values_supported` is set and empty, so no proof could be accepted, and
  `CertificateBoundAccessTokenCoherenceRule` a client that requires certificate binding while its effective support is
  `false` ([ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md),
  [ADR-1206](docs/decisions/1206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issues
  [#233](https://github.com/NCodeGroup/identity/issues/233), [#256](https://github.com/NCodeGroup/identity/issues/256)).
  The settings `dpop_nonce_required` and `dpop_proof_lifetime` tune DPoP, and a tenant's mTLS endpoint alias
  (`OpenIdTenant.MtlsEndpointAliasAuthority`) advertises the RFC 8705 §5 mTLS endpoint aliases. The errors `invalid_dpop_proof` and `use_dpop_nonce` (with a `DPoP-Nonce`
  response header) report a rejected proof, and the DPoP replay cache (`IDPoPProofReplayCache`) and nonce service
  (`IDPoPNonceService`) are replaceable seams.

- **An OAuth 2.0 token introspection endpoint** (`POST /oauth2/introspect`, [RFC
  7662](https://datatracker.ietf.org/doc/html/rfc7662), advertised as `introspection_endpoint` in discovery). An
  authenticated client probes a token's active state: the default handler validates an access token through the server's
  issued-token policy, otherwise resolves a refresh-token grant, and reports `{"active": false}` for anything else. The
  response is a discovery-style, extensible result (`IntrospectionResult` with `[JsonExtensionData]` claims) that
  further `ICommandHandler<IntrospectTokenCommand>` handlers may enrich. The endpoint is a thin
  `IOpenIdEndpointProvider` that authenticates the client, parses a typed `ITokenIntrospectionRequest` and delegates to
  the `IntrospectTokenCommand` mediator handler ([ADR-1029](docs/decisions/1029-token-introspection-endpoint.md)).

- **A resource server can introspect through a client that acts for it, and receive the answer as a signed JWT.** A
  client created with `CreateClientRequest.resourceServerId` acts for that resource server: it authenticates like any
  confidential client but is accepted only at the introspection endpoint, and the binding never changes. The resource
  server must be in the client's tenant and must not be a system resource server, and the caller must be allowed to
  update it. `ClientResource.resourceServerId` reports the binding, `GET …/clients?resourceServerId=` lists a resource
  server's clients, and a resource server with bound clients cannot be deleted. Such a client that sends
  `Accept: application/token-introspection+jwt` receives the response as a JWT typed `token-introspection+jwt` (RFC
  9701), signed with its `introspection_signed_response_alg` (default `RS256`) and encrypted when it registers
  `introspection_encrypted_response_alg` / `_enc`. Discovery advertises the
  `introspection_{signing_alg,encryption_alg,encryption_enc}_values_supported` lists and the client-authentication
  methods and algorithms of the introspection and revocation endpoints
  ([ADR-1168](docs/decisions/1168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)).

- **The server reads the encrypted access tokens it issues.** UserInfo, the management API, introspection and token
  exchange decrypt access tokens with the tenant's keys, and introspection also uses the calling resource server's own
  keys, so a client or resource server that requires encrypted access tokens can use them
  ([ADR-1167](docs/decisions/1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)).

- **An OAuth 2.0 token revocation endpoint** (`POST /oauth2/revoke`, [RFC
  7009](https://datatracker.ietf.org/doc/html/rfc7009), advertised as `revocation_endpoint` in discovery). An
  authenticated client revokes one of its own refresh tokens, together with every refresh token of its rotation family;
  an unknown or non-revocable token (a stateless JWT access token included) is a no-op, and the endpoint always responds
  `200 OK`, so it never reveals token state. The endpoint is a thin `IOpenIdEndpointProvider` that authenticates the
  client, parses a typed `ITokenRevocationRequest` and delegates to the `RevokeTokenCommand` mediator handler
  ([ADR-1028](docs/decisions/1028-token-revocation-refresh-tokens-only.md)).

- **An OpenID Connect UserInfo endpoint** (`GET`/`POST /oauth2/userinfo`, [OpenID Connect Core
  5.3](https://openid.net/specs/openid-connect-core-1_0.html#UserInfo), advertised as `userinfo_endpoint` in discovery).
  The endpoint is a thin shell over two mediator seams: the authentication of the presented access token, and a
  `GetUserInfoClaimsCommand` enricher pipeline, whose default handler supplies only the required `sub` claim, with
  applications registering further `ICommandHandler<GetUserInfoClaimsCommand>` handlers to contribute profile, email and
  other claims. The response is an extensible `[JsonExtensionData]` result (`UserInfoResult`)
  ([ADR-1030](docs/decisions/1030-userinfo-endpoint-and-subject-authentication-seam.md)). The endpoint validates a
  presented `Authorization: Bearer` access token inside the request environment, after the environment resolves the
  tenant's signing keys and issuer, through the self-issued `AuthenticateAccessTokenCommand` path the management API
  uses ([ADR-1044](docs/decisions/1044-management-api-authentication-and-globaladmin-bootstrap.md)), and requires the
  OpenID identity audience (`urn:ncode:openid`), so the host configures no authentication scheme for it. It also accepts
  the access token in a form-encoded request body: per RFC 6750 §2.2 and OpenID Connect Core 1.0 §5.3.1, an
  `access_token` form parameter on a non-GET request whose body is `application/x-www-form-urlencoded` is honored beside
  the preferred `Authorization: Bearer` header (RFC 6750 §2.1), which takes precedence when both are present. The
  URI-query transport (RFC 6750 §2.3) is deliberately unsupported, because RFC 9700 deprecates access tokens in the
  query string (issue [#131](https://github.com/NCodeGroup/identity/issues/131)).

- **Scope-driven claims are returned from UserInfo when an access token is issued, sourced from the local account.** Per
  OpenID Connect Core 1.0 §5.4, the `profile`/`email`/`address`/`phone` scope claims go in the UserInfo response
  whenever an access token is issued (the authorization-code flow), and in the ID token only when no access token is
  issued (a bare `id_token` response). `DefaultGetIdTokenSubjectClaimsHandler` gates the scope-driven claim set on no
  access token being issued (a non-null `AccessToken` on the token request); explicitly requested `claims.id_token`
  claims, `sub`, `auth_time`, `acr`, `idp`, and `amr` are always included. The account-sourced UserInfo enricher
  `DefaultGetLocalAccountUserInfoClaimsHandler`, registered by `AddLocalAccountAuthentication` /
  `AddLocalAccountInteractiveAuthentication`, resolves the caller's local account from its self-issued connection and
  returns the scope-permitted claims from the account store, gated by the access token's `scope` claim. It honors
  individual claims requested through the `claims` parameter's `userinfo` member (OIDC Core §5.5), serving them from the
  account store even when the owning scope was not granted, and coerces the standard typed claims (`updated_at` →
  number, `address` → JSON object, `phone_number_verified` → boolean) so the UserInfo response carries the correct JSON
  types. The conformance test-user seed provisions the full standard `profile`/`address`/`phone` claim set
  (`ConformanceSeedOptions.TestUserGivenName` / `TestUserFamilyName` for the name parts, deterministic values for the
  rest) (issue [#131](https://github.com/NCodeGroup/identity/issues/131)).

- **End-to-end support for the OpenID Connect `claims` request parameter** ([OIDC Core
  5.5](https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter)), advertised as
  `claims_parameter_supported: true` in discovery. Requested `claims.id_token` claims are added on top of the
  scope-driven claims during ID-token issuance, and requested `claims.userinfo` claims are honored at the UserInfo
  endpoint: when an access token is issued for a request that carried `claims.userinfo`, the requested claims are
  persisted as a short-lived stateful grant (`OpenIdConstants.PersistedGrantTypes.UserInfoClaims`) keyed by the access
  token's `jti` and bounded by its lifetime, then resolved at UserInfo, so the access token itself is never inflated.
  `value`/`values` are advisory and `essential` is best-effort; a host imposes stricter semantics with a higher-priority
  claims handler ([ADR-1039](docs/decisions/1039-claims-request-parameter.md)).

- **Tokens carry the authentication context: `amr`, an `acr` from a replaceable resolver, and `auth_time` when it is
  due** (issue [#49](https://github.com/NCodeGroup/identity/issues/49),
  [ADR-1091](docs/decisions/1091-acr-emission-and-conditional-auth-time.md)). Local interactive sign-in and the
  resource-owner-password subject handler record `amr=["pwd"]` (RFC 8176) and resolve `acr` through the async,
  DI-overridable `IAuthenticationContextReferenceResolver`: the default asserts the PAPE multi-factor URN
  (`OpenIdConstants.AuthenticationContextClassReferences.MultiFactor`) when several factors were used and otherwise the
  `default_acr` setting (unset omits the claim); a deployment overrides it by setting, by its own resolver or by a
  custom verifier. The resolved `acr` is stamped on the principal, captured on the session
  (`SessionGrant.AuthenticationContextClassReference`) and emitted by the ID-token and access-token subject-claims
  handlers. The `always_include_auth_time` setting (default `true`) governs whether the ID token always includes
  `auth_time`, which it always includes when a `max_age` request or an essential `auth_time` claim requires it; the
  access token includes `auth_time` only when it is available (RFC 9068).

- **Subject metadata (`ProfileMetadata` / `SystemMetadata`) is a principal-level concept resolved at issuance.** The two
  bags live on `FederatedPrincipal` / `PersistedFederatedPrincipal` and are resolved at token and UserInfo issuance
  through the pluggable `IPrincipalMetadataProvider` (default over `IFederatedPrincipalStore`), keyed by the resolved
  `PrincipalId`. They are projected identically for every grant flow (ROPC, interactive, refresh) and every connection
  kind (local or external IdP). The provider reads fresh per issuance, so the result is cluster-consistent; there is no
  built-in cache, and host-side caching is a concern of a custom provider, which must be distributed
  ([ADR-1055](docs/decisions/1055-subject-metadata-is-a-principal-level-concept-resolved-at-issuance.md), amends
  [ADR-1054](docs/decisions/1054-local-account-metadata-projection.md)).

- **`SubjectAuthentication.Create` guarantees `Subject.sub == PrincipalId` by construction** (issue
  [#60](https://github.com/NCodeGroup/identity/issues/60),
  [ADR-1094](docs/decisions/1094-resolve-principal-at-every-authentication-entry-point.md)). Every entry point builds
  its ticket through this factory, which normalizes the credential principal's subject claim to the resolved
  `PrincipalId`, and the interactive login cookie is stamped through the same seam. The normalization is the inverse of
  subject extraction and a tenant-aware, host-replaceable seam symmetric with `GetSubjectId`:
  `OpenIdContext.NormalizeSubjectId(ClaimsPrincipal, string)` calls `OpenIdEnvironment.NormalizeSubjectId(...)`,
  configurable through the `OpenIdOptions.NormalizeSubjectId` delegate.

- **Scopes are enforced through resource servers and client grants.** A replaceable `IClientScopeService` resolves a
  client's allowed scopes: every scope of a system resource server (`IsSystem`) is available to all clients, and a
  non-system API is available only through a client grant intersected with the API's current scopes at read time. The
  authorization and token validators reject out-of-scope requests, discovery's `scopes_supported` is derived from the
  tenant's resource-server catalog, `offline_access` comes from the system identity resource server, and refresh-token
  issuance is governed by the request and the `refresh_token` grant type
  ([ADR-1026](docs/decisions/1026-scope-enforcement-via-resource-servers-and-client-grants.md)).

- **An access token's `aud` names the resource servers that own its effective scopes.**
  `IClientScopeService.ResolveAudiencesAsync` resolves them, so a resource server can validate that a token was minted
  for it; a token spanning several resource servers carries a multi-valued `aud`, a scopeless token carries the client
  id, and the ID token's `aud` is the client
  ([ADR-1027](docs/decisions/1027-access-token-audience-from-resource-servers.md)).

- **The OP establishes a server-side end-user session at interactive sign-in and emits its `sid`** (issue
  [#39](https://github.com/NCodeGroup/identity/issues/39), the `sid` portion of
  [#49](https://github.com/NCodeGroup/identity/issues/49),
  [ADR-1074](docs/decisions/1074-session-management-and-logout.md)). The session is a `PersistedGrant` of type `session`
  keyed by an opaque `sid`, with no table of its own; the `sid` is minted onto the login cookie and emitted in ID
  tokens. A session spans every client, so its lifecycle controls are tenant settings (issue
  [#63](https://github.com/NCodeGroup/identity/issues/63)): `session_absolute_lifetime`; `session_idle_timeout`, a
  sliding inactivity window renewed on each authorize request and enforced server-side by
  `ISessionService.ValidateAndTouchAsync` on the authorize path, so an idle or expired session prompts for login; and
  `max_concurrent_sessions`, which evicts the oldest sessions beyond the cap through the swappable
  `ISessionEvictionPolicy`.

- **Every session termination flows through one mediator seam, `OnSessionTerminatedCommand`, with an open vocabulary of
  reasons** ([ADR-1098](docs/decisions/1098-session-lifecycle-termination-seam.md),
  [ADR-1150](docs/decisions/1150-vocabularies-are-open-unless-provably-closed.md)). The default handlers revoke the
  session's (or subject's) grants, send back-channel logout notifications and audit, so an idle, evicted or
  administratively revoked session notifies its relying parties and is audited exactly like an RP-initiated logout; an
  application extends termination by registering another `ICommandHandler<OnSessionTerminatedCommand>`.
  `SessionTerminationReasons` holds the `const string` names `logout`, `idle_timeout`, `absolute_expiry`,
  `concurrent_session_limit`, `administrative_revocation`, `global_sign_out`, `user_initiated`, `user_blocked` and
  `authorization_code_replay`. `ISessionService`, `IAuditEventRecorder.RecordSessionTerminatedAsync`,
  `SessionTermination.Reason` and `SessionTerminatedAuditEvent.TerminationReason` take and carry a `string`, so an
  extension can end a session for its own reason, such as a password change, and the `session.terminated` audit event's
  `reason` is the name, for example `global_sign_out`.

- **A session is terminated at most once on every path, and its termination notifies every client it lists when it is
  claimed.** `ISessionService.RevokeAsync`, `RevokeAllAsync` and `RevokeAllExceptAsync`, the eviction at the
  concurrent-session limit and RP-initiated logout claim each session with
  `IPersistedGrantService.RevokeSessionOnceAsync` before they dispatch its termination, so a session that two paths end
  at once notifies its relying parties and is audited once, and the claim revokes the grants issued under it in one unit
  of work. `RevokeAllAsync` revokes every grant of the subject before it dispatches, and the sessions management API
  reports the sessions the request terminated. `ISessionService.RevokeAsync` returns `bool`, and `RevokeAllAsync` and
  `RevokeAllExceptAsync` return the number of sessions the call terminated
  ([ADR-1227](docs/decisions/1227-a-session-is-terminated-at-most-once-on-every-path.md), issue
  [#286](https://github.com/NCodeGroup/identity/issues/286)). `IPersistedGrantService.RevokeSessionOnceAsync<TPayload>`
  returns the session grant as the call revoked it (`PersistedGrant<TPayload>?`), and every termination notifies the
  clients that grant lists, so a client added to the session by an ID token issued after an earlier read receives
  back-channel logout. A subject-wide termination also claims a session that ended and awaits the background termination
  ([ADR-1228](docs/decisions/1228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md),
  issue [#288](https://github.com/NCodeGroup/identity/issues/288)).

- **The grant prune survives a store timeout.** `GrantPruneService` logs a failed pass (`GrantPruneFailed`), a store
  timeout surfacing as `TaskCanceledException` included, and prunes again at the next tick; only the host's stopping
  token ends it, so a timeout never stops the service or, under `BackgroundServiceExceptionBehavior.StopHost`, the host
  ([#472](https://github.com/NCodeGroup/identity/issues/472),
  [ADR-1218](docs/decisions/1218-ended-grants-are-removed-after-a-retention-period.md)).

- **RP-Initiated Logout is served at the discovery-advertised `end_session_endpoint`, and Back-Channel Logout notifies
  relying parties server to server** (issue [#39](https://github.com/NCodeGroup/identity/issues/39),
  [ADR-1074](docs/decisions/1074-session-management-and-logout.md)). The endpoint loads a typed `IEndSessionRequest`
  through `LoadEndSessionRequestCommand`, validates the `id_token_hint`, clears the OP login, ends the session through
  the termination seam and redirects to a validated `post_logout_redirect_uri`, echoing `state`. The pluggable
  `IBackChannelLogoutNotifier` delivers a signed `logout_token`, typed `logout+jwt` (`JoseTokenTypes.LogoutJwt` is the
  single definition of its `typ`, [#355](https://github.com/NCodeGroup/identity/issues/355)), to each client's
  `backchannel_logout_uri`. The client settings are `post_logout_redirect_uris`, `backchannel_logout_uri` and
  `backchannel_logout_session_required`, and discovery advertises `backchannel_logout_supported` and
  `backchannel_logout_session_supported`.

- **Front-Channel Logout 1.0 and Session Management 1.0 are available behind opt-in settings, off by default**
  (issue [#22](https://github.com/NCodeGroup/identity/issues/22),
  [ADR-1088](docs/decisions/1088-opt-in-front-channel-logout-and-session-management.md)). With
  `frontchannel_logout_supported`, the `end_session_endpoint` renders a hidden iframe per participating client at its
  `frontchannel_logout_uri`, appending `iss` and `sid` when the client sets `frontchannel_logout_session_required`
  (advertised by `frontchannel_logout_session_supported`). With `session_management_supported`, the authorization
  response carries `session_state`, the OP keeps an opaque browser-state cookie, and discovery advertises a
  `check_session_iframe` the relying party polls via `postMessage`. Both depend on third-party cookies the browser
  platform is removing, so they stay off unless a deployment needs them; cookie-independent RP-Initiated and
  Back-Channel Logout are the recommended mechanisms.

- **End-user consent is a durable, first-class aggregate, separate from the grant store, and enforced at the authorize
  endpoint** ([ADR-1076](docs/decisions/1076-consent-persistence-and-enforcement.md)). `PersistedConsent`,
  `IConsentStore` and the `ConsentEntity` table, keyed by `(tenant, client, subject)` and upserted in place, survive
  logout. The client setting `require_consent` (default off, and forced by `prompt=consent`) makes
  `DefaultAuthorizeHandler` route a subject whose recorded consent does not cover the request to the interactive consent
  screen (`AuthorizeDisposition.ConsentRequired` and the `ConsentChallengeCommand` seam the interactive module handles),
  or return `consent_required` under `prompt=none`.

- **The OpenID Provider hosts an interactive login and consent surface.** A React and Material UI app
  (`@ncode/app-auth`, `apps/auth`) on the shared frontend platform is served by the host, same-origin, under
  `/account/*`, backed by the opt-in leaf capability package `NCode.Identity.OpenId.Authentication.Interactive` (+
  `.Abstractions`, under the `NCode.Identity.OpenId.Authentication.Interactive.*` namespaces), which
  `NCode.Identity.Server` references like any other capability (`AddInteractiveAuthentication` /
  `UseInteractiveAuthentication`). Its endpoints register as an `IEndpointProvider` under an
  `InteractiveAuthenticationEndpointGroup`, so the host's single `MapEndpointGroups` call maps `/account/*` alongside
  every other endpoint. It registers the cookie scheme `Identity.Application`
  (`InteractiveAuthenticationDefaults.CookieSchemeName`) that the authorize endpoint's challenge redirects into, issues
  the cookie session, and honors a server-validated local return URL. The shell resolves its surfaces (account, consent,
  login) through a declarative route table, and the forms expose stable automation ids (`username`, `password`, `login`,
  `consent-allow`, `consent-deny`). See [ADR-1062](docs/decisions/1062-opt-in-interactive-login-consent-ui.md) and
  [ADR-1065](docs/decisions/1065-interactive-login-consent-op-hosted-auth-surface.md); issues
  [#125](https://github.com/NCodeGroup/identity/issues/125), [#135](https://github.com/NCodeGroup/identity/issues/135).

- **The interactive login surface is backend-agnostic through a pluggable credential verifier.** Credential verification
  goes through the `IInteractiveCredentialVerifier` seam in the `.Abstractions` package; its local-account
  implementation lives in `NCode.Identity.OpenId.Accounts.Authentication`, is registered by
  `AddLocalAccountInteractiveAuthentication()`, and shares the path the resource-owner password grant uses. See
  [ADR-1070](docs/decisions/1070-interactive-login-consent-leaf-package-and-credential-verifier-seam.md); issue
  [#142](https://github.com/NCodeGroup/identity/issues/142).

- **Local accounts have one pluggable, backing-store-agnostic seam: `ILocalAccountStore`.** Credential handling is a set
  of first-class store operations (`AddAsync(account, password?, ct)`, `SetPasswordAsync`, `VerifyCredentialAsync`)
  rather than a password hash on the persisted account, and `SecurityStamp` is store-managed. The ASP.NET Core Identity
  adapter (`NCode.Identity.OpenId.Accounts.AspNetIdentity`) implements `ILocalAccountStore` over `UserManager<TUser>`
  (`AddAspNetIdentityLocalAccountStore<TUser>()`). `IFederatedPrincipalStore` provides `UpdateAsync` and `RemoveAsync`,
  and `IFederatedIdentityStore` provides `RemoveAsync`. See
  [ADR-1060](docs/decisions/1060-local-account-one-store-seam-and-dependency-inverted-authentication.md).

- **The interactive surface is served under the tenant route.** The login, consent, logout, and account endpoints share
  the protocol endpoints' tenant route, so interactive flows work under `DynamicByPath` at `/{tenantId}/account/*`.
  Every redirect the surface issues, the consent challenge, and the cookie scheme's login challenge are tenant-relative,
  and the server-rendered forms post to page-relative `login` and `consent`. Every page the surface serves sets
  `<base href="{tenant path}/account/">`; the `apps/auth` bundle builds with a relative base and resolves its API and
  page paths against `document.baseURI`, so one build works beneath any tenant. The host adds no middleware for the
  bundle, and `defineBackendServedSurfaceConfig` in `@ncode/vite-config` takes no `base`. See
  [ADR-1193](docs/decisions/1193-the-interactive-surface-is-served-under-the-tenant-route.md).

- **The `apps/auth` bundle's files are cached for a year and served without resolving the tenant.** The host serves only
  the bundle's content-hashed `assets` directory, at `GET|HEAD {tenant}/account/assets/{**asset}`, with
  `Cache-Control: public, max-age=31536000, immutable` and its `ETag` and `Last-Modified` validators, from a route group
  that resolves no tenant, so a returning browser loads the bundle without a request. Other files of the bundle's
  directory, such as `index.html`, are not served, and `@ncode/vite-config` pins `build.assetsDir` to `assets`
  ([ADR-1254](docs/decisions/1254-the-interactive-bundles-hashed-files-are-served-immutable-without-resolving-the-tenant.md),
  issue [#221](https://github.com/NCodeGroup/identity/issues/221)).

- **The interactive login and consent pages work without JavaScript, and the bundle mounts at once over the page the
  host rendered.** The server renders a real `<form>` (login: `username`/`password`/`login`; consent: a per-scope
  checkbox plus `consent-allow`/`consent-deny`) carrying the antiforgery token and return URL, injected into the
  single-page bundle's `#root`, and serves it standalone when no bundle is configured. One surface, no user-agent
  sniffing: no-JS clients (assistive technology, hardened browsers, the OpenID conformance suite's headless
  HtmlUnitDriver) get a working form, and everyone else gets the `apps/auth` app. The bundle mounts as soon as its
  script runs and starts from the rendered page: the values typed or autofilled into its text and password fields, the
  field that had focus, and the consent scopes the end-user cleared carry into its forms, and a carried password is
  written to the input's `value` property, never to an attribute. The authorization error page served with the bundle
  shows its error, not a login form
  ([ADR-1253](docs/decisions/1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md), issue
  [#211](https://github.com/NCodeGroup/identity/issues/211)).

- **The server-rendered pages stream into the response from a cached, pre-split bundle index.** The login, consent and
  error pages and the `form_post` authorization response render into a pooled `Sequence<char>` transcoded straight into
  `HttpResponse.BodyWriter`, without building a document string. `SinglePageBundle<InteractiveBundleShell>` reads the
  bundle's `index.html` once per version and splits it at its `<div id="root"></div>` mount point into an
  `InteractiveBundleShell`, so each page writes the cached head, its form and the cached tail; the renderer's page
  methods take an `IBufferWriter<char>` and the split shell. Each request compares the file's last-write time and length
  with the cached copy's, so a rebuilt bundle is picked up without a restart, including on bind-mounted and network file
  systems where change notifications are unreliable.

- **`SinglePageBundle<TIndex>` reads and serves a built single-page bundle.** `NCode.Identity.AspNetCore` reads a bundle's
  `index.html` once per version into the index type its surface parses, revalidated per request by the file's last-write
  time and length, resolves only the files of its `assets` directory, and serves them with
  `Cache-Control: public, max-age=31536000, immutable`, validators and range requests; the interactive surface and the
  management console each register one
  ([ADR-1457](docs/decisions/1457-a-single-page-bundle-is-read-and-served-through-one-general-helper.md), issue
  [#612](https://github.com/NCodeGroup/identity/issues/612)).

- **The consent screen shows the real client and a per-scope choice with friendly names, every requested scope
  pre-selected** ([ADR-1076](docs/decisions/1076-consent-persistence-and-enforcement.md),
  [ADR-1081](docs/decisions/1081-granular-consent-and-authorized-applications.md), issue
  [#131](https://github.com/NCodeGroup/identity/issues/131)). `GET /account/consent/details` resolves the pending
  request from the return URL without consuming it and, through `ResolvePendingConsentAsync` and
  `IScopeDescriptionService`, returns the client's `client_name` and each scope's description and already-granted state;
  the screen renders a checkbox per scope (keeping the `consent-allow` and `consent-deny` locators), and
  `POST /account/consent` records the decision. On the no-JS form and on the `apps/auth` form, **Allow** grants the
  request as asked, including `openid` and `offline_access`, so the token response carries the `id_token` and
  `refresh_token`; the end-user unchecks a scope to grant fewer.

- **The OP's interactive surface has a first-party account page at `/account` with a Sign out that runs the full
  server-side logout** ([ADR-1074](docs/decisions/1074-session-management-and-logout.md)). The page reads the
  same-origin `/account/me` JSON endpoint and shows the current session (subject, `auth_time`, `amr`, `idp`, `sid`).
  Sign out POSTs, antiforgery-protected, to `/account/logout`, which runs the same `EndSessionCommand` as the
  `end_session_endpoint`, identified by the cookie rather than an `id_token_hint`, so a first-party sign-out revokes the
  session and its grants and sends back-channel notifications.

- **The account surface has an active-sessions page** (issue [#63](https://github.com/NCodeGroup/identity/issues/63)).
  The end-user reviews every session they hold (device/browser and origin IP captured at sign-in, sign-in and
  last-active times), with the current session marked, and signs out of one session, everywhere else (keeping the
  current session), or everywhere. The same-origin endpoints `GET /account/sessions/list`,
  `POST /account/sessions/revoke` and `POST /account/sessions/revoke-all` delegate to `ISessionService`
  (`RevokeAllExceptAsync`, and `RevokeAllAsync` taking the termination reason), so each revocation runs the full
  termination seam and is audited as `SessionTerminationReason.UserInitiated`; "sign out everywhere else" also revokes
  every grant issued under no session. `SessionGrant` and `CreateSessionRequest` capture the sign-in `UserAgent` and
  `RemoteIpAddress`.

- **The account surface has an authorized-applications page where the end-user reviews and revokes the applications they
  have authorized** (issue [#40](https://github.com/NCodeGroup/identity/issues/40),
  [ADR-1081](docs/decisions/1081-granular-consent-and-authorized-applications.md)): the `/account/applications` page,
  `/account/applications/list` JSON and an antiforgery-protected `/account/applications/revoke` POST, backed by
  `IConsentService.GetAuthorizedApplicationsAsync` and `RevokeConsentAsync`.

- **An end user can change their password.** The self-service page `account/password`
  (`InteractiveAuthenticationDefaults.PasswordPath`) takes the current password and the password to set; a change ends
  every other session of the principal with `password_changed` (`SessionTerminationReasons.PasswordChanged`), which
  sends back-channel logout, revokes every grant issued under no session (`GrantFilter.WithoutSession`), and keeps the
  current session and the consents. A credential backend offers the change by registering `IInteractivePasswordChanger`,
  which returns an `InteractivePasswordChangeResult`, as the local accounts do
  ([ADR-1230](docs/decisions/1230-an-end-user-changes-their-password-and-keeps-only-the-current-session.md), issue
  [#290](https://github.com/NCodeGroup/identity/issues/290)).

- **A tenant brands its login, consent, and account pages.** The interactive surface registers the tenant settings
  `branding_display_name`, `branding_logo_uri` (absolute `https`), `branding_favicon_uri` (absolute `https`),
  `branding_primary_color` and `branding_secondary_color` (hexadecimal), and `branding_color_mode` (`light` or `dark`),
  named in `InteractiveSettingNames` and `InteractiveSettingKeys`. Every page served with the `apps/auth` bundle
  carries the tenant's branding, in the bootstrap document's `branding` shape, in a
  `<script type="application/json" id="branding">` block after its `<base>`, and the bundle themes itself from it
  without a request: it themes the surface with the colors and mode, draws the logo, and titles the login and account
  pages with the display name; a page without branding gets the default theme. `@ncode/config` exports
  `parseBrandingConfig`, which the bootstrap parser uses too, so a malformed branding token is dropped, never trusted.
  A tenant that sets `branding_favicon_uri` shows that icon in its pages' tabs instead of the family's: the host links
  it, drops the bundle's family icon links, and admits its origin in the page's `img-src`
  ([ADR-1194](docs/decisions/1194-a-tenant-brands-its-interactive-surface-through-settings.md),
  [ADR-1253](docs/decisions/1253-the-interactive-bundle-mounts-at-once-over-the-page-the-host-rendered.md),
  [ADR-1494](docs/decisions/1494-a-tenant-sets-its-sign-in-tabs-icon-as-a-branding-setting-the-hosts-link-in-place-of-the-familys.md),
  issues [#211](https://github.com/NCodeGroup/identity/issues/211),
  [#745](https://github.com/NCodeGroup/identity/issues/745)).

- **The interactive surface renders in the language the request prefers.** The locales the end-user interface
  renders are a registered vocabulary (`IUiLocale`, resolved through `IUiLocaleCatalog`), so the tenant settings
  `ui_locales_supported`, which discovery advertises, and `default_ui_locale` name only locales the interface renders.
  An authorization request's `ui_locales` reaches the login and consent pages, an end-session request's
  (`IEndSessionRequest.UiLocales`) the logout confirmation page, and every page renders in the first preference it
  serves and the tenant supports (RFC 4647 lookup, so `fr-CA` selects `fr`), then the user agent's `Accept-Language`,
  then the tenant's default language, which `ui_locales_supported` bounds like a preference, so the advertised set and
  the rendered default never disagree; a preference nothing serves never fails the request (OpenID Connect Core 1.0
  §3.1.2.1). The surface ships English, German, Spanish and French as `InteractiveLocale` records (`InteractiveLocales`),
  each carrying the text of its server-rendered pages; a host adds a language, or rewords a shipped locale, with
  `AddInteractiveLocale`, and a locale registered without the surface's messages stops the host as it starts. The page
  carries the selected locale in its `html` element's `lang` attribute, which the `apps/auth` bundle reads to render
  from its own typed catalogs (`apps/auth/src/i18n`), and its forms carry it back as `ui_locales`, so a page the host
  redirects to, such as the login page after a refused sign-in or the account page after a declined sign-out, renders
  in the same language
  ([ADR-1402](docs/decisions/1402-the-interactive-surface-renders-in-the-locale-the-request-prefers-among-the-registered-ui-locales.md),
  issue [#540](https://github.com/NCodeGroup/identity/issues/540)).

- **The HTML page security model and the front-channel marker are general ASP.NET Core helpers in `NCode.Identity.AspNetCore`.**
  `HtmlPageSecurityPolicy`, `HtmlPageSecurityResponseExtensions` (`SetHtmlPageSecurity`),
  `ContentSecurityPolicyDirective`, `ContentSecurityPolicyDirectives` and `ContentSecurityPolicySources` are in the
  `NCode.Identity.AspNetCore` namespace, and `SetHtmlPageSecurity` writes its cache headers through `SetNoStore()`. The
  front-channel marker is general endpoint metadata: `IFrontChannelEndpointMetadata`, `FrontChannelEndpointMetadata` and
  `FrontChannelEndpointConventionBuilderExtensions`, with `WithFrontChannel()` and `Endpoint.IsFrontChannel`.
  `NCode.Identity.AspNetCore` references `NCode.Buffers` ([#513](https://github.com/NCodeGroup/identity/issues/513),
  [ADR-1388](docs/decisions/1388-the-html-page-security-model-and-the-front-channel-marker-are-general-aspnet-core-helpers.md)).

- **One TypeScript UI platform serves every frontend surface (`frontend`).** Management, login/consent, playground
  and OIDC conformance surfaces share React + Material UI, Redux Toolkit + RTK Query, Vite, Vitest + React Testing
  Library and Storybook, orchestrated by pnpm workspaces + Turborepo and pinned through Corepack. Shared `@ncode/*`
  packages (`config` runtime discovery, `api` RTK Query client with a global/tenant scope model, `store` Redux assembly,
  `ui` MUI design system) back thin per-surface app shells, including the management console (`@ncode/app-mgmt`) with
  global-vs-tenant boundary browsing. Bundles are tier-agnostic: all configuration is discovered at runtime from the
  serving host and nothing is baked in at build time, and a branding seam supports per-tenant theming. The toolchain is
  isolated under `frontend` and is separate from the .NET Definition of Done. See
  [ADR-1064](docs/decisions/1064-frontend-presentation-platform.md).

- **The frontend runs on React 19 and Material UI 9.** This covers every surface, including the login and consent pages
  the OpenID Provider host serves. Components style through `sx` and pass input attributes through `slotProps`, and
  `Switch` has the ARIA `switch` role, so assistive technology announces the "Disabled" toggles in the management forms
  as on/off switches.

- **Every frontend bundle splits into vendor and surface chunks, and an oversized chunk fails the build.** The shared
  Vite preset splits React, MUI with emotion, Redux and every other package into their own vendor chunks, and each app
  lazy-loads its route-level containers, so no chunk exceeds 500 kB after minification. The preset's
  `failOnOversizedChunks` plugin fails `pnpm build` when a chunk exceeds the build's `chunkSizeWarningLimit`
  ([ADR-1175](docs/decisions/1175-bundles-split-into-vendor-and-surface-chunks-and-an-oversized-chunk-fails-the-build.md)).

- **The management API is served under a top-level `/api` route group.** `ManagementEndpointGroupProvider` maps the
  management endpoints there, apart from the OpenID protocol endpoints at the root
  ([ADR-1009](docs/decisions/1009-endpoint-families-own-their-route-group.md)). The control plane is `/api/platform`
  and `/api/teams`, and tenants are created and listed under `/api/teams/{teamId}/tenants`; every tenant-plane family
  (a tenant's own secrets, settings, address and usage, its clients, resource servers, grants, sessions, principals,
  local accounts and audit records) is addressed under `/api/tenants/{tenantId}/…`, the
  `ManagementEndpointGroups.Tenant` group, so a tenant administrator can never address another tenant.

- **One management resource server (`urn:ncode:management`) gates the whole management API through granular per-leaf
  scopes.** Scopes are composed as `{verb}:{family}` (for example `read:clients`, `update:tenant_settings`,
  `update:team_settings`, `create:resource_servers`, `delete:grants`). The control-plane families
  (`platform_settings`, `platform_encryption_keys`, `teams`, `team_settings`, `tenants`) are seeded only into the root
  (control-plane) tenant, whose
  identifier is `TenantResolutionOptions.Root.TenantId`, bound from `TenantResolution:Root:TenantId` (default `"root"`);
  the tenant-plane families (a tenant's own settings and secrets, its clients, client secrets, resource servers,
  resource-server secrets, client grants and grants) are seeded into every tenant. A `SystemResourceServerDescriptor`
  declares a `Plane` (`SystemResourceServerPlane.Tenant`, the default, or `Control`), and the plane-aware system-resource-server seeder merges every provider that contributes to
  one audience into one resource server per tenant, seeding `Control`-plane families only into the root tenant. The
  static single-tenant strategy provisions the root tenant lazily alongside its workload tenant, so a single-tenant
  deployment has its control plane with no extra configuration. See
  [ADR-1033](docs/decisions/1033-authorization-model-scopes-roles-and-ownership.md),
  [ADR-1031](docs/decisions/1031-control-plane-management-resource-server-and-root-tenant-seeding.md) and
  [ADR-1024](docs/decisions/1024-control-plane-and-per-tenant-planes.md).

- **Resource ownership is data-driven.** Persisted `(principal, role, resource-node)` role assignments
  (`PersistedRoleAssignment` / `IRoleAssignmentStore`) back an additive `OwnershipHandler` that grants a principal every
  operation except `Create` on a resource it owns, or on any resource under a node it owns, and the principal that
  creates a client, resource server or tenant becomes its owner. Each of those resources has owner-management endpoints
  (`GET`/`POST`/`DELETE /{resource}/{id}/owners`), whose list answers `RoleListResource` and whose grant answers
  `RoleHolderResource`, a grant the principal already holds answers `200` with its assignment, and a one-owner invariant
  refuses to orphan a resource with `409`. The ownership service reports what it changed (`RoleAssignmentGrant`,
  `OwnerRemoval`). The ownership service and resource-node abstraction (`IResourceOwnershipService`, `IResourceNode`,
  `ResourceNode`, `ResourceNodeTypes`) are public, so applications own and enforce their own resource types; the
  built-in `ResourceNodeTypes` values are the `OpenIdResourceTypes` values. See
  [ADR-1034](docs/decisions/1034-persisted-role-assignments-and-ownership.md).

- **A client acting for itself is a principal.** Every client has a principal (`PersistedClient.PrincipalId`,
  `ClientResource.principalId`, `OpenIdClient.PrincipalId`) that its store generates when the client is added — 128
  random bits, Base64Url — and that no caller chooses. A `client_credentials` access token names it as its `sub` beside
  the client's `client_id` (RFC 9068 §2.2), and `IPrincipalResolver` resolves such a subject to the client's principal
  through `IClientPrincipalResolver` while the client is enabled and to none when it is disabled, never provisioning a
  federated principal for it. `IClientPrincipalResolver` answers each caller once per request, so a request reads the
  client its caller acts as at most once. Token exchange refuses such a token as its `subject_token` (`invalid_grant`)
  and accepts it as its `actor_token`, and UserInfo refuses it (`invalid_token`), both as `IClientPrincipalResolver`
  decides. A machine client becomes the owner of what it creates within its owning node, its management changes are
  audited with the `client` actor, which the management API resolves when it authenticates the caller, so a machine
  client that deletes itself is recorded as the `client` actor, and deleting a client removes the assignments it holds
  ([#557](https://github.com/NCodeGroup/identity/issues/557),
  [#587](https://github.com/NCodeGroup/identity/issues/587),
  [#632](https://github.com/NCodeGroup/identity/issues/632),
  [#633](https://github.com/NCodeGroup/identity/issues/633),
  [#634](https://github.com/NCodeGroup/identity/issues/634),
  [ADR-1419](docs/decisions/1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md),
  [ADR-1450](docs/decisions/1450-a-client-credentials-access-token-names-the-clients-principal-as-its-subject.md),
  [ADR-1470](docs/decisions/1470-a-callers-acting-client-is-resolved-once-per-request.md)).

- **A team or the platform owns its machine clients.** `POST`/`GET /api/teams/{teamId}/clients` and
  `POST`/`GET /api/platform/clients` create and list the control-plane clients that the team or the platform owns
  (`PersistedClient.OwningNodeType`/`OwningNodeId`, `ClientResource.owningNodeType`/`owningNodeId`,
  `IClientStore.GetPageByOwningNodeAsync`), with creation or read authority at that node. A machine client obtains
  tokens only for itself (its `grant_types_supported` is `client_credentials` alone, `400` otherwise), is managed
  through the control-plane tenant's client routes by its owner's administrators, holds roles only at its owning node
  or beneath it, and is left alone by promotions; a team that owns one cannot be deleted (`409`)
  ([#588](https://github.com/NCodeGroup/identity/issues/588),
  [ADR-1466](docs/decisions/1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md)).

- **The management API tells a caller where it holds authority.** `GET /api/me` returns the caller's principal
  (`CallerResource.principalId`), whether it holds authority at the platform node (`isPlatformAdministrator`) and its
  role assignments (`RoleAssignmentResource`); `GET /api/me/teams` and `GET /api/me/tenants` list the teams and
  tenants it holds a role at. They answer the caller its management access token authenticates, and a request without
  one `401` with a problem. Every request is authorized per resource from the caller's role assignments, so one token
  serves every team and tenant the caller administers and names none of them
  ([#566](https://github.com/NCodeGroup/identity/issues/566),
  [#715](https://github.com/NCodeGroup/identity/issues/715),
  [ADR-1420](docs/decisions/1420-a-callers-teams-and-tenants-are-discovered-from-its-role-assignments.md),
  [ADR-1484](docs/decisions/1484-a-management-routes-caller-is-the-principal-its-management-access-token-authenticates.md)).

- **The management console opens on the caller's home.** `/home` lists the platform for a caller with platform
  authority and the teams and tenants the caller holds a role at, and opens the only one at once when there is one.
  A team administrator signs in with the same token as a platform administrator and works within its teams; a
  platform route sends a caller without platform authority home, and a scope's back control leads home when the
  caller holds no role at the scope above it ([#566](https://github.com/NCodeGroup/identity/issues/566),
  [ADR-1420](docs/decisions/1420-a-callers-teams-and-tenants-are-discovered-from-its-role-assignments.md)).

- **Collection endpoints page through management resources by keyset.** A collection `GET`, such as `GET api/teams`,
  `GET api/tenants` or `GET api/tenants/{tenantId}/clients`, returns a page of resources (`CollectionResource<T>`, with
  `items` and `continuationToken`) using keyset (cursor) pagination (`?limit=` clamped to 100, `?cursor=` an opaque
  continuation token). Keyset pagination lives in `BaseStore`, so a store not keyed by a resource id, such as the grant
  store, paginates on its chronological surrogate id. There is deliberately no query protocol (no OData); see
  [ADR-1019](docs/decisions/1019-collection-endpoints-keyset-pagination.md).

- **Every management write honors optimistic concurrency.** A resource carries its `concurrencyToken`, a write sends it
  as `If-Match`, and a write that loses a concurrency race is answered `412 Precondition Failed`
  ([ADR-1214](docs/decisions/1214-a-management-write-that-loses-a-race-is-answered-412.md), issue
  [#261](https://github.com/NCodeGroup/identity/issues/261)).

- **`GET /api/capabilities` publishes a management discovery document composed from registered providers
  to an authenticated management caller**
  ([ADR-1067](docs/decisions/1067-secret-type-capability-catalog-and-capabilities-discovery.md)). Like the OpenID
  discovery document, the `[JsonExtensionData]` document is assembled from
  `ICommandHandler<DiscoverManagementCapabilitiesCommand>` providers, so a capability is exposed by registering a
  provider rather than by editing the endpoint or resource type. It reports the secret-type catalog, including which
  types are asymmetric, and an `algorithms` section (issue [#164](https://github.com/NCodeGroup/identity/issues/164)):
  `AlgorithmCapabilitiesProvider` projects every supported signature and key-management algorithm from the live
  `IAlgorithmCollectionProvider` as an `AlgorithmCapabilityResource` with its `code`, implied `use` (`sig` or `enc`)
  and every compatible secret type (from `ISecretKeyTypeCatalog`) with its key sizes and curves (`keyTypes: [{
  secretType, keyBitSizes, curves: [{ curveName, curveSizeBits }] }]`)
  ([#327](https://github.com/NCodeGroup/identity/issues/327),
  [ADR-1389](docs/decisions/1389-one-algorithm-name-serves-every-key-type-it-declares.md)).

- **Authenticated effective-settings preview endpoints.** `GET api/platform/effective-settings`,
  `GET api/teams/{teamId}/effective-settings`, `GET api/tenants/{tenantId}/effective-settings`,
  `GET api/tenants/{tenantId}/clients/{clientId}/effective-settings`
  and `GET api/tenants/{tenantId}/resource-servers/{resourceServerId}/effective-settings` return the resolved, merged
  settings view of each settings-bearing entity (platform baseline, team and tenant overrides, client or
  resource-server overrides and descriptor defaults) as a flat `name → value` JSON object, using the discovery
  document's value representation but including settings that are not discoverable. Each is gated by the same `Read`
  authorization as reading that entity's raw settings and carries no `ETag` (a merged view spans several persisted
  entities). The wire contracts include `PlatformEffectiveSettingsResource`, `TeamEffectiveSettingsResource`,
  `TenantEffectiveSettingsResource` and `ClientEffectiveSettingsResource`. See
  [ADR-1040](docs/decisions/1040-effective-settings-preview-endpoints.md) and
  [ADR-1042](docs/decisions/1042-management-api-route-driven-tenant-scope.md).

- **A management request answers `409` for a stored settings document it cannot honor.** The effective-settings reads, a
  client's create and settings update, the incoherent-client report, a local account's create and password reset, and a
  tenant settings write whose team or platform document cannot be honored answer `409 Conflict`, only after authorization, with a
  problem whose `settingsScope`, `ownerId` and `documentError` name the document, and log `StoredSettingsUnreadable`
  (event ID 3009); a settings write that overwrites the document repairs it. `IEffectiveSettingsResolver` provides
  `ResolveStored`, and it and `ResolvePlatformAsync(CancellationToken)` throw `UnreadableSettingsException`, a
  `JsonException` that names the document
  ([ADR-1245](docs/decisions/1245-a-management-request-answers-409-for-a-stored-settings-document-it-cannot-honor.md),
  issue [#301](https://github.com/NCodeGroup/identity/issues/301)).

- **The management API rejects a client algorithm its effective settings do not permit.** Client create and settings
  update merge the client's document onto its tenant's effective settings, as the runtime does, and answer `400` with a
  validation problem naming the setting when a registered ID token, UserInfo or introspection signing or encryption
  algorithm is outside its effective `*_values_supported` list; the default `A128CBC-HS256` of a registered encryption
  without an `enc` is checked the same way. A tenant settings update checks every client of the tenant, a team settings
  update every client of the tenants of the team and of every team beneath it, walked a page of child teams at a time,
  and a platform settings update every client of every tenant, each answering `400` naming the list and the client when
  the change excludes an algorithm a client registers; a mismatch the change does not introduce is not reported. A
  mismatch is reported when the settings are written rather than at issuance.
  `OpenIdPlatform.ConfiguredSettingsProvider` holds the settings the platform derives from its defaults and the host's
  configuration, onto which its persisted settings document
  is merged ([ADR-1155](docs/decisions/1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [ADR-1455](docs/decisions/1455-a-teams-subtree-is-walked-depth-first-one-page-of-child-teams-per-level.md)).

- **The management API lists a tenant's incoherent clients.** `GET api/tenants/{tenantId}/incoherent-clients` checks a
  page of the tenant's stored clients (`cursor`, `limit` up to 100) against the tenant's effective settings and returns
  each client they make incoherent with every violation (`settingName`, `value`, `governingSettingName`, `description`),
  or the `documentError` of a settings document that cannot be honored; a page can list no client while
  `continuationToken` remains. `IClientCoherenceReporter` finds them for the API and for the reconciliation's report,
  and `IncoherentClient`, `IncoherentClientResource` and `ClientCoherenceViolationResource` describe them
  ([ADR-1233](docs/decisions/1233-the-management-api-lists-a-tenants-incoherent-clients.md), issue
  [#278](https://github.com/NCodeGroup/identity/issues/278)).

- **A tenant is named by whoever creates it.** `POST /api/teams/{teamId}/tenants` requires `CreateTenantRequest.TenantId`: 1 to 63
  lowercase letters, digits or hyphens that starts and ends with a letter or digit, and never changes. It reads the same
  in the management API (`/api/tenants/acme`), in a path tenant's issuer (`https://op.example/acme`) and in a host
  tenant's hostname (`acme.auth.example`), so a path tenant can be created with its issuer in one request; `displayName`
  is the editable label. The configured root tenant ID and `StaticSingle:TenantId` must have the same form or the host
  does not start. Creating a tenant commits the tenant row first, then the creator's ownership role assignment, and the
  response carries the tenant's concurrency token after seeding
  ([ADR-1159](docs/decisions/1159-a-tenant-is-named-by-its-creator.md)).

- **A tenant's address is a routed tenant property the management API previews before it is written.** `TenantResource`,
  `CreateTenantRequest` and the tenant update carry the tenant's `issuer` and its mTLS endpoint alias authority
  (`mtlsEndpointAliasAuthority`, RFC 8705 §5), where an empty alias restores the derived one.
  `GET /api/teams/{teamId}/tenants/{tenantId}/address` previews the address a tenant that does not exist yet would have
  (it needs the authority to create a tenant in the team, answers `404` for a missing team and reports whether the id
  is taken in `exists`), and `GET /api/tenants/{tenantId}/address` previews an existing tenant's (it needs read on the
  tenant, and answers `404` when it does not exist). Each takes an optional proposed issuer and alias and returns a
  `TenantAddressResource`: the issuer the tenant has or would be given and whether it is the derived one, the alias and
  whether it is derived (`isMtlsEndpointAliasAuthorityDerived`), whether the tenant already exists, and every reason the
  tenant could not be served there, by the same rules the create and update apply
  ([ADR-1195](docs/decisions/1195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md), issue
  [#157](https://github.com/NCodeGroup/identity/issues/157)).

- **The management API reports each tenant's environment.** `TenantResource` carries `environment`
  (`TenantEnvironmentResource`: `type`, `name`, `isProduction`), the tenant's effective environment, on the tenant list,
  read and create responses; it is omitted when the tenant's, its team's or the platform's stored settings cannot be honored, so such
  a tenant is still listed. `isProduction` comes from the registered type, so a contributed type is told apart without
  knowing its name
  ([ADR-1289](docs/decisions/1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md) decision 13,
  issue [#412](https://github.com/NCodeGroup/identity/issues/412)).

- **Deleting a tenant removes its keys, its system resource servers and its role assignments.** They are deleted with
  the tenant in one unit of work, so a tenant with provisioned keys can be deleted and only a client or a resource
  server the tenant did not seed blocks the delete with `409`
  ([ADR-1169](docs/decisions/1169-every-tenant-is-provisioned-with-persisted-keys.md), issue
  [#242](https://github.com/NCodeGroup/identity/issues/242)).

- **The tenant usage query validates its range.** `GET /api/tenants/{tenantId}/usage` answers a `400` validation problem
  when the default range cannot be computed, such as a `to` less than 30 days after `0001-01-01` with no `from`.

- **A managed client belongs to the request's tenant and is identified by its server-generated id.**
  `POST api/tenants/{tenantId}/clients` takes no tenant identifier in its body: the server derives the owning tenant
  from the request, which it resolves under every tenant strategy, static-single included. A client has no display name;
  `ClientResource` is `{ clientId, tenantId, concurrencyToken, isDisabled }`.

- **Resource servers, their scopes and client grants are managed Auth0-style.** A tenant-owned **resource server** (an
  API or audience) owns its **scopes** (permissions), and a **client grant** authorizes a client to a resource server
  with a subset of its scopes. The persistence model is `ResourceServerEntity` / `ScopeEntity` / `ClientGrantEntity`
  with their DTOs and `IResourceServerStore` / `IClientGrantStore`. Each tenant is seeded through
  `ISystemResourceServerProvider` with a reserved OpenID Connect system resource server (`urn:ncode:openid`) carrying
  the standard scopes and with the tenant-plane management resource server (`urn:ncode:management`). The surface is
  `api/tenants/{tenantId}/resource-servers` (list, create, read, `PATCH`, delete) with its `/{resourceServerId}/scopes`
  sub-resource, whose update is a whole-resource `PUT` because a scope's value is immutable, and the nested
  `api/tenants/{tenantId}/clients/{clientId}/grants[/{resourceServerId}]` client-grant surface (list, create, read,
  `PUT`, delete). Granted scopes are validated at write time against the target resource server's scopes, both surfaces
  assert their preconditions up front through replaceable validators (`IResourceServerValidator` /
  `IClientGrantValidator`), and system resource servers and their scopes cannot be deleted. See
  [ADR-1023](docs/decisions/1023-scopes-and-resources-management-model.md).

- **Resource servers are settings and secret owners like clients and tenants** (issue
  [#134](https://github.com/NCodeGroup/identity/issues/134),
  [ADR-1066](docs/decisions/1066-resource-server-settings-and-secret-owner.md)). A resource server (an API, in Auth0
  terms) owns a wrapped settings object and a secret collection, each with its own concurrency token:
  `GET`/`PATCH api/tenants/{tenantId}/resource-servers/{resourceServerId}/settings`, its effective settings, and full
  `…/secrets` CRUD with the one-time reveal ([ADR-1050](docs/decisions/1050-secret-material-revealed-once-on-create.md))
  and `If-Match`. A `resource_server_secrets` scope family is seeded. A resource server's introspection clients are the
  clients bound to it
  ([ADR-1168](docs/decisions/1168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)).

- **Every key the management API reports carries its `kid`.** `SecretResource` and `CreatedSecretResource` have a
  required `KeyId`: the `kid` the key was imported with, or its secret identifier when it has none
  ([ADR-1186](docs/decisions/1186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md)).

- **A secret is created with the key parameters its validation resolved.** `ISecretValidator.ValidateCreate` returns a
  `SecretCreateValidation` that carries the resolved `SecretKeyGenerationParameters`, and the endpoints generate the key
  with what was validated, on the curve its algorithm accepts
  ([#337](https://github.com/NCodeGroup/identity/issues/337),
  [ADR-1392](docs/decisions/1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md)).

- **The management API and its console show how a secret is stored.** `SecretResource.IsVerifier` is `true` for a
  secret stored as a one-way verifier, which authenticates a method that compares the secret and never one that keys
  from it, such as `client_secret_jwt`, and the management console marks such a secret with a `Verifier` chip from the
  contract's `isVerifier` ([#163](https://github.com/NCodeGroup/identity/issues/163), [ADR-1391](docs/decisions/1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).

- **The management API reads an asymmetric secret's public key.** The read-only sub-resource
  `GET /api/…/{tenants|clients|resource-servers}/{ownerId}/secrets/{secretId}/public-key` returns a
  `PublicKeyResource` carrying both a JWK (RFC 7517, the representation the JWKS endpoint publishes, produced by the
  key's `ISecretKeyTypeDescriptor.ExportPublicKey` seam) and a SubjectPublicKeyInfo PEM (`-----BEGIN PUBLIC KEY-----`)
  for an admin UI or crypto tooling. Private key material is never exposed; a symmetric secret has no public-key
  sub-resource and answers `404 Not Found`. The read is gated by the same `Operations.Read` authorization as reading the
  secret. See [ADR-1058](docs/decisions/1058-asymmetric-public-key-material-read-path.md).

- **Rotating a client secret keeps the rotated secret beside its replacement.**
  `POST api/tenants/{tenantId}/clients/{clientId}/secrets/{secretId}/rotate` generates a replacement shared secret with
  the rotated secret's type, entropy, `use` and `alg`, stored in the form the client's method needs, and the expiry the request names (`RotateSecretRequest`), adds it
  beside the rotated secret so both authenticate while its holder switches, and returns the replacement's
  `CreatedSecretResource`, the only place its material appears; delete the rotated secret when the switch is done. The
  optional `rotatedExpiresWhen` shortens the rotated secret's expiry and never extends it. Rotation is authorized as
  creating a secret and updating the rotated one, `If-Match` is honored, and a key answers `400`
  ([ADR-1320](docs/decisions/1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md),
  [ADR-1183](docs/decisions/1183-a-client-secret-is-rotated-by-replacing-it-in-one-change.md), issues
  [#159](https://github.com/NCodeGroup/identity/issues/159), [#445](https://github.com/NCodeGroup/identity/issues/445)).

- **Grant administration is a tenant-scoped, read-and-revoke surface.** `GET api/tenants/{tenantId}/grants` (paged,
  filtered by subject, client, session (`sid`) and grant type), `GET …/grants/{grantId}`, `DELETE …/grants/{grantId}`
  (single soft-revoke) and `DELETE …/grants` with filters (bulk soft-revoke of every active match, requiring at least
  one filter and returning `GrantRevocationResult { revoked }`); subject-wide "sign out everywhere" is the bulk revoke.
  Revocation sets `RevokedWhen` and keeps the row for audit, and is idempotent. Grants are addressed by an opaque,
  server-generated `GrantId` (`PersistedGrant.GrantId` / `GrantEntity.GrantId`), and `GrantResource` projects metadata
  only, including the grant's session id: the internal `HashedKey` and the grant `PayloadJson` are never exposed. See
  [ADR-1020](docs/decisions/1020-grant-management-read-surface.md),
  [ADR-1022](docs/decisions/1022-grant-filters-and-bulk-revocation.md) and
  [ADR-1080](docs/decisions/1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md).

- **`/api/tenants/{tenantId}/sessions` lets an administrator list and revoke a subject's sessions** (issue
  [#63](https://github.com/NCodeGroup/identity/issues/63),
  [ADR-1100](docs/decisions/1100-explicit-tenant-context-materialization.md)). `GET ?subjectId=…` lists a subject's
  active sessions as `SessionResource`; `DELETE /{sessionId}` revokes one session and `DELETE ?subjectId=…` every
  session the subject holds. Each revocation runs the full session-termination seam (grant revocation, back-channel
  logout and session auditing) in the addressed tenant's security domain and attributes the acting administrator
  (`session.revoked.admin`).

- **An administrator can block any end user.** `POST api/tenants/{tenantId}/principals/{principalId}/block` blocks a
  principal whatever its connections, `POST api/tenants/{tenantId}/principals/{principalId}/unblock` lifts the block,
  and `GET api/tenants/{tenantId}/principals/{principalId}` reports it (`PersistedFederatedPrincipal.IsBlocked`, stored
  on the federated principals table). The endpoints carry `OpenIdConstants.EndpointTags.Principals`
  ([ADR-1226](docs/decisions/1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md),
  issues [#284](https://github.com/NCodeGroup/identity/issues/284),
  [#289](https://github.com/NCodeGroup/identity/issues/289)).

- **Local-account management is an opt-in capability of the management API.** The `local-accounts` resource family under
  `/api/tenants/{tenantId}/local-accounts` lets a tenant or global administrator create, list, get, update, delete,
  disable and enable a local account, reset its credential, manage its profile claims, and read and set its server-owned
  metadata. Creating an account provisions its owning `FederatedPrincipal` and its one-to-one self-issued
  `FederatedIdentity` in the same unit of work, so principal-level metadata (`ProfileMetadata` / `SystemMetadata`) has a
  target from the moment the account exists; the metadata endpoint is the administrator-gated write path that keeps
  `SystemMetadata` never end-user-writable. Local accounts are end-user subjects rather than ownable resources, so every
  operation authorizes against the owning tenant scope (`TenantScopeResource`), gated by `*:local_accounts` scopes. The
  endpoints, their validator (`ILocalAccountValidator` / `DefaultLocalAccountValidator`), the wire contracts (namespace
  `NCode.Identity.OpenId.Accounts.Management.Contracts`) and `LocalAccountChangedAuditEvent` live in the account-owned
  packages `NCode.Identity.OpenId.Accounts.Management` (`AddLocalAccountManagement()`) and
  `NCode.Identity.OpenId.Accounts.Management.Abstractions`, so `NCode.Identity.OpenId.Management` does not reference
  `NCode.Identity.OpenId.Accounts.Abstractions`; the resource type is `AccountManagementResourceTypes.LocalAccount`. A
  capability is enabled by registering its package, with no per-request feature detection.
  `NCode.Identity.OpenId.Management.Abstractions` provides what a management capability reuses: the public
  `ManagementApiEndpointHandler` (generic authorization, `GET` and collection helpers), `DefaultResourceValidator`,
  `Operations`, `TenantScopeResource`, the `ManagementEndpointGroups.Tenant` group-name constant for attaching a keyed
  `IEndpointProvider` to the tenant group, and the generic audit seam
  `RecordResourceChangeAsync(HttpContext, ResourceChangeAuditEvent)`. See
  [ADR-1057](docs/decisions/1057-local-account-eager-provisioning-and-management-seam.md),
  [ADR-1060](docs/decisions/1060-local-account-one-store-seam-and-dependency-inverted-authentication.md) and
  [ADR-1061](docs/decisions/1061-local-account-management-is-an-opt-in-capability.md).

- **The management API looks up audit records.** `GET api/tenants/{tenantId}/audit-records` lists the tenant's records
  from the audit store, newest first by event time with ties broken by id, filtered by `startTime`/`endTime`,
  `eventName`, `eventCategory`, `outcome`, `subjectId`, `clientId`, `actorId`, `traceId` and a resource (`resourceType`,
  `resourceId`), with cursor pagination; each `AuditRecordResource` carries the envelope and the whole `record`.
  `IAuditRecordStore.GetPageAsync` takes an `AuditRecordFilter`
  ([ADR-1303](docs/decisions/1303-audit-records-are-kept-in-an-indexed-store-that-each-replica-writes-in-batches.md)
  decision 6, issue [#384](https://github.com/NCodeGroup/identity/issues/384)).

- **`@ncode/auth` holds the Playground's public-client protocol helpers.** The framework-free `@ncode/auth` package
  implements OAuth 2.0 authorization code with PKCE (`S256`): verifier and challenge generation, the authorize redirect,
  state/CSRF validation, code-for-token exchange, refresh and ID-token subject extraction, all injectable and
  unit-tested (including the RFC 7636 test vector).

- **The management console's address bar is its navigation state.** The console routes to the caller's home,
  `/home`, and by scope and surface: `/teams`, `/settings` and `/encryption-keys` in the platform scope,
  `/teams/{teamId}/tenants`, `…/settings` and
  `…/usage` within a team, and `/tenants/{tenantId}/clients`, `…/resource-servers`, `…/grants`, `…/secrets`,
  `…/encryption-keys`, `…/settings` and `…/usage` within a tenant, each tenant surface with its own tab. A view can be
  bookmarked, shared and reloaded, back and forward move between surfaces and scopes, and a deep link that lands on the sign-in screen returns
  to the same page once signed in. The management scope is derived from the route, and a host that serves the console
  answers every console path with `index.html`
  ([ADR-1174](docs/decisions/1174-the-management-consoles-url-is-its-navigation-state.md),
  [ADR-1255](docs/decisions/1255-each-tenant-surface-of-the-management-console-has-its-own-address.md), issue
  [#236](https://github.com/NCodeGroup/identity/issues/236)).

- **Management console cache tags are scoped.** Every tag id names the scope that owns the resource (`scopeTagId` in
  `@ncode/api`), so a mutation in the platform scope or one tenant refetches only that scope's caches, as
  [ADR-1064](docs/decisions/1064-frontend-presentation-platform.md) intends.

- **The management console creates, edits and deletes tenants.** RTK Query mutations
  (`createTenant`/`updateTenant`/`deleteTenant`), a stateless MUI `TenantFormDialog`, a reusable `ConfirmDialog`
  (Storybook-driven) and row-level edit and delete actions on the tenant table back it. The dialog asks for the tenant
  ID on create and locks it on edit, and edits the `issuer` and the mTLS endpoint alias, showing the derived alias as
  its hint and placeholder. As you type, `TenantsPage` previews the address (the create dialog through
  `getNewTenantAddress`, the edit dialog through `getTenantAddress`, each with an optional
  `mtlsEndpointAliasAuthority`): the issuer an empty field resolves to, a taken tenant ID, and why a proposed issuer or
  alias cannot be used; it re-judges an existing tenant's address only when its issuer or alias changes. The tenant
  table shows each tenant's issuer, alias and environment, highlights every non-production environment, and filters by
  environment type. Edits send the resource's `concurrencyToken` as `If-Match` over a `replace`-only JSON Patch; the
  UI-free `replacePatch` helper (`@ncode/api`) and the RFC 7807-aware `apiErrorMessage` helper (`@ncode/store`) are
  unit-tested directly (issues [#134](https://github.com/NCodeGroup/identity/issues/134),
  [#157](https://github.com/NCodeGroup/identity/issues/157), [#412](https://github.com/NCodeGroup/identity/issues/412)).

- **The management console manages clients.** Within a tenant the console creates, edits and deletes clients through
  RTK Query mutations addressed by the tenant (`createClient`/`updateClient`/`deleteClient` take a `tenantId`), a
  stateless MUI `ClientTable` (id, status and row actions) and a `ClientFormDialog`, beside the shared `ConfirmDialog`.
  Its `ClientResource` mirrors the server's shape, and writes send `concurrencyToken` as `If-Match` over a
  `replace`-only JSON Patch. Selecting a client opens its detail page with Secrets and Settings tabs (issue
  [#134](https://github.com/NCodeGroup/identity/issues/134),
  [ADR-1421](docs/decisions/1421-the-management-console-addresses-a-client-and-a-resource-server-through-their-tenant.md)).

- **The management console manages resource servers and their scopes.** Within a tenant the console creates, edits and
  deletes resource servers (APIs) and their nested scopes (permissions) through RTK Query mutations addressed by the
  tenant (`createResourceServer`/`updateResourceServer`/`deleteResourceServer`,
  `createScope`/`updateScope`/`deleteScope` take a `tenantId`), stateless MUI `ResourceServerTable`/`ScopeTable` with
  row actions, and `ResourceServerFormDialog`/`ScopeFormDialog` beside the shared `ConfirmDialog`. A resource server
  drills into a `ResourceServerDetailPage` with Scopes, Secrets, Settings and Clients tabs; resource servers embed their
  scopes inline, so a scope mutation refreshes both views, and the Clients tab lists and creates the clients bound to
  the resource server for introspection
  ([ADR-1168](docs/decisions/1168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)).
  Resource-server edits send `concurrencyToken` as `If-Match` over a `replace`-only JSON Patch, a scope update is a
  whole-resource `PUT`, and system resource servers and their scopes expose no delete control and no Secrets tab (issue
  [#134](https://github.com/NCodeGroup/identity/issues/134),
  [ADR-1421](docs/decisions/1421-the-management-console-addresses-a-client-and-a-resource-server-through-their-tenant.md)).

- **The management console reaches every owner's secrets and settings through one endpoint per operation.**
  `@ncode/store` exposes `useListSecretsQuery`, `useCreateSecretMutation`, `useDeleteSecretMutation`,
  `useRotateSecretMutation`, `useLazyGetSecretPublicKeyQuery`, `useGetSettingsQuery`, `useGetEffectiveSettingsQuery` and
  `useUpdateSettingsMutation`, each taking a `ManagementOwner` from `@ncode/api`, and `OwnerSecretsTab` and
  `OwnerSettingsTab` serve every detail page: clients, resource servers, the tenant's own surfaces, teams and the platform
  ([ADR-1257](docs/decisions/1257-the-management-console-reaches-every-owners-secrets-and-settings-through-one-endpoint-per-operation.md),
  issue [#236](https://github.com/NCodeGroup/identity/issues/236)).

- **The management console manages owners and administrators.** A team's and a tenant's Access surface lists, grants
  and revokes their administrators and owners, and a client's and a resource server's Access tab their owners, through
  `useListRoleAssignmentsQuery`, `useAddRoleAssignmentMutation` and `useRemoveRoleAssignmentMutation`, each taking a
  `RoleList` from `@ncode/api`. A list offers its grant form and revoke controls only when it answers `canChange`,
  shows itself read-only otherwise, and every role list refreshes after any grant or revoke
  ([#582](https://github.com/NCodeGroup/identity/issues/582),
  [#623](https://github.com/NCodeGroup/identity/issues/623),
  [ADR-1465](docs/decisions/1465-a-role-list-tells-its-reader-whether-it-may-grant-and-revoke-the-role.md)).

- **The management console manages secrets with a one-time reveal.** An owner's Secrets tab creates, lists and deletes
  secrets; generated key material is shown exactly once in a `RevealSecretDialog` with a copy affordance and is never
  re-fetched ([ADR-1050](docs/decisions/1050-secret-material-revealed-once-on-create.md)), and an asymmetric secret has
  no material to reveal. The create-secret form, driven by the capabilities document's `algorithms` section, offers the
  secret types the server advertises, such as `okp`, and only algorithms compatible with the chosen secret type, derives
  the read-only use, and bounds the key size by every size the chosen type's entry allows (min, max, step). It offers
  the chosen type's curves, such as `P-384` for `ECDH-ES` or `Ed448` for `EdDSA`, and sizes the key from the chosen
  curve; a secret type with curves takes a curve, which the create request carries for every such type, `okp`
  included. The form offers no secret type until the capabilities are discovered, and then the symmetric type when the
  server offers it or else the first one it does. The secret tables show each key's `kid` in a **Key ID** column and
  export an asymmetric secret's public key as JWK and PEM (copy or download). A client's shared secrets (the types the
  capabilities document reports as not asymmetric) carry a rotate action that, after confirmation, adds a replacement
  expiring a year out, reveals it once, and keeps the current secret for up to seven days
  ([ADR-1320](docs/decisions/1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md),
  [ADR-1389](docs/decisions/1389-one-algorithm-name-serves-every-key-type-it-declares.md),
  issues [#155](https://github.com/NCodeGroup/identity/issues/155),
  [#327](https://github.com/NCodeGroup/identity/issues/327), [#445](https://github.com/NCodeGroup/identity/issues/445)).

- **The management console edits settings beside their effective view.** An owner's Settings tab edits the opaque
  persisted settings JSON beside a read-only effective-settings view. Because settings are an open object, a save sends
  a top-level add/remove/replace JSON Patch diff, built by the UI-free `@ncode/api` `diffPatch` helper, with the owner's
  `concurrencyToken` as `If-Match`. The stateless MUI `GrantTable`, `SecretTable`, `SecretFormDialog`,
  `RevealSecretDialog` and `SettingsEditor` components are Storybook-driven and unit-tested (issue
  [#134](https://github.com/NCodeGroup/identity/issues/134)).

- **The management console manages a tenant's grants and mints initial access tokens.** The tenant's Grants surface
  lists persisted grants with subject and client filters and revokes them one by one or as a filtered bulk operation
  (`listGrants`/`revokeGrant`/`revokeGrants`); only active grants expose a revoke control. Its Mint initial access token
  action (RFC 7591; `createInitialAccessToken`) takes an optional description, lifetime and resource server, picked from
  the tenant's non-system resource servers and sent as `resourceServerId`, and shows the plaintext token exactly once in
  `RevealInitialAccessTokenDialog`, naming the resource server the registered client is bound to
  ([ADR-1192](docs/decisions/1192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md), issue
  [#134](https://github.com/NCodeGroup/identity/issues/134)).

- **An OpenID protocol client for server-side callers that needs no ASP.NET Core runtime.**
  `NCode.Identity.OpenId.Client`, which `AddOpenIdClientLibrary()` registers on the JWT library builder, makes token
  requests through `ITokenClient` and revocation requests through `ITokenRevocationClient`, and
  `IProviderMetadataResolver` and `IProviderKeySetResolver` resolve and cache a provider's discovery document and JWKS.
  Client authentication is an open `IClientAuthenticator` collection with `client_secret_basic`, `client_secret_post`,
  `private_key_jwt`, `tls_client_auth` and `self_signed_tls_client_auth`. A `ClientRegistration` names its signing keys
  through a `KeyRing`, which selects a keyed `ISecretKeyCollectionProvider`. The package references
  `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Http`,
  `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Options` and `Microsoft.Extensions.Primitives`
  directly and not the `Microsoft.AspNetCore.App` shared framework, so a console app, worker or function runs it on the
  .NET runtime alone
  ([ADR-1270](docs/decisions/1270-the-openid-protocol-client-uses-the-familys-jose-stack-and-named-key-rings.md),
  [ADR-1285](docs/decisions/1285-the-openid-protocol-client-needs-no-aspnet-core-runtime.md), issues
  [#123](https://github.com/NCodeGroup/identity/issues/123), [#321](https://github.com/NCodeGroup/identity/issues/321)).

- **The protocol client's calls are bounded and retried.** Every call of `NCode.Identity.OpenId.Client`, mutual-TLS
  clients included, runs through a total and a per-attempt timeout: `OpenIdClientOptions.TotalTimeout` bounds a whole
  request, the response body included, and `AttemptTimeout` (default 10 seconds) bounds each attempt. A GET or HEAD
  without a body or a DPoP proof is retried up to twice by default (`MaxRetryAttempts`) with jittered backoff
  (`RetryDelay`) after a connection failure, a timed-out attempt or a 408, 429 or 5xx response. The options are
  validated when the host starts, and the host does not start while `AttemptTimeout` exceeds `TotalTimeout`, so a
  `TotalTimeout` below 10 seconds needs an `AttemptTimeout` no higher. The BFF checks its refresh lease against
  `TotalTimeout`. The package bounds its calls through `Polly.Core` alone, without
  `Microsoft.Extensions.Http.Resilience` ([#469](https://github.com/NCodeGroup/identity/issues/469),
  [ADR-1336](docs/decisions/1336-the-protocol-client-bounds-its-calls-with-a-polly-core-pipeline.md)).

- **The OpenID protocol client authenticates with mutual TLS.** The methods `tls_client_auth` and
  `self_signed_tls_client_auth` present the TLS client certificate from `ClientRegistration.KeyRing`: the first RSA or
  EC key with a currently valid client-authentication certificate and a private key, never an `enc` key. The handshake
  presents the key's issuer certificates with it, so an OP that trusts only the root accepts a certificate issued by an
  intermediate CA. Each request goes over a connection per certificate, to the provider's `mtls_endpoint_aliases` entry
  when it has one, and sends `client_id`. `IClientAuthenticator.UsesMutualTls` declares the transport,
  `OpenIdProviderMetadata.MtlsEndpointAliases` carries the aliases, and
  `OpenIdClientDefaults.MutualTlsHttpClientNamePrefix` names the per-certificate HTTP clients
  ([ADR-1288](docs/decisions/1288-the-protocol-client-authenticates-with-a-tls-client-certificate-from-its-key-ring.md),
  [ADR-1300](docs/decisions/1300-an-asymmetric-key-carries-its-issuers-certificates-and-a-tls-client-presents-them.md),
  issues [#392](https://github.com/NCodeGroup/identity/issues/392),
  [#407](https://github.com/NCodeGroup/identity/issues/407)).

- **The OpenID protocol client binds tokens with DPoP.** `ClientRegistration.DPoPKeyRing` names the proof key ring, and
  `DPoPBoundAccessTokens` makes DPoP required. Token requests carry proofs and require a `DPoP` token type, and
  authorization requests send `dpop_jkt`. A `use_dpop_nonce` challenge is retried once, with nonces cached per origin.
  `TokenRequest`, `TokenResult` and `AuthorizationRequestResult` carry `DPoPJwkThumbprint`, `IDPoPProofFactory` creates
  resource proofs, and `OpenIdProviderMetadata.DPoPSigningAlgValuesSupported` carries the provider's
  `dpop_signing_alg_values_supported`
  ([ADR-1286](docs/decisions/1286-the-protocol-client-binds-tokens-with-dpop-whenever-the-provider-supports-it.md),
  issue [#123](https://github.com/NCodeGroup/identity/issues/123)).

- **The OpenID protocol client and a BFF keep their keys in a key management service.** A client key ring can hold an
  `IRemoteSecretKey`, whose remote key provider's row decides whether it signs `private_key_jwt` client assertions and
  DPoP proofs or decrypts the ID tokens, UserInfo responses and logout tokens encrypted to the client, through its KMS;
  its JWK, DPoP `jwk` and thumbprint are its public half. `IRemoteKeyResolver` resolves a provider's row for a key's
  algorithm, secret type, size and curve without calling the KMS, reads a key's public half from the KMS and creates the
  key with the row's `use` and `alg`; it is the one path by which the secret generator creates and binds a tenant's
  remote key, the serializer reads a stored one and a BFF loads a key ring's remote keys, and it refuses with
  `RemoteKeyRefusedException`. `IRemoteKeyProver` proves that a remote key holds the private half of its public half, a
  signing key by a challenge it signs and an encryption key by a challenge it decrypts, for the management API's bind
  and a BFF's key rings. The remote key providers register on the secrets library's builder, and a provider registers
  without a key-creation location, so the management console's reference host registers all three and needs no
  `SecretProtection:RemoteKeyProviders` setting
  ([#651](https://github.com/NCodeGroup/identity/issues/651),
  [#721](https://github.com/NCodeGroup/identity/issues/721),
  [ADR-1479](docs/decisions/1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md),
  [ADR-1489](docs/decisions/1489-a-remote-key-provider-registers-without-a-key-creation-location-and-only-one-that-has-one-creates-and-sweeps-keys.md)).

- **The OpenID protocol client pushes authorization requests.**
  `IAuthorizationRequestClient.CreateAuthorizationUriAsync` takes a `ClientRegistration` and an `AuthorizationRequest`
  (redirect URI, `state`, PKCE verifier, optional `nonce`, `scope`, `response_mode`, resources and open additional
  parameters) and returns an `AuthorizationRequestResult`. It always uses the code flow with PKCE `S256`, pushes the
  request (RFC 9126) whenever the provider advertises a PAR endpoint, and refuses the front channel when the provider or
  `ClientRegistration.RequirePushedAuthorizationRequests` requires PAR. A PAR error is returned as data. An
  authorization URI percent-encodes every parameter character outside the RFC 3986 unreserved set (`! * ( ) @` included)
  ([ADR-1275](docs/decisions/1275-the-protocol-client-pushes-authorization-requests-whenever-the-provider-offers-par.md),
  [ADR-1285](docs/decisions/1285-the-openid-protocol-client-needs-no-aspnet-core-runtime.md), issues
  [#123](https://github.com/NCodeGroup/identity/issues/123), [#321](https://github.com/NCodeGroup/identity/issues/321)).

- **The OpenID protocol client validates ID tokens.** `IIdTokenValidator.ValidateAsync` takes a `ClientRegistration` and
  an `IdTokenValidationRequest` and returns an `IdTokenValidationResult` with `IsValid`, the `Payload`, a `FailureCode`
  and a `FailureReason`. The signature algorithm policy is asymmetric-only, never `none` or HMAC, and is limited by
  `ClientRegistration.IdTokenSigningAlgorithms` and the provider's `id_token_signing_alg_values_supported`. An unknown
  `kid` triggers one rate-limited JWKS refresh. The `at_hash`/`c_hash`/`s_hash` claims are checked in constant time
  through `SignatureAlgorithm.GetTokenHashFunction` by the shared `TokenHash` helper in
  `NCode.Identity.Jose.Algorithms`, which the server's ID-token payload handler also uses. The ID-token, logout-token
  and UserInfo validations share one validation core
  ([ADR-1273](docs/decisions/1273-the-protocol-client-validates-id-tokens-under-an-asymmetric-algorithm-policy.md),
  issue [#335](https://github.com/NCodeGroup/identity/issues/335)).

- **The OpenID protocol client decrypts encrypted ID tokens and logout tokens.**
  `ClientRegistration.IdTokenKeyManagementAlgorithms` and `IdTokenContentEncryptionAlgorithms` mirror the registered
  `id_token_encrypted_response_alg` and `id_token_encrypted_response_enc` (`enc` defaults to `A128CBC-HS256`). When they
  are set, `IIdTokenValidator` and `ILogoutTokenValidator` decrypt with the asymmetric keys of the client's `KeyRing`,
  accept only RSA-OAEP and ECDH-ES and no compression, require every token to be encrypted, and check a replicated `iss`
  header. An undecryptable token fails with `TokenValidationFailureCodes.DecryptionFailed` (`decryption_failed`), and a
  client that registers no encryption refuses an encrypted token with `algorithm_not_permitted`
  ([ADR-1280](docs/decisions/1280-the-protocol-client-decrypts-id-and-logout-tokens-with-its-key-ring.md), issue
  [#347](https://github.com/NCodeGroup/identity/issues/347)).

- **The OpenID protocol client validates back-channel logout tokens.** `ILogoutTokenValidator.ValidateAsync` takes a
  `ClientRegistration` and a `LogoutTokenValidationRequest` and returns a `LogoutTokenValidationResult` with `IsValid`,
  the `TokenId`, `ExpiresAt`, `Issuer`, `Subject`, `SessionId`, a `FailureCode` and a `FailureReason`. It applies the
  ID-token signature policy, then requires the `logout+jwt` `typ`, the back-channel logout `events` claim and a `sid` or
  `sub`, refuses a `nonce`, and bounds a token's lifetime to 10 minutes.
  `ClientRegistration.BackchannelLogoutSessionRequired` makes the `sid` mandatory. `ValidateAsync` refuses a token whose
  `jti` is recorded in `IExpiringKeySet`, and `TryRecordHandledAsync` records it once the client has ended the token's
  sessions, so a delivery the client failed to handle validates again when the OP retries it; that needs a SharedState
  adapter registered
  ([ADR-1274](docs/decisions/1274-the-protocol-client-validates-logout-tokens-and-rejects-replays.md),
  [ADR-1284](docs/decisions/1284-a-logout-token-is-recorded-only-after-the-client-handles-it.md), issues
  [#123](https://github.com/NCodeGroup/identity/issues/123), [#349](https://github.com/NCodeGroup/identity/issues/349)).

- **The protocol client's token validation failures carry a machine-readable code.** `IdTokenValidationResult` and
  `LogoutTokenValidationResult` carry a `FailureCode` from the open `TokenValidationFailureCodes`, which extends the JWT
  codes with `subject_mismatch`, `nonce_mismatch`, `max_age_exceeded`, `token_hash_mismatch` and `token_replayed`; their
  failure constructors take `(failureCode, failureReason)`, and `FailureReason` is text without the token.
  `NCode.Identity.OpenId.Client.Abstractions` references `NCode.Identity.Jwt.Abstractions`
  ([ADR-1278](docs/decisions/1278-token-validation-failures-carry-a-machine-readable-code.md), issue
  [#343](https://github.com/NCodeGroup/identity/issues/343)).

- **The protocol client reads UserInfo, plain or signed and encrypted.** `IUserInfoClient` requests the provider's
  UserInfo endpoint with an access token the way it was issued (bearer, DPoP with a nonce retry, or over mutual TLS to
  the endpoint's alias), refuses a response about another subject than the ID token's, and maps the claims through
  `IClaimsIdentityFactory`, the JWT stack's one claim mapping, which `DefaultJwtValidator` also uses. A JSON response
  whose `iss` names another issuer fails with `UserInfoFailureCodes.InvalidIssuer`.
  `ClientRegistration.UserInfoSigningAlgorithms`, `UserInfoKeyManagementAlgorithms` and
  `UserInfoContentEncryptionAlgorithms` record the client's `userinfo_signed_response_alg` and
  `userinfo_encrypted_response_alg`/`enc`. A client that registered signing asks for `application/jwt` and validates the
  response as it validates an ID token: the provider's keys under its registered algorithms and the provider's
  `userinfo_signing_alg_values_supported`, decryption of a Nested JWT with its key ring, `iss`, `aud`, `typ` and any
  lifetime claims, then the ID token's `sub`; it refuses JSON, and a client that registered no signing refuses a JWT.
  The BFF names what it registered in each frontend's `IdToken` and `UserInfo` sections (`SigningAlgorithms`, `KeyManagementAlgorithms`,
  `ContentEncryptionAlgorithms`), validated at startup
  ([ADR-1299](docs/decisions/1299-userinfo-is-read-as-json-under-the-familys-claim-mapping-and-merged-once-at-login.md),
  [ADR-1301](docs/decisions/1301-a-signed-userinfo-response-is-validated-like-an-id-token-under-the-clients-registered-algorithms.md),
  issues [#415](https://github.com/NCodeGroup/identity/issues/415),
  [#421](https://github.com/NCodeGroup/identity/issues/421)).

- **The protocol client accepts an `expires_in` written with a zero fraction.** A pushed authorization response or token
  response whose `expires_in` is a whole number written as `60.0` or `6e1` is read as 60 seconds, and a non-zero
  fraction such as `60.5` is rejected with `OpenIdProviderException`. `NCode.Identity.Json` provides
  `JsonElement.TryGetWholeInt32`, `TryGetPropertyWholeInt32` and `WholeInt32JsonConverter` for such values, and
  `NCode.Identity.OpenId.Client.Abstractions` references `NCode.Identity.Json`
  ([ADR-1277](docs/decisions/1277-a-whole-number-json-value-is-accepted-with-a-zero-fraction.md), issue
  [#352](https://github.com/NCodeGroup/identity/issues/352)).

- **The backend-for-frontend core (`NCode.Identity.Bff`).** The `NCode.Identity.Bff` and
  `NCode.Identity.Bff.Abstractions` packages make an ASP.NET Core app the confidential OAuth client of its browser
  frontend. `AddBffLibrary` (on the protocol client's builder) and `AddBffConfiguration` compose it; `MapBffEndpoints`
  maps each frontend's `login`, `callback`, `user`, `logout` and `backchannel-logout` endpoints under its `PathPrefix`
  (`/bff` by default); and
  `AsBffApi`/`[BffApi]` let local APIs accept the session cookie. The session lives behind the replaceable
  `IBffSessionStore`. `IBffTokenAccessor` and `AddBffAccessTokenHandler` attach the user's access token, refreshed
  single-flight across replicas under a lease on the refresh token, and DPoP-bound when the provider supports it;
  `BffAccessToken.TokenType` is `Bearer` when the provider's token response omits the `token_type` that RFC 6749 §5.1
  requires. The login requests `openid` and the scopes `Bff:Frontends:{name}:Scopes` lists, never `offline_access` on its own, so by
  default the grant stays inside the provider's login session and the provider's logout reaches the BFF.
  `IBffLoginGate` and `IBffSessionValidator` let an app refuse a login or end a session, including when a hub or Blazor circuit
  connection revalidates the session without an `HttpContext`, and back-channel logout ends
  sessions through `sid`/`sub` indexes. `AsBffPage`/`[BffPage]` mark a page that accepts GET, HEAD,
  OPTIONS and TRACE, and a form post with a valid antiforgery token, and `AsBffHub`/`[BffHub]` a SignalR hub whose connection holds the session
  (`IBffConnectionSession`) and obtains tokens through `IBffTokenContext`.
  A won refresh and a logout are never cancelled by the browser, so a rotated refresh token is kept and a session always
  ends. `AddIdentityBff` validates the refresh lease against the client's request timeout, and the protocol client's
  `IdTokenValidationResult` exposes the validated `Identity` ([#394](https://github.com/NCodeGroup/identity/issues/394),
  [#395](https://github.com/NCodeGroup/identity/issues/395),
  [ADR-1290](docs/decisions/1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
  [ADR-1291](docs/decisions/1291-the-bff-refreshes-a-session-single-flight-under-a-lease-on-its-refresh-token.md),
  [ADR-1293](docs/decisions/1293-the-bff-ends-sessions-by-guarded-logout-and-by-back-channel-logout-through-indexes.md),
  [ADR-1294](docs/decisions/1294-a-bff-client-is-registered-with-explicit-metadata-not-a-bff-security-profile.md),
  [ADR-1431](docs/decisions/1431-the-bff-requests-offline-access-only-when-its-deployment-lists-it.md)).

- **A BFF is composed in one call.** The `NCode.Identity.Bff.Server` package is the BFF's composition root:
  `AddIdentityBff(configuration)` composes the shared-state (in-process by default), events, secrets, JOSE, JWT,
  protocol-client and BFF libraries and binds the `Bff` section, and `IIdentityBffBuilder` exposes each library's
  builder, such as `SharedStateLibraryBuilder.AddValkey(configuration)` for several replicas, as `AddIdentityServer()`
  does for the OP. The host registers the key rings its configuration names
  ([#413](https://github.com/NCodeGroup/identity/issues/413),
  [ADR-1290](docs/decisions/1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)).

- **The BFF releases a refresh lease only by the owner token its winner holds.**
  `IExpiringValueSet.TryRemoveAsync(key, expectedValue, cancellationToken)` is an atomic compare-and-remove that deletes
  a value only while it still holds the expected bytes; the in-process and Valkey adapters implement it, and the Valkey
  script is recorded as the `value_set.try_remove` operation. `IBffSessionStore.TryAcquireRefreshLeaseAsync` returns a
  `BffRefreshLease` naming a fresh random owner token, or `null` when the lease is held, and `ReleaseRefreshLeaseAsync`
  takes that lease and removes it only while its owner still holds it; a custom `IExpiringValueSet` adapter or
  `IBffSessionStore` implements these members. The winner of a refresh releases its own lease as soon as it finishes and
  never removes a lease another replica took after its own expired. When the session store cannot be reached as the
  winner releases its lease, the refresh whose tokens are written succeeds, the release failure is logged
  (`RefreshLeaseReleaseFailed`, event 19004), and the lease expires on its own; a failure of the refresh itself is
  reported as that failure ([#422](https://github.com/NCodeGroup/identity/issues/422),
  [#462](https://github.com/NCodeGroup/identity/issues/462),
  [ADR-1308](docs/decisions/1308-a-refresh-lease-is-released-only-by-the-owner-token-its-winner-holds.md)).

- **BFF session claim and `/bff/user` contributors.** `IBffSessionClaimContributor` strategies shape a session's claims
  once, at login, after the ID token validates and before the login gates, which judge the shaped claims;
  `BffSessionClaimContext` refuses to add or remove `iss`, `sub`, `sid`, `auth_time`, `acr` and `amr`.
  `IBffUserContributor` strategies add computed fields to `/bff/user` through `BffUserContext`, which refuses the
  reserved `sub`, `name`, `claims`, `session`, `logoutUrl` and `logoutSid`
  ([#414](https://github.com/NCodeGroup/identity/issues/414), [#442](https://github.com/NCodeGroup/identity/issues/442),
  [ADR-1290](docs/decisions/1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md)
  decision 11).

- **The BFF can merge UserInfo into a session at login.** A frontend's `UserInfo:Load` merges UserInfo into a session at login,
  before the claim contributors and the login gates; a failure ends the login with `userinfo_failed`
  ([#415](https://github.com/NCodeGroup/identity/issues/415),
  [ADR-1299](docs/decisions/1299-userinfo-is-read-as-json-under-the-familys-claim-mapping-and-merged-once-at-login.md)).

- **The BFF serves several token requirements and forwards remote APIs.** An endpoint or remote API names its token
  requirement from `BffTokenRequirementNames`: `user` (the user's token), `client` (the BFF's own `client_credentials`
  token, served only to a request with a session and cached in memory on each replica per resource and scope),
  `user_or_client` (the user's token when a session resolves, otherwise the BFF's own, never falling back from a failed
  user token), `exchange` (an RFC 8693 token exchange that presents the user's token, cached in the session per resource
  and scope and bound to its DPoP key) and `none` (a session but no token).
  `MapBffRemoteApi(path, destination, configure)` and `MapBffRemoteApis()` over each frontend's `RemoteApis`
  section, limited to that frontend (`BffRemoteApiOptions`: `Path`, `Destination`, `TokenRequirement`, `Resource`, `Scope`, `AllowedMethods`, `Timeout`)
  forward a BFF API's requests to a remote API through YARP's `IHttpForwarder` with the token attached, DPoP-bound when
  the token is, over the `IHttpClientFactory` client `BffDefaults.RemoteApiHttpClientName`, whose handlers a host
  configures; `NCode.Identity.Bff` depends on `Yarp.ReverseProxy`
  ([#395](https://github.com/NCodeGroup/identity/issues/395),
  [#716](https://github.com/NCodeGroup/identity/issues/716),
  [ADR-1381](docs/decisions/1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md),
  [ADR-1487](docs/decisions/1487-the-bff-forwards-a-remote-api-through-a-named-http-client-a-host-can-configure.md)).

- **YARP-configured routes can carry the BFF's token.** `AddReverseProxy()…AddBffTransforms()` makes every route whose
  metadata names `BffTokenRequirement` (with the optional `BffResource` and `BffScope`) a remote API, and
  `MapReverseProxy().AsBffRemoteApis()` marks those routes as BFF APIs, so they require the session and the anti-forgery
  header. Such a route loses the browser's cookies, credentials and anti-forgery header, gets the token attached,
  DPoP-bound when the token is, and drops the upstream `Set-Cookie`; every other route keeps its cookies but never
  forwards or accepts the BFF's own. Route metadata is validated when YARP loads it, and the proxy refuses to start when
  another `IForwarderHttpClientFactory` is registered after `AddBffTransforms()`. Routing beyond a path prefix goes
  through YARP's routes and clusters ([#395](https://github.com/NCodeGroup/identity/issues/395),
  [ADR-1381](docs/decisions/1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md)
  decision 8).

- **The BFF can publish its client keys.** A frontend's `Jwks:Enabled` maps an anonymous, never-cached
  `GET {PathPrefix}/jwks` for that frontend that publishes the public keys of its `ClientAuthentication:KeyRing`, for an
  OP that reads a `private_key_jwt` client's keys from its `jwks_uri`; it is off by default and validated when the host
  starts ([#396](https://github.com/NCodeGroup/identity/issues/396),
  [ADR-1382](docs/decisions/1382-the-bff-sends-browser-security-headers-and-publishes-its-client-keys-only-when-the-host-opts-in.md)).

- **One BFF serves several browser frontends.** Each `Bff:Frontends:<name>` entry (`BffFrontendOptions`) is a frontend
  with its own client registration, `PublicOrigin`, `PathPrefix`, return URLs, claims, session, DPoP, security headers,
  JWKS and remote APIs; `Bff:Antiforgery` and `Bff:Tokens` are shared. A registered `IBffFrontendSelector` rule (`host`,
  `path`, `BffFrontendSelectorNames`) selects a request's frontend, one frontend may serve every request no other
  selects, and the set is validated at startup and whenever its configuration changes. `MapBffEndpoints()` maps each
  frontend's endpoints under its `PathPrefix`, and `RequireBffFrontend()`/`[BffFrontend]` and the YARP `BffFrontend`
  metadata limit an endpoint to frontends ([#396](https://github.com/NCodeGroup/identity/issues/396),
  [ADR-1432](docs/decisions/1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md)).

- **BFF frontends follow their configuration without a restart.** When the `Bff` section reloads,
  `BffFrontendCatalog` validates the whole candidate set (the options validators, the selection rules, each frontend's
  client-authentication method, and the checks that `MapBffRemoteApi()` and `MapBffRemoteApis()` add) and only then
  replaces the frontends, `Bff:Antiforgery` and `Bff:Tokens` at once; an invalid change keeps the last valid set and is
  logged (events 19011 and 19012). The endpoints of `MapBffEndpoints()` and `MapBffRemoteApis()` follow the set with the
  conventions added to their route group, the endpoint check runs again, and each frontend's security headers and
  client-credential tokens belong to the frontend as built for the set
  ([#602](https://github.com/NCodeGroup/identity/issues/602),
  [ADR-1442](docs/decisions/1442-the-bffs-frontends-follow-their-configuration-and-a-change-takes-effect-only-when-the-whole-set-is-valid.md)).

- **Blazor hosts use the BFF session.** The opt-in `NCode.Identity.Bff.Blazor` package (`AddBffBlazorLibrary`,
  `AsBffComponents`, a revalidating `AuthenticationStateProvider`, circuit token access, the user document persisted
  for WebAssembly) serves Blazor's interactive server render mode, and the `NCode.Identity.Bff.Blazor.Client` package
  (`AddBffBlazorClient`, `AddBffAntiforgeryHeader`) serves WebAssembly through `/bff/user` and the anti-forgery
  header. The Blazor server package depends only on the BFF's abstractions, and the dependency-free
  `NCode.Identity.Bff.Protocol.Abstractions` package holds the names the server and the WebAssembly client share:
  `BffDefaults`, `BffClaimTypes` (the `bff_logout_url` and `bff_logout_sid` claims that render the logout form in every
  render mode), `BffEndpointPaths` and `BffUserDocumentNames`. `IBffUserDocumentFactory` builds the `/bff/user`
  document, and a frontend's `Session:RevalidationInterval` sets how often `IBffConnectionRevalidator` revalidates the
  session a hub connection or a circuit holds, idle or not. `IBffConnectionSession.SessionEnded` is cancelled once any
  use of a connection finds its session ended, which wakes every watcher of the connection at once, and an invocation
  of a `[BffSelfRevalidatingHub]` hub, such as Blazor's, that finds the session ended fails without closing the
  connection, so the circuit signs out instead of reconnecting. The Blazor packages build against the Microsoft package
  band 10.0.14
  ([#524](https://github.com/NCodeGroup/identity/issues/524),
  [#604](https://github.com/NCodeGroup/identity/issues/604),
  [#605](https://github.com/NCodeGroup/identity/issues/605),
  [#613](https://github.com/NCodeGroup/identity/issues/613),
  [ADR-1435](docs/decisions/1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md),
  [ADR-1440](docs/decisions/1440-the-names-a-bff-and-its-browser-clients-share-live-in-a-dependency-free-package.md),
  [ADR-1441](docs/decisions/1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md),
  [ADR-1445](docs/decisions/1445-a-self-revalidating-hub-keeps-its-connection-when-the-session-ends-and-every-watcher-learns-the-end-at-once.md)).

- **The management console is served by a BFF host of its own.** `NCode.Identity.ManagementConsole`
  (`AddManagementConsole(configuration)`, `MapManagementConsole()`) and `NCode.Identity.ManagementConsole.Host` serve
  the built `apps/mgmt` bundle under a per-response CSP nonce, `/config/bootstrap.json`, the `mgmt` frontend's `/bff`
  endpoints, and `/management/api/*` forwarded to `ManagementConsole:ManagementApiUrl` with the user's DPoP-bound
  `urn:ncode:management` token. The console is the confidential client `mgmt-console` of the control-plane tenant
  (`private_key_jwt`, PAR, DPoP, back-channel logout) and listens on `https://localhost:5005` under the AppHost, which
  generates the console's keys per machine. The host refuses a missing or wildcard `AllowedHosts` in Production,
  trusts forwarded headers only from the proxies its `ForwardedHeaders` section names, and sends
  `Strict-Transport-Security` outside Development. The console's `ManagementConsole:Branding:FaviconUrl` (absolute
  `https`) replaces the family's tab icon the same way
  ([#158](https://github.com/NCodeGroup/identity/issues/158),
  [#611](https://github.com/NCodeGroup/identity/issues/611),
  [#745](https://github.com/NCodeGroup/identity/issues/745),
  [ADR-1437](docs/decisions/1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md),
  [ADR-1447](docs/decisions/1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md),
  [ADR-1494](docs/decisions/1494-a-tenant-sets-its-sign-in-tabs-icon-as-a-branding-setting-the-hosts-link-in-place-of-the-familys.md)).

- **A BFF loads its key rings from PEM files and key management services when the host starts.**
  `Bff:KeyRings:{name}:PemFiles` lists EC or RSA private-key PEM files, each key's `kid` its file name without the
  extension, and `Bff:KeyRings:{name}:RemoteKeys:{kid}` names a key a KMS holds by its `Provider`, `KeyId`, `Algorithm`,
  `SecretType`, `KeySizeBits` and `Curve`, the provider's row deciding whether it signs or decrypts. Every configured
  key ring loads before the host serves: a remote key's public half is read from its KMS and the key proves it holds the
  private half, and `/bff/jwks` publishes the half without a KMS call. The host validates the entries when it starts,
  and refuses a key ring that holds no key or one id twice. A set of frontends whose `ClientAuthentication:KeyRing` or
  `DPoP:KeyRing` names a key ring that `IClientKeyRingProvider.HasKeyRing` does not find is refused, when the host
  starts and on every configuration change
  ([#614](https://github.com/NCodeGroup/identity/issues/614),
  [#651](https://github.com/NCodeGroup/identity/issues/651),
  [ADR-1439](docs/decisions/1439-a-bff-loads-the-key-rings-its-configuration-names-from-pem-files.md),
  [ADR-1446](docs/decisions/1446-a-set-of-frontends-that-names-an-unregistered-key-ring-is-refused.md),
  [ADR-1479](docs/decisions/1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)).

- **The management console reads its session from its BFF.** The `RuntimeConfig` union and `loadBffRuntimeConfig`, the
  `session` slice and `fetchBffUser`, `SessionGate` (poll, expiry and visibility probes, `BroadcastChannel`), sign-out
  by the logout form, `defineBffServedSurfaceConfig` and `readCspNonce` in `@ncode/ui` give the console its session
  from `/bff/user`
  ([ADR-1438](docs/decisions/1438-the-management-console-holds-no-token-and-reads-its-session-from-its-bff.md)).

- **The BFF is proven in real browsers and against a second OpenID Provider.** The `End-to-end` check runs
  `./build/e2e.ps1`, which drives the BFF with Playwright in Chromium, Firefox and WebKit against Keycloak in a
  container. The session cookie is host-only, `HttpOnly`, `Secure` and `SameSite=Strict` (or `Lax` when chosen). A
  cross-site navigation carries it only under `Lax`, a cross-origin `fetch` stops at its CORS preflight, and Fetch
  Metadata refuses a cross-site or untrusted same-site request even when CORS admits it. Login with PAR, PKCE,
  `private_key_jwt` through `/bff/jwks`, DPoP, RFC 9207 and PS256 ID tokens works, and so do refresh-token
  rotation, RP-initiated logout and back-channel logout, all from Keycloak's discovery metadata alone. A Blazor Web App
  host (interactive server and WebAssembly) shows that a circuit signs out after back-channel logout, an idle hub
  connection closes once its session ends, a `WebSocket` from another origin is refused and a WebAssembly component's
  traffic carries no token. `./build/dod.ps1` never runs these `*.EndToEnd.Tests` suites
  ([#418](https://github.com/NCodeGroup/identity/issues/418), [#419](https://github.com/NCodeGroup/identity/issues/419),
  [#603](https://github.com/NCodeGroup/identity/issues/603),
  [ADR-1430](docs/decisions/1430-end-to-end-suites-run-the-bff-in-real-browsers-against-a-containerized-keycloak-in-their-own-gate.md),
  [ADR-1441](docs/decisions/1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md)).

- **Entity Framework persistence is a reusable framework plus domain slices.** The framework package
  `NCode.Identity.OpenId.Persistence.EntityFramework` provides a `DbSet`-less `OpenIdDbContext` that slices plug into
  through `IOpenIdModelContributor`; the core OpenID schema is the
  `NCode.Identity.OpenId.Persistence.EntityFramework.Core` slice and the local-account schema the
  `NCode.Identity.OpenId.Persistence.EntityFramework.Accounts` slice, each slice's root namespace matching its package
  id. The framework's store bases (`BaseStore`, `BaseStoreWithResourceId`), the `AddStore` registration helper and the
  `UseIdGenerator` attribute are public so a slice reuses them. Hosts register the framework plus each slice
  (`AddEntityFrameworkCorePersistence<TDbContext>()`), and a slice may expose typed `DbSet` accessors for its own
  entities as extension members. `EntityStoreManager` has a `DbContext`-less store-factory fallback, so a store that is
  not built on Entity Framework resolves without referencing `OpenIdDbContext`
  ([ADR-1052](docs/decisions/1052-entity-framework-persistence-is-a-reusable-framework-that-domain-slices-plug-into.md),
  [ADR-1060](docs/decisions/1060-local-account-one-store-seam-and-dependency-inverted-authentication.md)).

- **Store queries take extensible parameter records.** Every store's `GetPageAsync` pages with a shared `PageRequest {
  Cursor, Limit }` init-only record struct (in `NCode.Identity.Persistence.Stores`, beside `PagedResult<T>`) across
  `IGrantStore`, `IClientStore`, `IClientGrantStore`, `IResourceServerStore`, `IPlatformStore`, `ITeamStore`, `ITenantStore`,
  `ILocalAccountStore` and the generic `BaseStore`, and the grant store's `GetPageAsync` and `RevokeWhereAsync` select
  grants with a `GrantFilter` init-only record struct (`SubjectId`, `ClientId`, `SessionId`, `GrantType`), so pagination
  (for example a sort direction) and selection criteria grow without a signature change (issue
  [#26](https://github.com/NCodeGroup/identity/issues/26),
  [ADR-1080](docs/decisions/1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md)).

- **`ITenantStore.GetSettingsPageAsync` and `IClientStore.GetSettingsPageAsync` page through settings documents.** Each
  reads a page of settings documents without their secrets and without change tracking, for a pass over every tenant or
  client (issue [#212](https://github.com/NCodeGroup/identity/issues/212)).

- **A tenant's issuer is optional and, when present, unique.** Its unique index is filtered (`WHERE "NormalizedIssuer"
  IS NOT NULL`), so on relational providers any number of tenants may omit it.

- **A lost optimistic-concurrency race surfaces as the store-neutral `StoreConcurrencyException`.** The store manager
  throws it from `IStoreManager.SaveChangesAsync` when another unit of work changed an entity after it was read, and
  every store's concurrency-token check throws it when the token its caller presents differs from the stored one. A
  caller catches one type around any store method or `SaveChangesAsync` without a reference to Entity Framework, and a
  store that is not built on Entity Framework reports a lost race the same way; a caller that saves through a
  `DbContext` directly receives Entity Framework's `DbUpdateConcurrencyException`
  ([ADR-1012](docs/decisions/1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md), issue
  [#257](https://github.com/NCodeGroup/identity/issues/257)).

- **The persistence model stores resource-server settings and secrets.** `PersistedResourceServerSettings`,
  `PersistedResourceServerSecrets`, the `BasePersistedResourceServerResource` base, `ISupportResourceServerId` and the
  `resource_server` resource type; `IResourceServerStore.UpdateSettingsAsync` and secret CRUD; and in EF Core the
  `SettingsConcurrencyToken` and `SecretsConcurrencyToken` columns and a tenant-scoped `ResourceServerSecrets` join
  table. Deleting a resource server cascades to its scopes and secrets
  ([ADR-1066](docs/decisions/1066-resource-server-settings-and-secret-owner.md)).

- **Resource-server runtime reads push their filtering into the database through intent-named `IResourceServerStore`
  queries.** `GetEnabledScopesAsync`, `GetEnabledIdentifiersAsync`, `GetEnabledScopeProjectionsByScopesAsync`,
  `GetEnabledScopeProjectionsBySystemOrIdsAsync` and `FindEnabledByScopesAsync`, with the lightweight
  `ResourceServerScopeProjection`, serve scope discovery, the client scope service, scope descriptions and the
  resource-server credential resolver without paging the tenant's whole resource-server set or loading settings and
  secrets for identifier and scope reads. Every runtime reader, `IScopeDescriptionService.DescribeScopesAsync` included,
  ignores disabled resource servers. Scope and identifier matching is case-sensitive (RFC 6749, RFC 8707): the database
  filter is a widening pre-filter on the normalized column, and the exact match is applied in memory. A standalone
  non-unique index on `ScopeEntity.NormalizedValue` backs the owns-any-scope lookup, so the scope-driven join seeks on
  scope value rather than on the owner-leading composite unique `(ResourceServerId, NormalizedValue)` index;
  `ResourceServerEntity.IsDisabled` is deliberately not indexed, because a low-selectivity boolean index would not be
  chosen by the planner and would only add write cost (issue [#23](https://github.com/NCodeGroup/identity/issues/23),
  [ADR-1080](docs/decisions/1080-filtered-reads-are-intent-named-store-methods-and-growing-criteria-are-init-only-dtos.md)).

- **`IPersistedGrantService.AddAsync` returns the server-generated grant id.** It returns `ValueTask<string>`, the
  opaque public `GrantId` of the persisted grant, so a caller such as the initial-access-token management endpoint can
  reference it for revocation ([ADR-1063](docs/decisions/1063-dynamic-client-registration.md)).

- **At-rest grant payloads are schema-versioned and upcastable.** Every grant payload
  (`PersistedGrant<TPayload>.Payload`: authorization codes, refresh tokens, device and CIBA requests, pushed
  authorization requests, DCR tokens, the end-user `session` grant, continue envelopes) is written through the
  `IPersistedPayloadSerializer` seam, which wraps it in a self-describing envelope `{ "$ver": N, "$payload": … }`
  stamped with the schema version declared by `[PersistedPayloadVersion(N)]` (default `1`). On read, registered
  `IPersistedPayloadUpcaster` steps bring an older stored document up to the current version before deserialization; a
  document newer than the current version is read forward-compatibly, and a document that is not an envelope, or that
  lacks a member the serving code always writes, is rejected with a `JsonException`. The long-lived, in-place-mutated
  payloads (`SessionGrant`, `CibaGrant`, `DeviceCodeGrant`) carry a `[JsonExtensionData]` bag, so a mixed-version node
  never drops unknown members on a read-modify-write
  ([ADR-1113](docs/decisions/1113-at-rest-grant-payloads-are-schema-versioned-and-upcastable.md),
  [ADR-1395](docs/decisions/1395-a-stored-record-that-lacks-what-the-serving-code-writes-is-malformed-and-fails-closed.md)).

- **A change to a grant's payload loses no concurrent write.** The touch of a session, the addition of a client to it,
  and the polls and decisions of the device-code and CIBA grants apply their change to the grant as stored and read it
  again when another write lands first, so a client that a concurrent request added to a session stays, a poll leaves an
  approval that landed before its write, and only the CIBA approval that decided the request delivers.
  `IPersistedGrantService.UpdatePayloadAsync` takes the function that computes the change, as a
  `PersistedGrantUpdate<TPayload>`, and `ISessionService.AddClientAsync` reports whether this call added the client
  ([ADR-1222](docs/decisions/1222-a-grants-payload-is-changed-by-a-read-modify-write.md), issue
  [#282](https://github.com/NCodeGroup/identity/issues/282)).

- **Grants are removed after a retention period.** `GrantPruneService` runs in every replica every
  `GrantRetentionOptions.PruneInterval` (one hour) and removes, in bounded batches, the grants that expired or were
  revoked more than `GrantRetentionOptions.Retention` ago (14 days by default), so the grants table holds live grants
  and those that ended within the retention. `IGrantStore.RemoveEndedBeforeAsync` stages one batch for removal, and
  `Grants.RevokedWhen` is indexed ([ADR-1218](docs/decisions/1218-ended-grants-are-removed-after-a-retention-period.md),
  issue [#267](https://github.com/NCodeGroup/identity/issues/267)).

- **The usage ledger adds each numbered batch of a replica's usage once.** `ITenantUsageStore.AddAsync(replicaId,
  batchNumber, usage, cancellationToken)` takes the identifier the replica drew when it started and the number of the
  batch, from one, in the order the replica adds its batches. It adds the counts and records the number as the replica's
  last added batch in the same unit of work, and a batch numbered at or below the last added one changes nothing, so a
  batch that a replica retries is added once. An empty `usage` and a `batchNumber` below one throw
  `ArgumentOutOfRangeException`. A store implementer skips such a batch and records the number with the counts. The
  Entity Framework store keeps the checkpoint in `TenantUsageCheckpointEntity`, in the `TenantUsageCheckpoints` table
  ([ADR-1209](docs/decisions/1209-a-replica-adds-each-usage-batch-to-the-ledger-once.md), issue
  [#216](https://github.com/NCodeGroup/identity/issues/216)).

- **The usage ledger is pruned in bounded batches on every provider.** `ITenantUsageStore.RemoveBeforeAsync(day, limit,
  cancellationToken)` stages for removal a batch of at most `limit` usage entries from before `day`, together with the
  checkpoints of the replicas whose latest usage day is before it, and returns the count of usage entries it staged;
  they are removed when the unit of work is saved. The removal goes through the change tracker, which every Entity
  Framework provider supports, EF InMemory included, and the prune service removes batches of 256 entries, each its own
  unit of work, while a batch is full ([ADR-1213](docs/decisions/1213-the-usage-ledger-is-pruned-in-bounded-batches.md),
  issue [#264](https://github.com/NCodeGroup/identity/issues/264)).

- **Audit records are kept in an indexed store.** Every audit event's record is queued in memory and written in batches
  to the `AuditRecords` table, with its resources in `AuditRecordResources`, through `IAuditRecordStore`
  (`PersistedAuditRecord`, `PersistedAuditResource`). The table indexes the envelope an operator filters on (event id
  and time, name, category, outcome, severity, tenant, subject, client, actor, trace and source IP) and keeps the whole
  CloudTrail-shaped JSON, which `PersistedAuditRecord.Record` holds as a `JsonElement`. `OpenId:AuditRecords`
  (`AuditRecordOptions`) sets `Enabled`, `FlushInterval` (five seconds), `QueueCapacity` (10,000), `Retention` (90 days)
  and `PruneInterval` (one hour, validated as a timer period when the host starts), and every `OpenId:AuditRecords`
  validation failure names its option and key. A full queue drops and counts records, which the log still carries, and a
  failed batch is retried without duplicating records. `AuditRecordPruneService` removes the records past `Retention`
  every `PruneInterval`, and a batch another replica removed first counts as progress
  ([ADR-1303](docs/decisions/1303-audit-records-are-kept-in-an-indexed-store-that-each-replica-writes-in-batches.md),
  issues [#383](https://github.com/NCodeGroup/identity/issues/383) and
  [#468](https://github.com/NCodeGroup/identity/issues/468)).

- **The Definition of Done fails a migration that would break the release still serving.**
  `ExpandOnlyRule.Check(Migration)` judges each host migration, and `ExpandOnlyRuleTests`, in
  `NCode.Identity.OpenId.Host.Database.Tests`, runs it over the `Up` operations of every one. The rule fails a migration
  that drops or renames a schema, table, column or sequence; adds a required column without a default; makes a column
  required, even with a default; removes a default; changes a column's type; shortens it or narrows its precision or
  scale; adds a unique index, unique constraint, primary key, check constraint or foreign key to an existing table; or
  adds a foreign key that restricts deleting rows of an existing table. It fails a migration that runs raw SQL or
  changes seed data unless the migration carries `[ExpandMigration("why")]`, when the release still serving reads every
  row it writes, or `[ContractMigration("why")]`, when it does not; a deliberate contract step carries
  `[ContractMigration("why")]`, and the rule fails that marker on a migration that only expands
  ([ADR-1184](docs/decisions/1184-a-migration-only-expands-the-schema-unless-it-is-marked-a-contract-step.md),
  [ADR-1250](docs/decisions/1250-a-migration-classifies-its-data-change-and-a-constraint-added-to-an-existing-table-contracts.md),
  issue [#205](https://github.com/NCodeGroup/identity/issues/205)). Until the first stable release,
  `InitialCreateTests` fails a host context that holds any migration but its `InitialCreate`, and a migration that
  changes data, so the `InitialCreate` migrations pass the rule unmarked
  ([ADR-1394](docs/decisions/1394-until-the-first-stable-release-each-host-database-context-has-one-initial-migration.md)).

- **`NCode.Identity.Dispatch(.Abstractions)` runs deferred, at-least-once background work behind a
  bring-your-own-orchestration port.** A job is a serializable `IDispatchJob`, handled by an `IDispatchJobHandler<TJob>`
  that throws to retry, and dispatched through `IJobDispatcher.DispatchAsync<TJob>(job, DispatchOptions?, …)`.
  `AddDispatchServices().AddInProcessJobDispatcher()` registers the in-process, self-scheduling adapter: a bounded
  in-memory queue drained by hosted workers that retry with exponential back-off. A deployment registers its own
  `IJobDispatcher` for a durable or distributed backend (a transactional outbox, a broker such as RabbitMQ, a scheduler
  such as Quartz) without changing producers or handlers; the durable-audit outbox
  ([ADR-1048](docs/decisions/1048-durable-audit-delivery-outbox.md)) is one adapter of this port
  ([#24](https://github.com/NCodeGroup/identity/issues/24),
  [ADR-1090](docs/decisions/1090-durable-outbound-delivery-dispatch-adapter.md)).

- **The in-process dispatcher runs jobs concurrently and is configured from `OpenId:InProcessJobDispatcher`.** Up to
  `MaxConcurrentJobs` jobs (16 by default) run at once, so a delivery that waits on an unreachable endpoint holds one
  worker while the jobs behind it run on the others. `AddConfiguration` binds `InProcessJobDispatcherOptions`
  (`QueueCapacity`, `FullMode`, `MaxConcurrentJobs`) strictly from the OpenID section's `InProcessJobDispatcher`
  subsection, which `InProcessJobDispatcherOptions.SectionName` names, and the host refuses to start on an unknown key,
  a `QueueCapacity` or `MaxConcurrentJobs` below 1, or a `FullMode` that names no member of `BoundedChannelFullMode`;
  each failure names its key
  ([ADR-1232](docs/decisions/1232-the-in-process-dispatcher-runs-jobs-concurrently-up-to-a-limit.md),
  [ADR-1242](docs/decisions/1242-the-in-process-dispatcher-is-configured-under-the-openid-section.md), issues
  [#276](https://github.com/NCodeGroup/identity/issues/276) and
  [#294](https://github.com/NCodeGroup/identity/issues/294)).

- **A dispatched job whose handler times out is retried, and the host keeps running.** A handler's
  `OperationCanceledException` that does not come from the host stopping, such as the `TaskCanceledException` of an
  `HttpClient` timeout during a back-channel logout or CIBA delivery, fails the attempt and retries the job with
  back-off without ending the dispatcher's worker or stopping the host. A job whose handlers fail is retried once per
  attempt, however many of them failed, and a handler that cannot be resolved fails the attempt
  ([ADR-1232](docs/decisions/1232-the-in-process-dispatcher-runs-jobs-concurrently-up-to-a-limit.md), issue
  [#276](https://github.com/NCodeGroup/identity/issues/276)).

- **The event bus is a marker-bearing library
  ([ADR-1069](docs/decisions/1069-di-registration-proves-every-prerequisite-at-compile-time-through-marker-builders.md)).** `AddEventServices()` mints
  the `EventsLibrary` marker (`NCode.Identity.Events.Abstractions`) and returns `IServiceBuilder<EventsLibrary>`; its
  opt-ins `AddEventHandler<TEvent, THandler>()` and `AddBackgroundEventDelivery()` (in `EventLibraryRegistration`)
  receive that builder, so an event opt-in cannot be added without the event services.

- **Published events are counted on `ncode.identity.events.published`.** The counter is recorded through a meter created
  from `IMeterFactory` and named `EventMetrics.MeterName` (`NCode.Identity.Events`), with the attributes
  `ncode.identity.event.type`, `ncode.identity.audit.event_name`, `ncode.identity.audit.event_category` and
  `ncode.identity.audit.outcome`; `DefaultEventMetricsHandler` owns no disposable state. An event adds bounded
  attributes of its own by implementing `ISupportMetricAttributes`: `TokenIssuedAuditEvent` adds
  `ncode.identity.token.type` and `ClientAuthenticationFailedAuditEvent` adds
  `ncode.identity.client_authentication.method`.

- **Audit events are CloudTrail-shaped JSON records.** `AuditRecordWriter` writes each audit event as one compact,
  snake_case record whose field names are the constants of `AuditRecordFields`: `schema_version` (`"1.0"`), `event_id`
  (a UUIDv7), `event_time` (UTC), `event_name`, `event_category`, `event_source`, `outcome`, `severity`, `tenant_id`,
  `tenant_environment_type`, `subject_id`, `client_id`, the `actor`, `request` and `error` objects, a `resources` array
  of `{type, id}` and an event-specific `details` object that the event writes through `ISupportAuditDetails`. Null
  members and an empty `resources` are omitted. `AuditActor`, `AuditRequest`, `AuditResource` and `AuditError` model the
  objects, and `AuditActorTypes`, `AuditSeverities`, `AuditEventCategories`, `AuditEventSources`, `AuditResourceTypes`,
  `OpenIdAuditEventNames` and `ManagementAuditEventNames` hold the open vocabularies. An `AuditEvent` declares an
  abstract `EventName` and `EventCategory`, a required `EventSource` and its `EventTime`, and `OpenIdErrorEvent` carries
  its `Error`. `AuditRequestFactory` builds `request` from the HTTP context: the request, trace and span ids, the source
  IP, the User-Agent, the method, the path (never the query string) and the endpoint.
  `IAuditEventRecorder.RecordAuthorizationDeniedAsync` takes an `errorCode`, and `RecordClientAuthenticationFailedAsync`
  takes the attempted `authenticationMethod` and an `errorCode`, so `ClientAuthenticationFailedAuditEvent` requires its
  `AuthenticationMethod`. `DefaultAuditLoggingHandler` logs the record under the category `NCode.Identity.Audit` with
  EventId 14005 and a state of `audit.*` key/value pairs, and the audit span event carries the same `audit.*` tags. A
  refresh-token replay logs at Critical
  ([ADR-1283](docs/decisions/1283-audit-records-are-cloudtrail-shaped-json-with-an-indexable-envelope.md), [audit
  records](docs/audit-records.md)).

- **UserInfo access is audited on opt-in, as a trail of the personal data each client reads.** With
  `audit_userinfo_access` on (off by default; it applies to tenants and clients, merges with `Or` so a client cannot
  turn off its tenant's choice, and is not discoverable), each UserInfo response is audited as a `userinfo.accessed`
  event (`UserInfoAccessedAuditEvent`, category `data_access`) once the response is built: `success`, with the subject,
  the client as the actor and the names of the claims disclosed in `details.claim_names`, never a claim value or the
  access token. An access token that authenticates but names no client the tenant serves is audited, when the tenant
  turns the setting on, as `denied` with the error code `invalid_token` and no claims.
  `IAuditEventRecorder.RecordUserInfoAccessedAsync` and `RecordUserInfoAccessDeniedAsync` publish the two outcomes
  ([ADR-1401](docs/decisions/1401-userinfo-access-is-audited-when-a-tenant-or-client-opts-in.md),
  [#21](https://github.com/NCodeGroup/identity/issues/21), [audit records](docs/audit-records.md)).

- **Several replicas share DPoP nonces and single-use records through Valkey.**
  `NCode.Identity.SharedState.Abstractions` defines `IExpiringKeySet`, an atomic add-if-absent set of expiring keys;
  `NCode.Identity.SharedState` provides the in-process adapter `AddIdentityServer()` registers by default (correct for
  one replica), and the opt-in `NCode.Identity.SharedState.Valkey` package adds
  `SharedStateLibraryBuilder.AddValkey(configuration)`, which uses the `ConnectionStrings:valkey` connection string and
  binds the `Valkey` section (`KeyPrefix`) strictly. Without the connection string it registers nothing; with it, it
  registers the Valkey adapters and adds a `valkey` health check tagged `ready`. The reference host calls it, and the
  Aspire AppHost runs a Valkey container and injects the connection string, so local development uses the same adapter
  as a multi-replica deployment
  ([ADR-1164](docs/decisions/1164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)).

- **A shared expiring counter set backs rate limiting.** `NCode.Identity.SharedState.Abstractions` provides
  `IExpiringCounterSet`: `IncrementAsync(key, expiresWhen)` atomically increments a counter that spans one fixed window
  and returns its count, and `GetAsync(key)` reads a count. `AddInProcessExpiringCounterSet` registers the in-process
  default, which `AddIdentityServer` registers beside the in-process key set, and `AddValkey` registers in its place a
  Valkey adapter that runs `INCR` and `PEXPIRE` as one script, so every replica counts against one counter. It is the
  foundation of per-tenant rate limiting
  ([ADR-1196](docs/decisions/1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md), issue
  [#84](https://github.com/NCodeGroup/identity/issues/84)).

- **Shared state holds versioned expiring values and membership indexes.** `IExpiringValueSet` stores an opaque,
  versioned `ExpiringValue` with an atomic take and a compare-and-replace, and `IExpiringIndexSet` keeps expiring
  membership indexes. `AddInProcessExpiringValueSet` and `AddInProcessExpiringIndexSet` register the in-process
  adapters, and `AddValkey` registers both Valkey adapters
  ([ADR-1267](docs/decisions/1267-replicas-share-versioned-expiring-values-and-membership-indexes.md), issue
  [#123](https://github.com/NCodeGroup/identity/issues/123)).

- **`HealthCheckTags.Readiness` tags every readiness health check.** `NCode.Identity.Registration` publishes the tag
  (`"ready"`) of every health check that decides whether a replica can serve requests. The Valkey adapter tags its check
  with it and `MapHealthProbes()`'s `/health/ready` endpoint runs the checks that carry it, so a package that adds a
  readiness check uses the constant.

- **`IReplicaBroadcaster` and `IReplicaBroadcastHandler` announce a change to every replica.** A replica publishes a
  message on a channel, and every replica's handlers for that channel receive it, the publisher's included. Delivery is
  best effort, at most once, and unordered, so a handler is idempotent, and a handler's failure is logged and never
  reaches the publisher: the in-process broadcaster lets a handler's `OperationCanceledException` through only when the
  publisher cancelled. `AddInProcessReplicaBroadcaster()` is the default, and `AddIdentityServer` registers it. The
  Valkey adapter publishes with `PUBLISH` and `SUBSCRIBE` on the key prefix plus the channel, so its access-control user
  needs both commands. The `tenant-changed` channel evicts a changed tenant on every replica
  ([ADR-1205](docs/decisions/1205-replicas-announce-changes-through-a-broadcast-port.md), issue
  [#240](https://github.com/NCodeGroup/identity/issues/240)).

- **A replica that may have missed a broadcast drops what the message would have invalidated.** The Valkey subscriber
  calls `IReplicaBroadcastHandler.HandleMissedMessagesAsync` once it subscribes a channel and whenever its connection is
  restored. The tenant handler evicts only the cached tenants whose stored row changed or is gone, with their master
  keys, comparing the concurrency token each served tenant was materialized from (`OpenIdTenant.ConcurrencyToken`,
  `IOpenIdTenantCache.GetRevisionsAsync`) with the store's (`ITenantStore.GetConcurrencyTokensAsync`) and evicting
  every cached tenant and master key when it cannot read them, because every change a `tenant-changed` announcement
  carries commits to the tenant row, a tenant root key change through `ITenantStore.MarkSecretsChangedAsync`; the team
  handler evicts only the cached teams whose stored row, or the row of a team above them, changed or is gone, with
  every team and tenant beneath them, comparing the concurrency token of each served team and of every team it was
  materialized over (`OpenIdTeam.ConcurrencyToken`, `IOpenIdTeamCache.GetRevisionsAsync`,
  `IOpenIdTenantCache.GetTeamRevisionsAsync`) with the store's (`ITeamStore.GetConcurrencyTokensAsync`) and clearing
  both caches when it cannot read them, and the platform handler reads the platform's settings again
  ([ADR-1205](docs/decisions/1205-replicas-announce-changes-through-a-broadcast-port.md),
  [ADR-1219](docs/decisions/1219-server-settings-converge-on-every-replica-through-one-reconciliation.md),
  [ADR-1416](docs/decisions/1416-a-replica-that-may-have-missed-a-team-announcement-evicts-only-the-teams-whose-row-changed.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [ADR-1454](docs/decisions/1454-a-replica-that-may-have-missed-a-tenant-announcement-evicts-only-the-tenants-whose-row-changed.md),
  issue [#249](https://github.com/NCodeGroup/identity/issues/249),
  [#564](https://github.com/NCodeGroup/identity/issues/564),
  [#579](https://github.com/NCodeGroup/identity/issues/579)).

- **A committed tenant, team or platform settings change is applied and announced on its own bounded token.**
  `TenantChangedHandler`, `TeamChangedHandler` and `PlatformSettingsChangedHandler` evict, read back and announce a committed change each on its
  own 30-second token, so a client that disconnects does not skip the announcement and a store or Valkey timeout does
  not fail a write that has already committed; a failed refresh or announcement is logged, and a failed eviction is
  still announced ([#485](https://github.com/NCodeGroup/identity/issues/485),
  [ADR-1351](docs/decisions/1351-a-committed-change-is-applied-and-announced-on-its-own-bounded-token.md)).

- **Protocol-endpoint metrics.** The protocol endpoints tag ASP.NET Core's `http.server.request.duration` with
  `ncode.identity.openid.endpoint` (the endpoint's registered name, stable when routes or tenant routing change),
  `ncode.identity.openid.error` (the OAuth error code of every error response, including the rate limiter's `429` and an
  authorization error redirected to the client), and, on token requests that reach grant-handler selection,
  `ncode.identity.openid.grant_type` (a registered grant type, else `_OTHER`); the attribute names are constants on
  `OpenIdMetricAttributes`. `NCode.Identity.OpenId.Authentication` owns the meter
  `OpenIdAuthenticationMetrics.MeterName` and the counter `ncode.identity.rate_limiting.requests` (`{request}`) by
  `ncode.identity.rate_limiting.partition` and `ncode.identity.rate_limiting.result` (`admitted`, `rejected`,
  `store_unavailable`). `NCode.Identity.SharedState.Valkey` owns the meter `ValkeyMetrics.MeterName` and the histogram
  `ncode.identity.shared_state.operation.duration` (`s`) of each Valkey round trip by operation, adapter, and
  `error.type`. `NCode.Identity.Server` provides `AddOpenTelemetryMetrics(configuration)`, which the reference host
  calls: the family's meters, ASP.NET Core, Kestrel, `System.Net.Http` (bounded attributes only), and the runtime,
  exported over OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` is set; the exporter is added to the metrics pipeline only, so
  logs and traces stay the host's choice. Every meter comes from `IMeterFactory`. The catalog is
  [`docs/metrics.md`](docs/metrics.md)
  ([ADR-1197](docs/decisions/1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md), issue
  [#119](https://github.com/NCodeGroup/identity/issues/119)).

- **The outbound clients count their retries and timeouts.** The guarded outbound client records
  `ncode.identity.outbound_http.retries` (by `error.type`) and `ncode.identity.outbound_http.timeouts` (by `attempt` or
  `total` scope) on the `NCode.Identity.OpenId.Authentication` meter, and the OpenID protocol client records
  `ncode.identity.openid_client.retries` and `ncode.identity.openid_client.timeouts` on its
  `NCode.Identity.OpenId.Client` meter (`OpenIdClientMetrics.MeterName`), so operators see how often a call to a
  caller-supplied URL or an OpenID Provider is retried or times out; `docs/metrics.md` lists them
  ([#519](https://github.com/NCodeGroup/identity/issues/519)).

- **The guarded client's options are its only time bound.** The one client that serves every fetch of, and delivery to,
  a caller-supplied URL has `HttpClient`'s own timeout off, so `OutboundHttpOptions.TotalTimeout` applies in full, above
  100 seconds included ([#481](https://github.com/NCodeGroup/identity/issues/481),
  [ADR-1315](docs/decisions/1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md)).

- **The BFF measures and traces its refreshes and its session store.** The `NCode.Identity.Bff` meter records
  `ncode.identity.bff.token.refresh.duration` (with the result `refreshed`, `joined` or `failed`, lock and lease waits
  included) and `ncode.identity.bff.session_store.operation.duration` (for whichever `IBffSessionStore` is registered),
  and the `NCode.Identity.Bff` activity source traces both. `BffAuditEvent` adds its reason as
  `ncode.identity.bff.reason` to `ncode.identity.events.published`, which counts logins, session ends, refresh failures
  and rejections. `BffTelemetry` names the meter and the source; a package traces its own mechanisms through one
  activity source named like its meter
  ([ADR-1197](docs/decisions/1197-telemetry-has-one-owner-per-signal-bounded-attributes-and-spans-that-mirror-their-histograms.md),
  issue [#416](https://github.com/NCodeGroup/identity/issues/416)).

- **A replica adds each usage batch to the ledger once, and its final flush runs once.** A replica numbers its batches,
  and the ledger skips a batch its replica already added, so a save that commits but fails at its caller, for example
  when the connection drops after the commit, never overcounts a tenant's billed usage. Every host stop waits for the
  one final flush, so a host stopped twice at once never disposes its services under it; usage the flush cannot add is
  logged as discarded (event 2051), and the stop does not throw
  ([ADR-1209](docs/decisions/1209-a-replica-adds-each-usage-batch-to-the-ledger-once.md), issues
  [#216](https://github.com/NCodeGroup/identity/issues/216) and
  [#245](https://github.com/NCodeGroup/identity/issues/245)).

- **The OpenID exception handler logs a rejected client request at Warning.** A 4xx `OpenIdException`, such as
  `unauthorized_client` for an unregistered `redirect_uri`, logs at Warning with its error code; only 5xx and unexpected
  faults log at Error (issue [#131](https://github.com/NCodeGroup/identity/issues/131)).

- **The self-issued access-token authenticator logs why it rejects a token.** It writes a Debug log with the triggering
  exception, naming the issuer, audience, lifetime or signature failure, and returns `invalid_token`.

- **Every setting has a registered descriptor, enforced wherever a setting enters.** Every setting the platform, a team, a tenant,
  client or resource server carries must have a registered descriptor, and its value must convert to the descriptor's
  type; nothing invents a descriptor for an unknown name. `OpenId:Platform:Settings` is validated when the host starts: an
  unregistered name, a value that does not convert, a list setting written as a single value, or a single-value setting
  written as a list stops the host with an error naming the configuration path, and an empty array is an empty list. A
  configuration reload that cannot be honored is rejected with an error log and the settings in force stay. The
  management API validates the settings of a created team, tenant, client or resource server and every settings
  `PATCH`, returning `400` with a validation problem keyed by setting name and storing nothing; a non-object `settings`
  payload is rejected. A stored settings document that names an unregistered setting fails when it is loaded, so a
  control an operator configured is never silently dropped. `ISettingSerializer.ValidateSettings` returns `SettingError`
  values, and `ISettingCollection.Set(key, value)` throws for a key with no descriptor of that value type; an extension
  registers a descriptor for every setting it reads or writes
  ([ADR-1139](docs/decisions/1139-setting-names-are-a-closed-vocabulary.md)).

- **A setting is accepted only at a scope that reads it.** Every setting declares the scopes it applies to
  (`SettingDescriptor.AppliesTo`): the platform, team, tenant, client or resource-server scopes whose effective settings the
  runtime reads it from. A settings document may carry it only at one of those scopes or a scope above it in the
  cascade; anywhere else, such as `security_profiles` or `require_pkce` on a resource server, or `session_idle_timeout`,
  `subject_claim_types` or discovery metadata on a client, it is rejected wherever it enters, like an unregistered name:
  configuration stops the host, a management create or settings patch returns `400`, dynamic client registration refuses
  it, a seeder stops naming the client, and a stored document fails the request that loads it. The error names the
  scopes the setting applies to. A resource server selects no security profile; a constraint it needs on its access
  tokens is one of its own settings ([#101](https://github.com/NCodeGroup/identity/issues/101)). The management API's
  effective-settings previews list only the settings that apply within the previewed scope.
  `ISettingSerializer.DeserializeSettings`, `ISettingSerializer.ValidateSettings` and the JSON overload of
  `ISecurityProfileCatalog.Expand` take the name of the scope whose document they read
  ([ADR-1149](docs/decisions/1149-settings-declare-the-scopes-they-apply-to.md)).

- **Setting scopes are an open vocabulary.** Each scope is a registered `SettingScopeDescriptor` (a name and the scope
  it inherits from), collected by `ISettingScopeCollection` through `ISettingScopeCollectionProvider`, and the cascade
  is derived from them. The family registers `platform`, `team`, `tenant`, `client` and `resource_server` (`OpenIdSettingScopes`);
  a package that stores settings for an entity of its own, such as an organization or a connection, registers that
  entity's scope beneath the one it inherits from and declares its own settings for it, with no change to the library
  ([ADR-1150](docs/decisions/1150-vocabularies-are-open-unless-provably-closed.md)).

- **A setting with no declared default is absent from the settings lattice, value types included, and
  `GetValueOrNull<TValue>()` reads such a setting as a `Nullable<TValue>`.** `SettingDescriptor<TValue>.HasDefault` is
  `true` only when a default is supplied, so the root settings data source loads no `default(TValue)` for a value-type
  setting such as `subject_max_age`; for such a setting `TryGetValue` returns `false`, and it imposes no constraint
  ([#131](https://github.com/NCodeGroup/identity/issues/131)).
  `SettingCollectionExtensions.GetValueOrNull<TValue>(SettingKey<TValue>) where TValue : struct`, in
  `NCode.Identity.Settings`, returns the setting's value when present and `null` when absent, so a caller distinguishes
  "unset" from a materialized zero or default without a `TryGetValue` pattern at freshness and limit call sites.

- **The default `claims_supported` list is part of the replaceable `IDefaultSettingsProvider` baseline.** A host that
  enables `claims_supported_is_strict` extends the allow-list by registering its own baseline provider, unbound by a
  descriptor literal ([ADR-1010](docs/decisions/1010-supported-settings-unset-means-unrestricted.md)).

- **Configuration is bound strictly and checked before the host accepts traffic.** A key that maps to no option stops
  the host with an error naming the section, the options type and the key. This covers the whole OpenID section and the
  `BootstrapAdmin`, `Frontend`, `ForwardedHeaders`, `ConformanceSeed` and `DeveloperInteractiveSeed` sections. Every
  bound options type is validated on start, and a tenant-resolution configuration that cannot work (an unknown strategy,
  a path route without its tenant parameter, an invalid host pattern, a static issuer that does not end with the tenant
  path) stops the host before it reports ready. `AddConfiguration` owns the OpenID section's shape: `Platform` (including
  `Settings`, carried by `OpenIdPlatformOptions.Settings`), `Team` (team settings reconciliation), `Tenant` (tenant materialization and caching),
  `TenantResolution` (tenant selection), `MutualTls`, `GrantRetention` and `TenantUsage`; any other subsection is
  rejected. `AddConfiguration` binds the mutual-TLS forwarding options too, so a host's chosen section name applies to
  them ([ADR-1137](docs/decisions/1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).

- **Intervals and retentions are configured and validated when the host starts, and the host refuses one that a timer
  cannot run.** `AddConfiguration` binds `GrantRetentionOptions` (`OpenId:GrantRetention`: `Retention`, `PruneInterval`)
  and `TenantUsageOptions` (`OpenId:TenantUsage`: `FlushInterval`, `Retention`, `PruneInterval`) strictly and validates
  them on start; `GrantRetentionOptions.SectionName` and `TenantUsageOptions.SectionName` name the subsections. The
  platform's and the tenants' settings and secrets refresh intervals, `OpenId:Team:ReconciliationLeaseDuration`,
  `OpenId:Tenant:TenantCacheLifetime`, `GrantRetentionOptions.PruneInterval` and `TenantUsageOptions.FlushInterval` must
  be between one millisecond and 4,294,967,294 milliseconds (about 49.7 days), and a retention must not be negative; the
  error names the option and its configuration key. `NCode.Identity.Time.TimerPeriod` states the range
  ([ADR-1137](docs/decisions/1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md), issues
  [#274](https://github.com/NCodeGroup/identity/issues/274) and
  [#277](https://github.com/NCodeGroup/identity/issues/277)).

- **`IEffectiveSettingsResolver` resolves a scope's effective settings in one place.** It merges the platform's settings, a team's document,
  a tenant's document, and a client's or resource server's document, with the security profiles' ceilings and defaults:
  `ReadScopeSettings`, which takes the parent settings, and `Resolve` read a scope's document and merge it onto its
  parent's effective settings, and the two `ResolvePlatformAsync` overloads resolve a proposed or the committed platform
  document onto the configured settings and `ResolveTeamAsync` resolves a team's. Every effective-settings merge, `DefaultOpenIdTenantFactory` and
  `EphemeralOpenIdTenantFactory` included, goes through it. Seeds and management previews read the platform's committed
  settings rather than a replica's 5-minute poll of them, and `ITenantStore` and `IClientStore` expose
  `GetSettingsPageAsync`, which a custom store implements. Its default is registered with `TryAddSingleton`, so a host
  registers its own ([ADR-1204](docs/decisions/1204-a-scopes-effective-settings-are-resolved-by-one-service.md),
  [#230](https://github.com/NCodeGroup/identity/issues/230)).

- **Redirect URI kinds, which a client's settings narrow.** The `redirect_uri_kinds_supported` setting lists the kinds
  of redirect URI a client may register and use. It applies to tenants and clients, merges with `Intersect`, is not
  advertised, and permits every kind while unset. A kind is a registered `IRedirectUriKind`, judged through
  `IRedirectUriKindCatalog`, so a package adds one by registering it. The built-in kinds, named in
  `OpenIdConstants.RedirectUriKinds`, sort a redirect URI into at most one: `https`, `loopback` (an `http` URI whose
  host is `127.0.0.1` or `[::1]`, RFC 8252 §7.3), `http` (any other `http` URI, `localhost` included), and `private_use`
  (a reverse-domain scheme such as `com.example.app`, RFC 8252 §7.1)
  ([ADR-1203](docs/decisions/1203-redirect-uris-are-matched-exactly.md),
  [#244](https://github.com/NCodeGroup/identity/issues/244)).

- **Security profiles are an open registry of ceilings, defaults and requirement objects**
  ([#54](https://github.com/NCodeGroup/identity/issues/54),
  [ADR-1109](docs/decisions/1109-fapi-behavioral-rules-and-discovery-validation-details.md)). A profile is an
  `ISecurityProfile` (its name, its ceiling settings, its default settings and its composed `Requirements`);
  `ISecurityProfileCatalog` is the single registry that resolves the selected profiles, expands their settings, and runs
  their requirements for a given enforcement phase through `EvaluateRequirementsAsync<TContext>`. Each requirement is a
  self-contained rule object (`ISecurityProfileRequirement`, `ISecurityProfileRequirement<TContext>`) implementing a
  typed phase seam, `IAuthorizationRequestProfileRequirement`, `ITokenRequestProfileRequirement` or
  `IClientAuthenticationProfileRequirement`, each with its evaluation context, so a requirement kind or a whole profile
  (a custom organizational profile, for example) is added without changing an enforcement site. FAPI 2.0 carries three
  requirements: a confidential-client rule (public clients are rejected at the authorization endpoint), a
  sender-constrained access token rule (a DPoP or mutual-TLS binding is required at the token endpoint), and an
  issuer-only `client_assertion` audience rule (FAPI 2.0 §5.3.2.1).

- **A security profile supplies compliant defaults, within the scope that selects it.**
  `ISecurityProfile.ApplyDefaultSettings` contributes overridable defaults through `ISecurityProfileDefaultsBuilder`,
  which accepts only a setting that does not merge with a lattice operation. The catalog places a profile's defaults
  beneath the settings of the scope that selects it, so that scope and every scope below it may set another value, and
  when several selected profiles default one setting, the first in ordinal order supplies it. A profile's default
  applies only to a setting that applies within that scope: the platform, a team or a tenant that selects `fapi1-advanced` supplies
  `tls_client_certificate_bound_access_tokens`, while a client that alone selects it takes its tenant's support, since
  that setting does not apply at a client, and `CertificateBoundAccessTokenCoherenceRule` reports the client while that
  support is off. Ceilings merge at every scope, because a bound only narrows. FAPI 2.0 defaults
  `id_token_signed_response_alg` and `introspection_signed_response_alg` to `PS256`, so a client under the profile that
  registers no algorithm is signed with an algorithm the profile permits
  ([ADR-1148](docs/decisions/1148-security-profiles-accumulate-down-the-settings-cascade.md),
  [ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md),
  [#259](https://github.com/NCodeGroup/identity/issues/259)).

- **A security profile selected in the platform's configuration supplies its defaults at the platform root, and every
  `ISecurityProfileCatalog.Expand` overload names its scope.** The root's descriptor defaults, computed supported values
  and default-settings providers are a baseline beneath the configuration, and `Expand(scopeSettings, scopeName,
  baselineSettings)` layers them: a configured value overrides the baseline, a profile's default overrides the baseline
  but never a configured value, and a ceiling bounds both. So `fapi1-advanced` advertises certificate-bound access token
  support, and the FAPI profiles' `PS256` signed-response defaults apply, when the platform's configuration selects the
  profile. `Expand(scopeSettings, scopeName)` and `Expand(scopeSettings, scopeName, baselineSettings)` take the name of
  the scope whose settings they expand, `OpenIdSettingScopes.Platform` at the platform's root, as the overload that takes a
  settings document does, and an unregistered scope name is refused with an `ArgumentException`
  ([ADR-1148](docs/decisions/1148-security-profiles-accumulate-down-the-settings-cascade.md), issues
  [#233](https://github.com/NCodeGroup/identity/issues/233) and
  [#259](https://github.com/NCodeGroup/identity/issues/259)).

- **Client coherence rules check the settings a client registers against what its effective settings permit.**
  `IClientCoherenceRule` names the settings it reads, and its `FindViolations(ClientCoherenceContext)` receives the
  client's own settings document as written, without its tenant's settings and without any security profile's ceilings
  and defaults, beside the effective settings that result, so a rule sees a value that a ceiling dropped.
  `IClientCoherenceValidator.Validate(tenantSettings, clientSettings)` is the one way to check a client: it reads the
  registered settings from the client's settings document and resolves the effective settings onto its tenant's, so
  every caller gives every rule the same two views, and it tells a caller whether a change touches a setting a rule
  reads. A `ClientCoherenceViolation` names the setting that governs a value as `GoverningSettingName`, because a
  setting that requires a companion is not a ceiling, and carries a `Description` that every caller reports;
  `ClientCoherenceViolation.Missing` reports a required value that is absent. A package adds a rule by registering it
  with `TryAddEnumerable`
  ([ADR-1206](docs/decisions/1206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issues
  [#231](https://github.com/NCodeGroup/identity/issues/231) and
  [#253](https://github.com/NCodeGroup/identity/issues/253)).

- **Built-in coherence rules check a client's algorithms, redirect URIs, authentication, token binding, encryption and
  delivery.** They check a client's response algorithms, its redirect URI kinds, its token endpoint authentication
  method, and a required certificate-bound access token against the server's support for it.
  `RegisteredValueCoherenceRule` compares what a client registers in a list with the effective list, and reports a value
  that a client lists, or a `true` that it registers, that an `Intersect` or `And` ceiling drops from its effective
  settings, so a client cannot register a list value its tenant or a security profile excludes.
  `IdTokenEncryptionCoherenceRule` reports a client that must encrypt its ID tokens and registers no
  `id_token_encrypted_response_alg`. `BackchannelTokenDeliveryCoherenceRule` reports a client that has no
  `client_notification_endpoint` while its delivery mode is not `poll`, and a client that may use the CIBA grant while
  `backchannel_token_delivery_modes_supported` excludes its delivery mode
  ([ADR-1206](docs/decisions/1206-a-clients-settings-are-checked-by-registered-coherence-rules.md),
  [#253](https://github.com/NCodeGroup/identity/issues/253)).

- **Every entry point checks a client through the coherence validator.** Dynamic registration, the management API and
  the configuration seeders apply the same registered coherence rules, with uniform error descriptions under the
  registration error codes. A platform, team or tenant settings change that narrows `redirect_uri_kinds_supported`, selects
  `fapi2` or `fapi1-advanced`, narrows `token_endpoint_auth_methods_supported`, or drops any other value a client
  registers is refused while a client registers a value it would exclude. The bootstrap administrator, developer and
  conformance seeders run the validator on a client they create and throw an `InvalidOperationException` that names the
  client and lists its violations; the bootstrap seeder checks before it imports the secret, and a client that exists is
  left as it is. A management validation problem lists the first ten errors of each setting, in order, and adds `Errors
  not listed for '<name>': <n>.` for the rest, so a change that makes many clients incoherent answers a bounded response
  ([ADR-1206](docs/decisions/1206-a-clients-settings-are-checked-by-registered-coherence-rules.md), issues
  [#231](https://github.com/NCodeGroup/identity/issues/231), [#246](https://github.com/NCodeGroup/identity/issues/246),
  [#253](https://github.com/NCodeGroup/identity/issues/253) and
  [#254](https://github.com/NCodeGroup/identity/issues/254)).

- **A settings write passes a stored document that cannot be honored.** When a stored client's or tenant's settings
  document names a setting, a value or a scope the settings vocabulary rejects, a platform, team or tenant settings write logs
  the document (`ClientSettingsUnreadable`, `TenantSettingsUnreadable`, event IDs 3006 and 3007) and does not count it
  as a violation the change introduces; a write that overwrites such a document of its own checks no client and logs it
  (`PlatformSettingsUnreadable`, 3008, `TeamSettingsUnreadable` or `TenantSettingsUnreadable`), so the management API can repair it. The
  incoherent-client report lists such a client
  ([ADR-1241](docs/decisions/1241-a-settings-write-is-not-refused-for-a-stored-document-it-cannot-read.md),
  [#295](https://github.com/NCodeGroup/identity/issues/295)).

- **One tenant-resolution abstraction serves the OpenID runtime and the management API, and one
  `DefaultOpenIdTenantFactory` materializes OpenID tenants.** `ITenantResolver`, `ITenantStrategy` and
  `TenantResolutionOptions` live in `NCode.Identity.OpenId.Abstractions` (namespace `NCode.Identity.OpenId.Tenants`),
  with the default implementation in `NCode.Identity.OpenId.Core`, so both select the ambient tenant from a single
  source of truth. The factory composes `ITenantResolver` for tenant selection and performs only materialization, and
  `OpenIdTenantOptions` carries only materialization settings
  ([ADR-1018](docs/decisions/1018-tenant-scoped-data-access-at-the-persistence-layer.md)).

- **Tenant resolution probes the tenant cache before reading the tenant store, and a cached tenant lives at most 15
  minutes.** `ITenantResolver` and `ITenantStrategy` expose `TryGetTenantId(httpContext, out tenantId)`, a store-free
  identity probe satisfied by the static-single (configured id) and dynamic-by-path (route value) strategies, which
  `DefaultOpenIdTenantFactory` uses to serve a warm tenant with no tenant-store round trip; dynamic-by-host performs a
  full resolve, because mapping a domain to a tenant requires the store. A cache hit does not re-read the store.
  `OpenIdTenantOptions.TenantCacheLifetime` (`OpenId:Tenant:TenantCacheLifetime`, 15 minutes) is an absolute lifetime
  counted from materialization, however often the tenant is used, so a replica that missed a tenant's change
  announcement serves the tenant at most that long
  ([ADR-1037](docs/decisions/1037-tenant-cache-probe-before-store-load.md),
  [ADR-1219](docs/decisions/1219-server-settings-converge-on-every-replica-through-one-reconciliation.md),
  [#249](https://github.com/NCodeGroup/identity/issues/249)).

- **Every replica sees a tenant change at once.** Every management write to a tenant (its record, its settings, a
  secret, or its deletion) sends a `TenantChangedCommand` after it commits and announces the change, so every replica
  evicts the tenant from its tenant cache (`IOpenIdTenantCache.RemoveAsync`) and no replica serves a disabled, deleted
  or re-addressed tenant from its cache. After a settings change the default handler seeds the tenant again, so a key
  for an algorithm the settings permit is provisioned before the write returns. The platform provider evicts every cached
  tenant whenever the platform's settings change, so a platform-wide setting such as `environment_type` reaches tenants at
  once. `Ephemeral` developer mode keeps one key per tenant for the life of the process, so the eviction does not
  invalidate issued tokens ([ADR-1169](docs/decisions/1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-1205](docs/decisions/1205-replicas-announce-changes-through-a-broadcast-port.md), issues
  [#240](https://github.com/NCodeGroup/identity/issues/240) and
  [#409](https://github.com/NCodeGroup/identity/issues/409)).

- **An `OpenIdContext` can be materialized for an explicit, route-addressed tenant**
  ([ADR-1100](docs/decisions/1100-explicit-tenant-context-materialization.md)).
  `IOpenIdContextFactory.CreateForTenantAsync` and `IOpenIdTenantFactory.CreateTenantByIdAsync` build the context from
  the tenant's own configured issuer, independent of the request's tenant-resolution strategy. Each is paired by name
  with its request-driven counterpart, `CreateForRequestAsync` and `CreateTenantForRequestAsync`; "Resolve" is reserved
  for `ITenantResolver` selecting a `PersistedTenant`.

- **The control-plane tenant is named from configuration and served from the tenant cache.** `ITenantStrategy` and
  `ITenantResolver` expose `GetControlPlaneTenantId()`, and `CreateControlPlaneTenantAsync` probes the tenant cache with
  that identifier first, reading the store only on a miss
  ([ADR-1191](docs/decisions/1191-the-management-api-authenticates-against-the-control-plane-tenant.md)).

- **A tenant's issuer is a first-class tenant property, and a tenant created without one is given the issuer its tenant
  strategy derives.** Each tenant holds its own issuer, `PersistedTenant.Issuer`, alongside `DisplayName` and
  `IsDisabled`; it is set through the management API (`Issuer` on `TenantResource`, `CreateTenantRequest` and
  `UpdateTenantRequest`) or, for a single-tenant host, `OpenId:TenantResolution:StaticSingle:Issuer`. The host and path
  strategies take an issuer template (`OpenId:TenantResolution:DynamicByHost:IssuerTemplate =
  https://{tenantId}.auth.example`, `DynamicByPath:IssuerTemplate = https://op.example/{tenantId}`), and a tenant
  created through the management API without an issuer is given its expansion, stored with it and judged by the same
  rules as an explicit issuer. A stored issuer stays as it is when the template changes, and clearing a tenant's issuer
  restores the derived one. The root tenant takes `OpenId:TenantResolution:Root:Issuer` when it is set (the hosted
  platform's `https://auth.<apex>` beside `https://{tenantId}.auth.<apex>`), and its address is reserved for it. A
  template is validated when the host starts: it carries `{tenantId}` once, varies the host under the host strategy, and
  ends with the tenant route under the path strategy
  ([ADR-1165](docs/decisions/1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)).

- **A tenant's mTLS endpoint alias is a routed tenant property with a derived default (RFC 8705 §5).** The alias is
  `PersistedTenant.MtlsEndpointAliasAuthority` (an `https` origin, stored with a derived, indexed
  `NormalizedMtlsEndpointAliasHost`), exposed as `OpenIdTenant.MtlsEndpointAliasAuthority`, and discovery builds
  `mtls_endpoint_aliases` from it. The host strategy routes a request to a tenant by its issuer host or its alias host,
  through `ITenantStore.GetOrDefaultByHostAsync`, which resolves no tenant when more than one matches; validation lists
  every holder through `ITenantStore.ListTenantIdsByHostAsync`. Validation judges both hosts: the alias must be an
  `https` origin, the host pattern must map its host back to the tenant, it must differ from the issuer host, and no
  other tenant may serve it; `ITenantResolver.ValidateTenantAsync` and `ITenantStrategy.ValidateTenantAsync` take the
  alias beside the issuer. `DynamicByHost:MtlsEndpointAliasTemplate` (for example `https://{tenantId}.mtls.example`)
  gives every tenant written without an alias its expansion, and `Root:MtlsEndpointAliasAuthority` and
  `StaticSingle:MtlsEndpointAliasAuthority` configure the root and static tenants' alias beside their issuers;
  `ITenantStrategy` and `ITenantResolver` provide `GetDefaultMtlsEndpointAliasAuthorityAsync`. Management create and
  update, and the provisioner, store the derived alias, so a stored alias stays as it is when the template changes, and
  an update that clears the alias restores the derived one. The template is checked at startup: an `https` origin
  template with `{tenantId}` in its host, whose host the host pattern maps to itself and which differs from the issuer
  template's host. The management API carries `mtlsEndpointAliasAuthority` on tenant create, update and read, and the
  address previews (`GET /teams/{teamId}/tenants/{tenantId}/address` for a tenant that does not exist yet and
  `GET /tenants/{tenantId}/address` for an existing one) take an optional `mtlsEndpointAliasAuthority` and return the
  effective `MtlsEndpointAliasAuthority` and `IsMtlsEndpointAliasAuthorityDerived` on `TenantAddressResource`
  ([ADR-1195](docs/decisions/1195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md),
  [#117](https://github.com/NCodeGroup/identity/issues/117)).

- **A tenant is provisioned through one pipeline under every tenant strategy: the root tenant is created from
  configuration, and every tenant is seeded.** `ITenantProvisioner.EnsureCreatedAsync(teamId, tenantId, displayName)`
  ensures the tenant's team first through `ITeamProvisioner`, then creates a configuration-defined tenant at its
  strategy's default issuer, judged like any other write, and `SeedAsync(tenantId)` seeds it and returns the tenant row
  its seed committed; `ITenantStrategy` and `ITenantResolver` expose `GetDefaultIssuerAsync`, which a custom strategy
  implements. Under the host and path strategies the root (control-plane) tenant, configured under
  `TenantResolution:Root` (`TenantId`, `DisplayName`, `Issuer`, `TeamId`, `TeamDisplayName`), is created when the first
  request reaches its address, so a multi-tenant deployment boots from configuration alone: the bootstrap administrator,
  named into the root tenant, obtains the first management token there. A tenant created through the management API is
  seeded before the response, so its OpenID and management resource servers exist before its first request, and every
  tenant is seeded again when it is next materialized, so every seed contributor a deployment registers reaches existing
  tenants under every strategy. A tenant is materialized, and a created tenant answered, from the row its seed
  committed, so it carries the concurrency tokens its own seed wrote, and a replica that revalidates its cached tenants
  after a missed announcement keeps it. The Aspire conformance instance serves its own `conformance` tenant
  ([ADR-1166](docs/decisions/1166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md),
  [ADR-1463](docs/decisions/1463-a-seeded-tenant-is-materialized-from-the-row-its-seed-committed.md),
  [#619](https://github.com/NCodeGroup/identity/issues/619)).

- **Concurrent seeds of one tenant converge.** When two replicas, or two requests that miss the tenant cache together,
  seed the same tenant, a seed whose commit fails runs once more in a fresh unit of work; seed contributors are
  idempotent, so it adds only what the other seed did not, and both requests succeed with one set of keys and resource
  servers ([ADR-1169](docs/decisions/1169-every-tenant-is-provisioned-with-persisted-keys.md)).

- **A tenant the deployment requires cannot be deleted.** `DELETE /api/tenants/{tenantId}` answers `409 Conflict` for
  the root tenant or the static single-tenant strategy's tenant, because a deleted tenant identifier is never reissued.
  `ITenantStrategy` and `ITenantResolver` expose `IsRequiredTenant`, so a custom strategy names the tenants it requires
  ([ADR-1189](docs/decisions/1189-a-retired-tenant-identifier-or-issuer-is-never-reissued.md)).

- **Tenants declare their environment.** The tenant settings `environment_type`, `environment_name` and
  `deletion_protection` say which environment a tenant is: the type is one of the registered `development`, `testing`,
  `staging` and `production` (a package registers more as `ITenantEnvironmentType`), and the name is a display label. A
  tenant that declares nothing is `production`, and the Development and Conformance host settings declare `development`
  and `testing`. A non-production tenant's login pages show an environment banner. The audit record carries
  `tenant_environment_type`, and management audit events that name a tenant carry it too, resolved from the tenant's
  effective settings once per request; the metric attribute `ncode.identity.tenant.environment_type` tags
  `ncode.identity.events.published` and `ncode.identity.rate_limiting.requests`
  ([ADR-1289](docs/decisions/1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md),
  [#411](https://github.com/NCodeGroup/identity/issues/411)).

- **A platform settings write takes effect at once on every replica.** After the platform settings PATCH commits, it
  sends `PlatformSettingsChangedCommand`, carrying the previous and the updated document; the default handler reads the
  committed document again on the replica that handled the write before it answers, and announces the change on the
  `platform-settings-changed` replica broadcast channel, so every replica that receives the announcement reads it
  again. `OpenIdPlatform.RefreshSettingsAsync` reads the document on demand, and the poll
  (`OpenId:Platform:SettingsPeriodicRefreshInterval`, 5 minutes) is the fallback for a replica that missed the
  announcement. `OpenIdPlatform.RefreshSettingsAsync`, `IOpenIdTenantCache.ClearAsync` and
  `IReplicaBroadcastHandler.HandleMissedMessagesAsync` are members a custom implementation implements
  ([ADR-1219](docs/decisions/1219-server-settings-converge-on-every-replica-through-one-reconciliation.md), issues
  [#241](https://github.com/NCodeGroup/identity/issues/241), [#248](https://github.com/NCodeGroup/identity/issues/248),
  [#249](https://github.com/NCodeGroup/identity/issues/249) and
  [#252](https://github.com/NCodeGroup/identity/issues/252)).

- **Every tenant and stored client is reconciled with its team's effective settings when they change.**
  `TenantKeyReconciliationHandler` seeds a tenant that lacks a key its settings use, and `ClientCoherenceReportHandler`
  logs every stored client the settings make incoherent, each handling `ReconcileTeamSettingsCommand` for the team's
  tenants only; `PersistedTeam.ReconciledSettings` and `ReconciledSettingsFingerprint` record what was reconciled, so a
  pass a restart interrupted runs again and a start that changes nothing runs none
  ([ADR-1412](docs/decisions/1412-settings-are-reconciled-per-team.md),
  [ADR-1169](docs/decisions/1169-every-tenant-is-provisioned-with-persisted-keys.md),
  [ADR-1234](docs/decisions/1234-a-server-settings-reconciliation-reaches-only-the-tenants-its-change-affects.md)).

- **Rate limits and quotas are registered descriptors judged in one round trip per stage.** Each limit is a
  `RateLimitDescriptor` (name, stage, order, window, sliding or fixed, limit and overage settings, metered only, usage
  meters, partition selector) contributed through `ICollectionDataSource<RateLimitDescriptor>`, and
  `RateLimitPartitions` names the five built-ins. `IRequestRateLimiter.AcquireAsync(stage, RateLimitContext,
  CancellationToken)` and `RateLimitDecision` are public in `NCode.Identity.OpenId.Authentication.Abstractions`, so a
  package judges a stage of its own (`RateLimitStages`); the limiter marks a refused request, and
  `RateLimitEndpointFilter` records the usage meters once the endpoint has run.
  `IExpiringCounterSet.IncrementUntilExceededAsync`, which every implementation of the port implements, counts a stage's
  limits in one call (one Valkey script, measured as `counter.increment_until_exceeded`). Shared-state keys carry the
  tenant in braces as a hash tag, and the `RateLimitExceeded` log event names the window
  ([ADR-1246](docs/decisions/1246-rate-limits-are-registered-descriptors-judged-in-one-round-trip-per-stage.md),
  [#232](https://github.com/NCodeGroup/identity/issues/232)).

- **The rate limiter bounds its store calls and pauses them during an outage.** Each counter call is bounded to 100 ms
  and, after a failure, the limiter stops calling the store for 10 s, admitting every request uncounted
  (`store_unavailable`) before trying it again. The failure that opens the pause logs one warning and the answer that
  ends it logs one information message (`RateLimitStoreRecovered`), so an outage is logged once per replica. Valkey
  stays a readiness dependency, since single-use state fails closed without it
  ([ADR-1196](docs/decisions/1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md)).

- **Per-tenant daily quotas.** Two tenant settings bound a tenant's consumption per UTC calendar day for plan tiers:
  `quota_tenant_requests_per_day` (requests to its metered protocol endpoints, judged after the per-minute rate limits)
  and `quota_tenant_token_requests_per_day` (token requests from confidential clients, judged at the token endpoint
  after client authentication, so a caller that knows only a public `client_id` cannot spend it). A request over a quota
  is refused with `429 Too Many Requests`, `temporarily_unavailable`, and `Retry-After` until midnight UTC. Both are
  unlimited when unset and merged with `Min`, so the platform's value caps every tenant. They are limits: the platform
  writes a plan tier's quotas on a team, and a team's administrators may set lower ones on its tenants. They are
  counted in the shared `IExpiringCounterSet` and reported under the rate-limiting partitions `daily_requests` and
  `daily_token_requests`
  ([ADR-1198](docs/decisions/1198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md),
  [ADR-1418](docs/decisions/1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md),
  [#563](https://github.com/NCodeGroup/identity/issues/563)).

- **Billed overage past a daily quota.** A plan tier can admit requests past its daily quota and bill them:
  `quota_tenant_requests_overage_per_day` and `quota_tenant_token_requests_overage_per_day` set how many more requests
  (or confidential clients' token requests) are served past the matching quota in a UTC day. Unset or `0` refuses at the
  quota; a set allowance is also the hard ceiling, past which the tenant is refused until midnight UTC. Like the quotas,
  a platform-scope allowance is every tenant's default and caps a tenant-scope one, and is a limit set from above. Each
  request admitted past a quota is recorded in the usage ledger under the meters `requests_overage` and
  `token_requests_overage`, which the usage report and the admin UI's Usage tab show with the others, and is counted in
  `ncode.identity.rate_limiting.requests` with the result `overage`
  ([ADR-1200](docs/decisions/1200-a-tier-admits-a-bounded-overage-past-its-quota-and-bills-it-from-the-ledger.md),
  [ADR-1418](docs/decisions/1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md),
  [#563](https://github.com/NCodeGroup/identity/issues/563)).

- **Per-tenant usage ledger.** Every tenant's daily usage is recorded durably for billing and customer reports in the
  `TenantUsage` table, one row per tenant, UTC day, and meter: `requests` (requests admitted by the rate limits and the
  daily quota) and `token_requests` (confidential clients' token requests admitted by the token quota). The meter
  vocabulary is open: a package records its own through `ITenantUsageRecorder`. Each replica counts in memory and adds
  to the ledger every minute (`TenantUsageOptions.FlushInterval`); the ledger keeps 400 days
  (`TenantUsageOptions.Retention`), and `TenantUsagePruneService` removes the days past retention in bounded batches
  every `TenantUsageOptions.PruneInterval` (`OpenId:TenantUsage:PruneInterval`, one hour). `GET
  /api/tenants/{tenantId}/usage?from=&to=` reads a tenant's usage for up to 366 days, the last 30 by default; the admin
  UI shows it on the tenant page's **Usage** tab, a column per meter with a total row, over a chosen UTC date range
  ([ADR-1199](docs/decisions/1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md),
  [ADR-1213](docs/decisions/1213-the-usage-ledger-is-pruned-in-bounded-batches.md),
  [#443](https://github.com/NCodeGroup/identity/issues/443)).

- **An endpoint declares whether it is the tenant's metered usage, and only served requests are billed.** A metered
  endpoint's requests spend the daily request quota and are recorded in the usage ledger; an endpoint without
  `IOpenIdEndpointMeteredMetadata` is metered, and a package that adds a public metadata endpoint opts it out with
  `WithOpenIdMetered(false)`. The request filter records a request once its endpoint has run and skips one that any
  limit (per-client rate limit, token quota) refused with `429`, so a refused request never counts as usage or overage
  ([ADR-1199](docs/decisions/1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md)).

- **One public form reader serves the OpenID endpoints and the BFF.**
  `NCode.Identity.AspNetCore.FormRequestExtensions.TryReadFormAsync` reads a request's form, or returns `null` when it has none
  or its body is malformed or over the form limits ([#517](https://github.com/NCodeGroup/identity/issues/517),
  [#758](https://github.com/NCodeGroup/identity/issues/758),
  [ADR-1500](docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md)).

- **`NCode.Identity.Json.ObjectJsonConverter` deserializes loosely-typed JSON.** The public `System.Text.Json` converter in the
  `NCode.Identity.Json` foundation package reads JSON into an `object` graph (objects → `Dictionary<string, object?>`,
  arrays → `List<object?>`) for any package to reuse, beside the `JsonElements` helpers. `ObjectJsonConverterOptions`
  (passed to the constructor; `ObjectJsonConverter.Default` is the shared default instance) drives scalar mapping.
  `ObjectJsonConverterOptions.Default` is lenient — most-specific integers, `double` reals, and strings coerced to the
  most specific matching type (`DateTimeOffset`, then `Guid`, then a numeric type) before `string` — where the built-in
  `System.Text.Json` behavior is narrower. Each facet is configurable: integer handling
  (`ObjectJsonIntegerHandling.MostSpecific` / `Int64`), real handling (`ObjectJsonRealHandling.Double` / `Decimal`),
  oversized integers (`AllowBigInteger` → `System.Numerics.BigInteger` rather than a lossy `double`), date-string
  parsing (`ObjectJsonDateStringHandling.None` / `DateTime` / `DateTimeOffset`), and `CoerceStringToGuid` /
  `CoerceStringToNumber` (both on by default; off for strict round-trip fidelity, so `"123"` stays a `string`). The
  `.NewtonsoftCompatible` preset widens integers to `long` and parses date strings while, like Newtonsoft.Json, leaving
  number- and `Guid`-like strings as `string`. A bare JSON number is never interpreted as an RFC 7519 `NumericDate` here
  — that interpretation is exclusive to `JsonElements.TryGetValue<T>`, where the caller supplies a date target type.
  Both helpers share the same lenient, invariant-culture date/time parsing (`DateCoercion`), so they recognize the same
  formats (including date-only values and non-hyphenated `Guid`s) and normalize kind identically: a `Z`/UTC value is
  `Utc`, an explicit offset is `Local`, and a zone-less value is `Utc` (never `Unspecified`), so a parsed `DateTime` is
  never treated as machine-local downstream.

- **`NCode.Identity.Json.UriJsonConverter` and `NCode.Identity.Json.DelegatingJsonConverter<TInterface, TImplementation>` are reusable
  converters.** Both live in the `NCode.Identity.Json` foundation package beside `ObjectJsonConverter` and `JsonElements`.
  `UriJsonConverter` round-trips `System.Uri` with absolute/relative fidelity and has a shared, stateless
  `UriJsonConverter.Singleton`. `DelegatingJsonConverter<TInterface, TImplementation>` serializes an interface by
  delegating to a concrete implementation and has no singleton, because it carries mutable `MatchExact` state per
  interface/implementation pair. The OpenID-coupled `StringValuesJsonConverter` (which relies on
  `OpenIdConstants.ParameterSeparatorChar`) and the claims, message, and JWK converters live in their domain packages,
  so `NCode.Identity.Json` stays dependency-free.

- **`NCode.Identity.Json.ObjectGraphExtensions.TryGetValue<T>` reads typed values from loosely-typed object graphs, tolerant of
  numeric width.** The accessor lives in the `NCode.Identity.Json` foundation package beside `JsonElements` and
  `ObjectJsonConverter`, keeping logic out of the abstractions packages. Its receiver is the common
  `IEnumerable<KeyValuePair<string, object>>` base of `IDictionary<string, object>` and
  `IReadOnlyDictionary<string, object>`, so a single overload serves both interface-typed callers — and any concrete
  dictionary that implements both — without an ambiguous call, and dictionary receivers take an O(1) lookup fast path. A
  boxed integer converts across `sbyte`…`ulong`, `System.Numerics.BigInteger`, and the real types (range-checked, so an
  out-of-range read fails rather than wraps), and a boxed integer requested as `DateTimeOffset`/`DateTime` is read as an
  RFC 7519 `NumericDate` (Unix seconds, UTC), matching the `JsonElement` path; both paths share the internal
  `DateCoercion` helper, so they cannot drift. A JWT's caller-supplied `iat`, `nbf`, and `exp` are honored whatever
  integer width the claim boxed as.

- **The `NCode.Identity.Time` foundation package holds the whole-second time helpers.** `DateTimeExtensions`,
  `DateTimeOffsetExtensions` and `TimeProviderExtensions` (`WithPrecisionInSeconds`, `GetUtcNowWithPrecisionInSeconds`,
  `GetTimestampWithPrecisionInSeconds`) live in namespace `NCode.Identity.Time`, so `NCode.Identity.Jose.Abstractions` holds no
  logic ([ADR-1500](docs/decisions/1500-every-package-names-one-capability-under-the-ncode-identity-prefix-as-an-abstractions-and-implementation-pair.md), issue
  [#31](https://github.com/NCodeGroup/identity/issues/31)).

- **Every namespace is named for its purpose, and a contract, its DTOs and its default implementation share it** (issue
  [#27](https://github.com/NCodeGroup/identity/issues/27)). Across each `.Abstractions`/implementation pair:
  `NCode.Identity.Cryptography` holds `ICryptoService`, `CryptoServiceExtensions` and the `BinaryEncodingType` /
  `HashAlgorithmType` enums it takes; the `NCode.Identity.Secrets.Persistence` root holds `ISecretSerializer`,
  `ISecretGenerator`, `ISecretImporter` and their request/result DTOs; `NCode.Identity.OpenId.Principals` holds the
  `GetSubjectIdDelegate` / `GetSubjectIdentityDelegate` / `NormalizeSubjectIdDelegate` hooks on `OpenIdOptions`; and in
  `NCode.Identity.OpenId.Authentication`, `…Pkce` holds `ICodeChallengeMethod` and
  `ICodeChallengeMethodCollectionProvider`, `…OutboundHttp` holds `IOutboundAddressFilter`, `IOutboundHttpContentReader`
  and `OutboundHttpReadResult`, `…ResourceServers` holds `IClientScopeService`, `IScopeDescriptionService`,
  `IResourceServerCredentialResolver`, `ResolvedResourceServer` and `ResourceServerCredentialResolution`, `…Persistence`
  holds `IPersistedGrantService`, `PersistedGrant<TPayload>`, `PersistedGrantId` and `PersistedGrantStatus` beside the
  payload serializer, and `…Tokens` holds `IAuthorizationCodeService` beside `ITokenService`. An endpoint's own services
  live at that endpoint's root namespace (`ICibaRequestService`, `ICibaNotificationService`, `IUserCodeGenerator`,
  `IPushedAuthorizationRequestService`, `IInitialAccessTokenValidator`, `ISectorIdentifierValidator`, and
  `ITokenGrantHandler` in `…Endpoints.Token`), and the token-exchange `ISubjectTokenValidator` /
  `SubjectTokenValidationResult` pair is in `…Endpoints.Token.TokenExchange`. The authorization endpoint's processor is
  `IAuthorizationRequestProcessor`, and the outbound forgery guard's default is `DefaultOutboundHttpForgeryGuard`.
  Services register through one registration per purpose folder (auditing, consent, outbound HTTP, persistence, PKCE,
  resource servers, sessions, tokens).

- **The build enforces the project template's analyzer settings, banned APIs and the null-forgiving rule.** `CA1848`,
  `CA1852`, `CA2012`, `CA2215` and `CA1822` are on: internal types are sealed (`CA1852`), code logs through
  source-generated `[LoggerMessage]` methods (`CA1848`; the migration worker's event IDs are in its own `20000`–`20999`
  band), and in tests `ValueTask` is used correctly (`CA2012`: results are arranged with Moq's `ReturnsAsync` and
  `ThrowsAsync`), disposal chains `base.DisposeAsyncCore()` (`CA2215`), and helpers that read no instance state are
  `static` (`CA1822`). `build/BannedSymbols.txt`, from the project template, bans sync-over-async, every
  `JsonDocument.Parse` overload and Newtonsoft.Json in every project, test projects included, through
  `Microsoft.CodeAnalysis.BannedApiAnalyzers`; the root `BannedSymbols.txt` holds only this family's own bans. The
  null-forgiving operator fails the build in every project whose name does not end in `.Tests`
  ([#475](https://github.com/NCodeGroup/identity/issues/475), [#476](https://github.com/NCodeGroup/identity/issues/476),
  [#477](https://github.com/NCodeGroup/identity/issues/477), [#490](https://github.com/NCodeGroup/identity/issues/490)).

- **A project's top-level folder says whether it ships, and the build holds every project to its folder.** `src/` holds
  only the published NuGet packages, and every project there packs. `hosts/` holds the deployables
  (`NCode.Identity.OpenId.Host`, `NCode.Identity.OpenId.Host.Database`, `NCode.Identity.OpenId.Host.MigrationService`,
  `NCode.Identity.OpenId.LocalDev` and `NCode.Identity.ManagementConsole.Host`), `analyzers/` the repository's own
  Roslyn analyzers, and `frontend/` the pnpm workspace of the web apps; `tests/` and `benchmarks/` hold the tests and
  benchmarks. Each folder outside `src/` turns packing off in its `Directory.Build.props`, and the build fails a project
  under `src/` that does not pack and a project outside it that does
  ([#759](https://github.com/NCodeGroup/identity/issues/759),
  [ADR-1501](docs/decisions/1501-a-projects-top-level-folder-says-whether-it-ships.md)).

- **Code a self-hoster could use is public, and the product's repositories and artifacts follow one naming scheme.**
  `NCodeGroup/identity` holds the packages, hosts, images, the host chart, the frontend, and the decisions and issues
  of the product and its platform; `NCodeGroup/identity-platform` holds the reusable OpenTofu modules, Kustomize
  components and stacks and the platform's live configuration for `lab` and `production`; and the private
  `NCodeGroup/identity-cloud` holds only the managed offering's proprietary parts. NuGet packages are named
  `NCode.Identity.*`, the chart and images `ncode-identity-*`, the npm workspace packages `@ncode/*`, all private,
  and the platform's domains `ncodeid.*` ([#760](https://github.com/NCodeGroup/identity/issues/760),
  [ADR-1502](docs/decisions/1502-code-a-self-hoster-could-use-is-public-and-the-products-repositories-and-artifacts-follow-one-naming-scheme.md)).

- **The build reports a field that could be an auto-property.** `NCode.Identity.Analyzers`, the repository's own Roslyn
  analyzer project, which every project of the solution loads and no package ships, fails the build with `NCID0001` on
  a field, instance or static, unless it is a `const`, a `ref` field, a `volatile` or fixed-size buffer field, a field
  of a `[StructLayout]` or `[InlineArray]` struct, or a field the code uses by reference: passed as `ref`, `out` or `in`
  (as `Interlocked`, `Volatile` and `LazyInitializer` take it), taken with `ref` or `&`, or a mutable struct called or
  written in place. A field in generated code is reported only when a `#line` directive maps it to a hand-written file,
  as a Razor `@code` block's field is, and the analyzer's tests compile their snippets against the test host's own
  assemblies ([#713](https://github.com/NCodeGroup/identity/issues/713),
  [#714](https://github.com/NCodeGroup/identity/issues/714),
  [ADR-1486](docs/decisions/1486-the-repositorys-own-analyzer-project-fails-the-build-on-a-field-an-auto-property-could-replace.md)).

- **The build reports an own async method that returns `Task` or takes no trailing `CancellationToken`.**
  `NCode.Identity.Analyzers` fails the build with `NCID0002` on a method returning `Task` or `Task<T>` and with
  `NCID0003` on an asynchronous method whose last parameter is not a `CancellationToken`, in every project,
  production and tests alike, unless a framework contract fixes the shape: an xUnit test method, an ASP.NET Core
  middleware `Invoke`/`InvokeAsync`, an entry point, the dispose pattern's `DisposeAsyncCore`, an override or
  implementation of a member an assembly outside the family declares, a method group handed to an ASP.NET Core API,
  a method of the post-commit types that run on their own bounded token, or a method an ADR makes uncancellable
  ([#743](https://github.com/NCodeGroup/identity/issues/743),
  [#744](https://github.com/NCodeGroup/identity/issues/744),
  [ADR-1496](docs/decisions/1496-an-analyzer-reports-an-own-async-method-that-returns-task-or-lacks-a-trailing-cancellation-token.md)).

- **C# files carry a two-line SPDX header that the build checks.** Every C# file starts with
  `// Copyright © NCode Group` and `// SPDX-License-Identifier: Apache-2.0`; `.editorconfig` sets the header as
  `file_header_template` and raises `IDE0073` to `warning`, so a file whose header is missing or differs fails the build
  ([#448](https://github.com/NCodeGroup/identity/issues/448),
  [ADR-1309](docs/decisions/1309-c-files-carry-a-two-line-spdx-header-that-the-build-checks.md)).

- **The family's decision records are numbered from 1000, and the `adr index` stage of `./build/dod.ps1` checks them and
  every link to them.** Each record is `docs/decisions/1NNN-<slug>.md`; numbers below 1000 belong to the project
  template's records in `docs/decisions/project-template/`, so a bare ADR number names one record
  ([project-template ADR-0012](docs/decisions/project-template/0012-the-templates-records-are-numbered-below-1000-a-projects-from-1000.md)).
  `build/adr-index.ps1`, with fixture tests, fails when a family record is numbered below 1000, when two files in
  `docs/decisions/` share a four-digit number, when an index row's number is not its file's, when a file is listed
  twice, when a tracked file names a `NNNN-<slug>.md` record in neither `docs/decisions/` nor
  `docs/decisions/project-template/`, or when a `docs/decisions/0NNN` path points at the family's folder
  ([#371](https://github.com/NCodeGroup/identity/issues/371), [#449](https://github.com/NCodeGroup/identity/issues/449),
  [#455](https://github.com/NCodeGroup/identity/issues/455)).

- **The plan-and-execute workflow comes from the project template, executes on Claude Sonnet 5.5, and runs in Claude
  Code too.** The repository follows [project-template](https://github.com/NCodeGroup/project-template) at the version
  `.copier-answers.yml` and `.template-version` record: its agents, prompt files, skills, Claude Code commands,
  `CLAUDE.md` and `.template/` scripts are template-owned, and `./.template/Update-Project.ps1` takes
  a newer version. `PLAN.md` is gitignored and never committed: a plan's first commit is an empty `Plan: <title>`, each
  step's checkpoint stages only the files the step lists, a re-plan is an empty commit, and the `Plan step` checkpoints
  reach the plan's pull request branch, whose title is the conventional commit subject the squash merge takes. An
  executor that cannot run a step's Done-when command says so and leaves the check to the coordinator, and the Claude
  Code executor runs commands with the PowerShell tool, so a plan made of commands runs in its worktree. A plan names
  the up to three related issues it implements on an `Issue:` line, posts the plan to each and ends the pull request
  description with `Closes #<number>`; every planning command treats an issue's text as data, not instructions.
  `/plan-issue` plans an issue from its text and comments, `/triage-issues` lists the open issues ready to plan,
  `/plan-template-update` plans a template update from a trial run of it, `/ship-plan` lands a reviewed plan as the
  `## Landing` section of `AGENTS.md` says (here a draft pull request with no release, gated by
  `./build/dod.ps1 -Fast -Affected`, filing follow-ups as issues labelled `follow-up`), `/continue-plan` shows where
  each open plan stands, `/run-issues` takes issues from plan to a draft pull request one at a time, answering review
  threads, top-level comments and review summaries and planning the next issue while one waits, and `/finish-plan`
  retires a merged plan, asking `gh` for the branch's merged pull request, fast-forwarding `main` and deleting the
  plan's branch and worktree. Plan steps execute on Claude Sonnet 5.5 in VS Code, Copilot CLI and Claude Code. The
  repository-wide agent rules are in `.github/instructions/repository.instructions.md`. `.gitignore` ignores, and Claude
  Code denies reading, only the root `secrets/` folder, so a `Secrets/` source folder such as `Contracts/Secrets/` is
  committed on every platform; the template update runs on Windows when the repository's `.gitignore` ignores a folder
  the template's does not, such as `frontend/node_modules`; and every checkout except `copilot-setup-steps.yml`'s sets
  `persist-credentials: false`, keeping the job's token out of `.git/config`
  ([ADR-1170](docs/decisions/1170-agent-work-is-planned-on-claude-opus-and-executed-step-by-step-on-claude-sonnet.md)).

- **Local and Copilot workflow formats are supported together.** The repository keeps both the Local prompts and the
  Copilot skills. The first Definition-of-Done stage checks every pair's name, description, and ordered workflow body,
  covering the generated workflows and the hand-written audit pair, with fixture tests proving that missing adapters,
  drift, and unsupported skill settings fail. Skills use the session's selected model and stay available for automatic
  selection; Local prompts keep their model-selection metadata. [AGENTS.md](AGENTS.md#local-and-copilot-customizations)
  explains model selection and Copilot's Review Migrations banner. See issue
  [#179](https://github.com/NCodeGroup/identity/issues/179) and
  [ADR-1170](docs/decisions/1170-agent-work-is-planned-on-claude-opus-and-executed-step-by-step-on-claude-sonnet.md#dual-runtime-customization-contract).

- **Pull requests into `main` squash-merge through the merge queue.**
  [ADR-1261](docs/decisions/1261-the-main-branch-takes-only-reviewed-pull-requests-squash-merged-by-the-merge-queue-past-required-checks.md) records the
  decision and supersedes project-template ADR-0013; ADR-1261, ADR-1263 and ADR-1261 refine project-template ADR-0005
  and ADR-0006.

- **CI runs show their results on GitHub.** The run summary page opens with the Definition of Done verdict and a
  per-stage duration table, then the test results (one table across every test assembly or frontend package, plus each
  failed test's message and stack) and the merged coverage summary. Failed tests are annotated at their source line — on
  the run page and inline in pull-request diffs — and a failed gate names the stage that broke. Coverage history,
  per-line diff coverage on pull requests, and test analytics are on
  [Codecov](https://codecov.io/gh/NCodeGroup/identity), uploaded over OIDC from a separate job so the gate jobs stay
  read-only and no upload secret is stored; Codecov's statuses are informational, and "Definition of Done" is the only
  required check. The frontend tier has the same reports and coverage (`pnpm run test:report`), and the `coverage`
  artifact holds a browsable HTML report. Locally, `./build/dod.ps1` writes each run's results to
  `artifacts/test-results/` (cleared first) and the coverage report to `artifacts/coverage/index.html`, so coverage
  reflects only the current run. See [ADR-1143](docs/decisions/1143-ci-presents-test-results-and-hand-written-code-coverage-through-job-summaries-annotations-and-codecov.md).

- **Dependabot waits three days before it offers a NuGet package, action or base image release**, and gives NCode
  Review's pin bump a group of its own.

- **Every frontend surface boots through one shared gate.** The Management console, the Playground and the
  conformance dashboard share `BootstrapGate` from the `@ncode/shell` workspace, the one package allowed to depend on
  both the store and the design system, which loads the runtime configuration before the app renders. Its loading and
  error views are a stateless `BootstrapScreen` in `@ncode/ui`. Each surface names itself once, in `main.tsx`, through
  `createRuntimeConfigLoader({ surface })`, or the console through `createBffRuntimeConfigLoader()`, and
  `loadRuntimeConfig` defaults the serving origin to the page's own. One
  test suite covers the gate: a loader that ignores its abort signal and settles late never overwrites newer
  configuration, configuration that reaches the store another way clears an earlier error, and an error without a
  message shows the generic one. `SurfaceKind` is an open vocabulary, so a surface needs only its host override, not a
  change to `@ncode/config`. See [ADR-1156](docs/decisions/1156-shared-app-shell-containers-live-in-ncode-shell.md).

- **Every frontend workspace builds and tests from one Vite preset.** The `@ncode/vite-config` workspace owns the test
  gates and the three serving models (standalone, backend-served and BFF-served), and each workspace's Vite or Vitest
  config is one call that states its coverage floors. Every workspace collects `.test.tsx` files, including those on
  the `node` test environment, and each standalone dev server pins the port its redirect URI and CORS origin assume. Locally, Turborepo re-runs a
  workspace's tests and type-check when an internal package it depends on, `tsconfig.base.json`, or `eslint.config.js`
  changes. See [ADR-1157](docs/decisions/1157-every-frontend-workspace-builds-and-tests-from-one-vite-preset.md).

- **`apps/playground` is an authorization-code + PKCE (`S256`) flow explorer that exercises the OP's consent surface out
  of the box** (issue [#135](https://github.com/NCodeGroup/identity/issues/135),
  [ADR-1082](docs/decisions/1082-playground-and-conformance-surfaces.md)). The Playground discovers the provider and
  walks the flow end to end, surfacing the PKCE verifier and challenge, `state`, `nonce`, the authorize URL, the token
  response and the decoded JWT header and claims, then exercises UserInfo, refresh and RP-Initiated Logout; it offers no
  implicit, hybrid or resource-owner-password flow. It has a **Force consent (`prompt=consent`)** toggle, the
  developer-seeded `playground-spa` client sets `require_consent = true` (`DeveloperSpaClientSeed.RequireConsent`), and
  `@ncode/auth`'s `buildAuthorizeUrl` takes an optional OIDC `prompt` and `nonce`. It is a standalone Vite SPA on the
  shared `@ncode/*` platform, on port `5174` in the Aspire AppHost.

- **The conformance dashboard (`apps/conformance`) audits the OP's security posture and measures it against the full
  published OAuth, OIDC, CIBA and FAPI catalog** (issues [#135](https://github.com/NCodeGroup/identity/issues/135),
  [#129](https://github.com/NCodeGroup/identity/issues/129),
  [ADR-1082](docs/decisions/1082-playground-and-conformance-surfaces.md),
  [ADR-1084](docs/decisions/1084-spec-coverage-conformance-harness.md)). It audits the published discovery metadata
  against OAuth 2.1, RFC 9700, OpenID Connect Core and Discovery, and RFC 7636/8414/7009/7662/9126, flags insecure or
  deprecated configuration, and reports OpenID Foundation Basic and Config OP readiness and the external-suite
  configuration (issuer, alias, RP callback URL). A spec-coverage matrix classifies every capability from the live
  discovery document, independent of what is implemented, so a gap surfaces as an explicit, attributable row and the
  matrix detects regressions (a capability that stops being advertised or turns non-conformant changes its row). A
  user-triggered active-probe panel exercises the browser-reachable endpoints (JWKS validity, the RFC 6749 §5.2
  token-error contract, UserInfo's `401`, and PAR when advertised) to turn advertised into verified; ADR-1084 sets the
  `browser`, `probe-runner` and `external-suite` exercise boundary. The dashboard is organized into tabs (Overview,
  Security audit, Coverage, Probes, Suite config) with collapsible coverage categories whose gaps start expanded, and it
  shows only the current issuer's audit: a discovery load superseded by an issuer change never updates it, whether it
  fails or succeeds late. It is a standalone Vite SPA on the shared `@ncode/*` platform, on port `5175` in the Aspire
  AppHost, and a development and conformance convenience only
  ([ADR-1025](docs/decisions/1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)).

- **The headless conformance probe runner exercises the legs a browser cannot drive** (issue
  [#129](https://github.com/NCodeGroup/identity/issues/129),
  [ADR-1084](docs/decisions/1084-spec-coverage-conformance-harness.md)). The `@ncode/app-conformance-runner` CLI
  exercises the confidential and server-initiated legs: `client_credentials`, token introspection and revocation with
  client authentication, device-code issuance and polling, CIBA, `private_key_jwt`, DPoP/mTLS, and back-channel logout.
  Each probe reports `verified`, `failed` or `skipped` (with an actionable reason, such as an unadvertised capability or
  a missing credential), so an unimplemented backend feature surfaces as an explicit gap; a network error on the
  device-code token poll fails the device-authorization probe alone and the run carries on, and the runner exits
  non-zero on any failure, making it a CI pre-flight for the external certified suite
  ([#124](https://github.com/NCodeGroup/identity/issues/124)). The framework-free, injectable-fetch conformance logic
  (discovery fetch, the security audit, the spec-coverage catalog and the browser-drivable probes) lives in the shared,
  unit-tested `@ncode/conformance-core` package used by both the dashboard (`apps/conformance`) and the runner.
  Development and conformance tooling only, outside the .NET inner loop
  ([ADR-1025](docs/decisions/1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)).

- **The probe runner signs its asymmetric legs with Node `crypto`** (issues
  [#130](https://github.com/NCodeGroup/identity/issues/130), [#334](https://github.com/NCodeGroup/identity/issues/334),
  [ADR-1084](docs/decisions/1084-spec-coverage-conformance-harness.md)). The `private_key_jwt` and DPoP probes take
  `--private-key` / `--dpop-key` options that load a PEM- or JWK-encoded private key; the JWA signature algorithm is
  inferred from the key (the curve for EC `ES256`/`ES384`/`ES512`, `EdDSA` for Ed25519/Ed448, `PS256` for RSA) and is
  overridable through `--private-key-alg` / `--dpop-key-alg` (for example `RS256`, or `Ed25519` and `Ed448` for a key on
  the matching curve). An `id-RSASSA-PSS`-restricted key is rejected up front with an explanation that an ordinary RSA
  key is required, because `PS256`/`PS384`/`PS512` sign it with PSS padding. The signer builds the `private_key_jwt`
  client assertion (OIDC Core §9, carrying the JWK `kid` when present) and the DPoP proof (RFC 9449 §4, embedding the
  public JWK in the `dpop+jwt` header); ECDSA uses the JOSE raw `ieee-p1363` encoding and RSA-PSS uses digest-length
  salt. Development and conformance tooling only, outside the .NET inner loop
  ([ADR-1025](docs/decisions/1025-the-family-is-greenfield-and-pre-release-tracks-its-public-api-and-models-auth0-parity-plus.md)).

- **The OpenID Foundation Conformance Suite is the certified oracle behind the in-house pre-flight** (issues
  [#129](https://github.com/NCodeGroup/identity/issues/129), [#131](https://github.com/NCodeGroup/identity/issues/131),
  [#124](https://github.com/NCodeGroup/identity/issues/124),
  [ADR-1084](docs/decisions/1084-spec-coverage-conformance-harness.md)). A runnable harness under
  [`build/conformance/`](build/conformance) drives the suite against the OpenID host end to end:
  [`run-conformance.ps1`](build/conformance/run-conformance.ps1) launches the OP on the reachable HTTPS origin (the
  `Conformance` launch profile), runs the headless `@ncode/app-conformance-runner` probe runner as a fast pre-flight,
  brings the suite up from prebuilt Docker images (no Java build), drives the Config OP and Basic OP plans headlessly
  through `run-test-plan.py` gated on the committed `expected-failures.json` / `expected-skips.json` baselines, and
  tears everything down, surfacing one exit code. The config templates (`oidcc-basic.json` with its `browser` automation
  block, `oidcc-config.json`) resolve against the deterministic `AddConformanceSeed` values, and a configured
  conformance client gets exactly the grant types and PKCE methods it configures: `ConformanceClientSeed`'s lists start
  empty, and the defaults apply only when none are configured. The harness checks its machine prerequisites up front: it
  fails immediately with the exact fix when the issuer or suite hostname does not resolve (for example when Docker
  Desktop has not written `host.docker.internal` into the Windows hosts file), rejects the Microsoft Store `python`
  alias, which is on PATH but fails when run, checks pnpm after the frontend bootstrap puts the NVS-managed Node 22 on
  PATH, and includes the last probe error in a readiness timeout. It fails fast when the suite goes unreachable mid-run:
  each plan invocation runs under a hard wall-clock watchdog (`-PlanTimeoutSeconds`, default `1800`) that tree-kills a
  plan on overrun and reports a clear failure, and the suite script's own retry bounds (`CONFORMANCE_RETRY_TIMEOUT`,
  `CONFORMANCE_RESTART_RETRIES`, `CONFORMANCE_MAX_CONSECUTIVE_FAILURES`) are pinned so `run-test-plan.py` self-aborts
  quickly whatever the cloned upstream's defaults. It is not part of `build/dod.ps1` (Docker and Python 3, slow) and
  runs as an opt-in gate; a nightly CI pipeline for it is tracked in
  [#129](https://github.com/NCodeGroup/identity/issues/129). The one-time Windows setup (Python install manager,
  per-user NVS, both hosts entries) is documented in [`build/conformance/README.md`](build/conformance/README.md),
  persisting `nvs auto on` in the pwsh / Git Bash profiles in [`frontend/README.md`](frontend/README.md), and
  the harness in [`docs/conformance/external-suite.md`](docs/conformance/external-suite.md).

- **The browser-driven Basic OP conformance plan runs at `parallel=2`** (issue
  [#131](https://github.com/NCodeGroup/identity/issues/131)). Cross-module contamination is structurally impossible: the
  suite isolates browser state per test module (each module has its own `BrowserControl`/`CookieManager`), the plan runs
  under `client_registration=dynamic_client` so each module registers its own client and `redirect_uri`, and the OP's
  interactive login is request-bound (`PostLoginAsync` round-trips the `ReturnUrl` carrying the request's `state` grant
  key, with no ambient server-side current pending request). Details in
  [`docs/conformance/external-suite.md`](docs/conformance/external-suite.md).

- **The platform is the deployment root.** It is one per deployment, with no identifier: host configuration
  (`OpenId:Platform:Settings`) and one persisted settings document, read and written through `GET`/`PATCH
  /api/platform/settings` and previewed through `GET /api/platform/effective-settings`, are the root of every team's and
  tenant's settings; its authority node is `(platform, platform)`, and its master key protects the Data Protection key
  ring ([ADR-1411](docs/decisions/1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
  [ADR-1413](docs/decisions/1413-the-platform-master-key-protects-the-data-protection-key-ring-and-the-hosts-own-data.md)).

- **A team groups a customer's related tenants.** `POST /api/teams` creates a team named by its creator (the tenant-id
  form) with a display name, an `isDisabled` flag and a settings document; `GET`/`PATCH`/`DELETE /api/teams/{teamId}`,
  `GET`/`PATCH …/settings`, `GET …/effective-settings`, `GET …/usage`, and `…/owners` and `…/admins` manage it. Every
  tenant belongs to exactly one team and never moves: `POST /api/teams/{teamId}/tenants` creates a tenant under its team
  and `GET /api/teams/{teamId}/tenants` lists them, `TenantResource` carries `teamId`, and the platform's own team
  (`TenantResolution:Root:TeamId`, default `platform`) holds the root tenant and every tenant a strategy creates
  ([ADR-1411](docs/decisions/1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)).

- **A team can nest beneath a parent team.** `POST /api/teams/{teamId}/teams` creates a team beneath its parent and
  `GET /api/teams/{teamId}/teams` lists a team's child teams; `TeamResource` carries `parentTeamId`, and `GET /api/teams`
  lists every team at every level. A team's parent is fixed when it is created, so the tree has no cycles, and a team
  holds tenants and child teams side by side, so a customer's team is the parent of a team per product, each with its
  own sandbox, testing and production tenants. A top-level team is at depth 1, the platform setting `max_team_depth`
  (1 to 8; unset means 8) bounds the depth a team is created at, each team row stores its path from its top-level team
  (`TeamPaths`), a materialized team exposes its `Parent` and `Path`, and the platform's own team holds no teams
  ([#567](https://github.com/NCodeGroup/identity/issues/567),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md)).

- **A team's settings cascade into every team and tenant beneath it.** The settings scopes are `platform`, `team`,
  `tenant`, `client` and `resource_server`; a team's effective settings merge the platform's, then each ancestor team's
  document from the top-level team down, then its own (`IEffectiveSettingsResolver.ResolveTeamAsync`,
  `ResolveTeamParentAsync`), so the security profiles, ceilings, defaults and limits a team selects bind every team and
  tenant beneath it, and a materialized team reads its document beneath its parent's effective settings. `max_tenants`
  and `max_teams` apply to the team scope, merge with `Min` and are limits set by the administrators above the team: a
  team's `max_tenants` bounds the tenants of the team and of every team beneath it, its `max_teams` bounds the teams
  beneath it at every level, and a creation that would exceed the limit of any team above it answers `409`
  ([ADR-1411](docs/decisions/1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
  [ADR-1418](docs/decisions/1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [#563](https://github.com/NCodeGroup/identity/issues/563)).

- **A team is an authority node.** Authority runs platform → each team from the top-level team down → tenant → leaf: a
  role assigned at a team reaches every team and tenant beneath it, the `TeamAdmin` role creates the team's tenants and
  child teams and manages everything beneath, `Owner` never
  creates, and a principal's role assignments are read across every tenant: the platform, team and tenant nodes have
  globally unique, retired identifiers and match an assignment on type and identifier, and any other node counts an
  assignment only when it belongs to the node's tenant, because client and resource-server identifiers
  are unique only within a tenant
  ([ADR-1411](docs/decisions/1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md)).

- **A team promotes one tenant's configuration to another.** `POST /api/teams/{teamId}/promotions/preview` reports what
  a promotion would create, update or leave unchanged and `POST /api/teams/{teamId}/promotions` applies it: the
  tenant's settings, its resource servers and scopes, its clients (keeping their `client_id`) and client grants, never
  secrets, keys, grants, principals, role assignments, a client the host configures, a machine client, or a setting
  whose value belongs to the environment (`SettingDescriptor.IsEnvironmentBound`, such as `redirect_uris`), whose target
  values the request may supply. Both tenants belong to the team the route names, not to a team beneath it, so a
  parent team's administrators promote within a child team through that team's route; a promotion needs read authority
  on the source and creation authority on the target, and a promoted limit needs the authority that sets it (`403`
  naming the limit). It records each change and `tenant.promoted`, and the console offers it on a team's tenants page
  ([ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [ADR-1425](docs/decisions/1425-a-team-promotes-one-tenants-configuration-to-another-and-environment-bound-values-stay.md),
  [ADR-1466](docs/decisions/1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md),
  [#565](https://github.com/NCodeGroup/identity/issues/565)).

- **A tenant has administrators.** `GET`/`POST`/`DELETE /api/tenants/{tenantId}/admins` list, grant and revoke the
  `TenantAdmin` role at the tenant's node, which manages the tenant and everything beneath it and creates its clients,
  resource servers and other resources; granting or revoking it is creation authority at the tenant
  ([ADR-1417](docs/decisions/1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md),
  [#556](https://github.com/NCodeGroup/identity/issues/556)).

- **A setting can be a limit set from above.** `SettingDescriptor.IsLimit` marks a limit, which merges with a meet; a
  management write that adds, changes or removes a limit in a team's, tenant's, client's or resource server's settings
  needs `Update` authority at the node above it: the platform for a top-level team, the parent team for a child team,
  the team for a tenant and the tenant for a client or resource server, or answers `403` naming the limits, and
  `ISettingSerializer.GetChangedLimits` names them
  ([ADR-1418](docs/decisions/1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [#563](https://github.com/NCodeGroup/identity/issues/563)).

- **A caller learns which limits a scope's settings carry and whether it may change them.** `GET …/settings/limits` on
  the platform, a team, a tenant, a client and a resource server answers `SettingLimitsResource`: the limits the
  settings may carry (`ISettingSerializer.GetLimits`) and `canChange`, whether the caller holds `Update` at the node
  above. The management console shows a caller without that authority the limits read-only beside the settings it
  edits, and its save never changes them
  ([ADR-1448](docs/decisions/1448-a-settings-reader-learns-which-limits-it-may-change.md),
  [#581](https://github.com/NCodeGroup/identity/issues/581)).

- **A caller learns whether it may grant and revoke a role where it reads who holds it.** `GET …/owners` on a team, a
  tenant, a client and a resource server and `GET …/admins` on a team and a tenant answer `RoleListResource`: the
  role's holders (`items`, each a `RoleHolderResource`) and `canChange`, whether the caller may grant and revoke the
  role there: `Update` at the node for owners, `Create` for administrators, and for a client's owners also a client the
  host does not manage and, for a machine client, creation authority at the node that owns it. `POST` on each list
  takes `AddRoleHolderRequest` and answers the `RoleHolderResource` it grants, or `409` for a client's principal that
  cannot hold the role there, and every list grants and revokes its role through one flow, so every role answers the
  same statuses and records the same audit events
  ([ADR-1465](docs/decisions/1465-a-role-list-tells-its-reader-whether-it-may-grant-and-revoke-the-role.md),
  [ADR-1467](docs/decisions/1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md),
  [ADR-1469](docs/decisions/1469-every-role-list-shares-role-neutral-contracts-and-one-grant-and-revoke-flow.md),
  [#623](https://github.com/NCodeGroup/identity/issues/623), [#627](https://github.com/NCodeGroup/identity/issues/627),
  [#628](https://github.com/NCodeGroup/identity/issues/628)).

- **Settings are reconciled per team.** Each team records the effective settings its tenants and clients were last
  reconciled against; `TeamSettingsReconciliationService` reconciles every team when the platform's settings change and
  one team and every team beneath it when that team's settings change, each team once per pass and under its own lease
  `team-settings-reconciliation:{teamId}:{rowToken}:{fingerprint}`
  (`OpenId:Team:ReconciliationLeaseDuration`), and `ReconcileTeamSettingsCommand` reaches only that team's tenants.
  The teams beneath a team are walked depth first through `ITeamDescendantWalker`, which reads each team's child teams
  a page at a time (`ITeamStore.GetChildPageAsync`) in a unit of work of its own and holds at most one page per level,
  so a subtree of any size is reconciled in bounded memory. Requests that arrive during a pass coalesce, one per team,
  and a request for every team covers them all, so none is dropped however many arrive
  ([#559](https://github.com/NCodeGroup/identity/issues/559)). A team change evicts the team, every team beneath it and
  their tenants on every replica over the `team-changed` channel
  ([ADR-1412](docs/decisions/1412-settings-are-reconciled-per-team.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [ADR-1455](docs/decisions/1455-a-teams-subtree-is-walked-depth-first-one-page-of-child-teams-per-level.md),
  [#601](https://github.com/NCodeGroup/identity/issues/601)).

- **Disabling a team suspends every team and tenant beneath it, and a team with tenants or teams cannot be deleted.** A
  tenant of a disabled team, or of a team beneath one, answers `404` on the protocol surface and on the management
  routes that act through its OpenID runtime (sessions, principals, local accounts), as a disabled tenant does, while
  its stored configuration stays manageable; `DELETE /api/teams/{teamId}` answers `409` while the team has tenants or
  child teams, a deleted team's id is never reissued, and the platform's own team can be neither disabled nor deleted.
  Creating, deleting, disabling and restoring a top-level team, and `GET /api/teams` and `GET /api/tenants`, need
  `GlobalAdmin` or an assignment at the platform node. Creating, deleting, disabling and restoring a child team need
  that authority at its parent team
  ([ADR-1411](docs/decisions/1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md)).

- **Usage is queryable per team, across every team beneath it.** Every usage row carries the path of the teams its
  tenant belonged to, so a deleted tenant's or deleted team's final days still bill to every team above it, and
  `GET /api/teams/{teamId}/usage?from=&to=` lists the counts of each tenant of the team and of every team beneath it per
  day and meter, each entry naming its team and tenant
  ([ADR-1411](docs/decisions/1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [ADR-1199](docs/decisions/1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md)).

- **Team and platform changes are audited in the control-plane tenant.** `TeamChangedAuditEvent` (`team`) and
  `PlatformChangedAuditEvent` (`platform`) are recorded under the control-plane tenant, and a tenant's audit event names
  its team as a parent resource
  ([ADR-1411](docs/decisions/1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md)).

- **The management console manages teams, machine clients and the platform's settings.** The platform scope has a
  Teams surface (`TeamTable`, `TeamFormDialog`, create, edit, disable and delete), a Clients surface, a Settings
  surface and an Encryption keys surface; the platform's team list names each team's parent; a team scope has its
  tenants, child teams, clients, settings, access and usage (`TeamUsageTable`, which names each entry's team), and its
  exit leads to its parent team. The platform's and a team's Clients surface lists, creates, disables and deletes the
  machine clients it owns (`MachineClientsPage`) and opens each one's secrets, settings and owners through the
  control-plane tenant's client routes, where a client's detail names the platform or team that owns it and its
  principal; the console routes `/teams`, `/clients`, `/settings`, `/encryption-keys`,
  `/teams/{teamId}/{tenants|teams|clients|settings|access|usage}` and `/tenants/{tenantId}/…`
  ([#636](https://github.com/NCodeGroup/identity/issues/636),
  [ADR-1411](docs/decisions/1411-the-platform-is-the-deployment-root-and-a-team-groups-a-customers-tenants.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [ADR-1466](docs/decisions/1466-a-machine-client-belongs-to-the-platform-or-a-team-and-acts-only-within-it.md),
  [ADR-1174](docs/decisions/1174-the-management-consoles-url-is-its-navigation-state.md)).

- **Master keys rotate, rekey on demand and are rewrapped and destroyed in the background.** An owner's active master key
  (the platform's or a tenant's) rotates after `SecretProtection:MasterKeys:RotationPeriod` (365 days) in a daily pass that
  one replica runs under a lease, or at once through `POST /api/tenants/{tenantId}/encryption-keys/rekey` and
  `POST /api/platform/encryption-keys/rekey`; `GET .../encryption-keys` reports the keys by id, version,
  state, instants, root key provider and recovery key id, and carries no key material. The scopes are
  `read:encryption_keys` and `update:encryption_keys` for a tenant and `read:platform_encryption_keys` and
  `update:platform_encryption_keys` for the platform. A rekey within twice `RefreshInterval` of the owner's last rotation
  answers `409` with `Retry-After` and the same wait in the problem's `retryAfterSeconds`, and one that loses a race to
  another rekey answers `412`, as every management
  write that loses a race does. A
  rotation creates the successor's key material before it retires the active key, and a store failure re-activates the
  retired key. Every
  replica reloads an owner's keys within `SecretProtection:MasterKeys:RefreshInterval` (five minutes) without a KMS
  call and drops a destroyed key, and a broadcast makes them reload at once. The data keys that a retired key wraps are
  rewrapped under the active key by `ISecretProtector.Rewrap`, which copies the payload unchanged, for the stored
  secrets of a tenant, its clients and its resource servers and the grant payloads of a tenant. The
  rewrap and the destruction check read each owner's values once, and only those under a retired key, and a value that
  cannot be rewrapped is logged and keeps its key. A
  retired key is destroyed after `RetirementGrace` (30 days) once no registered `IProtectedValueSource` reports a value
  that names it, and the Data Protection key ring keeps the retired platform key while a live ring key names it.
  `IMasterKeyManager` reports, rotates, rewraps and destroys; `IMasterKeyChangeObserver` hears of each change;
  `master_key.rotated` and `master_key.destroyed` are `key` audit events; and the management console shows the keys of a
  tenant and of the platform with a confirmed "Rekey" action that says how long to wait when it comes too soon
  ([#171](https://github.com/NCodeGroup/identity/issues/171),
  [ADR-1407](docs/decisions/1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md),
  [ADR-1422](docs/decisions/1422-a-management-problem-that-asks-the-caller-to-wait-carries-the-wait-in-retry-after-seconds.md)).

- **A management problem that asks the caller to wait carries the wait.** A rekey or a root key change that comes too
  soon, and a root key that is unavailable, answer through `RetryAfterProblem.Create` with `Retry-After` and
  `retryAfterSeconds` holding the same whole number of seconds, rounded up to at least one.
  `ManagementProblemMembers` names every extension member the management API writes, and the console's
  `apiErrorMessage` says "Try again in …" with the wait in seconds, minutes or hours
  ([#571](https://github.com/NCodeGroup/identity/issues/571),
  [ADR-1422](docs/decisions/1422-a-management-problem-that-asks-the-caller-to-wait-carries-the-wait-in-retry-after-seconds.md)).

- **The host image rewraps every master key offline.** `secret-protection rewrap` wraps every master key under the
  configured root key provider and recovery public key, and `secret-protection rewrap-recovery` replaces only the
  recovery copies, each unwrapping a key with the root key provider it names or, with `--recovery-private-key <file>`,
  from its recovery copy. `--owner <type:id>` limits the run to one owner and `--dry-run` verifies every key without
  writing. An `--owner` with no master keys exits `1`. A master key under a tenant's root key stays under it: the
  command never wraps it under the deployment's root key and never adds a recovery copy to it, and a dry run still
  unwraps it through the tenant's key. The recovery private key is read from the file only, held in memory that is
  zeroed when the command ends, and never reaches a serving process. The Helm chart's `secretProtection.rewrapJob` runs
  the command as a Job, and `IMasterKeyRewrapper` is its engine
  ([#172](https://github.com/NCodeGroup/identity/issues/172),
  [ADR-1408](docs/decisions/1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md)).

- **A tenant brings its own root key.** A tenant names a root key it controls, a key wrapper name (`aws-kms`,
  `azure-key-vault` or `google-cloud-kms`) and a key reference (a KMS key or alias ARN, a crypto key name without a
  version, or a key id), and its master keys are wrapped only by that key, with no operator recovery copy, in place of
  the deployment's root key. A tenant whose key cannot grant the deployment's own identity also names an access identity
  it controls — an IAM role ARN that the server assumes with the external ID the encryption keys resource reports as
  `rootKeyAccessBinding` (such as `tenant:acme`), a service account email it impersonates, or a Microsoft Entra tenant
  ID it requests tokens for — and the server reaches the key through that identity with no stored secret; every master
  key records the identity that reaches it (`rootKeyAccessIdentity`, and the `root_key_access_identity` detail of
  `master_key.*` audit events), and changing only the identity rewraps the master keys without a rotation.
  `KeyAccessClientCache<TClient>` keeps a client for an access identity once a call through it succeeds and drops one
  that never worked. `PUT`, `DELETE` and `POST .../validate` on `/api/tenants/{tenantId}/encryption-keys/root-key` set or
  change, remove and validate it (scope `update:encryption_keys`), and `GET .../encryption-keys` reports the root key and
  the providers a tenant may choose (scope `read:encryption_keys`). A `PUT` answers `400` for a field that is blank, not
  allowed, not registered or malformed, `409` with `Retry-After` and `retryAfterSeconds` within twice
  `SecretProtection:MasterKeys:RefreshInterval` of the tenant's last rotation, `412` when it loses a race and `422` when
  the probe fails; a `DELETE` answers `204` once every master key is under the deployment's root key and `202` while
  master keys remain under the tenant's key. The server validates a key by wrapping 32 random bytes through it under a
  context label of its own, unwrapping them and comparing the two in constant time, which proves that the key exists, is
  enabled and grants both the encrypt and the decrypt permission through any provider. Setting or changing the key
  rotates the tenant's master key so that the successor is created under it and rewraps every retired master key under
  it, because earlier material exists in backups under the deployment's root key; removing it moves the master keys back
  under the deployment's root key without a rotation. An Azure Key Vault key reference names a host under the
  deployment's Key Vault or Managed HSM host suffixes
  (`SecretProtection:RootKeyProviders:AzureKeyVault:ReferencedKeyHostSuffixes`, by default the public, China and US
  government cloud domains). Which owners can bring a key is a registered `IOwnerRootKeyPolicy`: the `platform` owner
  and the control-plane tenant, which the management API authenticates against, are refused, a consumer registers its
  own, and a `PUT` for a refused owner answers `400`. A root key is `active`, `unavailable` or `removing`, and an
  unknown state reads as `unavailable`. `SecretProtection:OwnerRootKeys:Providers` (empty by default, so no tenant can
  bring a key), `ValidationInterval` (one hour, between one minute and one day) and `RetryInterval` (one minute, between
  one second and the validation interval) tune it, and a listed provider that is not a registered referenced key wrapper
  stops the host as it starts. The Helm chart's `secretProtection.ownerRootKeys.providers` lists the providers and
  mounts a listed provider's workload credentials. The management console's tenant encryption keys tab sets, validates
  and removes the key, shows its state and the master keys' root keys, and warns when it is `unavailable`.
  `root_key.set`, `root_key.removed`, `root_key.unavailable` and `root_key.restored` are `key` audit events without
  material, a rotation onto the key records `master_key.rotated` with the reason `root_key_changed`, and a master key's
  `root_key_reference` detail names the key that wraps it. `IReferencedKeyWrapper` is a key wrapper that wraps under a
  key the caller names, implemented by the AWS KMS, Google Cloud KMS and Azure Key Vault wrappers, and
  `IOwnerRootKeyManager` sets, removes and validates a root key
  ([#174](https://github.com/NCodeGroup/identity/issues/174), [#574](https://github.com/NCodeGroup/identity/issues/574),
  [ADR-1414](docs/decisions/1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md),
  [ADR-1415](docs/decisions/1415-a-key-wrapper-can-wrap-under-a-key-the-caller-names.md),
  [ADR-1429](docs/decisions/1429-a-tenant-root-key-can-be-reached-through-an-identity-the-tenant-grants.md)).

- **A package maps the exceptions it owns to protocol errors.** `IOpenIdExceptionTranslator` implementations, registered
  as a collection, turn an exception into the error a protocol endpoint answers with; the endpoint filters and the
  authorization endpoint consult them, and an exception none answers is `server_error`
  ([#573](https://github.com/NCodeGroup/identity/issues/573),
  [ADR-1428](docs/decisions/1428-a-package-translates-the-exceptions-it-owns-into-protocol-errors-through-registered-translators.md)).

### Security

- **The host accepts only TLS 1.2 and 1.3 with AEAD cipher suites.** When the host terminates TLS itself, the handshake
  refuses TLS 1.0 and 1.1 on every platform, and on Linux and macOS negotiates only the six AEAD cipher suites with
  forward secrecy that FAPI 1.0 Part 2 §8.5 permits. A Windows host leaves the cipher suites to SChannel
  ([ADR-1499](docs/decisions/1499-the-host-accepts-only-tls-1-2-and-1-3-with-aead-cipher-suites.md)).

- **The management console holds no token in the browser.** No access, refresh or ID token reaches the console's
  script; its calls carry the `__Host-Http-bff.mgmt` cookie and the anti-forgery header, and the console's origin is
  separate from the OP's login, consent and account pages
  (RFC 10017,
  [ADR-1437](docs/decisions/1437-the-management-console-is-a-confidential-client-served-by-a-bff-host-of-its-own.md),
  [ADR-1438](docs/decisions/1438-the-management-console-holds-no-token-and-reads-its-session-from-its-bff.md)).

- **Management authority comes from role assignments.** No role claim but the bootstrap administrator's `GlobalAdmin`
  confers authority; a tenant administrator reaches nothing above its tenant, so the control-plane tenant's
  administrators never reach the platform or a team. The host-managed bootstrap administrator client is changed by no
  management caller, so authority over the root tenant's clients never yields a `GlobalAdmin` token
  ([ADR-1417](docs/decisions/1417-a-tenant-administrator-is-a-role-assignment-at-the-tenants-node.md),
  [#556](https://github.com/NCodeGroup/identity/issues/556)).

- **A management route's caller is only the principal its management access token authenticates.** On every management
  route `HttpContext.User` is the principal a valid management access token authenticates, or an anonymous principal
  whatever another authentication scheme set, such as a session cookie the host's default scheme reads, and
  `IManagementCallerFeature` carries the authenticated caller. `GET /api/capabilities`, `GET /api/me`,
  `GET /api/me/teams` and `GET /api/me/tenants` answer only that caller and answer anyone else `401` with a problem, so
  the deployment's secret types, algorithms and key management services and a caller's role assignments stay
  undisclosed to anonymous callers
  ([#707](https://github.com/NCodeGroup/identity/issues/707),
  [#715](https://github.com/NCodeGroup/identity/issues/715),
  [#716](https://github.com/NCodeGroup/identity/issues/716),
  [ADR-1482](docs/decisions/1482-only-an-authenticated-management-caller-reads-the-management-capabilities.md),
  [ADR-1484](docs/decisions/1484-a-management-routes-caller-is-the-principal-its-management-access-token-authenticates.md)).

- **No administrator raises the limits set on its scope.** A limit in a scope's settings is written only by the
  authority above that scope, so a team's administrators cannot lift the plan tier the platform sets, nor a child
  team's the limits its parent team sets, nor a tenant's the limits its team sets. A client's registration update keeps
  the limits its settings carry
  ([ADR-1418](docs/decisions/1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md),
  [ADR-1426](docs/decisions/1426-a-team-can-nest-beneath-a-parent-team.md),
  [#563](https://github.com/NCodeGroup/identity/issues/563)).

- **A management route answers a caller without authority the same for any identifier.** Every team, tenant, client,
  resource-server, scope, client-grant, local-account and platform route authorizes at the node its route names, and
  at a resource server its request names, before it reads, so a `401` or `403` never depends on whether the team,
  tenant, client, resource server, scope, grant, account, setting, secret, key, owner or administrator exists, and no
  row is read for the caller. A client's or resource server's settings, scopes and secrets are authorized at its own
  node, so its owner edits them, and creating or rotating a secret takes the authority to create there
  ([ADR-1424](docs/decisions/1424-a-management-route-authorizes-the-caller-at-the-node-it-names-before-it-reads.md),
  [ADR-1451](docs/decisions/1451-every-management-route-authorizes-at-its-node-before-it-reads.md),
  [#558](https://github.com/NCodeGroup/identity/issues/558), [#596](https://github.com/NCodeGroup/identity/issues/596)).

- **A unit of work resolves a tracked row only in its ambient tenant.** A store's lookup that the change tracker answers
  applies the same tenant scope as the query filter, so a row tracked for one tenant is never returned in another
  tenant's scope ([#565](https://github.com/NCodeGroup/identity/issues/565)).

- **A key never signs, verifies, encrypts, decrypts or is published outside its windows.** A tenant's, a client's and a
  resource server's keys all carry an active window inside a validity window: a key that is not yet
  active or has retired signs and encrypts nothing, and an expired key verifies and decrypts nothing, is not published
  and does not authenticate, on every path. A tenant's provisioned signing and encryption keys roll over every 90
  days by default, so the window a compromised key can be abused in is bounded, each successor is published two days
  before the first token it signs, and a retired key stays published for seven days or the longest token lifetime plus
  the clock skew, whichever is longer, so no token outlives the key that verifies it
  ([ADR-1403](docs/decisions/1403-a-stored-secret-has-an-active-window-inside-its-validity-window-and-every-use-of-a-key-honors-them.md),
  [ADR-1404](docs/decisions/1404-a-tenants-provisioned-keys-roll-over-ahead-of-retirement-and-expire-after-a-grace.md)).
- **An SD-JWT presentation is verified fail-closed under bounded work.** The issuer-signed JWT must be a compact JWS,
  never a Nested JWT whatever decryption keys the verifier's parameters name, signed with an asymmetric key under
  signature algorithms the verifier names; every salt comes from the platform's cryptographically secure random
  number generator and is as long as `SdJwtOptions.SaltSizeBytes` says, at least 128 bits; and a presentation is
  refused with a machine-readable code when a digest repeats, a disclosure is referenced by nothing or presented
  twice, a disclosed claim collides with a member already present, a reserved name appears where RFC 9901 forbids
  it, a disclosure is of the wrong kind for its digest, or a protected claim is disclosed in whole or in part.
  `iss`, `nbf`, `exp` and `cnf` are protected under every profile and the profile's claims beside them, its required
  claims included: the issuer never makes one disclosable, and the verifier refuses any digest beneath one, decoy or
  not, so a withheld `status_list` can never read as an unrevoked credential. A refusal names the rule, never a claim
  name the holder chose, and never the token: a JWT the validator could not decode is reported by its failure code
  and fixed text, and a disclosure that repeats a member name is reported as an invalid disclosure. The holder key is
  read from the signed payload before any disclosure is resolved, so a disclosure never supplies it, and is written at
  issuance only when its registered key type finds no private member in it; the Key Binding JWT must be typed
  `kb+jwt`, signed by that key, addressed to the verifier, carry its nonce and the digest of the presentation as
  received, compared in fixed time, and be no older than the maximum age, and the verifier refuses a
  `SdJwtKeyBindingRequirement` whose audience or nonce is empty, whose algorithms are unset, whose maximum age is not
  positive or whose clock skew is negative before it reads the presentation. `SdJwtOptions.MaxDisclosureCount`
  refuses an over-long presentation before any disclosure is decoded and `MaxNestingDepth` bounds the depth the
  resolved claims may reach; the issuer refuses claims beyond either limit, and both are validated when the host
  starts ([#73](https://github.com/NCodeGroup/identity/issues/73),
  [ADR-1405](docs/decisions/1405-selective-disclosure-jwts-are-an-opt-in-package-over-the-jwt-validator.md)).

- **A COSE message (`NCode.Identity.Cose`) and a CWT (`NCode.Identity.Cwt`) are processed fail-closed under bounded
  work.** The algorithm of a message must be in the protected header bucket and in the caller's permitted list, which is
  checked before the algorithm or a key is looked up; a header in both buckets, a `crit` parameter that is unprotected
  or names a label the verifier does not understand, a Partial IV and a recipient that carries recipients are refused; a
  direct recipient must be the only recipient. The input is read in strict mode and refused before parsing when it is
  larger than `CoseOptions.MaxMessageSizeBytes`, and refused when it uses an indefinite length, has bytes after the
  message, repeats a label, nests deeper than `MaxNestingDepth` or carries more than `MaxSignatures` signatures or
  `MaxRecipients` recipients; the limits are validated when the host starts. A message authentication code is compared
  in fixed time, and a content key lives only in sensitive pooled memory. A `COSE_Key` is read and written as a public
  key: a key with a private parameter or a Base IV is refused. A CWT is validated against explicit algorithm, issuer and
  key lists, none of which defaults to unrestricted; its claims are read only after the COSE layer verifies; a token
  that is protected only by encryption is refused, and a token that carries an `aud` is refused unless the
  verifier names its audiences. A failure carries a machine-readable code and fixed text, never the message or the token
  ([#76](https://github.com/NCodeGroup/identity/issues/76),
  [ADR-1409](docs/decisions/1409-cose-messages-are-an-opt-in-package-family-over-system-formats-cbor-and-the-familys-keys-and-algorithms.md),
  [ADR-1410](docs/decisions/1410-a-cwt-is-validated-fail-closed-and-its-claims-are-read-only-after-its-cose-layer-verifies.md)).

- **Every development-only convenience is inert unless configured, behind one call.**
  `AddDevelopmentEnvironment(configuration)` registers the developer CORS policy, the developer and conformance seeders,
  and the developer key pruning, and reads only configuration, not the host environment: each piece does nothing unless
  its own configuration section is present, so a host can leave the call wired unconditionally and a further convenience
  inherits the same rule. `AddProductionSafety` is the one Production check, and it refuses each of those sections
  ([#441](https://github.com/NCodeGroup/identity/issues/441),
  [ADR-1313](docs/decisions/1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md)).

- **The server refuses to start in Production with development-only settings or unprotected secrets.**
  `AddProductionSafety(configuration, hostEnvironment)` validates configuration during composition and throws before the
  port is bound. It checks the secret-protection configuration (a root key that outlives the process, not held inline in
  configuration, and a decision about recovering a lost root key), `AllowedHosts` (unset or `*`), and the
  `DeveloperKeys`, `Cors`, `DeveloperInteractiveSeed` and `ConformanceSeed` sections, and reports **every** finding in
  one message so a misconfigured deployment is fixed in a single pass. As the host starts it also refuses a Data
  Protection key ring with no repository or no `IXmlEncryptor`, and configured platform settings whose `environment_type`
  is a registered non-production type. It is inert in every non-Production environment, so local, test, and conformance
  hosts keep their conveniences. Each setting checked is correct in Development, dangerous on a public origin, and
  silent — a deployment that forgets to override it starts, serves traffic, and looks healthy — so each is a startup
  failure rather than a log warning
  ([ADR-1313](docs/decisions/1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md)).

- **The Data Protection key ring is encrypted at rest under a root key outside the database.** The host persists the key
  ring to the same database as the secrets it unprotects, and encrypts it under the platform master key, whose root key
  the `SecretProtection` section names and which lives outside the database, so a database dump or backup is useless
  without it. In Production, `AddProductionSafety` refuses to start the host when the key ring has no repository or no
  `IXmlEncryptor`
  ([ADR-1235](docs/decisions/1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).

- **Forwarded headers are trusted only from configured proxies (`AddTrustedForwardedHeaders` /
  `UseTrustedForwardedHeaders`).** When TLS terminates at a proxy, the connection the application sees is plaintext
  HTTP to an internal address. The framework defaults are wrong in both directions — they trust loopback, so a proxy in
  another container is not trusted, while clearing the allow-lists trusts every peer — so trust is granted only to the
  proxies and CIDR networks named in the `ForwardedHeaders` configuration section. No middleware is added when none are
  configured, the correct posture for a direct-TLS deployment, and `ForwardLimit` defaults to a single hop so a client
  cannot prepend its own entries. The section is validated: a trusted proxy that is not an IP address (IPv4 written as
  four decimal octets, so `10.0.0` is never read as another address), a trusted network that is not CIDR notation or
  has bits set past its prefix (so `10.0.0.4/8` is never masked to `10.0.0.0/8`), and a forward limit below one stop
  the host. `NCode.Identity.AspNetCore` holds it, and the reference host and the management console host compose it
  ([#611](https://github.com/NCodeGroup/identity/issues/611),
  [ADR-1139](docs/decisions/1139-setting-names-are-a-closed-vocabulary.md),
  [ADR-1447](docs/decisions/1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md)).

- **Host configuration sections are bound strictly and validated.** `InteractiveAuthentication`, `DeveloperKeys` and
  `Cors` are bound strictly into `InteractiveAuthenticationOptions`, `DeveloperKeysOptions` (read through
  `DeveloperKeysConfiguration.GetDeveloperKeysOptions()`) and `DeveloperCorsOptions`, so a key that maps to no option
  stops the host, and an allowed origin that can never match a browser's `Origin` header (a trailing slash, a path, a
  wildcard) stops the host with an error naming the entry. `TrustedForwardedHeadersOptions` and `InteractiveAuthenticationOptions`
  are resolved through `IOptions<T>` ([ADR-1139](docs/decisions/1139-setting-names-are-a-closed-vocabulary.md)).

- **The reference host and the management console host send `Strict-Transport-Security` on every HTTPS response outside
  Development.** `AddStrictTransportSecurity` (`NCode.Identity.AspNetCore`) binds `StrictTransportSecurityOptions` strictly from
  the `StrictTransportSecurity` section (`Enabled`, `MaxAge`, `IncludeSubDomains`, `Preload`, `ExcludedHosts`) and
  validates it when the host starts, and `UseStrictTransportSecurity`, which a host calls right after
  `UseTrustedForwardedHeaders`, adds the header to every response to a request that arrived over HTTPS, directly or
  through a trusted proxy. The default is
  `max-age=31536000; includeSubDomains`; `Preload` requires `IncludeSubDomains` and a `MaxAge` of at least 365 days;
  `localhost`, `127.0.0.1` and `[::1]` never receive it; a deployment whose TLS-terminating proxy sends the header sets
  `StrictTransportSecurity:Enabled` to `false`
  ([ADR-1258](docs/decisions/1258-the-server-sends-strict-transport-security-outside-development.md),
  [ADR-1447](docs/decisions/1447-forwarded-header-trust-and-strict-transport-security-are-general-aspnet-core-helpers-each-host-composes.md),
  issues [#304](https://github.com/NCodeGroup/identity/issues/304),
  [#611](https://github.com/NCodeGroup/identity/issues/611)).

- **The reference host runs `UseAntiforgery` after `UseAuthentication` and `UseAuthorization`.** The interactive
  surface's single cookie scheme is the default authenticate scheme, so the login POST validates its claims-bound
  antiforgery token against the authenticated user, and every interactive login passes antiforgery validation, not only
  the first. `InteractiveLoginAntiforgeryTests` verifies the middleware order.

- **The bootstrap administrator, the development administrator, and the conformance clients are seeded into one named
  tenant only.** `BootstrapAdmin:TenantId`, `DeveloperInteractiveSeed:TenantId`, and `ConformanceSeed:TenantId` are
  required whenever their seeder has something to seed, and the host does not start without them; the named tenant may
  be the root tenant. Tenants created at runtime are seeded through the same pipeline, so none of them receives a client
  whose tokens carry `GlobalAdmin` or accounts with well-known passwords. `GlobalAdmin` is conferred only on the
  bootstrap client in its tenant, not on a client that shares its identifier elsewhere
  ([ADR-1166](docs/decisions/1166-a-tenant-is-provisioned-through-one-pipeline-under-every-strategy.md)).

- **No development or conformance credential is committed.** The bootstrap administrator's client secret
  (`BootstrapAdmin__ClientSecret`), the developer seed's administrator password
  (`DeveloperInteractiveSeed__AdminPassword`), the secrets of the `ConformanceSeed` clients `client`, `client2` and
  `client-secret-post` and the conformance test user's password are generated once per machine, beside the development
  root key in `LocalApplicationData/NCode.Identity/LocalDev`, by the LocalDev AppHost and, for the conformance
  credentials, by `build/conformance/Get-ConformanceSeedCredentials.ps1`, which reads the same files. The AppHost hands
  them to the hosts it starts as environment variables, and `run-conformance.ps1` hands the conformance credentials to
  the conformance OP as `ConformanceSeed__` variables and fills them into the pre-flight and into the suite
  configurations it renders from the committed `oidcc-basic.template.json` and `fapi1-advanced.template.json`. A host
  run without the AppHost reads the administrator credentials from its user secrets and otherwise seeds neither
  administrator, and without the conformance credentials the seed skips those clients and the test user; the
  `Conformance` profile starts by hand after `Get-ConformanceSeedCredentials.ps1 -SetEnvironment`
  ([#496](https://github.com/NCodeGroup/identity/issues/496), [#514](https://github.com/NCodeGroup/identity/issues/514),
  [ADR-1370](docs/decisions/1370-a-host-names-its-root-key-provider-and-development-secrets-are-generated-per-machine.md)).

- **The host chart takes its root key by reference, and its README documents rotating it by hand.**
  `secretProtection.rootKeyProvider` selects `local`, `aws-kms`, `azure-key-vault` or `google-cloud-kms` (`ephemeral`
  only outside Production); a local root key lives in a Secret the chart mounts, and
  `secretProtection.local.previousKeySecrets` keep older root keys available so the master keys they wrapped still
  unwrap through a rotation. The README warns never to overwrite a root key in place, such as by writing other contents
  into the same Secret, because that destroys the only key that can unwrap the existing master keys and leaves the
  stored secrets unreadable after the next restart, recoverable only with the recovery key
  ([ADR-1235](docs/decisions/1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md)).

- **The host image's base images are pinned by digest and tracked by Dependabot.** Every `FROM` of the host and console
  Dockerfiles names its tag and the digest of its multi-architecture index, and each Dockerfile takes its .NET and Node
  versions from those lines alone, with no version build argument; a weekly `docker` Dependabot stream moves the digests
  of both images in one pull request, and the `Host
  image` workflow checks the Node major against `.node-version`
  ([ADR-1249](docs/decisions/1249-the-host-images-base-images-are-pinned-by-digest-and-tracked-by-dependabot.md), issue
  [#226](https://github.com/NCodeGroup/identity/issues/226)).

- **Dependencies are pinned to releases without known advisories.** Every `Microsoft.AspNetCore.*`,
  `Microsoft.EntityFrameworkCore.*`, `Microsoft.Extensions.*`, `System.Text.Json`, and
  `System.Security.Cryptography.Xml` pin (and the `dotnet-ef` tool) is on the .NET 10.0.14 servicing band, which brings
  `System.Security.Cryptography.Pkcs` 10.0.14 transitively, clear of GHSA-2fmv-rv2c-59x7 and GHSA-846f-f764-76mx
  (`Pkcs`) and GHSA-7hqp-7c5m-4vq6, GHSA-c399-9853-6jmj, and GHSA-r955-8q5j-3h83 (`Xml`).

- **Frontend dependencies must be three days old before anyone installs them.** pnpm refuses a version published less
  than three days ago (`minimumReleaseAge`), so a compromised release is usually caught and pulled before it reaches the
  lockfile, and `pnpm add` picks the newest version old enough. The window equals Dependabot's npm cooldown, stated in
  `dependabot.yml`, because on pnpm 10 Dependabot enforces its cooldown on every version already in the lockfile.
  Dependabot security updates bypass both gates, and any other urgent version goes in `minimumReleaseAgeExclude`
  ([ADR-1153](docs/decisions/1153-one-release-age-window-gates-frontend-dependencies.md)).

- **Dependabot raises security updates, and version updates are held.** The `nuget` and `npm` ecosystems in
  `.github/dependabot.yml` set `open-pull-requests-limit: 0`, GitHub's documented way to hold version updates while
  keeping their configuration, and `github-actions` allows only the NCode Review pin (issue
  [#36](https://github.com/NCodeGroup/identity/issues/36)); the `docker` stream runs, because a digest pin receives its
  fixes only through it. Dependabot security updates are on for the repository, with one group per ecosystem, so an
  advisory that affects a dependency produces a fix pull request automatically, and every entry waits a three-day
  cooldown before it offers a version.

- **The JWT validator takes an explicit algorithm and key policy.** `JwtValidationParameters` carries `DecryptionKeys`
  (`UseDecryptionKeys`) and the allow-lists `ValidSignatureAlgorithms`, `ValidKeyManagementAlgorithms`,
  `ValidContentEncryptionAlgorithms` and `ValidCompressionAlgorithms` (`null` is unrestricted, empty permits none),
  checked against every header before any cryptographic operation. An encrypted token is decrypted only with the keys
  the caller names, never with the verification keys, and must enclose a signed JWT (`cty` `JWT`); a key is used only
  for the `use` and `alg` its metadata declares; the JSON serializations are rejected. `DecodedJwt.SigningKey` reports
  the verifying key, and a Nested JWT also reports its `EncryptionHeader` and `DecryptionKey`. The encoder marks a
  Nested JWT with `cty` `JWT`
  ([ADR-1154](docs/decisions/1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md)).

- **The JOSE library does not implement the unsecured `none` algorithm, and the JWT validator rejects an unsigned
  token.** The default algorithm registry has no `none` signature algorithm, a JWS that names it fails as an unsupported
  algorithm, and every policy refuses `none` by name. The JWT validator rejects an unsigned (`alg:none`) token at every
  level, with no option to accept one, so such a token authenticates nowhere: not at UserInfo, introspection, token
  exchange or the management API
  ([ADR-1154](docs/decisions/1154-jwts-are-validated-as-signed-tokens-under-explicit-algorithm-and-key-policy.md),
  [ADR-1295](docs/decisions/1295-the-jose-library-does-not-implement-the-none-algorithm.md),
  [#379](https://github.com/NCodeGroup/identity/issues/379)).

- **JWT validation enforces an explicit, expected `typ` (RFC 8725 §3.11).**
  `JwtValidationParameters.ValidateTokenType(...)` adds a `TokenTypeRequirement`, which defeats token substitution and
  cross-JWT confusion, beside the `ValidateIssuer`, `ValidateAudience` and `ValidateTokenLifeTime` requirements.
  Matching follows the media-type rules of RFC 7515 §4.1.9 (the `application/` prefix is optional and the comparison is
  case-insensitive), exposed as the reusable `JoseTokenTypes.Normalize` / `JoseTokenTypes.AreSame` helpers.
  `JoseTokenTypes` carries the registered `typ` constants `Jwt`, `AccessTokenJwt`, `DPoPJwt` (`dpop+jwt`, RFC 9449),
  `LogoutJwt` (`logout+jwt`, OIDC Back-Channel Logout), `SecurityEventJwt` (`secevent+jwt`, RFC 8417) and
  `AuthorizationRequestJwt` (`oauth-authz-req+jwt`, RFC 9101).

- **`RSA1_5` is not a registered JOSE algorithm.** RSAES-PKCS1-v1_5 key encryption is open to padding-oracle attacks
  (RFC 8725 §3.2), so `AddJoseLibrary()` registers RSA key management only as `RSA-OAEP`, `RSA-OAEP-256`, `RSA-OAEP-384`
  and `RSA-OAEP-512`, and a JWE that names `RSA1_5` fails with `JoseInvalidAlgorithmException` through `IJoseSerializer`
  and through every `IJwtValidator` call, including one that sets no `ValidKeyManagementAlgorithms`.
  `AlgorithmCodes.KeyManagement.RsaPkcs1` is the name a policy refuses, and the algorithm settings and the protocol
  client refuse `RSA1_5` even when a host registers an implementation of its own
  ([ADR-1282](docs/decisions/1282-the-jose-library-does-not-implement-rsa1-5.md),
  [#339](https://github.com/NCodeGroup/identity/issues/339)).

- **Algorithm settings name only what the server can safely honor.** The server neither compresses what it encrypts nor
  accepts a compressed JWT (RFC 8725 §3.6), so no setting offers compression, and CIBA defines no encrypted
  authentication request, so none offers one. The ID token, UserInfo and request-object encryption lists admit only
  asymmetric key management (`RSA-OAEP*`, `ECDH-ES*`), because those tokens are encrypted to a party's published key;
  the CIBA signing list admits only asymmetric signatures; and no list offers `RSA1_5`. The request-object, UserInfo and
  ID token lists apply to clients as well as tenants, and `RegisterClientRequest` and `RegisterClientResponse` carry
  that registration metadata
  ([ADR-1155](docs/decisions/1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).

- **ES256, ES384, ES512 and ES256K accept only their own curve.** RFC 7518 §3.4 binds `ES256`, `ES384` and `ES512` to
  P-256, P-384 and P-521, and RFC 8812 binds `ES256K` to secp256k1, so a 256-bit key on another curve does not sign as
  ES256, a P-256 key never signs as `ES256K`, and a secp256k1 key never signs as `ES256`. `EccSecretKey.GetECCurve()`
  reads the actual curve from the key material, not from the key size
  ([ADR-1272](docs/decisions/1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md),
  [ADR-1392](docs/decisions/1392-es256k-is-an-opt-in-package-that-registers-a-curve-and-an-ecdsa-algorithm-as-data.md)).

- **A key is refused when it is declared for an algorithm that rejects its curve.** `ValidateKeyAlgorithm` checks the
  declared key's type, size and curve with the check the cryptographic runtime makes before it uses the key, so a
  client JSON Web Key Set, a management import or a management update cannot register an Ed25519 key for `ECDH-ES`, an
  X25519 key for `EdDSA`, a P-256 key for `ES256K` or a secp256k1 key for `ES256`, the signing and agreement confusion
  RFC 8037 §3.1 and §3.2 forbid. A curve's kind keeps the `OKP` curves apart: an X25519 or X448 key is never accepted
  for `EdDSA`, `Ed25519` or `Ed448`, and an Ed25519 or Ed448 key never for `ECDH-ES`. A key to generate is resolved onto
  one of its algorithm's curves, so no path checks a curve-bound algorithm by type and size alone, and a held
  asymmetric key is checked by its stored public half, never by its type and size. A JWE decryption key
  on a curve the algorithm refuses is reported as the caller's configuration error rather than as a failed
  authentication tag ([#327](https://github.com/NCodeGroup/identity/issues/327),
  [#336](https://github.com/NCodeGroup/identity/issues/336), [#337](https://github.com/NCodeGroup/identity/issues/337),
  [#534](https://github.com/NCodeGroup/identity/issues/534),
  [ADR-1389](docs/decisions/1389-one-algorithm-name-serves-every-key-type-it-declares.md),
  [ADR-1398](docs/decisions/1398-a-key-size-selects-a-curve-only-when-no-other-curve-shares-it.md),
  [ADR-1390](docs/decisions/1390-x25519-and-x448-agree-keys-beside-the-okp-signing-keys-and-a-curves-use-separates-them.md)).

- **ECDH-ES publishes only the public half of its ephemeral key and refuses a degenerate agreement.** The `ECDH-ES`
  and `ECDH-ES+A*KW` key management algorithms write an `epk` header that carries only the required members of the key
  type (RFC 7518 §4.6.1.1), such as `kty`, `crv`, `x` and `y` for an `EC` key, the `epk`'s `kty` must be the recipient
  key's, and a received `epk` that carries a private member is refused, because the content it protects is not
  confidential. An X25519 or X448 agreement whose shared secret is all zeros, from an `epk` or a recipient key of small
  order, is refused on both sides (RFC 7748 §6) ([#327](https://github.com/NCodeGroup/identity/issues/327),
  [#336](https://github.com/NCodeGroup/identity/issues/336),
  [ADR-1390](docs/decisions/1390-x25519-and-x448-agree-keys-beside-the-okp-signing-keys-and-a-curves-use-separates-them.md)).

- **A failed JWE key unwrap is indistinguishable from a failed authentication tag.** A failed key unwrap substitutes a
  random content key, so a tampered or wrong-length encrypted key fails exactly like a tampered tag, with one
  `JoseEncryptionException` ("Failed to decrypt the JWE.") and no inner exception
  ([ADR-1296](docs/decisions/1296-a-failed-jwe-key-unwrap-is-indistinguishable-from-a-failed-authentication-tag.md),
  [#380](https://github.com/NCodeGroup/identity/issues/380)).

- **A JOSE token's encoded members are decoded whole or refused.** Every base64url segment of a JWS or JWE, the
  `PBES2` salt input (`p2s`), the AES-GCM key wrap's `iv` and `tag`, and the `x5t` and `x5t#S256` certificate
  thumbprints a JWT names are read only when they decode whole to the size they must have. A value that is not
  base64url or has another size fails with `JoseException`, or `JwtValidationException` for a thumbprint, before any
  key is derived from it or compared with it, and `PBES2` refuses an iteration count (`p2c`) outside its bounds and an
  encrypted key that is not a whole AES key wrap before it derives a key encryption key
  ([#541](https://github.com/NCodeGroup/identity/issues/541)).

- **Decrypted JOSE payloads are zeroed when released.** A decrypted JWE payload, a payload serialized or compressed
  before encryption, and their decoded text are held in pinned buffers from the secure pool and zeroed on release
  ([#440](https://github.com/NCodeGroup/identity/issues/440),
  [ADR-1266](docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).

- **An `AKP` JWK thumbprint covers `alg`.** The thumbprint is computed over `alg`, `kty` and `pub`, as RFC 9964 §6
  requires, so two `AKP` keys that differ only in algorithm have different thumbprints
  ([ADR-1271](docs/decisions/1271-a-key-type-registers-its-json-web-key-rules.md)).

- **A JWT signed with a certificate key is checked against the certificate's validity in UTC.** Client assertions,
  request objects and CIBA requests signed with an expired certificate fail, and current ones pass, whatever the host's
  time zone. A client key registered or published with an `x5c` certificate is subject to this certificate-lifetime
  check ([ADR-1161](docs/decisions/1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)).

- **RFC 7519 `NumericDate` values are read strictly and never fault the server.** `NCode.Identity.Json.DateCoercion` offers the
  non-throwing `TryFromNumericDate(long)`, `TryFromNumericDate(double)` (fractional seconds, kept to tick precision as
  RFC 7519 allows) and `TryParseNumericDate(string?)`; `JsonElements` offers the strict `TryGetNumericDate()` /
  `TryGetPropertyNumericDate(name)`, which accept only a JSON number, and the loosely typed
  `TryGetValue<DateTimeOffset>` accessors accept fractional `NumericDate`s. Every wire-facing read routes through them
  (the DPoP proof `iat`, the client-assertion `exp`, token-exchange and session `auth_time`, the `$created_when`
  parameter, and the encoder's `iat`), so an out-of-range value such as a DPoP proof with `iat: 99999999999999` gets the
  endpoint's normal rejection, a fractional `iat`/`exp`/`auth_time` is accepted, and `auth_time` is read
  culture-invariantly. The JWT lifetime validator requires a present `nbf` or `exp` to be a valid `NumericDate`, never a
  date string, or validation fails, so an unreadable `nbf` never lets a token be used early and an unreadable `exp`
  never makes it immortal. `DateTimeOffset.FromUnixTimeSeconds` / `FromUnixTimeMilliseconds` are banned in shipping code
  (`RS0030`) ([#33](https://github.com/NCodeGroup/identity/issues/33),
  [ADR-1123](docs/decisions/1123-numeric-date-is-read-strictly-and-range-guarded.md)).

- **JSON is parsed into detached elements only.** `JsonDocument.Parse`, `ParseAsync`, `ParseValue` and `TryParseValue`
  are banned (`RS0030`) in production and test code, which parse with `JsonElement.Parse` and read a stream with
  `JsonSerializer.Deserialize<JsonElement>`, so no code owns a `JsonDocument` that leaks its pooled buffers when it is
  not disposed. `NCode.Identity.Json` provides the static extension `JsonElement.Parse(ReadOnlySequence<byte>)`, which reads one
  value from a segmented buffer, honors every `JsonDocumentOptions` member and refuses trailing content. A parsed value
  is named `jsonElement`, never `document`, including the parameter of `IPersistedPayloadSerializer.Deserialize`
  ([#386](https://github.com/NCodeGroup/identity/issues/386)).

- **Text is encoded and decoded only through strict encodings.** The compact `b64=false` JWS signature covers the
  payload's real UTF-8 bytes, non-ASCII characters included. DPoP proof validation rejects a non-ASCII access token.
  `request_uri` request objects and other inbound text are decoded strictly. JWT UserInfo and introspection responses
  are written as strict UTF-8 bytes with `Content-Type: application/jwt`, and `text/plain` responses declare
  `charset=utf-8`. The build bans lenient encodings with `RS0030` in every project
  ([ADR-1266](docs/decisions/1266-binary-data-uses-bufferfactory-rentals-with-explicit-sensitivity-and-text-only-strict-encodings.md)).

- **Stored secrets are protected by a key hierarchy under an external root key.** Stored private and symmetric secrets
  are AES-256-GCM envelopes (`nce1.`) under a fresh data key, wrapped by an HKDF-derived purpose key of the owning
  tenant's master key, which is wrapped by a root key held by a root key provider (`ephemeral`, `local`, or a registered
  one) and, optionally, by an offline recovery RSA key. The platform's master key protects the Data Protection key ring
  and the application data a host protects under the platform, and no stored secret. The public contracts are
  `ISecretProtectorProvider`, `ISecretProtector`, `IRootKeyProvider`, `IMasterKeyStore`, `SecretOwner` and
  `SecretProtectionOptions`, and master keys are kept in the `MasterKeys` table. Deleting a tenant deletes
  its master keys ([ADR-1235](docs/decisions/1235-stored-secrets-are-protected-by-a-key-hierarchy-under-an-external-root-key.md),
  [ADR-1413](docs/decisions/1413-the-platform-master-key-protects-the-data-protection-key-ring-and-the-hosts-own-data.md),
  [#167](https://github.com/NCodeGroup/identity/issues/167)).

- **A database dump or backup reveals no secret material without the root key, and every host names its root key
  provider.** An unset or blank `SecretProtection:RootKeyProvider` stops the host at startup with an error naming the
  setting; set `local`, `aws-kms`, `azure-key-vault`, `google-cloud-kms` or another registered provider, or `ephemeral`
  for local development and tests. The reference host's `appsettings.Development.json` and
  `appsettings.Conformance.json` select `ephemeral`, and the Helm chart refuses an empty
  `secretProtection.rootKeyProvider` in every environment. Production refuses the ephemeral root key provider, an inline
  local root key (whatever provider is selected), and running without a recovery-key decision, and reports them with
  every other production-safety finding as "setting(s) are unsafe for a production deployment"
  ([ADR-1370](docs/decisions/1370-a-host-names-its-root-key-provider-and-development-secrets-are-generated-per-machine.md),
  [#495](https://github.com/NCodeGroup/identity/issues/495)).

- **A client secret that is only compared is stored as a one-way verifier, and its verification is bounded.** The
  `client_secret` of a client whose method compares it (`client_secret_basic`, `client_secret_post`) is stored only as a
  verifier in its `nce1.` envelope (`SecretEncodings.Verifier`), so not even the root key recovers it:
  `$ncode-sha256$…` for a secret the server generates, and `$ncode-pbkdf2-sha256$…`, the format and parameters of the
  family's password hashes, for one an operator supplies. A client whose method keys from its secret
  (`client_secret_jwt`) keeps it reversible. `Pbkdf2SecretVerifierOptions.Iterations` sets the PBKDF2 work factor
  (600,000, the least a Production host may set), and every verification with a work factor takes a turn of the
  process's `ISecretVerificationGate`, sized by `SecretVerificationGateOptions` (`MaxConcurrency`, `MaxQueueLength`,
  `QueueTimeout`), so a flood of presented secrets neither occupies every processor nor queues without bound
  ([#163](https://github.com/NCodeGroup/identity/issues/163), [ADR-1391](docs/decisions/1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).

- **Failed client authentications are limited per remote address by default, without a per-client lockout.** Every
  handler of a client-authentication method that verifies a credential asks `IClientAuthenticationAttemptLimiter`
  before it verifies one, at every endpoint that authenticates a client, and counts each credential it refuses, the
  identifier of a client it cannot find included, before it audits the failure and with no cancellation token, so a
  caller that disconnects after a guess is counted. A public client's `none` authentication verifies no credential, so
  it is neither refused at the limit nor counted, and an address at its limit cannot deny the public clients behind it;
  a Basic header that carries no credential is answered without a count. The mutual-TLS handlers ask once the resolved
  client's registration says the request is theirs, so a refusal is audited with the method the client is registered
  for. The default limiter counts failures in the shared `IExpiringCounterSet`, so the limits hold across replicas, in fixed
  windows of `client_authentication_failed_attempts_window` (15 minutes by default, one minute to one day) and in two
  partitions: one remote address with one `client_id`, limited by `client_authentication_failed_attempts_limit` (10 by
  default, at most 100), and one remote address across every `client_id`, limited by
  `client_authentication_failed_attempts_address_limit` (100 by default, at most 10,000). An IPv6 address is counted by
  its `/64` and an IPv4-mapped address as the IPv4 address it maps. The failures are never counted per `client_id`
  alone, so a caller that knows a public `client_id` cannot lock its client out from another address. The settings
  apply to tenants and a narrower scope may only lower a limit or lengthen the window. A request refused at a limit
  verifies nothing and is answered as a rate limit answers: `429 Too Many Requests` with `temporarily_unavailable`, a
  `Retry-After` until the window ends and `Cache-Control: no-store`; it is audited as a `client.authentication` failure
  with the error code `attempt_limit_reached`, logged as event 2072 without the address or the `client_id`, and not
  recorded as usage. A request without a remote address is not limited, and the first one logs event 2073 at
  `Warning`, so a host whose proxy does not forward the client's address learns that the limit is off. A shared state
  that cannot be reached fails client authentication rather than admitting unlimited
  guesses ([ADR-1399](docs/decisions/1399-failed-client-authentications-are-limited-per-remote-address-by-default.md),
  [#533](https://github.com/NCodeGroup/identity/issues/533)).

- **The Data Protection key ring is encrypted under the platform master key, and a key it cannot decrypt stops the host.**
  The host configures the key ring through the `SecretProtection` section, and the Helm chart through its
  `secretProtection` values, which the chart's schema, render-time refusals and CI releases check.
  `DataProtectionKeyRingGate` checks the live keys after the platform's protector loads and before Data Protection loads
  its key ring, and the host names each undecryptable key rather than letting Data Protection write a default key over
  them; a Development host revokes the key and logs a warning. `DataProtectionKeyRing:RevokedKeyIds`
  (`DataProtectionKeyRingOptions`) revokes keys by ID, and `AddConfiguration` registers Data Protection
  ([ADR-1247](docs/decisions/1247-a-data-protection-key-the-server-cannot-decrypt-stops-the-host.md),
  [#239](https://github.com/NCodeGroup/identity/issues/239)).

- **Every host signs with the keys tenant provisioning persists.** Tests included, no host signs with ephemeral
  in-memory developer keys; development conveniences run through the production path
  ([ADR-1313](docs/decisions/1313-development-conveniences-are-inert-unless-configured-and-run-through-the-production-path.md),
  [#441](https://github.com/NCodeGroup/identity/issues/441)).

- **Outbound calls to caller-supplied URLs share one guarded, bounded client.** Every fetch of, and delivery to, a
  caller-supplied URL — sector identifier, client JWKS, request URI, back-channel logout, CIBA delivery and
  client-certificate revocation — goes through one guarded HTTP client with a per-attempt and a total timeout and up to
  two jittered retries of a safe request, set by the `OpenId:OutboundHttp` section (`OutboundHttpOptions`:
  `AttemptTimeout`, `TotalTimeout`, `MaxRetryAttempts`, `RetryDelay`) and validated when the host starts. Each call is
  bounded by default to 10 seconds per attempt and 30 seconds in total, so an endpoint that takes longer than 10 seconds
  to answer fails that delivery attempt, and back-channel logout and CIBA deliveries do not read the response body
  ([#444](https://github.com/NCodeGroup/identity/issues/444),
  [ADR-1315](docs/decisions/1315-outbound-calls-to-caller-supplied-urls-share-one-guarded-bounded-client.md)).

- **Every server-initiated outbound HTTP call to a client-controlled URL is SSRF-guarded uniformly**
  ([ADR-1095](docs/decisions/1095-sector-identifier-uri-fetch-and-ssrf-guard.md),
  [ADR-1106](docs/decisions/1106-runtime-jwks-uri-client-key-resolution-and-shared-ssrf-guard.md),
  [ADR-1111](docs/decisions/1111-complete-outbound-fetch-ssrf-coverage.md)). One injectable guard,
  `IOutboundHttpForgeryGuard`, pins each connection to the resolved IP and refuses loopback, private, CGNAT, link-local,
  unique-local, multicast and reserved ranges (the IANA special-use registry), which defeats DNS rebinding, and no
  guarded call follows a redirect, so a registered internal URL cannot turn the OP into a confused deputy. That address
  floor is a non-weakenable security control; a deployment extends it additive-deny only by registering
  `IOutboundAddressFilter` implementations. The `sector_identifier_uri`, `jwks_uri` and JAR `request_uri` (RFC 9101)
  fetches each require an absolute `https` URI and are bounded by an optional host allow-list, a wall-clock timeout and
  a capped response body, through the non-discoverable settings `sector_identifier_uri_allowed_hosts`,
  `sector_identifier_uri_fetch_timeout` (default 10 seconds) and `sector_identifier_uri_max_size_bytes` (default 100
  KB), their `jwks_uri_*` counterparts, and `request_uri_allowed_hosts`, `request_uri_fetch_timeout` (default 10
  seconds) and `request_uri_max_size_bytes` (default 100 KB). The server-to-client POST deliveries that target
  client-registered URLs, back-channel logout (`backchannel_logout_uri`) and CIBA ping/push notification
  (`client_notification_endpoint`), go through the same guard. The streamed response-size-capped read is one
  replaceable, DI-registered `IOutboundHttpContentReader` seam used by every body-reading fetch; it writes into a
  caller-supplied (pooled) `IBufferWriter<byte>` and returns an `OutboundHttpReadResult`, and the callers parse directly
  from the resulting `ReadOnlySequence<byte>` without an intermediate array.

- **Every absolute URL the server publishes or compares is anchored to the tenant's published URLs, never the incoming
  request.** Discovery (`token_endpoint`, `authorization_endpoint`, `jwks_uri`, …), the RFC 7592
  `registration_client_uri`, the device `verification_uri`, the `check_session_iframe` and the continue URL come from
  the tenant's issuer, so a spoofed `Host` header cannot redirect a client's secret or authorization code to an
  attacker. A client assertion's `aud` may name the tenant's issuer or the receiving endpoint's URL under the tenant's
  issuer origin or its mutual-TLS alias authority, and a DPoP proof's `htu` is compared with the endpoint's URL under
  the origin the tenant publishes for it; neither is built from the request's `Host`, so a forged `Host` cannot widen
  what the server accepts ([#518](https://github.com/NCodeGroup/identity/issues/518),
  [ADR-1387](docs/decisions/1387-the-client-assertion-audience-and-the-dpop-htu-are-compared-with-the-tenants-published-urls.md)).

- **An absolute URI means the same on every platform.** `NCode.Identity.OpenId.AbsoluteUri.TryParse` accepts only text
  that names its own scheme and contains no whitespace, so `/path` is never read as `file:///path` and whitespace is
  never trimmed. Dynamic client registration (`redirect_uris`, `post_logout_redirect_uris`), the redirect URI kinds, the
  URI message parameters, the URI setting rules and every other OpenID URI check parse with it, so a relative URI is
  refused on every platform.

- **Discovery publishes only what is marked discoverable.** The discovery endpoint (`/.well-known/openid-configuration`)
  returns only the settings marked `IsDiscoverable` and the endpoints marked discoverable, and no query-string parameter
  widens it, so an anonymous caller cannot enumerate non-advertised settings or endpoints; the full merged settings view
  is served only by the authenticated effective-settings endpoints of the management API
  ([ADR-1040](docs/decisions/1040-effective-settings-preview-endpoints.md)).

- **Discovery metadata is secure by default and operator-overridable
  ([ADR-1083](docs/decisions/1083-secure-by-default-discovery-metadata.md)).** The OpenID Provider advertises no
  deprecated or insecure flows out of the box: `response_types_supported` defaults to `["code"]`,
  `grant_types_supported` omits `implicit`, `code_challenge_methods_supported` defaults to `["S256"]`, and
  `subject_types_supported` defaults to `["public"]` so the required OpenID Connect Discovery field is present. The
  cross-trust-boundary token-signing fields (`id_token`, `userinfo`, `access_token`) advertise asymmetric algorithms
  only, never `HS256`; `request_object` and `token_endpoint_auth` signing list the full set for `client_secret_jwt`.
  Discovery and request-time enforcement share one settings source, so the implicit and hybrid authorization paths are
  fail-closed by default; a deployment enables and advertises any of them together by widening the setting in
  configuration (host `OpenId:Platform:Settings` overrides apply last and overwrite the baseline). Issue
  [#128](https://github.com/NCodeGroup/identity/issues/128).

- **Token, pushed authorization, device authorization and backchannel authentication responses are never cached.** The
  token endpoint sets `Cache-Control: no-store` and `Pragma: no-cache` before handling the request, so success and error
  responses carry them and neither the client nor an intermediary caches issued tokens ([RFC 6749
  §5.1/§5.2](https://datatracker.ietf.org/doc/html/rfc6749#section-5.1), issue
  [#131](https://github.com/NCodeGroup/identity/issues/131)). `POST /oauth2/par`, `POST /oauth2/device_authorization`
  and `POST /oauth2/bc-authorize` answer with `Cache-Control: no-store, no-cache, max-age=0` and `Pragma: no-cache`, so
  no client or intermediary cache keeps the one-time `request_uri`, `device_code` and `user_code`, or `auth_req_id` (RFC
  9126 §2.2, RFC 8628 §3.2, CIBA Core §7.3) ([#488](https://github.com/NCodeGroup/identity/issues/488),
  [ADR-1345](docs/decisions/1345-a-response-that-reveals-a-secret-is-marked-uncacheable-by-its-endpoint.md)).

- **No CORS on the front-channel endpoints.** The authorization, continue, and end-session endpoints and the interactive
  login/consent surface carry `IFrontChannelEndpointMetadata` (`WithFrontChannel()`), and `UseDeveloperCors` applies the
  developer policy only to endpoints without it, so even in Development they never answer a cross-origin request with
  `Access-Control-Allow-Origin` (FAPI 2.0 Security Profile §5.3.2.1). The marker also makes routing send a CORS
  preflight to the endpoint itself, which refuses it with `405` and no CORS headers; `UseDeveloperCors` answers no
  preflight that selects no endpoint, such as one for a method a front-channel path does not map. A host that adds its
  own CORS middleware skips both
  ([ADR-1109](docs/decisions/1109-fapi-behavioral-rules-and-discovery-validation-details.md)).

- **Authorization responses carry only wire parameters.** The authorization response executor applies
  `ShouldSerialize(…, SerializationFormat.OpenId)`, the contract the JSON serializer uses, so internal `$`-prefixed
  parameters such as `$created_when` and `$authorization_source_type` are never in the query, fragment or form-post
  response sent to the client.

- **A redirect URI is matched exactly, and only a registered loopback URI may vary its port.** The requested
  `redirect_uri` is compared ordinally, as written, query included, to the client's registered strings, and more than
  one value is refused (RFC 9700 §4.1.3, OAuth 2.1); the query that reaches the client is the registered one (RFC 6749
  §3.1.2), so a client registers the exact URIs it sends. `allow_loopback_redirect` (default `true`) follows RFC 8252
  §7.3: a registered `http://127.0.0.1` or `http://[::1]` URI may be requested with another port, and `localhost` gets
  no flexibility. The `fapi1-advanced` profile sets a ceiling of `false` for it (FAPI 1.0 Part 1 §5.2.2-20), so a client
  under it registers the exact port; `fapi2` sets none. An unregistered URI is refused with `unauthorized_client` and no
  redirect (issue [#202](https://github.com/NCodeGroup/identity/issues/202)). `redirect_uri` is carried as the string
  the client wrote, never as a `Uri`: `OpenIdCommonParameters.RedirectUri` is a `KnownParameter<string>`;
  `IBaseAuthorizationRequestValues`, `IAuthorizationRequest`, `ITokenRequest` and `AuthorizationResponseDelivery` expose
  `RedirectUri` as a string; and `UriParser` writes a parsed `Uri` parameter as its original string. A parked
  continuation and a stored authorization code carry the redirect URI as the client sent it, the response goes to that
  string, each registered query parameter reaches the client as registered, and only a character a header value cannot
  carry as written (a non-ASCII character, RFC 3987 §3.1, or an ASCII control character or space) is percent-encoded in
  the `Location` header. The token endpoint compares `redirect_uri` ordinally with the authorization request's value
  (RFC 6749 §4.1.3), so a value that differs in the case of its host or in its user information is refused with
  `invalid_grant` (issue [#243](https://github.com/NCodeGroup/identity/issues/243))
  ([ADR-1203](docs/decisions/1203-redirect-uris-are-matched-exactly.md),
  [ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).

- **A redirect URI's kind is checked, and a URI of no kind is never permitted.** A `private_use` URI has a
  reverse-domain-name scheme of at least two non-empty labels, such as `com.example.app`, so `javascript:`, `data:`,
  `ws:`, `file:`, and `myapp:` are no redirect target, even with `redirect_uri_kinds_supported` unset (issue
  [#247](https://github.com/NCodeGroup/identity/issues/247)). The FAPI profiles refuse an `http` redirect URI:
  `fapi1-advanced` caps `redirect_uri_kinds_supported` at `https` (FAPI 1.0 Part 1 §5.2.2-20 and §7.5), and `fapi2` at
  `https`, `loopback`, and `private_use` (FAPI 2.0 §5.3.2.2-8), so `http` is left only for a native client's loopback
  redirect under `fapi2`. Dynamic client registration refuses a redirect URI the tenant's settings exclude with
  `invalid_redirect_uri`, the management API refuses one the client's effective settings exclude with a validation
  problem on `redirect_uris`, and the conformance seed stops the host on one. The authorization and PAR endpoints refuse
  such a redirect URI with `invalid_request` and a `400` that is not redirected (issue
  [#244](https://github.com/NCodeGroup/identity/issues/244)). The end-session endpoint refuses a
  `post_logout_redirect_uri` like an unregistered one when `redirect_uri_kinds_supported` excludes its kind, and escapes
  its `Location` header as an authorization response's is (issue
  [#247](https://github.com/NCodeGroup/identity/issues/247))
  ([ADR-1203](docs/decisions/1203-redirect-uris-are-matched-exactly.md),
  [ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).

- **`response_types_supported` is the only control over the tokens the authorization endpoint returns.** To forbid an
  access token from the authorization endpoint, a tenant or client lists no response type that includes `token`; the
  default list is `code` alone. Discovery, dynamic client registration and the authorization endpoint agree on the one
  list, and a refusal is `unsupported_response_type` when the tenant imposes it and `unauthorized_client` when only the
  client does ([ADR-1142](docs/decisions/1142-listed-response-types-are-the-only-front-channel-token-control.md)). A
  response type that returns a token or an ID token defaults to the fragment response mode on every path: without a
  `response_mode`, the authorization endpoint returns such a response — tokens or an error — in the fragment, as OAuth
  2.0 Multiple Response Type Encoding Practices requires, so no token is placed in a URL query
  ([ADR-1141](docs/decisions/1141-response-types-are-whole-values.md)).

- **`response_modes_supported` is checked once, for the requested and the resolved mode.** When the setting is set, the
  authorization endpoint requires both the response mode as the request wrote it, so `jwt` itself must be listed, and
  the mode it resolves to to be listed, for an explicit and a default mode alike. The check runs once, when the delivery
  is derived, and a response mode that is not listed is refused with `invalid_request` and a `400`, and is not
  redirected
  ([ADR-1201](docs/decisions/1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md), issue
  [#213](https://github.com/NCodeGroup/identity/issues/213)).

- **Every authorization code is bound to PKCE by default.** `require_pkce` defaults to `true`, so an authorization
  request whose response type issues a code must carry an `S256` `code_challenge`, for every client; a missing one is
  refused with `invalid_request` (RFC 7636 §4.4.1) at both the authorization and the pushed authorization request
  endpoints. A response type that issues no code (an implicit flow an operator enabled) is not affected. The only
  exception is the one OAuth 2.1 §7.5.1.1 allows, and every condition must hold: the platform's configuration relaxes
  `require_pkce`; an administrator attests the client with the `pkce_nonce_exemption` setting (dynamic client
  registration never sets it); the client is registered as confidential; and the request requests `openid` and carries a
  `nonce`. A public client is always bound to PKCE, and at the token endpoint a code that is not bound can be redeemed
  only by a client that authenticates and keeps `openid` in scope, so the ID Token returns the nonce. PKCE is `S256`,
  never `plain` (OAuth 2.1 §7.5.2): code-challenge methods are an extensible `ICodeChallengeMethod` strategy collection
  supplied by `ICodeChallengeMethodCollectionProvider` and governed by `code_challenge_methods_supported`
  (`OpenIdSettingKeys.CodeChallengeMethodsSupported`, `Intersect` merge, so a child only narrows its parent's set,
  advertised in discovery), which accepts the registered transformations and defaults to `S256` alone, so a deployment
  that registers another transformation offers it by listing it (issue
  [#50](https://github.com/NCodeGroup/identity/issues/50),
  [ADR-1079](docs/decisions/1079-extensible-pkce-code-challenge-methods.md)). A `code_challenge` must name its
  `code_challenge_method` (`AuthorizationRequest.CodeChallengeMethod` is `null` when the request names none), and both
  the challenge and the verifier must have the RFC 7636 syntax (43 to 128 unreserved characters)
  ([ADR-1144](docs/decisions/1144-authorization-codes-are-bound-to-pkce-by-default.md)).

- **A request object's parameters are the only parameters of an authorization request (RFC 9101 §6.3), so an unsigned
  parameter cannot change a signed request.** The outer parameters follow one table,
  `RequestObjectParameterPolicy.Rules`: the object must carry `response_type`, `scope`, `redirect_uri` and a `client_id`
  that names the requesting client, and an object without them is refused with `invalid_request_object` (FAPI 1.0
  Advanced §5.2.2-10); an outer `response_type`, `redirect_uri` or `client_id` is optional but, when sent, must equal
  the object's (with `redirect_uri` compared as written) or the request is refused as `invalid_request_object`; an outer
  `scope` and any parameter sent only outside the object, such as `response_mode` or `nonce`, are ignored. Once a
  request object is verified, the authorization endpoint takes `state` and `response_mode` from it for every later
  response, errors included, so a parameter such as `redirect_uri` carried outside the object never causes a later error
  to go back with the outer, unsigned `state`, or unsigned when `response_mode=jwt` appears only in the object (RFC 6749
  §4.1.2.1). A pushed authorization request may carry only `request` and client authentication (RFC 9126 §3): the
  authorization endpoint verifies the object before it redirects, takes `redirect_uri`, `response_mode` and `state` from
  it, and returns an error raised before then without redirecting
  ([ADR-1201](docs/decisions/1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md),
  [ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md), issues
  [#229](https://github.com/NCodeGroup/identity/issues/229), [#238](https://github.com/NCodeGroup/identity/issues/238)).

- **A signed request object is short-lived and addressed to the issuer.** The authorization endpoint (`request` /
  `request_uri`) and the CIBA backchannel endpoint refuse a request object valid for longer than the client-scoped
  `request_object_max_lifetime` setting, which defaults to 60 minutes as a `Min` ceiling. The authorization endpoint
  always validates `aud`, against the tenant's issuer unless `request_object_expected_audience` is set, so a request
  object carries `aud`. `request_object_expected_audience` is client-scoped and replace-merged, so no ceiling caps it;
  the `fapi1-advanced` and `fapi2` profiles compose `IssuerOnlyRequestObjectAudienceRequirement`, which refuses a
  request object from a client that sets it with `invalid_request_object`, so under a FAPI profile a request object
  names the issuer as its audience. The `request_object_nbf_required` setting (`Or` merge, default `false`) makes `nbf`
  mandatory. The FAPI 2.0 profile pins both, at 60 minutes and `true`. A CIBA signed authentication request carries
  `iat`, `nbf`, `exp`, and `jti` (CIBA Core §7.1.1), and the CIBA loader applies the client's `clock_skew`
  ([ADR-1187](docs/decisions/1187-a-signed-request-object-is-short-lived-and-addressed-to-the-issuer.md),
  [ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).

- **An unsigned (`alg:none`) request object is rejected with `request_not_supported`** (issue
  [#131](https://github.com/NCodeGroup/identity/issues/131)). As OpenID Connect Core 1.0 §6.1 requires,
  `DefaultLoadAuthorizationRequestHandler` detects an `alg:none` request object, inline `request` or fetched
  `request_uri`, up front, while a malformed request object returns `invalid_request_object`, so the OP never accepts an
  unsigned request object.

- **Request objects, CIBA requests, and client assertions are accepted only with the algorithms the client's settings
  permit.** A request object is verified under the client's effective `request_object_signing_alg_values_supported` and
  decrypted (with the tenant's keys) under its `request_object_encryption_{alg,enc}_values_supported`, so a narrower
  client list or the FAPI 2.0 profile's ceiling binds. A CIBA authentication request is verified under its
  asymmetric-only `backchannel_authentication_request_signing_alg_values_supported` and is never encrypted: CIBA defines
  no encrypted authentication request, so an encrypted one is rejected. A client assertion is verified under the
  authenticating client's effective `token_endpoint_auth_signing_alg_values_supported`, so a client's narrower list, and
  the ceiling of a security profile the client (not only its tenant) selects, such as FAPI 2.0, bind the algorithm of
  its `private_key_jwt` and `client_secret_jwt` assertions
  ([ADR-1149](docs/decisions/1149-settings-declare-the-scopes-they-apply-to.md)). At these trust boundaries an unset
  list, or a list narrowed to nothing, permits no algorithm
  ([ADR-1155](docs/decisions/1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).

- **The `fapi1-advanced` security profile requires `nonce` and `state`.** It refuses, with `invalid_request`, an
  authorization request for the `openid` scope without `nonce` and any other request without `state` (FAPI 1.0 Advanced
  §5.2.2.2–3, [ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md)).

- **FAPI 2.0 signs with `Ed25519`, never the polymorphic `EdDSA`, DPoP proofs included.** The `fapi2` security profile's
  signing ceiling is `PS256`, `ES256` and `Ed25519` (RFC 9864), so a FAPI 2.0 party cannot sign with an Ed448 key
  through `EdDSA`, and `dpop_signing_alg_values_supported` is under the same ceiling
  ([ADR-1272](docs/decisions/1272-signature-algorithms-are-fully-specified-and-bound-to-their-curve.md), issue
  [#333](https://github.com/NCodeGroup/identity/issues/333)).

- **A pushed `request_uri` is consumed once and bound to the client that pushed it, and client secrets are never
  persisted with it** ([ADR-1089](docs/decisions/1089-pushed-authorization-requests.md)). A replay is rejected, a
  `request_uri` issued to another client is rejected with `invalid_request_uri`, and `client_secret`, `client_assertion`
  and `client_assertion_type` are stripped before persistence and never replayed. The PAR endpoint derives the response
  delivery when a request is pushed (RFC 9126 §2.1), so it refuses a `redirect_uri` the client has not registered at
  push time rather than at the authorization endpoint
  ([ADR-1201](docs/decisions/1201-authorization-responses-are-delivered-by-registered-response-mode-handlers.md), issue
  [#238](https://github.com/NCodeGroup/identity/issues/238)).

- **A client authenticates only with the method it is registered for.** A client's `token_endpoint_auth_method` (RFC
  7591 §2) is the one method that authenticates it at every endpoint that authenticates clients (token, pushed
  authorization request, introspection, revocation, device authorization and back-channel authentication), and it
  decides the client's type: `none` registers a public client, any other method a confidential one. A confidential
  client that presents only its `client_id`, a credential of another method, and a secret presented by a public client
  are `invalid_client`, as is a request without a secret in HTTP Basic credentials (no colon). A client that registers
  no method uses the RFC 7591 default, `client_secret_basic`, unless its platform, team or tenant configures another, and
  every stored client names its method: the management API's client create and settings update write the client's
  effective method into its settings document when it names none, so a client created without one is a confidential
  client that authenticates with HTTP Basic once it holds a secret, and a change to the platform's, a team's or a tenant's
  `token_endpoint_auth_method` moves no stored client's method; an empty `token_endpoint_auth_method` is not a valid value. `token_endpoint_auth_methods_supported`
  is the ceiling the platform, team, tenant and security profile impose over the registered method: a client registered outside
  it cannot authenticate, dynamic client registration refuses such a method with `invalid_client_metadata`, and an empty
  list permits no method, so disjoint ceilings, such as a tenant that allows only `client_secret_basic` and a client
  under the FAPI 2.0 profile, refuse every client-authentication method. The authorization endpoint identifies the
  client by its `client_id` and never authenticates it, so a client under a profile whose ceiling excludes public
  clients, such as FAPI 2.0, reaches the profile's own rules, and it refuses a request that carries a `client_secret`
  with `invalid_request`. The developer single-page-application clients register `none`, a conformance client registers
  only its configured method, and the bootstrap administrator registers `client_secret_basic` and presents its secret
  with HTTP Basic, as does the API reference's client-credentials login
  ([ADR-1145](docs/decisions/1145-a-client-authenticates-only-with-its-registered-method.md), [ADR-1391](docs/decisions/1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).

- **Every client secret is held in the form its client's method needs.** `IClientSecretIssuer` issues every client
  secret, for the management API, dynamic client registration and the seeds, as `ClientAuthenticationMethodDescriptor`'s
  `RequiresClientSecret` and `UsesClientSecretAsKey` call for: a one-way verifier for a method that compares the secret,
  the reversible UTF-8 octets for a method that keys from it, and a verifier for a method that cannot be resolved or is
  not registered. When a client's settings change its method, through either API, `ReconcileClientSecrets` stores a
  reversible secret's verifier in its place or issues a secret the method can use, revealed once in the response (the
  registration's `client_secret`, or the management API's `200 OK` with a `CreatedSecretResource`, which the console
  shows); a registration update also issues one to a client whose secrets expired, a management settings update that
  leaves the method as it is leaves the client's secrets as they are, and neither rewrites a secret when the client's
  method cannot be resolved. `OpenIdClient.SecretVerifiers` holds the verifiers and `SecretKeys` never does, so a client
  that compares its secret cannot register a symmetric request-object algorithm such as `HS256`, and
  `OpenIdConfidentialClient.CredentialId` names the credential that authenticated the client. A verification refused a
  turn of the `ISecretVerificationGate` is answered `503 Service Unavailable` with `temporarily_unavailable` and
  `Retry-After: 1`, because the secret was never compared ([#163](https://github.com/NCodeGroup/identity/issues/163), [ADR-1391](docs/decisions/1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md)).

- **Client credentials stop authenticating at their expiry, and client secrets have at least 256 bits.** A client secret
  or registered key whose `expiresWhen` has passed does not authenticate its client.
  `SharedSecretPolicy.MinimumEntropyBits` (256) is the floor for every generated shared secret:
  `ISecretGenerator.GenerateSharedSecretVerifier` and `GenerateReversibleSharedSecret` throw below it, and creating a client secret below it answers `400`
  ([#445](https://github.com/NCodeGroup/identity/issues/445),
  [ADR-1320](docs/decisions/1320-a-rotated-client-secret-overlaps-its-replacement-until-it-expires-or-is-deleted.md)).

- **Client assertions have a bounded lifetime, and JWT lifetime validation reads `iat`** (issue
  [#78](https://github.com/NCodeGroup/identity/issues/78),
  [ADR-1124](docs/decisions/1124-jwt-lifetime-cap-and-client-assertion-max-lifetime.md)). A `private_key_jwt` /
  `client_secret_jwt` assertion is rejected with `invalid_client` when its lifetime exceeds the non-discoverable
  `client_assertion_max_lifetime` setting (`OpenIdSettingKeys.ClientAssertionMaxLifetime`, default five minutes, Auth0's
  maximum). The setting is a `Min` ceiling, so a tenant or client may only shorten it, and the FAPI 2.0 security profile
  pins it at five minutes so a raised platform-wide value never reaches a FAPI client. The lifetime runs from the earlier
  of the assertion's `iat` and `nbf` to its `exp`, or, when it carries neither, from the validation time plus the clock
  skew, so it bounds both how long an assertion stays usable and how stale a pre-minted one may be (RFC 7523 §3). The
  client-assertion replay record is retained for `exp` plus the effective clock skew. The cap is a general
  JWT-validation capability: `ValidateTokenLifeTimeOptions.MaxLifetime` sets `TokenLifetimeRequirement.MaxLifetime`, and
  a set maximum also requires `exp`. The lifetime requirement validates `iat` for every token: a present `iat` must be a
  valid `NumericDate` and may not be later than the clock skew in the future, matching `nbf` (FAPI 2.0 §5.3.2.1). A
  client assertion whose header or payload decodes to a JSON array, string or number is answered with `400`
  `invalid_client`, like any other malformed assertion.

- **A DPoP proof or client assertion is accepted once, across every replica.** The replay stores record a DPoP proof or
  a `private_key_jwt` / `client_secret_jwt` assertion with one atomic add-if-absent, so two concurrent presentations of
  the same one cannot both pass, and with Valkey configured the record holds across every replica
  ([ADR-1164](docs/decisions/1164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)).

- **A client's public keys have one source and are imported under one policy.** A client's keys are the keys stored for
  it (registered by value at dynamic client registration or imported through the management API) or, when it has a
  `jwks_uri`, the keys it publishes there, never both; every consumer (client assertions, request objects, CIBA
  requests, encryption to the client and `self_signed_tls_client_auth`) resolves the same set. A client with a
  `jwks_uri` is verified only with the keys published there, so a key the client rotates out at its `jwks_uri` verifies
  nothing, and a `jwks_uri` that cannot be fetched or whose set is refused leaves the client with no public keys. The
  management API refuses with `409` a settings write that adds a `jwks_uri` while public keys are stored, or a public
  key added while a `jwks_uri` is set. Registration imports the keys of a `jwks` as the client's secrets, every
  registration response projects them back from the stored keys, and a registration update (RFC 7592) sets the client's
  public keys to its `jwks`, or removes them when it switches to a `jwks_uri`
  ([ADR-1161](docs/decisions/1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)).

- **A client's key set carries only consistent public keys.** A key carrying any private or symmetric member (`d`, `p`,
  `q`, `dp`, `dq`, `qi`, `oth`, `k`, `priv`) is refused at registration, at the management import, and by every key-type
  descriptor, and a `jwks_uri` set that publishes one is refused as a whole, so no private or symmetric key material is
  stored in settings or echoed back. A key's `use`, `key_ops`, and `alg` must agree with each other and with the key at
  every entry point, including the management update of a client key's metadata
  ([ADR-1161](docs/decisions/1161-a-clients-public-keys-have-one-source-and-are-imported-under-one-policy.md)).

- **`tls_client_auth` validates the presented certificate's chain in the server and fails closed.** Matching a
  registered subject is not enough, and the server never trusts a TLS terminator to have validated the chain, so a
  self-signed certificate carrying a client's subject does not authenticate. The chain is built against the tenant- or
  client-scoped `tls_client_auth_trust_anchors` (PEM CA certificates; a self-issued one is an anchor, any other an
  intermediate) with custom root trust, the forwarded intermediates as untrusted hints, the `clientAuth` extended key
  usage, validity, and `tls_client_auth_revocation_mode`. A `tls_client_auth` client with no trust anchors does not
  authenticate. The replaceable `IClientCertificateChainValidator` performs the check, and a failure is audited as
  `client_certificate_untrusted`
  ([ADR-1173](docs/decisions/1173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md)).

- **Behind a proxy, a forwarded certificate that cannot be decoded fails the request, and the connection's certificate
  is never taken for the client's.** When a forwarded certificate header is configured it is the only source: an absent
  header is no certificate, and a value the configured format cannot decode is `invalid_request` with a warning naming
  the header and the format, so the proxy's own certificate on a re-encrypting hop never stands in for the client's
  ([ADR-1172](docs/decisions/1172-forwarded-client-certificates-are-decoded-by-a-registered-format.md)).

- **Client certificates are checked for revocation through OCSP and every CRL form, failing closed.**
  `tls_client_auth_revocation_mode` is `online` (the default), `online_soft_fail`, which refuses a revoked certificate
  and accepts an unknown status with a warning, or `none`, which a PKI that publishes no revocation data sets.
  `IClientCertificateRevocationChecker.CheckAsync` takes a `ClientCertificateRevocationContext` (`Certificate`,
  `Issuer`, `TrustAnchor`, `Intermediates`, `Depth`) and answers `Good`, `Revoked`, or `Unknown`
  (`ClientCertificateRevocationStatus`); `Unknown` is the zero value, so a checker that cannot tell fails closed. The
  chain validator asks it about every element of a chain except the trust anchor, and under `online` the chain passes
  only when every answer is `Good`. The default is registered with `TryAddSingleton`, so a host registers its own in its
  place, and asks the `IClientCertificateRevocationSource`s that `OpenId:ClientCertificateRevocation:Sources` selects,
  in the configured order, until one answers `Good` or `Revoked`. Each source is described by a registered
  `ClientCertificateRevocationSourceDescriptor` of the same name (`ClientCertificateRevocationSourceNames` names the
  built-in `ocsp` and `crl`), collected by `IClientCertificateRevocationSourceCatalog`, so a package adds a source with
  its descriptor and the configured names are validated when the host starts without constructing a source: a name no
  descriptor registers, or a name listed twice, stops the host, and so does a registered source that no descriptor
  names or a descriptor without a source, which the host finds as it resolves the checker at start. With no sources
  configured, every registered source is
  asked in its descriptor's default order, `ocsp` then `crl`, and a deployment that cannot reach its PKI's OCSP
  responders configures `crl` alone so it never waits for a responder's timeout. The `ocsp`
  source queries the responders that the certificate's authority information access names (RFC 6960 with the RFC 5019
  profile: a SHA-1 `CertID`, no nonce, GET up to 255 characters and POST beyond), accepts a response signed by the
  issuer or by a responder it issued with `id-kp-OCSPSigning`, checks a responder without `id-pkix-ocsp-nocheck` through
  its CRLs, and caches a response until its `nextUpdate`. The `crl` source reads the certificate's `http` and `https`
  distribution points, accepts only a current DER list, and follows RFC 5280 §6.3.3: partitioned lists (`reasons`,
  `onlySomeReasons`), indirect lists (`cRLIssuer`, `indirectCRL`, `certificateIssuer`), delta lists (`freshestCRL`,
  `deltaCRLIndicator`), `removeFromCRL`, and a list signed by the issuing certificate or by another key whose
  certificate chains to the same trust anchor and is not revoked; a list signed with RSASSA-PSS verifies. A list that is
  not DER, not current, not properly signed, or not understood is `Unknown`. Log events 2050 and 2061 name the
  revocation source and record the `Location`
  ([ADR-1173](docs/decisions/1173-tls-client-auth-certificates-chain-to-configured-trust-anchors.md),
  [ADR-1252](docs/decisions/1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md),
  [ADR-1259](docs/decisions/1259-revocation-is-checked-by-registered-sources-ocsp-before-every-crl-form.md),
  [ADR-1400](docs/decisions/1400-the-revocation-sources-are-described-and-configuration-selects-and-orders-them.md),
  issues [#207](https://github.com/NCodeGroup/identity/issues/207), [#255](https://github.com/NCodeGroup/identity/issues/255),
  [#38](https://github.com/NCodeGroup/identity/issues/38)).

- **Revocation data is fetched only by the server, through the outbound-request guard, and its retrieval is shared and
  bounded.** The platform chain engine retrieves nothing: certificate downloads are disabled and its own revocation
  check is off (`X509RevocationMode.NoCheck`), so no AIA, CRL, or OCSP URL in a presented certificate is fetched by the
  platform. The server fetches revocation data through the guarded outbound client, which follows no redirect, so a
  certificate cannot direct the server to an internal address. `ClientCertificateRevocationOptions` from
  `OpenId:ClientCertificateRevocation` (`FetchTimeout`, default 5 seconds; `MaxResponseSizeBytes`, default 10 MiB;
  `MaxCacheDuration`, default one hour; `FailureCacheDuration`, default one minute) is validated when the host starts.
  Concurrent checks of a list share one retrieval, which checks the caches again inside the shared retrieval, so a check
  that misses a CRL or OCSP result recorded a moment earlier finds it and does not download it once more; a parsed list
  is cached per location until its `nextUpdate` or `MaxCacheDuration` and verified by each check; and a retrieval or
  responder location that yields no usable answer is not repeated for `FailureCacheDuration`
  ([ADR-1252](docs/decisions/1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md),
  [ADR-1366](docs/decisions/1366-the-revocation-retrieval-checks-its-caches-again-inside-the-shared-retrieval.md),
  issues [#207](https://github.com/NCodeGroup/identity/issues/207),
  [#255](https://github.com/NCodeGroup/identity/issues/255), [#500](https://github.com/NCodeGroup/identity/issues/500)).

- **Dynamic Client Registration is protected by default.** The `registration_endpoint_allow_anonymous` setting defaults
  to `false`, so registration requires an initial access token, and it merges with the boolean meet, so a more specific
  scope cannot loosen it ([ADR-1063](docs/decisions/1063-dynamic-client-registration.md)). The registration endpoint
  validates every presented initial access token, even when anonymous registration is allowed
  ([ADR-1192](docs/decisions/1192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md)).

- **An initial access token authorizes one registration and expires within 30 days.** Every token, scoped or not, is
  spent by the registration it authorizes, so a leaked token registers at most one client, and only until it expires.
  `lifetimeSeconds` is optional, defaults to one day, and a value outside 1 to 2 592 000 (30 days) is refused with
  `400`; `CreatedInitialAccessTokenResource.ExpiresWhen` is a non-nullable `DateTimeOffset` for a scoped and an unscoped
  token alike, and a token without a lifetime authorizes no registration. `IInitialAccessTokenValidator.TryConsumeAsync`
  spends the token once every check has passed, immediately before the client is stored, and a custom validator returns
  `false` when the token does not authorize registration. A token that is spent, expired, revoked or cannot be spent is
  refused with `401` `invalid_token`, storing nothing. A registration that an earlier check refuses leaves the token
  unspent, and a failure storing the client after the spend leaves it spent. `PersistedGrant<TPayload>.ConsumedWhen`
  records when a grant was spent, `ValidateAsync` answers `null` for a grant whose `ConsumedWhen` is set, and the
  default spends the grant row, whose concurrency token makes the spend atomic across replicas
  ([ADR-1192](docs/decisions/1192-dynamic-registration-binds-a-client-to-the-resource-server-its-token-names.md),
  [ADR-1063](docs/decisions/1063-dynamic-client-registration.md), issue
  [#208](https://github.com/NCodeGroup/identity/issues/208)).

- **Access tokens can be sender-constrained by DPoP (RFC 9449) or mutual TLS (RFC 8705)** (issue
  [#45](https://github.com/NCodeGroup/identity/issues/45),
  [ADR-1097](docs/decisions/1097-sender-constrained-access-tokens-dpop-and-mtls.md)). `IProofOfPossessionService`
  evaluates, once per token request, an optional DPoP proof (the `DPoP` header: a `typ: dpop+jwt` JWS with an embedded
  public JWK, validated for signature, `htm`, `htu`, `iat`, `jti` and replay against the asymmetric
  `dpop_signing_alg_values_supported` set) and/or a mutual-TLS client certificate, and binds the issued token's
  `token_type` (`DPoP` or `Bearer`) and `cnf` claim (`jkt` or `x5t#S256`) uniformly across every grant: the evaluated
  `ProofOfPossessionBinding` is passed to `ITokenGrantHandler.HandleAsync` and carried as
  `CreateSecurityTokenRequest.Confirmation` (`TokenConfirmation`), from which the access-token payload-claims handler
  emits `cnf`. DPoP binds the whole flow: the authorization endpoint validates the `dpop_jkt` parameter (RFC 9449 §10),
  and the token endpoint requires the redeeming proof's key to match it; the refresh-token grant persists the `cnf`
  (`RefreshTokenGrant.Confirmation`, RFC 9449 §5), so rotated tokens stay bound to the original key and a bound refresh
  token presented without a matching proof is rejected. Resource-server endpoints re-check a bound token's `cnf` against
  the presented proof or certificate: the UserInfo and management-API endpoints accept both the `Bearer` and `DPoP`
  authorization schemes and verify the binding, including the DPoP `ath` access-token hash, through
  `IProofOfPossessionService.ValidateResourceAccessAsync`.

- **An access token is bound by its client and by every resource server it targets, and a resource server can constrain
  the tokens issued for it.** The client's effective settings and those of every targeted resource server combine by
  their lattice merges (`IReadOnlySettingCollection.Combine`, the lattice-only combination of sibling scopes' settings):
  a requirement any of them states holds, a lifetime is the shortest, and an algorithm list permits only what all of
  them permit, in the server's order, so a client, or a security profile it selects, that requires encryption or narrows
  the encryption algorithms binds even when the token targets a resource server. A resource server can set
  `access_token_lifetime`, `access_token_signing_alg_values_supported`, `access_token_sender_constraint_required` (the
  token must be DPoP- or mutual-TLS-bound) and `access_token_confirmation_methods_supported` (the `cnf` methods it
  verifies, `jkt` and `x5t#S256`); every audience of a multi-audience token binds it. A client's sender-constraint
  requirement is refused at the token endpoint before its grant is spent, and the FAPI 2.0 profile states its
  sender-constrained-token rule as the ceiling `access_token_sender_constraint_required = true`. An access token is
  always signed with the tenant's asymmetric key, validated through JWKS (RFC 9068, FAPI's `PS256`/`ES256`-only rule); a
  resource server never holds a token-signing key. An encrypted access token is encrypted to the party that reads it: a
  resource server that holds a key it can decrypt with under its own settings gets a token for it alone encrypted to
  that key within the combined settings, and issuance fails with `server_error` rather than encrypt to the tenant's key,
  which it cannot read; a token for it together with another resource server is refused with `invalid_request` rather
  than downgraded; any other encrypted token, including a multi-audience token whose resource servers hold no keys, goes
  to the tenant's key. A missing access-token signing or encryption key is `server_error`. At issuance
  `IResourceServerCredentialResolver` resolves the resource servers a token targets, and `IAccessTokenPolicyService`
  evaluates an `AccessTokenPolicy` under which `ITokenService` issues access tokens; an extension can register its own
  service to add a bound, and `ResolvedResourceServer` carries its `SecretsProvider` (issue
  [#139](https://github.com/NCodeGroup/identity/issues/139),
  [ADR-1075](docs/decisions/1075-runtime-consumption-of-resource-server-secrets.md),
  [ADR-1158](docs/decisions/1158-an-access-token-is-bound-by-its-client-and-every-resource-server-it-targets.md)).

- **Resource indicators (RFC 8707) are a multi-valued `resource` parameter that narrows and validates the access token's
  audience** (issue [#50](https://github.com/NCodeGroup/identity/issues/50),
  [ADR-1078](docs/decisions/1078-resource-indicators-rfc-8707.md)). The authorization endpoint rejects an unknown or
  disabled resource with `invalid_target` (`IOpenIdErrorFactory.InvalidTarget(...)`), the authorization-code token
  request rejects a resource not authorized at the authorization endpoint, and
  `IResourceServerCredentialResolver.ResolveAsync(…, requestedResources, …)`, the single choke point every grant funnels
  through, narrows `aud` to the requested resources and rejects any that does not resolve to an enabled resource server
  owning one of the token's effective scopes. Requested scope values are confirmed against the live resource-server
  scope definitions. The surface includes `OpenIdCommonParameters.Resources`, `Resources` on
  `IBaseAuthorizationRequestValues`, `IAuthorizationRequest`, `ITokenRequest` and `CreateSecurityTokenRequest`, and
  `IClientScopeService.GetEnabledResourceServerIdentifiersAsync`.

- **JWT access tokens are explicitly typed `at+jwt` (RFC 9068).** The `access_token_type` setting defaults to `at+jwt`
  (RFC 9068 §2.1), and the resource-server access-token authentication path requires the `at+jwt` type (RFC 9068 §4).

- **The server validates every token it issued under one policy, through one seam, `IIssuedTokenValidator`.** UserInfo,
  the management API, introspection, token exchange and the end-session `id_token_hint` use it. Access tokens are
  checked against the tenant's access-token algorithm lists and keys, with `typ` `at+jwt`, the issuer, the lifetime and
  session revocation; ID tokens against `id_token_signing_alg_values_supported` with `typ` `JWT`. Introspection reports
  an ID token as no access token and a token of a revoked session as inactive. Token exchange picks the policy by
  `subject_token_type` and accepts a `jwt` subject token only when it is an access token or an ID token, so it refuses a
  logout token or any other JWT of the wrong type. `ResolvedResourceServer` requires `IsSystem` and `Scopes`, and
  `IResourceServerCredentialResolver` exposes `ResolveByIdAsync`
  ([ADR-1167](docs/decisions/1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)).

- **Introspection discloses a token only to the party that consumes it.** An access token is reported active only to a
  client acting for a resource server in the token's audience, with `aud` reduced to that resource server and `scope` to
  its scopes; a refresh token is reported active only to the client it was issued to; everything else is
  `{"active": false}` (RFC 7662 §4, RFC 9701 §5), so no client reads another client's refresh token or what an encrypted
  access token hides. A client bound to a resource server is accepted at no other endpoint, and every other endpoint
  refuses it unless it opts in. An integration that introspects access tokens uses a client bound to its resource
  server: `OpenIdClient` and `PersistedClient` carry `ResourceServerId` (a foreign key in the client table),
  `IClientStore` exposes `GetPageByResourceServerAsync`, `IntrospectTokenCommand` carries the caller's
  `ResolvedResourceServer`, and `IOpenIdClientFactory.CreatePublicClientAsync` takes an `OpenIdClientDefinition`
  ([ADR-1168](docs/decisions/1168-introspection-discloses-a-token-only-to-the-party-that-consumes-it.md)).

- **The UserInfo endpoint requires an access token.** It authenticates only a bearer access token (in the
  `Authorization` header or the form body) whose audience includes the OpenID resource server, issued to an enabled
  client, and never the end-user's session cookie; anything else, an invalid or expired token included, is `401`
  `invalid_token` ([ADR-1155](docs/decisions/1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).

- **The ID token carries only the OpenID Connect ID-token claim set.** The client is identified in the ID token by the
  standard `azp` (authorized party) alone (`DefaultGetIdTokenPayloadClaimsHandler`); `tid` and `client_id` are JWT
  access-token claims (RFC 9068 §2.2) and appear only on the access token (`DefaultGetAccessTokenPayloadClaimsHandler`),
  where tenant- and client-scoped authorization reads them, so the ID token never discloses a tenant id or a client id
  the client did not request (OpenID Connect Core 1.0 §2) (issue
  [#131](https://github.com/NCodeGroup/identity/issues/131)).

- **An encrypted ID token is encrypted to the client's own public key with the algorithms it registered.** Under
  `id_token_encryption_required`, a client that registers no `id_token_encrypted_response_alg` is refused rather than
  given a guess. Logout tokens follow the client's ID token registration, and one the server cannot protect as
  registered is skipped with a warning
  ([ADR-1155](docs/decisions/1155-jwts-exchanged-with-a-client-follow-its-registered-metadata.md)).

- **Every authentication entry point resolves the verified credential to one stable, server-owned principal id** (issue
  [#60](https://github.com/NCodeGroup/identity/issues/60),
  [ADR-1094](docs/decisions/1094-resolve-principal-at-every-authentication-entry-point.md)). Interactive login (after
  the pluggable `IInteractiveCredentialVerifier`, before the session is established), resource-owner-password and bearer
  re-authentication all key grants, consent, sessions and revocation by `SubjectAuthentication.PrincipalId`, so revoking
  everything for a subject and reusing consent span a user's activity however they authenticated, and the internal
  `LocalAccountId` is never emitted as `sub`
  ([ADR-1035](docs/decisions/1035-federated-principals-and-identity-resolution.md)).
  `IPrincipalResolver.ResolvePrincipalIdAsync(OpenIdContext, ClaimsPrincipal, CancellationToken)` is a self-contained
  unit of work with no persistence types in its signature. The persistence and request `SubjectId` members hold the
  resolved principal id.

- **A client registered with `subject_type = pairwise` receives a per-sector Pairwise Pseudonymous Identifier as its
  `sub`**, so relying parties in different sectors cannot correlate the same end-user (issue
  [#47](https://github.com/NCodeGroup/identity/issues/47), OpenID Connect Core 1.0 §8,
  [ADR-1093](docs/decisions/1093-pairwise-subject-identifiers.md)). The identifier is a stateless, deterministic
  derivation, `base64url(SHA-256(sectorIdentifier ∥ principalId ∥ tenantId ∥ pairwise_salt))`, through the replaceable
  `ISubjectIdentifierService`, with no table or store of its own. The principal id stays the one internal subject behind
  grants, consent, sessions and revocation; pairwise changes only the emitted `sub` in the ID token, access token,
  back-channel `logout_token` and refresh-token introspection (UserInfo echoes the access token's `sub`). The sector is
  the host of the client's `sector_identifier_uri` when set, else the common host of its `redirect_uris`, else the
  `client_id`; the optional secret tenant setting `pairwise_salt` hardens the derivation. Discovery advertises
  `subject_types_supported` as `["public", "pairwise"]`, and `subject_type` defaults to `public`.

- **An essential `id_token.acr` claim request with specific `values` is enforced** (issue
  [#57](https://github.com/NCodeGroup/identity/issues/57), OpenID Connect Core 1.0 §5.5.1.1).
  `DefaultValidateSubjectAuthenticationHandler` denies with `access_denied` unless the subject's satisfied `acr` is
  among the requested values, so no token is issued with a mismatched `acr`. Voluntary `acr` requests, including the
  whole `acr_values` parameter, stay best-effort
  ([ADR-1091](docs/decisions/1091-acr-emission-and-conditional-auth-time.md)).

- **Consent is granular: the end-user grants a subset of the requested scopes, and the OP issues only that subset**
  (issue [#40](https://github.com/NCodeGroup/identity/issues/40),
  [ADR-1081](docs/decisions/1081-granular-consent-and-authorized-applications.md)). The consent record tracks the scopes
  the subject decided on (`PersistedConsent.RequestedScopes`, `ConsentEntity.RequestedScopesJson`) and the subset they
  granted, so the authorize endpoint prompts only when a request introduces an undecided scope and otherwise issues the
  intersection of the request and the granted scopes. `IConsentService.ResolveConsentAsync` returns a
  `ConsentResolution`, `GrantConsentAsync` takes the requested and granted sets (so re-consent can withdraw a scope),
  and `IAuthorizationRequest.GrantedScopes` carries the narrowed set into the authorization code and tokens so the token
  endpoint cannot re-expand it.

- **A blocked principal is shut out everywhere.** Blocking terminates every session of the principal with `user_blocked`
  (`SessionTerminationReasons.UserBlocked`), which revokes its grants and sends back-channel logout, and keeps its
  consents; every grant refuses a blocked principal with `access_denied`, and the authorization request and the
  interactive sign-in refuse it with `access_denied`
  ([ADR-1226](docs/decisions/1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md),
  issue [#289](https://github.com/NCodeGroup/identity/issues/289)).

- **Every grant that issues tokens for a subject validates it.** The device-code grant, the CIBA grant (at the token
  endpoint, and at the approval for every delivery mode) and the token-exchange grant send
  `ValidateSubjectAuthenticationCommand`, the device-code and CIBA grants before they spend their credential, so a
  disabled, deleted or blocked end user is refused with `access_denied`, and a refused CIBA approval denies the request.
  A device code and an `auth_req_id` are redeemed only by the client they were issued to (`invalid_grant`). The device
  verification endpoint checks the server-side session of its cookie and asks for a login when that session has ended.
  `ValidateSubjectAuthenticationCommand.OpenIdRequest` is nullable (`null` outside a protocol request skips the
  request's `max_age`, `acr_values` and `claims` checks), and `ICibaRequestService.ApproveAsync` returns `false` for a
  refused approving subject
  ([ADR-1225](docs/decisions/1225-every-grant-that-issues-tokens-for-a-subject-validates-it.md), issue
  [#283](https://github.com/NCodeGroup/identity/issues/283)).

- **A replayed authorization code terminates its session, and first-party resource endpoints enforce the termination.**
  Per RFC 6749 §4.1.2 and the OAuth Security BCP (RFC 9700), a second redemption of a one-time authorization code is a
  compromise signal: the token endpoint terminates the code's session with
  `SessionTerminationReasons.AuthorizationCodeReplay` (`authorization_code_replay`), so the grants issued under it, the
  access and refresh tokens issued from that code included, are revoked, its relying parties receive back-channel logout
  and the termination is audited; a code that expired unused terminates nothing
  ([ADR-1228](docs/decisions/1228-a-session-is-terminated-with-the-clients-it-lists-when-claimed-and-a-replayed-code-terminates-it.md),
  issue [#287](https://github.com/NCodeGroup/identity/issues/287)). The session identifier (`sid`, RFC 9068 §2.2.1) is
  always emitted into subject-bound access tokens, and every endpoint that authenticates a self-issued access token
  (UserInfo, introspection, the management API) rejects a token whose `sid` resolves to a revoked session
  (`invalid_token`). There is no global access-token denylist and no per-token store: one indexed session lookup on
  first-party access-token authentication; external resource servers treat the JWT as self-contained (issue
  [#131](https://github.com/NCodeGroup/identity/issues/131),
  [ADR-1112](docs/decisions/1112-authorization-code-replay-revokes-session.md), which refines
  [ADR-1028](docs/decisions/1028-token-revocation-refresh-tokens-only.md)).

- **A concurrent second spend of a single-use grant is answered as a second spend.** Of two requests that spend one
  authorization code, device code, CIBA request, pushed authorization request, initial access token, or `state` of a
  paused authorization flow at the same moment, `ConsumeOnceOrDefault` answers the one that loses the race on the grant
  row's concurrency token as a second spend: `invalid_grant` for an authorization code, a device code and a CIBA
  request, `invalid_request_uri` for a pushed request, `401` `invalid_token` for an initial access token, and `400` for
  the `state` of a paused authorization flow at the continue endpoint
  ([ADR-1012](docs/decisions/1012-concurrency-tokens-are-interceptor-managed-and-a-lost-race-surfaces-as-a-store-neutral-exception.md), issue
  [#257](https://github.com/NCodeGroup/identity/issues/257)).

- **A revocation wins a concurrency race.** The revocation endpoint, the device user code, logout and session eviction,
  the replay of an authorization code, and the management revocations read the grants again in a fresh unit of work and
  retry when another write changed a grant first, so a concurrent write such as a sliding-expiration update never leaves
  a grant active or reaches the caller as `500` or `412`. `IPersistedGrantService.RevokeWhereAsync` revokes the grants a
  `GrantFilter` (including `GrantFilter.GrantId`) selects, and `DefaultRevokeGrantsOnSessionTerminatedHandler` revokes
  through `IPersistedGrantService` ([ADR-1215](docs/decisions/1215-a-revocation-wins-a-concurrency-race.md), issue
  [#269](https://github.com/NCodeGroup/identity/issues/269)).

- **A rotated refresh token is spent with its successor, and a token two requests present at once is spent once.**
  Refresh-token rotation goes through `ITokenService.RotateRefreshTokenAsync`, which marks the spent grant consumed and
  stores the successor with `IPersistedGrantService.ReplaceOnceOrDefault` in one unit of work before any token is
  issued, and `IPersistedGrantService.TryRevokeOnceAsync` spends a token that has no successor; a custom implementation
  implements the three members. Of two requests that present one refresh token at once, the losing request receives
  `invalid_grant` and the `token.refresh.replay` audit event, and under `refresh_token_reuse_policy` `revoke_all` the
  whole rotation family is revoked, the winner's successor token included (RFC 9700 §4.14.2)
  ([ADR-1211](docs/decisions/1211-a-rotated-refresh-token-is-spent-with-its-successor.md), issue
  [#260](https://github.com/NCodeGroup/identity/issues/260)).

- **A replayed refresh token revokes its whole rotation family** (issue
  [#42](https://github.com/NCodeGroup/identity/issues/42),
  [ADR-1087](docs/decisions/1087-refresh-token-reuse-detection-and-family-revocation.md)). A revoked refresh token
  presented again, outside the client's `refresh_token_reuse_interval`, is audited (`token.refresh.replay`) and handled
  per the client setting `refresh_token_reuse_policy` (`OpenIdConstants.RefreshTokenReusePolicy`): `revoke_all`, the
  default, revokes every refresh token descended from the same authorization grant, so a stolen token cannot be
  exchanged even when the attacker races the legitimate client ([RFC 9700](https://www.rfc-editor.org/rfc/rfc9700));
  `none` only audits. Rotated tokens are linked by a server-generated lineage id persisted on the grant
  (`RefreshTokenLineageId` on `PersistedGrant`, `PersistedGrant<TPayload>`, `CreateSecurityTokenRequest` and
  `GrantEntity`); a fresh issuance starts a family and each rotation inherits its parent's lineage.
  `IGrantStore.RevokeByRefreshTokenLineageAsync` and `IPersistedGrantService.RevokeByRefreshTokenLineageAsync` revoke a
  family, and the revocation endpoint revokes every refresh token of the presented token's rotation family
  ([ADR-1217](docs/decisions/1217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md), issue
  [#270](https://github.com/NCodeGroup/identity/issues/270)).

- **A public client's refresh token is rotated unless it is sender-constrained.** `refresh_token_rotation_policy` is
  `unbound` (the default) or `always`: `unbound` rotates a refresh token that is bound neither to a proof-of-possession
  key nor to client authentication, and `always` rotates every refresh token. No setting turns off the rotation of a
  public client's unbound refresh token (RFC 9700 §4.14.2), and the FAPI 2.0 security profile defaults a client to
  `unbound`. The policy merges by replacement, so a tenant's `always` is a default that a client's own `unbound`
  overrides; a public client's unbound token rotates whatever the policy
  ([ADR-1216](docs/decisions/1216-a-refresh-token-is-rotated-unless-it-is-sender-constrained.md), issue
  [#268](https://github.com/NCodeGroup/identity/issues/268)).

- **A refresh token family expires at a fixed horizon, which the token response announces.** A rotation family expires
  `refresh_token_lifetime` after the grant that began it, however often its tokens are used, rotated or retried, so
  `absolute` is absolute. Under `sliding`, a token expires `refresh_token_sliding_lifetime` (default 15 days) after it
  was issued or last used, never after its family. Every token response that carries a refresh token, CIBA push delivery
  included, carries the de-facto `refresh_token_expires_in` member, the seconds left until the family's horizon; the
  protocol client reads it as `TokenResponse.RefreshTokenExpiresIn`, which the BFF uses to cap a session. A token whose
  family has passed its horizon is refused, so shortening the lifetime ends existing families, and introspection reports
  such a token as inactive. Token exchange starts the family of a refresh token it issues at the subject token's
  `auth_time` and refuses a subject token without one with `invalid_grant`. `ITokenService` returns `IssuedRefreshToken`
  for refresh tokens, and `RefreshTokenGrant` carries `LineageCreatedWhen`
  ([ADR-1297](docs/decisions/1297-a-refresh-token-family-expires-at-a-fixed-horizon-that-the-token-response-announces.md),
  issue [#417](https://github.com/NCodeGroup/identity/issues/417)).

- **Every end-user session ends, and its lifetimes are required.** `session_absolute_lifetime` defaults to 30 days and
  accepts one second to 365 days, and `session_idle_timeout` defaults to 3 days and accepts one second to 100 days; a
  zero value is refused. `CreateSessionRequest.AbsoluteLifetime` and `CreateSessionRequest.IdleTimeout` are required
  positive `TimeSpan`s, and the required `SessionGrant.IdleTimeout` and `SessionGrant.AbsoluteExpiresWhen` carry the
  idle window and the absolute end with the session. A session's grant always expires, at the end of its idle window
  or, if earlier, at its absolute end, and each renewal of the window extends the expiry in the same
  `IPersistedGrantService.UpdatePayloadAsync` write (`PersistedGrantUpdate<TPayload>.ExpiresWhen`), so the grant prune
  removes an abandoned session after the retention. A stored session without its bounds or its expiry is malformed:
  reading it throws, so it is never served. `SessionInfo.ExpiresWhen`, `SessionResource.ExpiresWhen` and `expiresWhen`
  of `/account/sessions/list` are the time the session ends unless it is used again, never `null`. An authorize
  request that finds an ended session terminates it with `absolute_expiry` or `idle_timeout`, which revokes the grants
  issued under it and notifies its relying parties
  ([ADR-1220](docs/decisions/1220-an-end-user-session-always-ends-and-its-grant-expires-when-it-does.md),
  [ADR-1395](docs/decisions/1395-a-stored-record-that-lacks-what-the-serving-code-writes-is-malformed-and-fails-closed.md),
  issue [#271](https://github.com/NCodeGroup/identity/issues/271)).

- **A session that ends while no request uses it is terminated within a minute.** One replica in each
  `OpenId:EndedSessionTermination:Interval` (one minute by default) revokes the grants issued under each session that
  ended unused, sends back-channel logout to its relying parties (`idle_timeout` or `absolute_expiry`), and audits the
  termination, once: a request and the pass that find the same ended session terminate it once. The claim revokes the
  grants issued under the session in one unit of work, so a failure after it leaves no token of the session active. A
  session the pass cannot terminate, such as a malformed one, is logged with its tenant and identifier and skipped, so
  the tenant's other ended sessions are terminated; a request that reads a malformed session fails closed. The grant
  prune keeps a session until it is terminated and removes it one retention after.
  `IPersistedGrantService.SetRevokedOnceAsync` returns `bool`, `IGrantStore.RemoveEndedBeforeAsync` takes the grant
  types that end by revocation, and `IGrantStore.GetUnrevokedExpiredPageAsync`,
  `IPersistedGrantService.RevokeSessionOnceAsync`, `ISessionService.TerminateIfEndedAsync`,
  `IOpenIdContextFactory.CreateForBackgroundAsync` and `IMaterializedPlatformAccessor` serve the pass, which the grants
  table's index on grant type, revocation and expiry supports
  ([ADR-1223](docs/decisions/1223-a-session-that-ends-unused-is-terminated-once-in-the-background.md),
  [ADR-1395](docs/decisions/1395-a-stored-record-that-lacks-what-the-serving-code-writes-is-malformed-and-fails-closed.md),
  issue [#281](https://github.com/NCodeGroup/identity/issues/281)).

- **Logout revokes only the terminating session's grants**, so signing out of one login leaves the subject's other
  sessions intact (issue [#55](https://github.com/NCodeGroup/identity/issues/55),
  [ADR-1077](docs/decisions/1077-session-scoped-grant-revocation-on-logout.md)). A grant records the opaque `sid` it was
  issued under (`PersistedGrant.SessionId`, `PersistedGrant<TPayload>`, and a `GrantEntity.SessionId` column with a
  `(TenantId, SessionId)` index); the authorization-code, refresh-token and per-access-token UserInfo-claims grants and
  the session grant stamp the `sid` from the authenticated subject (`SubjectAuthentication.SessionId`).
  `DefaultRevokeGrantsOnEndSessionHandler` revokes by `sid` when the session is resolved and falls back to subject-wide
  revocation only when it is not.

- **A logout without a valid `id_token_hint` for the current session asks the end-user first.** When an OP login is
  present and the end-session request carries no valid `id_token_hint` whose `sid` names that login's session, the
  `end_session_endpoint` parks the request as a one-time continuation bound to that session and asks the end-user to
  confirm (OpenID Connect RP-Initiated Logout 1.0 §2): the interactive surface shows its confirmation page at
  `/account/logout/confirm` beneath the tenant route (`logout-confirm`, `logout-cancel`, antiforgery-protected), and a
  host without it gets a minimal page from the core. A logout that names no subject or session leaves the login cookie
  in place, so a cross-site request cannot sign the end-user out. `ResolveEndSessionSubjectCommand` resolves the
  `EndSessionSubject` that `EndSessionCommand` takes; `EndSessionSubject` carries a positional `RequiresConfirmation`,
  so a custom subject resolver states whether the end-user must confirm. `ParkEndSessionRequestCommand` parks a logout
  that needs confirmation and `EndSessionConfirmationChallengeCommand` asks for it, and when an OP login is present its
  session takes precedence over the `id_token_hint`'s `sid` ([#494](https://github.com/NCodeGroup/identity/issues/494),
  [ADR-1380](docs/decisions/1380-a-logout-without-a-valid-id-token-hint-for-the-current-session-is-confirmed.md)).

- **RP-Initiated Logout reads and clears the login in the OP login scheme.** The end-session endpoint reads the
  end-user's login from, and clears it from, the tenant's `authorization_authenticate_scheme` and, when the logout names
  a registered client that overrides it, that client's too, whatever the host's default scheme. It asks for confirmation
  when any login it finds is not the session the `id_token_hint` names, so naming a client cannot hide the end-user's
  login. `EndSessionSubject` carries the schemes as `LoginSchemes`, a positional constructor parameter, so a custom
  `ResolveEndSessionSubjectCommand` handler states the schemes
  ([#515](https://github.com/NCodeGroup/identity/issues/515)).

- **End-session finds the subject from server state, never from the `sub` of the `id_token_hint`.** With pairwise
  subject identifiers ([ADR-1093](docs/decisions/1093-pairwise-subject-identifiers.md)), the hint's `sub` is a
  per-sector pseudonym from a one-way projection. `DefaultResolveEndSessionSubjectHandler` reads only `sid` and
  `client_id` from the hint and looks up the stable subject in the persisted session grant, or in the OP login cookie
  when there is no grant. Subject-keyed revocation therefore matches the subject's grants, and each relying party's
  back-channel `logout_token` carries the pairwise `sub` that party knows. A presented value is matched forward and
  never reversed, so no pseudonym-to-principal map exists to correlate subjects across sectors
  ([ADR-1096](docs/decisions/1096-end-session-subject-from-server-state.md)).

- **The interactive surface's cookies are host-only, and tenants that share an origin keep separate sessions.** The
  session cookie is `__Host-NCode.Identity.Interactive`, the antiforgery cookie `__Host-NCode.Identity.Antiforgery`, the
  request-token cookie `__Host-XSRF-TOKEN` (`InteractiveAuthenticationDefaults.SessionCookieName`,
  `AntiforgeryCookieName`, `AntiforgeryTokenCookieName`), and the Session Management cookie `__Host-opbs`. All are
  `Path=/` without `Domain`, so a sibling subdomain cannot set them, and a tenant's session cookie name is qualified by
  the tenant's path. Separate cookies are not an isolation boundary within an origin; tenants that must be isolated are
  selected by host ([#442](https://github.com/NCodeGroup/identity/issues/442),
  [ADR-1310](docs/decisions/1310-the-interactive-surfaces-cookies-are-host-only-and-a-tenants-session-is-named-for-its-path.md),
  [ADR-1193](docs/decisions/1193-the-interactive-surface-is-served-under-the-tenant-route.md)).

- **Every page the server writes has a strict Content-Security-Policy and cannot be framed, except the
  `check_session_iframe` and, by its client's origin, the `form_post` response.**
  `HttpResponse.SetHtmlPageSecurity(HtmlPageSecurityPolicy)` writes each page's policy (`default-src 'none'`, `base-uri`
  and `form-action` `'none'` unless the page lists them, `frame-ancestors 'none'`) with `X-Frame-Options: DENY`,
  `X-Content-Type-Options: nosniff`, `Referrer-Policy: same-origin` and `no-store`; `ContentSecurityPolicyDirective`,
  `ContentSecurityPolicyDirectives` and `ContentSecurityPolicySources` build it. The interactive pages run only the
  `apps/auth` scripts and styles that carry the response's nonce, including a page served to a browser that already
  holds the antiforgery cookie, and a bundle served from `InteractiveAuthentication:BundleRootPath` runs its scripts
  only when its build writes the nonce placeholder (`@ncode/vite-config`'s `CSP_NONCE_PLACEHOLDER`); the `form_post`
  response runs only its own script and may be framed only by its redirect URI's origin; the front-channel logout page
  frames only the clients' logout origins; the `check_session_iframe` runs only its script and stays embeddable
  ([ADR-1256](docs/decisions/1256-every-page-the-server-writes-has-a-strict-content-security-policy-and-is-framed-only-by-design.md),
  issue [#303](https://github.com/NCodeGroup/identity/issues/303)).

- **Interactive return URLs refuse tabs, newlines and backslashes.** `LocalUrl.IsLocal`
  (`NCode.Identity.OpenId.Protocol.Abstractions`) is the one local-path rule; the interactive surface and the BFF use
  it, so a return URL such as `/\t/evil.example` cannot redirect off-site after sign-in.

- **The frontend bootstrap document never takes its issuer or API base from the request.** With `Frontend:Issuer` unset,
  `/config/bootstrap.json` publishes the stored issuer of the tenant that serves the surface: the `StaticSingle` tenant,
  or the root tenant under the dynamic strategies, so a forged or proxied `Host` cannot send the bundle to an authority
  the operator never configured. Until that tenant is provisioned, it publishes its strategy's default issuer, and when
  neither exists the endpoint answers `500` rather than guess. With `Frontend:ApiBaseUrl` unset, it publishes the host's
  root-relative path base, never anything taken from the `Host` header. The response is `Cache-Control: no-store`, and `Frontend:Issuer` and `Frontend:ApiBaseUrl` are
  overrides (issues [#113](https://github.com/NCodeGroup/identity/issues/113),
  [#442](https://github.com/NCodeGroup/identity/issues/442),
  [ADR-1182](docs/decisions/1182-the-frontend-bootstrap-issuer-is-the-serving-tenants-not-the-requests.md),
  [ADR-1312](docs/decisions/1312-the-frontend-bootstrap-api-base-is-root-relative-unless-configured.md)).

- **A self-service session revoke is guarded to the authenticated subject**, so a crafted `sid` cannot terminate another
  subject's session (issue [#63](https://github.com/NCodeGroup/identity/issues/63)).

- **A blocked or disabled end user cannot sign in, and the client is answered `access_denied`.** The interactive sign-in
  validates the end user's standing (`ValidateSubjectStandingCommand`, which the blocked-principal and local-account
  checks handle, and which every subject validation applies) before it creates a session, so a blocked principal or a
  disabled local account gets neither a session nor a cookie: a sign-in that resumes an authorization request answers it
  with `access_denied` at the client's redirect URI through `IContinueProvider.RefuseAsync` and
  `IContinueRefusalService`, and any other sign-in shows the refusal on the login page.
  `ILocalAccountStore.VerifyCredentialAsync` returns the disabled account of a valid credential. The authorization
  request answers a signed-in end user whose standing is refused with `access_denied` for every `prompt`, a consent
  denial answers the request with `access_denied`, and the self-service pages serve a cookie only while the session it
  names is active
  ([ADR-1229](docs/decisions/1229-a-refused-end-user-cannot-sign-in-and-the-client-is-answered-access-denied.md), issue
  [#289](https://github.com/NCodeGroup/identity/issues/289)).

- **Deleting or disabling a local account ends its sessions, and every token request checks the account.** Disabling an
  account terminates its principal's sessions with `account_disabled`, and deleting it terminates them with
  `account_deleted` before it removes anything (`SessionTerminationReasons.AccountDisabled` and `AccountDeleted`): every
  grant of the principal is revoked, its relying parties receive back-channel logout, and each termination is audited. A
  deleted principal is erased with its consents and grants (`IFederatedPrincipalStore.RemoveAsync` erases them), and a
  disabled account keeps its consents. The local-account capability refuses a subject whose account is disabled or gone
  with `access_denied` at the authorization request and at the authorization-code, refresh-token and password grants,
  whichever connection the subject signed in through. `NCode.Identity.OpenId.Accounts.Management` references
  `NCode.Identity.OpenId.Authentication.Abstractions`
  ([ADR-1224](docs/decisions/1224-deleting-or-disabling-an-account-ends-its-sessions.md), issue
  [#280](https://github.com/NCodeGroup/identity/issues/280)).

- **Every password a local account is given meets the password policy.** The management API's create and reset, the
  self-service change and the developer and conformance seeders judge the password with `IPasswordPolicy`
  (`PasswordPolicyContext`, `PasswordPolicyResult`, `PasswordPolicyErrors`) against the tenant's settings: at least
  `password_min_length` characters (15 by default, never below 8, counted as Unicode code points), at most
  `password_max_length` (128 by default, never below 64), no composition rules, and not on a blocklist — the bundled
  list of 46,483 common passwords and the account's own user name or email; a deployment adds a breached-password
  checker by registering an `IPasswordBlocklist`. The management API answers a refused password `400`; the change page
  shows `password_too_short`, `password_too_long` or `password_blocklisted` with guidance, carried by the
  `InteractivePasswordChangeResult` class as an `InteractivePasswordChangeErrors` code
  ([ADR-1236](docs/decisions/1236-a-password-is-judged-by-one-policy-wherever-it-is-set.md), issue
  [#292](https://github.com/NCodeGroup/identity/issues/292)).

- **Failed password attempts are limited per account, and a password refused at the limit costs what a wrong one
  costs.** `IPasswordAttemptLimiter` counts failed passwords per account for the interactive sign-in, the password grant
  and the self-service change in `IExpiringCounterSet` (which offers `RemoveAsync`); once
  `password_failed_attempts_limit` failures (10 by default, at most 100) fall within `password_failed_attempts_window`
  of the first (15 minutes by default), every password for the account is refused with the answer a wrong password gets
  until the window ends. A verified password, and a password an administrator sets, clear the count.
  `ILocalAccountStore.VerifyCredentialAsync` looks the account up once, asks its caller's `isRefusedAsync` about the
  user name the account holds, and returns `LocalAccountVerificationResult` (`UserName`, `Account`, `IsRefused`,
  `IsVerified`); the sign-in, the password grant and the self-service change ask
  `IPasswordAttemptLimiter.IsLimitReachedAsync` through a gate that enforces its answer whatever the store returns, and
  a store that never calls `isRefusedAsync` fails every verification. A password refused at the limit is verified
  against the dummy hash and counted as a failed attempt, so neither the answer nor the work tells whether an account
  has the user name under any spelling a database collation matches
  ([ADR-1237](docs/decisions/1237-failed-password-attempts-are-limited-per-account.md),
  [ADR-1244](docs/decisions/1244-a-password-refused-at-the-attempt-limit-costs-what-a-wrong-one-costs.md), issues
  [#293](https://github.com/NCodeGroup/identity/issues/293), [#300](https://github.com/NCodeGroup/identity/issues/300)).

- **A wrong password takes as long whether or not an account has the user name.** When no account has the user name, the
  account has no password, the ASP.NET Core Identity user is locked out, or the default hasher cannot read the stored
  hash, the store verifies the password against `IPasswordHasher.DummyPasswordHash`, a dummy hash of the current
  parameters, and refuses it, so a wrong password costs the same hash verifications whether or not the account exists
  ([ADR-1239](docs/decisions/1239-a-password-is-verified-in-the-same-time-whether-or-not-its-account-exists.md), issue
  [#298](https://github.com/NCodeGroup/identity/issues/298)).

- **A password is judged, hashed and verified in its NFC form.** The policy counts the code points of a password's
  Unicode Normalization Form C (NFC) and its blocklists see that form, and an `IPasswordHasher` hashes and verifies the
  NFC form of the bytes it is given, so a password typed with another composition of the same characters, such as `e`
  and a combining accent for `é`, verifies; a stored hash of a password in another form verifies and is rehashed in its
  NFC form ([ADR-1238](docs/decisions/1238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md), issue
  [#297](https://github.com/NCodeGroup/identity/issues/297)).

- **A host's own ASP.NET Core Identity pages verify the password the adapter stores.**
  `AddAspNetIdentityLocalAccountStore<TUser>()` wraps the host's `IPasswordHasher<TUser>` registration, made before or
  after the call, in the public `NormalizingPasswordHasher<TUser>`, which hashes and verifies a password's NFC form and
  verifies the typed form as a fallback that asks for a rehash, so the adapter and the host's pages accept every
  composition of a password. A host that registers its `IPasswordHasher<TUser>` after the call, or sets
  `UserManager<TUser>.PasswordHasher`, wraps it in `NormalizingPasswordHasher<TUser>`: the adapter throws
  `InvalidOperationException` rather than hash or verify through a hasher that does not normalize
  ([ADR-1243](docs/decisions/1243-every-aspnet-core-identity-path-hashes-and-verifies-the-nfc-form-of-a-password.md),
  issue [#299](https://github.com/NCodeGroup/identity/issues/299)).

- **The ASP.NET Core Identity adapter stores a password the password policy accepts.** It hashes a password with the
  user manager's password hasher and stores the hash through `IUserPasswordStore<TUser>`, creating the user with
  `CreateAsync(user)` and rotating the security stamp on a reset, so ASP.NET Core Identity's password validators, whose
  default composition rules NIST SP 800-63B forbids, never refuse a password the policy accepted; a transparent rehash
  keeps the security stamp ([ADR-1238](docs/decisions/1238-a-password-is-judged-hashed-and-verified-in-its-nfc-form.md),
  issue [#296](https://github.com/NCodeGroup/identity/issues/296)).

- **The management API authenticates every caller against the control-plane tenant and takes its data scope from the
  route.** Whatever host or path it is reached on (a collection route such as `POST /api/teams/{teamId}/tenants` under `DynamicByPath`,
  a `/api/tenants/{tenantId}/…` route, or the `manage.<apex>` host under `DynamicByHost`), the caller's token is
  validated with the control-plane tenant (the root tenant, or a static strategy's own tenant). `ITenantStrategy` and
  `ITenantResolver` expose `ResolveControlPlaneTenantAsync`, `IOpenIdTenantFactory` exposes
  `CreateControlPlaneTenantAsync`, and `IOpenIdContextFactory` exposes `CreateForControlPlaneAsync`
  ([ADR-1191](docs/decisions/1191-the-management-api-authenticates-against-the-control-plane-tenant.md)). The route's
  `{tenantId}` only scopes the data: it sets the ambient tenant scope, so a central-admin caller administers a tenant by
  id while the persistence-layer query filter confines the read fail-closed
  ([ADR-1018](docs/decisions/1018-tenant-scoped-data-access-at-the-persistence-layer.md)); a control-plane route without
  `{tenantId}` uses the resolved tenant, which has no effect on the unscoped platform, team and tenant rows those routes act on
  ([ADR-1042](docs/decisions/1042-management-api-route-driven-tenant-scope.md)).

- **The bootstrap administrator's `ClientSecret` is supplied Base64Url-encoded and kept off the GC heap.** The seed
  handler decodes it into a pinned, zeroed buffer (threaded as `ReadOnlyMemory<byte>`) and stores it as a PBKDF2
  verifier, because the administrator registers `client_secret_basic`, and the OpenAPI example and
  Scalar prefill that surface the presentable secret appear only in the development environment. See
  [ADR-1044](docs/decisions/1044-management-api-authentication-and-globaladmin-bootstrap.md) and [ADR-1391](docs/decisions/1391-a-client-secret-that-is-only-compared-is-stored-as-a-one-way-verifier.md).

- **Every owner's keys are imported and updated under the client-key policy.** The client, tenant and
  resource-server import endpoints use `IClientKeyImporter.TryImportKey`, so a JSON Web Key keeps its `kid`, a key whose
  `use`, `key_ops` or `alg` contradicts it is refused with a `400` that says why, and a `kid` already used by another of
  the owner's keys is a `409 Conflict`. Their update endpoints check `use` and `alg` against the key through
  `ISecretValidator.ValidateUpdate` and keep the key's `kid`
  ([ADR-1186](docs/decisions/1186-every-owners-keys-are-imported-and-updated-under-the-client-key-policy.md)).

- **A system resource server holds no keys.** The management API refuses to create or import a secret for a system
  resource server with `409 Conflict`, and issuance ignores any key stored on one
  ([ADR-1167](docs/decisions/1167-the-server-validates-the-tokens-it-issued-under-one-policy.md)).

- **Responses that reveal a secret are never cached.** Creating or rotating a client secret, a client settings update
  that issues one, creating a resource server key, minting an initial access token, and dynamic client registration (the RFC 7591 registration and the RFC 7592 read
  and update) answer with `Cache-Control: no-store, no-cache, max-age=0` and `Pragma: no-cache`, so no browser, proxy or
  gateway cache keeps the one-time secret. An endpoint opts in with the `.WithNoStore()` endpoint convention, and
  `HttpResponse.SetNoStore()` is public in the `NCode.Identity.AspNetCore` package
  ([#473](https://github.com/NCodeGroup/identity/issues/473),
  [ADR-1345](docs/decisions/1345-a-response-that-reveals-a-secret-is-marked-uncacheable-by-its-endpoint.md)).

- **A caller who may not read a secret never loads its owner's master keys.**
  `ManagementApiEndpointHandler.ProcessGetAsync` has an overload whose asynchronous mapper runs only after authorization
  succeeds, and the secret public-key endpoints use it.

- **Every management change is audited.** The client, tenant, team, platform, resource-server, principal and
  local-account handlers record their changes through `IManagementAuditRecorder` with non-sensitive snapshots:
  `TeamChangedAuditEvent` (`team`) and `PlatformChangedAuditEvent` (`platform`), recorded in the control-plane tenant
  because neither belongs to a tenant, issue [#141](https://github.com/NCodeGroup/identity/issues/141),
  `ResourceServerChangedAuditEvent`, `ResourceServerScopeChangedAuditEvent` and `ResourceServerSecretChangedAuditEvent`
  for every resource-server operation (create, update and delete, scopes, settings and secrets, issue
  [#139](https://github.com/NCodeGroup/identity/issues/139)), `PrincipalChangedAuditEvent` for a block or unblock
  (resource type `principal`, `ManagementResourceTypes.Principal`), `LocalAccountChangedAuditEvent` for a local
  account and `RoleAssignmentChangedAuditEvent` (`role_assignment.created` / `role_assignment.deleted`) for every role
  the management API grants or revokes, the creator's included, naming the acting administrator
  ([ADR-1449](docs/decisions/1449-every-role-assignment-the-management-api-grants-or-revokes-is-audited.md),
  [#583](https://github.com/NCodeGroup/identity/issues/583)).

- **A management audit record survives the request's cancellation.** Every `IManagementAuditRecorder` method records a
  change that has committed, and the whole record, resolving the tenant's environment type included, runs on its own
  30-second token, so a client that disconnects after the commit drops no audit event; a record that fails or times
  out is logged and never fails the committed write, and the recorder's methods take no `CancellationToken`
  ([#621](https://github.com/NCodeGroup/identity/issues/621),
  [ADR-1464](docs/decisions/1464-a-management-audit-record-runs-on-its-own-bounded-token-and-takes-no-request-token.md)).

- **A security effect that follows a committed change is bounded and never fails the change.** Ending a principal's
  or an account's sessions after a block, a disable, an administrator's password reset or a self-service password
  change, and clearing the failed password attempts after an administrator sets a password, run through
  `IPostCommitSecurityEffects` (`NCode.Identity.OpenId.Authentication.Abstractions`), each on its own 30-second token:
  a client that disconnects skips none of them, a hung store holds the response at most 30 seconds, and an effect that
  fails or times out is logged (`PostCommitSessionTerminationFailed`, `PostCommitPasswordAttemptResetFailed`) and never
  fails the committed change or skips its audit record ([#626](https://github.com/NCodeGroup/identity/issues/626),
  [ADR-1468](docs/decisions/1468-a-security-effect-that-follows-a-committed-change-runs-on-its-own-bounded-token.md)).

- **An administrator's password reset ends the account's sessions.** After the reset commits, every session of the
  account's principal is terminated with the `password_reset` reason (`SessionTerminationReasons.PasswordReset`): its
  grants and refresh tokens are revoked, its relying parties receive back-channel logout, and its consents are kept
  ([ADR-1226](docs/decisions/1226-an-administrator-can-block-any-end-user-and-a-password-reset-ends-the-accounts-sessions.md),
  issue [#285](https://github.com/NCodeGroup/identity/issues/285)).

- **Deleting an owner removes the role assignments it anchors.** Deleting a client or a resource server, also when a
  dynamically registered client is deregistered, removes the assignments of every role at its node, ownership included,
  in the same unit of work. Removing a federated principal, or deleting a local account, removes every role assignment
  its principal holds, ownership included, in every tenant, and deleting a tenant removes the assignments its principals
  hold in other tenants ([ADR-1212](docs/decisions/1212-deleting-an-owner-removes-what-it-owns.md), issues
  [#266](https://github.com/NCodeGroup/identity/issues/266), [#273](https://github.com/NCodeGroup/identity/issues/273)).

- **A platform or team settings PATCH reads only what it checks.** It walks the clients only when a setting that the client
  response-algorithm rule reads changed, and it reads tenants and clients as untracked projections without their secrets
  (issue [#212](https://github.com/NCodeGroup/identity/issues/212)).

- **The management console treats the access token as opaque.** The user's subject comes from the ID token only (RFC
  9068 §6), so sign-in and silent renewal work with opaque access tokens, and a refresh that returns no ID token keeps
  the current subject and ID token
  ([ADR-1151](docs/decisions/1151-frontend-tests-treat-warnings-as-errors-and-hold-coverage-floors.md)).

- **An encrypted UserInfo response must also be signed.** The protocol client never accepts a UserInfo response
  encrypted without a signature, so a `ClientRegistration` with UserInfo encryption and no UserInfo signing throws
  before the request
  ([ADR-1301](docs/decisions/1301-a-signed-userinfo-response-is-validated-like-an-id-token-under-the-clients-registered-algorithms.md),
  issue [#421](https://github.com/NCodeGroup/identity/issues/421)).

- **The OpenID protocol client keeps provider responses in zeroed, pooled memory.** It streams each response body, under
  `OpenIdClientOptions.MaxResponseSizeBytes`, into a sensitive pooled buffer that is zeroed when released, because a
  body may carry tokens, parses JSON from that buffer with `JsonElement.Parse(ReadOnlySequence<byte>)` and decodes an
  `application/jwt` body from it. A body larger than the limit, whatever its `Content-Length` says, fails with an
  `OpenIdProviderException` that names the limit ([#423](https://github.com/NCodeGroup/identity/issues/423)).

- **The protocol client fails a token with an out-of-range NumericDate with `invalid_claim`.** An ID token's
  `auth_time`, checked under `max_age`, and a logout token's `exp` are read as strict NumericDates, so a value such as
  `1e300` or one near the end of time fails the token with `invalid_claim` and never throws
  ([ADR-1123](docs/decisions/1123-numeric-date-is-read-strictly-and-range-guarded.md), issue
  [#367](https://github.com/NCodeGroup/identity/issues/367)).

- **The BFF keeps tokens off the browser and accepts its cookie only where it should.** The browser holds only the
  `__Host-Http-bff.<frontend>` handle: the session is an AES-256-GCM record in the shared store, keyed and encrypted under keys
  derived from the handle. `UseBff` runs the anti-forgery header and Fetch Metadata checks before authentication and
  fails closed when misordered, and the session cookie authenticates, and the token accessor serves, only
  `AsBffApi`/`AsBffPage` endpoints and `/bff/user`. A page form post needs Fetch Metadata that shows no cross-site
  request and a valid antiforgery token in the `__Host-Http-bff-antiforgery` cookie, a hub refuses a cross-site request
  or a foreign `Origin`, a hub connection ends once its session ends, idle or not, and a WebAssembly client never
  receives a token. A page navigation that arrives without its Strict session cookie is answered once, under a
  60-second marker cookie, with a same-origin reload page (strict CSP, `no-store`), so the login's return and links
  from other sites reach the page with its session
  ([#394](https://github.com/NCodeGroup/identity/issues/394),
  [#524](https://github.com/NCodeGroup/identity/issues/524),
  [#603](https://github.com/NCodeGroup/identity/issues/603),
  [#604](https://github.com/NCodeGroup/identity/issues/604),
  [ADR-1290](docs/decisions/1290-the-bff-keeps-each-session-as-an-encrypted-server-side-record-under-a-cookie-handle.md),
  [ADR-1292](docs/decisions/1292-the-bff-accepts-its-cookie-only-with-a-static-header-and-fetch-metadata.md),
  [ADR-1435](docs/decisions/1435-server-rendered-and-blazor-hosts-use-the-bff-session-through-pages-hubs-and-two-opt-in-blazor-packages.md),
  [ADR-1436](docs/decisions/1436-a-bff-page-accepts-a-form-post-only-with-fetch-metadata-and-a-valid-antiforgery-token.md),
  [ADR-1441](docs/decisions/1441-a-bff-connection-ends-with-its-session-even-when-idle-and-a-blazor-web-app-proves-it.md),
  [ADR-1444](docs/decisions/1444-a-bff-page-reached-without-its-strict-session-cookie-reloads-itself-once.md)).

- **BFF logout is a form POST.** `POST /bff/logout` takes `sid` and `returnUrl` in a form body and is the only logout
  method; `/bff/user` returns `logoutUrl` as the path alone and the logout guard as `logoutSid`, a reserved field
  ([#442](https://github.com/NCodeGroup/identity/issues/442),
  [ADR-1311](docs/decisions/1311-the-bff-logs-out-by-a-form-post-that-carries-the-sessions-logout-guard.md)).

- **No published URL comes from the request's `Host`.** Each frontend's `PublicOrigin` is required: the absolute URL the BFF is
  published at (scheme, host, optional port and path base; `https` outside Development), from which it builds its
  `redirect_uri` and `post_logout_redirect_uri`; set it to the URL the BFF's callback is registered under. The
  interactive surface's login redirect is root-relative, so a forged `Host` header cannot choose where any of them point
  ([#493](https://github.com/NCodeGroup/identity/issues/493),
  [ADR-1365](docs/decisions/1365-the-bff-and-the-login-redirect-take-no-url-from-the-requests-host.md)).

- **Every `/bff/*` response is uncacheable the same way the OP's are.** The BFF's endpoints send
  `Cache-Control: no-store, no-cache, max-age=0` and `Pragma: no-cache` through the shared `SetNoStore()`
  ([#512](https://github.com/NCodeGroup/identity/issues/512),
  [ADR-1345](docs/decisions/1345-a-response-that-reveals-a-secret-is-marked-uncacheable-by-its-endpoint.md)).

- **The BFF's remote-API forwarder stays inside its destination and keeps browser credentials at the edge.** The
  forwarder answers `400` for a path that could leave the destination's base path and `405`
  (`BffProblemTypes.MethodNotAllowed`) for a method outside `AllowedMethods`, never forwards the browser's cookies,
  credentials or forwarding headers, drops the upstream `Set-Cookie`, and never follows a redirect
  ([#395](https://github.com/NCodeGroup/identity/issues/395),
  [ADR-1381](docs/decisions/1381-the-bff-serves-its-own-and-exchanged-tokens-without-a-lease-and-forwards-remote-apis-through-yarp.md)).

- **The BFF can send browser security headers.** `app.UseBffSecurityHeaders()` writes `Content-Security-Policy`,
  `X-Content-Type-Options: nosniff`, `Referrer-Policy` and `Cross-Origin-Opener-Policy` on every response, from
  the selected frontend's `SecurityHeaders` (`BffSecurityHeadersOptions`: `ContentSecurityPolicy`, `ReferrerPolicy`,
  `CrossOriginOpenerPolicy`; the strict defaults for a request no frontend selects), unless the application set the
  header itself; the options are validated when the host starts
  ([#396](https://github.com/NCodeGroup/identity/issues/396),
  [ADR-1382](docs/decisions/1382-the-bff-sends-browser-security-headers-and-publishes-its-client-keys-only-when-the-host-opts-in.md)).

- **BFF frontends never share a session.** Each frontend's cookies are `__Host-Http-bff.<name>` and
  `__Host-Http-bff-login.<name>`, another frontend ignores a frontend's session and deletes its cookie, and refuses its
  login transaction (`frontend_mismatch`), and a logout token ends only the receiving frontend's sessions
  ([#396](https://github.com/NCodeGroup/identity/issues/396),
  [ADR-1432](docs/decisions/1432-one-bff-serves-several-frontends-each-selected-per-request-by-an-open-rule.md)).

- **A BFF session ends when its frontend is removed or registered as another client.** A session records its
  frontend's `Authority` and `ClientId`; a request or connection ends one whose frontend is configured with another
  (`client_changed`), a removed frontend's sessions answer no request, and a background pass on one replica, at start
  and after every change, ends every stored session of a removed or re-registered frontend (`frontend_removed`,
  `client_changed`) through the store's client index (`IBffSessionStore.FindClientsAsync`, `FindByClientAsync`), under a
  lease and retry delay that `Bff:SessionRetirement` configures
  ([#602](https://github.com/NCodeGroup/identity/issues/602),
  [ADR-1443](docs/decisions/1443-a-bff-session-ends-when-its-frontend-is-removed-or-registered-as-another-client.md)).

- **A BFF host refuses to start when an endpoint's BFF metadata is incomplete.** A YARP route naming
  `BffTokenRequirement` or `BffFrontend` without `AsBffRemoteApis()`, YARP routes without `AddBffTransforms()`, or an
  endpoint naming an unconfigured frontend stop the host with one message; a later change of the endpoints or the
  frontends is checked again and logged, and such a route still answers `502`
  ([#522](https://github.com/NCodeGroup/identity/issues/522),
  [ADR-1433](docs/decisions/1433-a-bff-host-refuses-to-start-when-an-endpoints-bff-metadata-is-incomplete.md)).

- **All persisted OpenID data is tenant-scoped except the platform and teams.** `FederatedPrincipal`, `FederatedIdentity` and
  `LocalAccount` are tenant-owned: their persisted DTOs carry a required `TenantId`, and their server-generated ids and
  natural keys are unique per tenant, so the same human authenticating in two tenants is two principals. Grants are
  strictly tenant-scoped: `TenantId` is required on `PersistedGrantId`, `PersistedGrant`, `PersistedGrant<TPayload>`,
  `GrantEntity`, `GrantResource` and `IPersistedGrantService.CreateGrantId`, and every grant is created against the
  resolved request tenant, with no global grant key space. The tenant-scope global query filter is navigation-free:
  `ISupportTenantEntity` carries a denormalized `NormalizedTenantId` discriminator, so the filter is a single
  indexed-column comparison and the framework stays decoupled from the concrete tenant entity
  ([ADR-1035](docs/decisions/1035-federated-principals-and-identity-resolution.md),
  [ADR-1018](docs/decisions/1018-tenant-scoped-data-access-at-the-persistence-layer.md)).

- **The management API never materializes a resource outside the request's tenant.** An `IAmbientTenantAccessor`
  (`NCode.Identity.OpenId.Persistence.Abstractions`) and an `OpenIdDbContext` global query filter on tenant-child
  entities scope every read when tenancy is request-derived (dynamic-by-host or dynamic-by-path), so a cross-tenant read
  returns `404` and no fetch-then-reject leak window exists. A reusable `TenantScopeEndpointFilter` establishes the
  scope; tenant, team and platform administration are central-admin (unscoped)
  ([ADR-1018](docs/decisions/1018-tenant-scoped-data-access-at-the-persistence-layer.md)).

- **Deleting an owner removes what it owns.** `IClientStore.RemoveAsync` removes a client's secrets, grants to resource
  servers, consents, the grants issued to it and the role assignments at its resource node in the same unit of work as
  the client, so deleting a client through the management API and RFC 7592 deregistration (`DELETE
  /oauth2/register/{client_id}`) answer `204` whatever the client holds; deleting a tenant whose users signed in
  likewise removes what exists for it, on every provider including PostgreSQL
  ([ADR-1212](docs/decisions/1212-deleting-an-owner-removes-what-it-owns.md), issue
  [#250](https://github.com/NCodeGroup/identity/issues/250)).

- **Grant payloads are encrypted at rest.** A persisted grant's payload (authorization codes, refresh tokens, sessions,
  device, CIBA and pushed authorization requests, DCR tokens and continue envelopes) is stored only as an `nce1.`
  envelope in `PersistedGrant.ProtectedPayload`/`GrantEntity.ProtectedPayload` (`string`), under the master key of the
  grant's tenant, with purpose `grant-payload` and the grant's hashed key as the item id, so a payload copied to another
  grant row does not decrypt. A payload that cannot be unprotected makes the grant absent and logs a warning rather than
  failing the request ([ADR-1265](docs/decisions/1265-grant-payloads-are-protected-under-the-tenant-master-key.md),
  issue [#175](https://github.com/NCodeGroup/identity/issues/175)).

- **A failed sign-in records the attempted user name as a detail, never as the subject.** `subject.authentication`
  failures write the user name a password grant presented as `details.attempted_username` and leave `subject_id` absent,
  so `subject_id` only ever holds a verified subject id; `IAuditEventRecorder.RecordSubjectAuthenticationFailedAsync`
  takes `(errorCode, errorMessage, attemptedUsername)` and `SubjectAuthenticationAuditEvent` carries
  `AttemptedUsername`, which is never a credential. `ISupportAuditDetails.HasAuditDetails` (default `true`) lets an
  event with nothing to write omit `details` entirely; `BffAuditEvent` and `SubjectAuthenticationAuditEvent` use it
  (issue [#385](https://github.com/NCodeGroup/identity/issues/385)).

- **DPoP nonces and the DPoP-proof and client-assertion replay records live in the shared-state port, scoped by
  tenant.** They are recorded in `IExpiringKeySet` under keys that name the tenant and digest the request-supplied
  values, so tenants sharing a store never collide and a crafted `jti` cannot make a key ambiguous or unbounded; if the
  store is unreachable, these checks fail closed. `IDPoPProofReplayCache.TryRecordAsync` and
  `IClientAssertionReplayCache.TryRecordAsync` take the request's `OpenIdContext` as their first parameter. The server
  registers no `IDistributedCache`, so a host that resolves one registers its own
  ([ADR-1164](docs/decisions/1164-replicas-share-single-use-state-through-an-atomic-expiring-key-set.md)).

- **Session creation and termination are audited and logged.** `IAuditEventRecorder.RecordSessionCreatedAsync` and
  `RecordSessionTerminatedAsync` record the `session.created` and `session.terminated` events (issue
  [#63](https://github.com/NCodeGroup/identity/issues/63)).

- **Setting merge is a first-class policy that keeps a more specific scope from loosening a broader one.** The public
  `SettingMerge` vocabulary of lattice dual pairs (`Min`/`Max`, `And`/`Or`, `Intersect`/`Union`, `Keep`/`Replace`) in
  `NCode.Identity.Settings.Abstractions` is reusable by host-authored descriptors. Each built-in setting is a ceiling (a parent
  caps the child), a floor (a parent sets an un-loosenable minimum) or an override, chosen by its value polarity: token
  and code lifetimes, `clock_skew` and `subject_max_age` are ceilings a child may only shorten; `require_pkce`, the
  `*_encryption_required` flags, `claims_supported_is_strict`, `refresh_token_rotation_enabled` and the
  `federated_identity_*` requirement flags are floors; `allow_*` and `request(_uri)_parameter_supported` are ceilings;
  and `allowed_identity_providers` and `subject_types_supported` merge as intersect ceilings.
  `allowed_identity_providers` is fail-closed: unset means unrestricted, but an explicit or narrowed-to-empty allowlist
  denies every IdP ([ADR-1038](docs/decisions/1038-setting-merge-lattice-duals.md)).

- **Setting values are validated against their domain, not only their type.** A value of the right type that the server
  would not honor is rejected wherever a setting enters: configuration stops the host (and a reload is rejected), a
  management write returns `400` keyed by setting name, a stored document fails the request that loads it, dynamic
  client registration returns `invalid_client_metadata` (or `invalid_redirect_uri`), and the conformance and developer
  seeders stop with an error naming the client. Every library setting declares its domain through
  `SettingDescriptor.TryValidate` and `SettingDescriptor<TValue>.Rule` (a `SettingRule<TValue>` built with
  `SettingRules`): `security_profiles` lists only registered security profiles, and the security-profile catalog throws
  for an unregistered profile name; `token_endpoint_auth_method` and `token_endpoint_auth_methods_supported` accept only
  the methods of the registered client-authentication handlers (the authentication library contributes the
  `token_endpoint_auth_method` descriptor); the enumerated settings (`subject_type`, `backchannel_token_delivery_mode`,
  `refresh_token_reuse_policy`, `access_token_type`, the response-type, response-mode, prompt, display, PKCE,
  subject-type, delivery-mode and claim-type lists) accept only their protocol vocabularies; a grant type is a named
  grant type or an absolute URI (an extension grant); algorithm lists accept only registered algorithms of their kind,
  never `none` or `dir`, and the signing lists for ID tokens, UserInfo responses, access tokens and DPoP proofs accept
  only asymmetric algorithms; lifetimes and intervals are at least one second, timeouts are positive, so every
  `request_uri`, `jwks_uri` and `sector_identifier_uri` fetch stays bounded, sizes are at least one, durations that zero
  disables are not negative, and `user_code_length` is between 8 and 32; URIs, host allow-lists, the mutual-TLS alias
  authority, the expected `request_uri` media type, the `tls_client_auth` identities and `jwks` are checked the way the
  runtime uses them (`jwks_uri`, `sector_identifier_uri` and `client_notification_endpoint` are https,
  `client_notification_endpoint` defaults to empty, and redirect URIs are absolute without a fragment). A list setting
  that contains `null` is a type error. A refresh token always expires (RFC 9700 §4.14.2)
  ([ADR-1140](docs/decisions/1140-setting-values-are-validated-against-their-domain.md)).

- **A security profile that the platform, a team or a tenant selects binds every scope below it, whichever profile it is.**
  `security_profiles` (`OpenIdSettingKeys.SecurityProfiles`, a `SettingKey<IReadOnlyCollection<string>>`) is a list that
  accumulates down the platform → team → tenant → client cascade: a scope can add a profile but cannot clear or swap out one a
  scope above it selects, and every selected profile applies, its ceilings and its request-time requirements. A layered
  profile selects the profile it builds on, and selecting it applies both. Profiles whose ceilings contradict leave
  nothing permitted on that dimension, which fails closed. A profile selected at the platform scope, in
  `OpenId:Platform:Settings` or the platform's stored settings, injects its ceilings, so discovery advertises them and every
  tenant and client enforces them. The management API's tenant, client and resource-server effective-settings previews
  apply the selected profiles' ceilings, as the runtime does. A profile's ceiling binds by construction:
  `ISecurityProfile.ApplyCeilingSettings` contributes it through `ISecurityProfileCeilingBuilder`, which accepts only a
  registered setting that merges with a lattice operation (`Min`, `Max`, `And`, `Or`, `Intersect` or `Union`, declared
  by `SettingDescriptor.HasLatticeMerge`), so no child scope can override it and several profiles combine in any order;
  a ceiling on a setting that a child could override, or whose merge would ignore it, fails when a scope selects the
  profile. A requirement's error description reads "a selected security profile"
  ([ADR-1148](docs/decisions/1148-security-profiles-accumulate-down-the-settings-cascade.md)).

- **A client value that a ceiling lowers is incoherent.** `RegisteredValueCoherenceRule` checks every setting a client
  registers whose merge is a meet (`Min`, `And` or `Intersect`, which the abstract `SettingDescriptor.HasMeetMerge`
  states), so a lifetime, timeout, size or rate above what the tenant, its team, the platform or a selected security profile permits
  is refused at registration and at a management write, a tenant, team or platform write that would lower it below a client's
  value is refused, and the reconciliation and the incoherent-client report list it: `The 'access_token_lifetime' value
  '01:00:00' exceeds the effective 'access_token_lifetime' value '00:05:00'.` A value that a ceiling already lowers
  blocks no write that moves that ceiling, because a write matches a violation that predates it without its description.
  A floor (`Max`, `Or`, `Union`) is never reported
  ([ADR-1240](docs/decisions/1240-a-client-value-that-a-ceiling-lowers-is-incoherent.md),
  [#262](https://github.com/NCodeGroup/identity/issues/262)).

- **A tenant or client opts into the FAPI 2.0 Security Profile by selecting `fapi2`**
  ([#54](https://github.com/NCodeGroup/identity/issues/54),
  [ADR-1108](docs/decisions/1108-fapi-security-profile-merge-lattice-ceiling-injection.md)). Listing `fapi2`
  (`OpenIdConstants.SecurityProfiles.Fapi2`) in `security_profiles` expands the profile into concrete ceiling settings
  at the scope that selects it, and the floor/ceiling merge lattice carries them down the platform → team → tenant → client
  cascade un-loosenably, so discovery and request-time enforcement read one source of truth: signing restricted to
  `PS256`/`ES256`/`EdDSA`, strong client authentication only (`private_key_jwt`, `tls_client_auth`,
  `self_signed_tls_client_auth`), `response_type=code` with PKCE `S256` only, required PKCE and PAR, and short
  authorization-code and PAR `request_uri` lifetimes. A tenant can mandate the profile and a client cannot loosen it.
  `AlgorithmCodes.DigitalSignature.EdDsa` names the EdDSA algorithm, implemented in the opt-in
  `NCode.Identity.Jose.EdDsa` package
  ([ADR-1117](docs/decisions/1117-eddsa-clean-room-implementation-opt-in-package.md)).

- **FAPI 1.0 Advanced security profile (`fapi1-advanced`).** A tenant or client that selects it through
  `security_profiles` gets these ceilings: `code` with a JWT-secured (JARM) response mode, or `code id_token` once the
  tenant lists it; PAR, PKCE `S256`, and signed request objects that carry `nbf`, live at most 60 minutes, and are
  addressed to the issuer; access tokens bound to the client's certificate through mutual TLS, with
  `require_tls_client_certificate_bound_access_tokens` as a ceiling and `tls_client_certificate_bound_access_tokens`
  support as a default, because FAPI 1.0 Advanced §5.2.2-6 requires mutual TLS support; `private_key_jwt`,
  `tls_client_auth`, or `self_signed_tls_client_auth` client authentication, with public clients refused; PS256 or ES256
  signing only, with PS256 the default for ID tokens, introspection, and authorization responses; and authorization
  codes that live at most 60 seconds and PAR `request_uri`s at most 90. A request for plain `code` without a `.jwt`
  response mode is refused with `invalid_request`. `AuthorizationRequestProfileContext` carries the composed
  `IAuthorizationRequest` for request-time requirements, and the spec-coverage matrix has a `profile-fapi1-advanced` row
  ([ADR-1202](docs/decisions/1202-fapi-1-0-advanced-is-a-second-registered-security-profile.md),
  [#233](https://github.com/NCodeGroup/identity/issues/233)).

- **A client whose refresh tokens rotate under the FAPI 2.0 profile must be able to retry a rotation.** The client
  setting `refresh_token_rotation_retry_required` (`false` by default; a child scope cannot un-require it) is required
  by the FAPI 2.0 security profile, and the coherence rule `RefreshTokenRotationRetryCoherenceRule` refuses a client
  that requires it while its refresh tokens rotate (`refresh_token_rotation_policy` `always`, or `unbound` for a public
  client) and its `refresh_token_reuse_interval` is zero (FAPI 2.0 §5.3.2.1)
  ([ADR-1206](docs/decisions/1206-a-clients-settings-are-checked-by-registered-coherence-rules.md),
  [ADR-1217](docs/decisions/1217-a-rotated-refresh-token-may-be-retried-within-a-reuse-interval.md),
  [#272](https://github.com/NCodeGroup/identity/issues/272)).

- **A tenant's issuer is its single authoritative origin, and the host-based tenant strategy routes on the hosts it
  derives.** The routing host is derived from the issuer on write and never set independently, so the host a tenant is
  reached at cannot diverge from the identity it publishes, as [OIDC Discovery
  §4.3](https://openid.net/specs/openid-connect-discovery-1_0.html#ProviderConfigurationValidation) requires. A filtered
  unique index on the normalized issuer means no two tenants share an issuer (a protocol-level collision, since the
  issuer is asserted in every token), and a deliberately non-unique index on the derived host lets path-based tenants
  share a host (`https://op.example/tenant-a`, `…/tenant-b`); host uniqueness is a requirement of the host-based
  strategy alone, enforced by validation. `ITenantStore` exposes `GetOrDefaultByIssuerAsync`, and
  `GetOrDefaultByHostAsync` routes by the issuer host or the mTLS endpoint-alias host
  ([ADR-1129](docs/decisions/1129-the-tenant-issuer-is-the-single-authoritative-origin.md),
  [ADR-1195](docs/decisions/1195-a-tenants-mtls-endpoint-alias-is-a-routed-tenant-property.md)).

- **Every tenant a strategy serves has an issuer, and no published URL follows the request's `Host` header.** The tenant
  factory takes a tenant's issuer and base address from its stored issuer alone and fails closed for a served tenant
  without one, so a caller's forged `Host` header never chooses where clients send their credentials: discovery
  endpoints, `registration_client_uri` and the device `verification_uri` are published under the issuer.
  `OpenId:TenantResolution:StaticSingle:Issuer` is required for the host to start (the reference host's launch profiles
  pin `https://localhost:5001`, `https://localhost:44369`, `https://host.docker.internal:5001`, `https://localhost:5003`
  and the Docker profile's `https://localhost:5011`), and the host and path strategies require an address for the root
  tenant: `Root:Issuer` or their issuer template. A host is reached at the origin its issuer names, an issuer's host
  labels may be at most 63 characters, and a disabled static tenant is not served, like any other disabled tenant.
  `DefaultOpenIdTenantFactory` resolves `GetTenantIssuerAsync` before `GetTenantBaseAddressAsync`, which receives the
  issuer ([ADR-1165](docs/decisions/1165-every-served-tenant-has-an-issuer-and-its-strategy-supplies-the-default.md)).

- **A tenant's identifier and issuer, and each strategy's options, are validated by the tenant strategy that serves
  it.** Creating a tenant, and updating its issuer, require an absolute `https` issuer (`http` only for a loopback host)
  with no user information, query or fragment, held by no other tenant, and ending with the tenant's route (a path
  tenant `acme` ends in `/acme`); a host-routed tenant needs an issuer whose host the configured host pattern maps to
  itself and no other tenant holds. An invalid value answers `400` and a conflict `409`, so no tenant publishes a
  discovery document conforming clients reject, and an empty issuer is stored as none. Each built-in strategy registers
  its own options validator beside it, judged by the same rules, and a custom strategy does the same for its own section
  and implements `ValidateTenantAsync` on `ITenantResolver` and `ITenantStrategy`, which returns
  `TenantValidationFailure`s. A configured `StaticSingle:Issuer` must use `https` (`http` only for a loopback host) and
  carry no user information, and a `DynamicByPath:TenantPath` route may carry only the tenant parameter, with no
  constraint, default value, optional marker or catch-all, so a tenant's route follows from its identifier alone
  ([ADR-1160](docs/decisions/1160-a-tenants-address-is-validated-by-the-strategy-that-serves-it.md)).

- **A retired tenant identifier or issuer is never reissued.** Deleting a tenant retires its identifier and its issuer,
  and changing a tenant's issuer retires the one it gives up, so no other tenant can take an identity that relying
  parties trust. The tenant store records them in the `RetiredTenants` and `RetiredIssuers` tables, in the same unit of
  work as the delete or update. `ITenantResolver.ValidateTenantAsync` reports a retired identifier, or an issuer another
  tenant gave up, as a conflict: the management API answers `409 Conflict` on create and update, and the address preview
  lists it. Only the tenant that gave up an issuer can take it back. `ITenantStore` exposes `IsTenantIdRetiredAsync` and
  `GetRetiredIssuerHolderOrDefaultAsync`
  ([ADR-1189](docs/decisions/1189-a-retired-tenant-identifier-or-issuer-is-never-reissued.md)).

- **Deleting a tenant erases the data of its end users.** Its local accounts and their claims, federated principals and
  identities, sessions and other grants are removed with it. Deleting a tenant that still has clients or resource
  servers answers `409`, and its usage records are kept
  ([ADR-1212](docs/decisions/1212-deleting-an-owner-removes-what-it-owns.md),
  [#250](https://github.com/NCodeGroup/identity/issues/250)).

- **A production tenant gets production guardrails, and a production host refuses a non-production platform default.** The
  `production` security profile, which the `production` and `staging` environment types select, drops plain `http`
  redirect URIs, forbids anonymous client registration and turns `deletion_protection` on, which makes `DELETE
  /api/tenants/{tenantId}` answer `409`. In the Production host environment `AddProductionSafety` refuses to start when
  `OpenId:Platform:Settings:environment_type` names a registered type whose `IsProduction` is false
  ([ADR-1289](docs/decisions/1289-a-tenant-declares-its-environment-type-which-selects-its-guardrails.md),
  [#410](https://github.com/NCodeGroup/identity/issues/410)).

- **Per-tenant rate limits.** Three settings limit the requests per minute a tenant's protocol endpoints serve:
  `rate_limit_tenant_per_minute` (every request), `rate_limit_address_per_minute` (one remote address, an IPv6 address
  counted by its `/64`), and `rate_limit_client_per_minute` (one authenticated confidential client, set on the tenant or
  the client). Each is unset (unlimited) by default and merges with `Min`, and each is a limit, set by the
  administrators above the tenant or client whose settings carry it. The limiter counts in the shared
  `IExpiringCounterSet` with a two-window sliding estimate, so the limits hold across replicas that share Valkey.
  A refused request is answered `429 Too Many Requests` with `temporarily_unavailable`, `Retry-After`, and
  `Cache-Control: no-store`, and is neither handled nor audited
  ([ADR-1196](docs/decisions/1196-tenants-are-rate-limited-through-a-shared-expiring-counter-set.md),
  [ADR-1418](docs/decisions/1418-a-limit-is-set-by-the-authority-above-the-scope-whose-document-carries-it.md),
  [#84](https://github.com/NCodeGroup/identity/issues/84),
  [#563](https://github.com/NCodeGroup/identity/issues/563)).

- **Anonymous discovery and JSON Web Key Set requests cannot spend a tenant's daily request quota.** The two endpoints
  are not metered: they count only against the per-minute rate limits and are not recorded as `requests` usage, so no
  anonymous caller can exhaust a tenant's quota, run up billable overage, or have the tenant's clients refused until
  midnight UTC ([ADR-1198](docs/decisions/1198-plan-tier-quotas-are-daily-utc-counts-enforced-at-the-quota.md),
  [ADR-1199](docs/decisions/1199-tenant-usage-is-a-daily-ledger-that-replicas-add-to.md),
  [ADR-1200](docs/decisions/1200-a-tier-admits-a-bounded-overage-past-its-quota-and-bills-it-from-the-ledger.md)).

- **Agent attribution stays out of commits by rule and setting, and no check enforces it.** The instructions tell every
  agent never to add a `Co-authored-by:` trailer naming an agent, a `Claude-Session:` trailer or a "Generated with"
  line to a commit message, Claude Code's and VS Code's settings add none, and `/ship-plan` and `/run-issues` delete
  such lines from the pull request title and description they write, which the squash merge makes the commit on
  `main`. Nothing checks commits, pushes or pull requests for attribution, so one a tool adds anyway blocks nothing
  ([project-template ADR-0018](docs/decisions/project-template/0018-agent-attribution-is-avoided-not-enforced.md)).
- **A master key is bounded in time and destroyed once nothing names it.** An owner's master key rotates every year, the
  values a tenant's key protected, and a platform value that a registered protected-value source reports, are rewrapped
  under the new version without their payloads being touched, a retired key is destroyed only after its grace and when
  no stored secret, grant payload, live Data Protection key or reported platform value names it, so a retired platform
  version stays while a live Data Protection key names it, and the recovery private key that can
  unwrap every master key exists only inside the offline rewrap command
  ([#171](https://github.com/NCodeGroup/identity/issues/171),
  [#172](https://github.com/NCodeGroup/identity/issues/172),
  [ADR-1407](docs/decisions/1407-a-master-key-rotates-its-data-keys-are-rewrapped-in-the-background-and-it-is-destroyed-once-nothing-names-it.md),
  [ADR-1408](docs/decisions/1408-an-offline-host-command-rewraps-every-master-key-under-the-configured-root-and-recovery-keys.md)).
- **A revoked tenant root key shreds the tenant's data and the server fails closed.** A master key under a root key the
  tenant brings has no operator recovery copy, so revoking, disabling or destroying that key makes every value written
  or rewrapped under it unreadable, in the database and in every backup. The key is re-validated every hour by default,
  and a replica unwraps a tenant-keyed master key again at least that often, so a revocation takes effect within that
  period on every replica. While the key is `unavailable` the server reads and writes none of the tenant's secrets: the
  management API answers `503` with `Retry-After` and `retryAfterSeconds`, a protocol endpoint answers
  `temporarily_unavailable` with `503`, `Retry-After` and `Cache-Control: no-store`, and the authorization endpoint
  returns it to the client's redirect URI, the host logs the failure, and `root_key.unavailable` is audited; development
  pruning never deletes the master keys of such a tenant. A change to the key's state marks the tenant's secrets changed
  before the change is announced, so a replica that missed the announcement evicts the tenant when its connection is
  restored
  ([#174](https://github.com/NCodeGroup/identity/issues/174),
  [#573](https://github.com/NCodeGroup/identity/issues/573),
  [#579](https://github.com/NCodeGroup/identity/issues/579),
  [ADR-1414](docs/decisions/1414-a-tenant-can-wrap-its-master-keys-under-a-root-key-it-controls.md),
  [ADR-1415](docs/decisions/1415-a-key-wrapper-can-wrap-under-a-key-the-caller-names.md),
  [ADR-1428](docs/decisions/1428-a-package-translates-the-exceptions-it-owns-into-protocol-errors-through-registered-translators.md),
  [ADR-1454](docs/decisions/1454-a-replica-that-may-have-missed-a-tenant-announcement-evicts-only-the-tenants-whose-row-changed.md)).
- **An imported elliptic-curve public key is checked against its curve.** A JWK's `x` and `y` must each be the curve's
  field length, below its prime and satisfy its curve equation before the platform imports the point, so an off-curve or
  invalid-curve point is rejected with the same error on every platform
  ([#575](https://github.com/NCodeGroup/identity/issues/575),
  [ADR-1427](docs/decisions/1427-an-imported-elliptic-curve-point-is-checked-against-its-curve-before-the-platform-imports-it.md)).

- **Custody of a machine client needs the authority to grant every role it can hold.** Updating, deleting, configuring
  or changing the owners or secrets of a machine client needs creation authority at the platform or team that owns it,
  and a role is granted to a client's principal only when it is a machine client and only at its owning node or beneath
  it (`409` otherwise), so whoever can set a client's credentials could grant every role it holds, whenever the
  credentials were set, and an administrator of the control-plane tenant cannot set the credentials of a team's machine
  client ([#557](https://github.com/NCodeGroup/identity/issues/557),
  [#589](https://github.com/NCodeGroup/identity/issues/589),
  [ADR-1419](docs/decisions/1419-a-client-acting-for-itself-is-a-principal-that-holds-role-assignments.md),
  [ADR-1467](docs/decisions/1467-custody-of-a-machine-client-needs-creation-authority-at-the-node-that-owns-it.md)).

- **A remote key never leaves its KMS, every signature it makes is verified before a token carries it, and every content
  key it unwraps is authenticated by the JWE's tag.** The server holds the key's public half and a reference only; a
  remote signature whose length or verification against the stored public half fails is refused; a remote key is bound
  to one algorithm and fails when its tenant loads if its provider, mapping row or public half is missing; KMS keys are
  created sign-only or decrypt-only and, on Azure Key Vault and Google Cloud KMS, hardware-protected by default; logs
  carry key identifiers only; and the server destroys only the KMS keys it created; a key it created but could not read,
  check or record is destroyed at once, and a KMS response that names another key or algorithm, or fails its checksum,
  is refused. A sweep destroys only keys tagged with its deployment, older than its grace period, that no stored secret
  names, and each destruction stays recoverable for the service's window. A protocol client's or a BFF's key ring holds
  only a remote key's public half, a BFF does not start with a remote key whose public half its KMS does not return or
  that does not prove it holds the private half, and neither creates nor destroys a KMS key
  ([#165](https://github.com/NCodeGroup/identity/issues/165),
  [#650](https://github.com/NCodeGroup/identity/issues/650),
  [#653](https://github.com/NCodeGroup/identity/issues/653),
  [#654](https://github.com/NCodeGroup/identity/issues/654),
  [#659](https://github.com/NCodeGroup/identity/issues/659),
  [#651](https://github.com/NCodeGroup/identity/issues/651),
  [ADR-1471](docs/decisions/1471-a-tenants-signing-keys-can-be-held-in-a-key-management-service-that-signs-every-token.md),
  [ADR-1474](docs/decisions/1474-a-remote-key-provider-is-described-by-data-creates-a-key-once-and-signs-into-the-callers-buffer.md),
  [ADR-1477](docs/decisions/1477-a-tenants-encryption-keys-can-be-held-in-a-key-management-service-that-decrypts-every-content-key.md),
  [ADR-1478](docs/decisions/1478-a-background-job-destroys-the-key-management-service-keys-that-no-stored-secret-names.md),
  [ADR-1479](docs/decisions/1479-a-protocol-clients-and-a-bffs-keys-can-be-held-in-a-key-management-service-read-and-proved-at-start.md)).
