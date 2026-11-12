# 1400. The revocation sources are described, and configuration selects and orders them

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** NCode Group
- **Amends:** [ADR-1259](1259-revocation-is-checked-by-registered-sources-ocsp-before-every-crl-form.md)

## Context

The default `IClientCertificateRevocationChecker` asks the `IClientCertificateRevocationSource`s in turn, and the
first `Good` or `Revoked` answer decides
([ADR-1259](1259-revocation-is-checked-by-registered-sources-ocsp-before-every-crl-form.md)). Which sources a
deployment can use, and in what order, depends on its PKI:

- A deployment whose OCSP responders the server cannot reach, such as one behind an egress policy that admits only the
  CRL distribution points, waits one `FetchTimeout` per responder in each `FailureCacheDuration` of each replica before
  the CRLs answer
  ([ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md)).
- A PKI whose CRLs are authoritative and whose responder lags them is better asked through its CRLs first.
- A package can add a source, such as a local deny list, that belongs before the network sources.

The sources are an open vocabulary ([ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md)). A configured
selection is an operator's input, so it is validated when the host starts
([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)); but the sources read
`ClientCertificateRevocationOptions`, the options being validated, so validation cannot construct them to read their
names. Client-authentication methods meet the same constraint with registered descriptors that name each handler
([ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md)).

## Decision

**Each revocation source is described by a registered `ClientCertificateRevocationSourceDescriptor`, collected by
`IClientCertificateRevocationSourceCatalog`. `ClientCertificateRevocationOptions.Sources`
(`OpenId:ClientCertificateRevocation:Sources`) names the sources to ask, in order, and is validated against the catalog
when the host starts; empty, the default, asks every registered source in the descriptors' default order, `ocsp` then
`crl`.**

1. **A descriptor names a source and its default position.** `ClientCertificateRevocationSourceDescriptor` (in
   `NCode.Identity.OpenId.Authentication.Abstractions`) has a required `Name`, the `Name` of the source it describes,
   and an `Order`. A package contributes descriptors through
   `ICollectionDataSource<ClientCertificateRevocationSourceDescriptor>` beside the sources it registers;
   `AddClientCertificateServices()` contributes `ocsp` at 100 and `crl` at 200.
2. **The catalog is built from descriptors alone.** `IClientCertificateRevocationSourceCatalog` gives `SourceNames`, the
   default order (by `Order`, then by name with the ordinal comparison), and `TryGet(name, out descriptor)`, which
   compares names ordinally. Its constructor refuses a descriptor whose name is empty or contains whitespace, and two
   descriptors of one name. Reading it constructs no source.
3. **The selection is validated when the host starts.** `ClientCertificateRevocationOptionsValidator` reads the catalog
   and fails the host when `Sources` names a source the catalog does not describe, naming the registered ones, or names
   one source twice. An empty list is valid and means the default order: a deployment that checks no revocation sets a
   client's `tls_client_auth_revocation_mode` to `none`, which says so, rather than selecting no source, which would
   make every `online` check `Unknown`.
4. **The checker asks the selected sources in the configured order.** `DefaultClientCertificateRevocationChecker`
   receives every registered source, the catalog and the options, and asks the sources that `Sources` names in its
   order, or every described source in the default order when it is empty; a source the selection leaves out is never
   asked. The first answer that is not `Unknown` decides. Its constructor refuses a source that
   no descriptor names, two sources of one name, and a descriptor without a source, and the hosted
   `ClientCertificateRevocationCheckerWarmup` resolves the checker as the host starts, after the options are
   validated, so a registration that does not correspond stops the host rather than skipping a source or failing the
   first mutual-TLS authentication ([ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md)).
5. **ADR-1259 is amended.** Its decision 2 asks the sources in the configured order or the descriptors' default order,
   not in registration order, and the option it deferred is this decision. A host replaces the checker only to combine
   the sources other than by asking them in turn.

## Options considered

- **Keep registration order and let a deployment replace the checker.** Rejected: a deployment that cannot reach its
  responders would have to write a checker to say so, and the order would depend on the order packages register in.
- **Validate the names against the registered sources.** Rejected: the sources take the options being validated, so
  resolving them inside validation recurses (Context).
- **An `enum` of the built-in sources.** Rejected: a package's source could never be named (ADR-1150).
- **A per-tenant or per-client setting.** Rejected: which sources the server can reach is a property of the
  deployment's network and its PKI, not of a tenant, and a tenant must not be able to select a source that makes every
  check `Unknown`. A client's `tls_client_auth_revocation_mode` already decides what `Unknown` means for it.
- **Treat an empty list as "check nothing".** Rejected: configuration binding cannot tell an empty list from an absent
  one, and turning revocation off is the revocation mode's job (decision 3).
- **Ignore an unknown name.** Rejected: a misspelled `ocsp` would silently drop OCSP; a configuration error stops the
  host.
- **Ask every selected source and let any `Revoked` win.** Rejected for the reasons of ADR-1259.

## Consequences

- A deployment that cannot reach its OCSP responders configures `Sources` as `crl` alone and never waits for a
  responder; one that trusts its CRLs first configures `crl`, `ocsp`.
- A package that adds a source registers its descriptor with it, and the source can be named in configuration at once;
  a source without a descriptor, or a descriptor without a source, stops the host when it starts.
- A misspelled or repeated source name stops the host when it starts.
- The host constructs every revocation source as it starts, so a source's own dependencies must resolve then.
- The selection is one for the whole server.
- This implements issue [#38](https://github.com/NCodeGroup/identity/issues/38).

## References

- [ADR-1150](1150-vocabularies-are-open-unless-provably-closed.md): vocabularies are open unless provably closed.
- [ADR-1145](1145-a-client-authenticates-only-with-its-registered-method-which-a-registered-descriptor-names.md): client-authentication
  methods are named by registered descriptors.
- [ADR-1137](1137-configuration-binds-strictly-and-every-option-timer-period-and-retention-is-validated-at-startup.md): intervals and
  retentions are configured and validated when the host starts.
- [ADR-1252](1252-a-revocation-list-retrieval-is-shared-a-failed-one-is-remembered-and-soft-fail-is-opt-in.md) and
  [ADR-1259](1259-revocation-is-checked-by-registered-sources-ocsp-before-every-crl-form.md).
- Code: `src/NCode.Identity.OpenId.Authentication.Abstractions/ClientCertificates/` for
  `ClientCertificateRevocationSourceDescriptor` and `IClientCertificateRevocationSourceCatalog`, and
  `src/NCode.Identity.OpenId.Authentication/ClientCertificates/` for `ClientCertificateRevocationOptions`,
  `ClientCertificateRevocationOptionsValidator`, `DefaultClientCertificateRevocationSourceCatalog`,
  `DefaultClientCertificateRevocationSourceDataSource` and `DefaultClientCertificateRevocationChecker`.
