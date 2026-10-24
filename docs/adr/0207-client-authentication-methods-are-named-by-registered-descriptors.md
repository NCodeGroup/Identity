# 207. Client-authentication methods are named by registered descriptors

- **Status:** Accepted
- **Date:** 2026-10-22
- **Deciders:** NCode Group

## Context

A client registers the one method that it authenticates with, `token_endpoint_auth_method`
([ADR-0145](0145-a-client-authenticates-only-with-its-registered-method.md)), and the server advertises the methods that
it supports in `token_endpoint_auth_methods_supported`. The domain of both settings is the set of registered methods
([ADR-0140](0140-setting-values-are-validated-against-their-domain.md)), and the advertised default is that set too.

The setting descriptors learned the methods from the handlers. `DefaultClientSettingDescriptorDataSource` resolved every
`IClientAuthenticationHandler` to read its `AuthenticationMethod`. The setting-descriptor catalog that it feeds is read
by the `OpenIdOptions` validators, so composing the host constructed every handler and its whole dependency graph inside
options validation. A handler that depended, however indirectly, on a service whose constructor reads
`IOptions<OpenIdOptions>.Value` deadlocked host composition, and the container could not detect the cycle because its
last edge is a runtime `GetServices` call
([ADR-0204](0204-a-scopes-effective-settings-are-resolved-by-one-service.md)). The factories that read the options on
use avoid the cycle, but only by a convention that nothing enforced.

Dynamic registration had a second copy of the vocabulary. It named the methods that verify a shared secret in a
hard-coded list, to decide whether to issue a `client_secret`, so a method that a package added was never issued one. A
review (backlog `R60`) found both.

## Decision

1. **A method is registered as a descriptor, not discovered from a handler.** `ClientAuthenticationMethodDescriptor`
   (`NCode.Identity.OpenId.Authentication.Abstractions`, namespace `NCode.Identity.OpenId.Authentication.Clients`) has
   two members: `Name`, the `token_endpoint_auth_method` value and the `AuthenticationMethod` of its handler, and
   `RequiresClientSecret`, whether the method verifies a shared secret. A descriptor has no dependency, so reading it
   constructs nothing. Packages contribute descriptors through
   `ICollectionDataSource<ClientAuthenticationMethodDescriptor>`, as they contribute `SettingScopeDescriptor`. The
   built-in data source, `DefaultClientAuthenticationMethodDataSource`, lists the seven built-in methods in the order
   that the server advertises them: `client_secret_basic`, `client_secret_post`, `private_key_jwt`,
   `client_secret_jwt`, `tls_client_auth`, `self_signed_tls_client_auth`, and `none`. The three methods that verify a
   secret require a client secret. A trait is declared on the descriptor and is never inferred from a name
   ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).
2. **A catalog reads the descriptors once.** `IClientAuthenticationMethodCatalog` has two members: `MethodNames`, the
   registered names in registration order, and `TryGet(methodName, out descriptor)`.
   `DefaultClientAuthenticationMethodCatalog` reads each data source in its constructor and does not watch its change
   token, because the descriptors are static. It throws an `InvalidOperationException` for a descriptor with an empty
   name, a name that contains whitespace, or the name of another descriptor. Names compare ordinally.
3. **The setting descriptors read the catalog.** `DefaultClientSettingDescriptorDataSource` takes the catalog instead of
   an `IServiceProvider`. The domain and the default of `token_endpoint_auth_methods_supported`, and the domain of
   `token_endpoint_auth_method`, are the catalog's `MethodNames`, so options validation constructs no handler.
4. **Composition checks that the handlers and the descriptors correspond.** The constructor of
   `DefaultClientAuthenticationService` already receives every handler, and it runs when the endpoints map at host
   start, outside options validation. It throws an `InvalidOperationException` when a handler's method has no
   descriptor, when two handlers authenticate one method, and when a descriptor has no handler. A method that no
   descriptor names would not be advertised, an ambiguous method would be authenticated by either handler, and a method
   without a handler would be advertised but could not authenticate.
5. **Dynamic registration issues a `client_secret` when the descriptor requires one.** The registration handler asks
   the catalog for the descriptor of the registered method and issues a secret when `RequiresClientSecret` is `true`; a
   method that is not registered receives none. That `none` registers a public client remains a fact of RFC 7591 §2.

## Options considered

- **Keep resolving the handlers lazily.** Rejected: it still constructs them inside options validation, so the cycle
  remains for the first handler that depends on a service that reads the options.
- **A strategy class for each method.** Rejected: it duplicates the handler concept for two traits, a name and a secret
  requirement, that are data.
- **A trait on the handler.** Rejected: reading it needs the handler constructed, which is the cost that this decision
  removes.

## Consequences

- A package that adds a client-authentication method registers a handler and a descriptor of the same name. The setting
  domains, the advertised list, and the issuance of a secret follow without another change, and composing a host that
  registers one without the other fails at start.
- `none` remains the only public method (RFC 7591 §2). No other method is inferred to be public or confidential from its
  name.
- `DefaultOpenIdEnvironmentFactory` and `DefaultOpenIdServerFactory` keep reading `IOptions<OpenIdOptions>.Value` on
  use, never in a constructor. Options validation constructs no handler, so the cycle of
  [ADR-0204](0204-a-scopes-effective-settings-are-resolved-by-one-service.md) cannot arise through the handlers.
- The domain of the client-authentication settings follows the registered descriptors, not the registered handlers. This
  amends [ADR-0140](0140-setting-values-are-validated-against-their-domain.md).
- This implements backlog `R60`.
