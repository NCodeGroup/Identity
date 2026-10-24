# 240. A client value that a ceiling lowers is incoherent

- **Status:** Accepted
- **Date:** 2026-10-23
- **Deciders:** NCode Group

## Context

`RegisteredValueCoherenceRule` checks that a value a client registers survives into its effective settings
([ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md)). It reports a list value that an
`Intersect` ceiling drops and a `true` that an `And` ceiling turns off, but a client's own value of a `Min` setting (a
lifetime, a timeout, a size, a rate) is lowered to its tenant's, the server's or a selected security profile's ceiling
without a report. A client that registers a one-hour `access_token_lifetime` receives tokens that live five minutes, the
server's default, and its registration, the management write and the incoherent-client report all accept it (backlog
`R71`).

`SettingDescriptor.HasLatticeMerge` says that a setting merges with a meet or a join, not whether the merge can lower a
value. A meet (`Min`, `And`, `Intersect`) never yields a value above either value, so a ceiling can lower what a child
registers. A join (`Max`, `Or`, `Union`) never yields a value below either value, so a floor can only raise it.

## Decision

**A value that a client registers in a setting that merges with a meet must survive into its effective settings, and a
value that a ceiling lowers is a coherence violation, reported wherever a dropped list value is.**

1. **The descriptor states the fact.** `SettingDescriptor.HasMeetMerge` is `true` when the setting's merge is
   `SettingMerge.Min`, `SettingMerge.And` or `SettingMerge.Intersect`. `SettingDescriptor<TValue>` computes it with
   `SettingMerge.IsMeetOperation`, as it computes `HasLatticeMerge` with `SettingMerge.IsLatticeOperation`. A meet never
   yields a value above either value, so a registered value that differs from its effective value was lowered by a
   ceiling.
2. **The rule checks every meet.** For each registered setting with `HasMeetMerge`, `RegisteredValueCoherenceRule`
   compares the registered value with the effective value and names the setting as both the client setting and the
   governing setting:
   - A `bool` violates only when the registered `true` is effectively `false`.
   - A list, any `IEnumerable` that is not a `string`, reports each distinct registered element that is missing from the
     effective elements. Elements compare with default equality, so strings compare ordinally, and the elements may be
     of any type.
   - Any other value violates when it is not equal to the effective value. The description names both values, which are
     formatted with the invariant culture, so a `TimeSpan` reads in its constant format:

     ```text
     The 'access_token_lifetime' value '01:00:00' exceeds the effective 'access_token_lifetime' value '00:05:00'.
     ```

3. **A join never reports.** A join (`Max`, `Or`, `Union`) only raises a value to a parent's floor, such as a polling
   interval or a user-code length. It is the dual of `Or` turning on a requirement and `Union` adding a security
   profile, which the rule never reports, and refusing it would block a tenant from raising a floor.
4. **A lowered value is a violation, not a warning.** A write answers success or a validation problem, so a warning has
   no channel, and the value would still be clamped without a word. Every caller of `IClientCoherenceValidator` reports
   it as it reports a dropped list value: dynamic registration, the management client writes, the seeders, the tenant
   and server settings writes, the reconciliation log and the incoherent-client report
   ([ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md)).
5. **`SettingNames` lists the meets that apply within the client scope.** The rule takes an
   `ISettingScopeCollectionProvider`, and `SettingNames` lists each descriptor with `HasMeetMerge` for which
   `ISettingScopeCollection.AppliesWithin(descriptor, OpenIdSettingScopes.Client)` holds. A change of a lifetime that a
   client can register walks a tenant's clients, and a change of a tenant-only setting, such as
   `sector_identifier_uri_fetch_timeout`, does not.
6. **A violation that predates a write is matched without its description.** A tenant or server settings write
   reports only the violations that its change introduces, and `DefaultTenantClientCoherenceChecker` matches a
   violation under the replaced settings by the client setting, the value and the governing setting that it names,
   not by its description, which names the effective value that the change moves. A value that a ceiling already
   lowers therefore blocks no write that moves that ceiling, and the incoherent-client report still lists the client.

## Options considered

- **A warning.** Rejected: a write answers success or a validation problem, so a warning has no channel, and the value
  would still be clamped without a word.
- **Reporting a `Max` raise too.** Rejected: a join only raises a value to a parent's floor, and refusing it would block
  a tenant from raising a floor.
- **Reading the order from `IComparable` on the value type.** Rejected: whether a merge lowers a value is a fact of the
  merge, not of the type of the value, and the descriptor states it.
- **A merge-kind enum.** Rejected: the rule needs one fact, and a descriptor states it without a vocabulary of kinds
  that a package would have to extend ([ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md)).

## Consequences

- A client cannot register more than its ceilings permit. To give one client more, raise the tenant's value and set
  lower values on the other clients.
- A write that lowers a ceiling below a value that a client registers is refused until the client changes, unless the
  ceiling already lowers that value.
- A client that selects `fapi2` or `fapi1-advanced` cannot register an `authorization_code_lifetime` or a `clock_skew`
  above the profile's ceiling of 60 seconds
  ([ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md)).
- **Breaking:** `SettingDescriptor` declares the abstract `HasMeetMerge`, which a type that derives from it implements.
- This implements backlog `R71`.

## References

- [ADR-0148](0148-security-profiles-accumulate-down-the-settings-cascade.md) — a profile's ceilings are merged down
  the cascade.
- [ADR-0150](0150-vocabularies-are-open-unless-provably-closed.md) — a closed vocabulary only where it is provably
  closed.
- [ADR-0206](0206-a-clients-settings-are-checked-by-registered-coherence-rules.md) — the coherence rules and the
  validator.
- [ADR-0233](0233-the-management-api-lists-a-tenants-incoherent-clients.md) — the report that lists an incoherent
  client.
- Code: `NCode.Identity.Settings.SettingDescriptor`, `NCode.Identity.Settings.SettingMerge`,
  `NCode.Identity.OpenId.Authentication.Clients.Coherence.RegisteredValueCoherenceRule`.
