// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Bff.Blazor.Client.Tests;

public class BffBlazorClientPackageDependencyTests : BaseTests
{
    [Fact]
    public void ClientAssembly_ReferencesOnlyTheSharedBffPackage()
    {
        foreach (var reference in typeof(BffBlazorClientLibrary).Assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            if (!name.StartsWith("NCode.Identity.", StringComparison.Ordinal))
                continue;

            Assert.Equal("NCode.Identity.Bff.Protocol.Abstractions", name);
        }
    }

    [Fact]
    public void ClientAssembly_ReferencesNoAspNetCoreServerAssembly()
    {
        foreach (var reference in typeof(BffBlazorClientLibrary).Assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            if (!name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal))
                continue;

            // The browser-safe assemblies that Microsoft.AspNetCore.Components.Authorization brings.
            var isAllowed =
                name.StartsWith("Microsoft.AspNetCore.Components", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.AspNetCore.Authorization", StringComparison.Ordinal)
                || name.StartsWith("Microsoft.AspNetCore.Metadata", StringComparison.Ordinal);

            Assert.True(isAllowed, name);
        }
    }
}
