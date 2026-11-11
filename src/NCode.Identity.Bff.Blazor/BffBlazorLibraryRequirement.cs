// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Registration;

namespace NCode.Identity.Bff.Blazor;

/// <summary>
/// Requires <c>AddBffBlazorLibrary()</c> for an endpoint that <c>AsBffComponents()</c> marks.
/// </summary>
internal sealed class BffBlazorLibraryRequirement : IBffEndpointStartupRequirement
{
    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static BffBlazorLibraryRequirement Instance { get; } = new();

    /// <inheritdoc />
    public string? Validate(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.GetService<IRegistrationMarker<BffBlazorLibrary>>() is null
            ? "AsBffComponents() requires AddBffBlazorLibrary()."
            : null;
    }
}
