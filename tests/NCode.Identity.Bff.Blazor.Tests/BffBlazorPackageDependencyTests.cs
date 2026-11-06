// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Bff.Blazor.Tests;

public class BffBlazorPackageDependencyTests : BaseTests
{
    #region Assembly Tests

    [Fact]
    public void Assembly_Always_ReferencesOnlyAbstractions()
    {
        var assembly = typeof(BffBlazorLibrary).Assembly;

        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            if (!name.StartsWith("NCode.Identity.", StringComparison.Ordinal))
                continue;

            Assert.True(name.EndsWith(".Abstractions", StringComparison.Ordinal), name);
        }
    }

    #endregion
}
