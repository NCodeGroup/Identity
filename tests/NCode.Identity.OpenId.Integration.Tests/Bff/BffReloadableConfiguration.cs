// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;

namespace NCode.Identity.OpenId.Integration.Tests.Bff;

/// <summary>
/// A configuration source that a test replaces at run time, as a host whose configuration reloads on change.
/// </summary>
internal sealed class BffReloadableConfiguration : ConfigurationProvider, IConfigurationSource
{
    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder) => this;

    /// <summary>
    /// Replaces every value of the source and signals the configuration change.
    /// </summary>
    /// <param name="values">The values that the source holds afterwards.</param>
    public void Replace(IReadOnlyDictionary<string, string?> values)
    {
        Data = new Dictionary<string, string?>(values, StringComparer.OrdinalIgnoreCase);
        OnReload();
    }
}
