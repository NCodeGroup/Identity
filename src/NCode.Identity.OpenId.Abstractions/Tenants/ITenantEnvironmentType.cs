// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Tenants;

/// <summary>
/// A kind of tenant environment, such as <c>production</c>, <c>staging</c> or <c>development</c>, that a tenant selects
/// through its <c>environment_type</c> setting.
/// </summary>
/// <remarks>
/// This is a strategy collection: implementations are registered in DI and resolved by <see cref="TypeName"/> through
/// <see cref="ITenantEnvironmentCatalog"/>. The vocabulary is open, so a consumer or sibling package adds a type by
/// registering one rather than by editing the library.
/// </remarks>
// See ADR-1289 and ADR-1150.
[PublicAPI]
public interface ITenantEnvironmentType
{
    /// <summary>
    /// Gets the name of the type, which is the <c>environment_type</c> value that selects it. Names are compared
    /// ordinally.
    /// </summary>
    string TypeName { get; }

    /// <summary>
    /// Gets a value indicating whether the type is a production environment. A type that is not production makes the
    /// server show the non-production banner.
    /// </summary>
    bool IsProduction { get; }

    /// <summary>
    /// Gets the <c>security_profiles</c> names that a tenant of this type selects in addition to the ones it declares.
    /// </summary>
    IReadOnlyCollection<string> SecurityProfiles { get; }
}
