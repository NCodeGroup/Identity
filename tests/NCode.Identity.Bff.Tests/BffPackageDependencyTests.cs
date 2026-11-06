// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Xunit;

namespace NCode.Identity.Bff.Tests;

public class BffPackageDependencyTests : BaseTests
{
    [Fact]
    public void BffAssemblies_ReferenceNoServerPackage()
    {
        Assembly[] assemblies =
        [
            typeof(BffLibrary).Assembly,
            Assembly.Load(new AssemblyName("NCode.Identity.Bff")),
        ];

        foreach (var assembly in assemblies)
        {
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                var name = reference.Name ?? string.Empty;
                if (!name.StartsWith("NCode.Identity.OpenId.", StringComparison.Ordinal))
                    continue;

                var isAllowed =
                    name == "NCode.Identity.OpenId.Client.Abstractions"
                    || name == "NCode.Identity.OpenId.Protocol.Abstractions";

                Assert.True(isAllowed, name);
            }
        }
    }

    [Fact]
    public void BffAssembly_ReferencesOnlyAbstractionAndModelPackages()
    {
        var assembly = Assembly.Load(new AssemblyName("NCode.Identity.Bff"));

        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            if (!name.StartsWith("NCode.Identity.", StringComparison.Ordinal))
                continue;

            // NCode.Identity.Jwk is the JWK model library the abstractions themselves expose (ADR-1119), not an
            // implementation package (ADR-1016).
            var isAllowed =
                name.EndsWith(".Abstractions", StringComparison.Ordinal)
                || name == "NCode.Identity.Jwk";

            Assert.True(isAllowed, name);
        }
    }

    [Fact]
    public void ProtocolAssembly_ReferencesOnlyTheRuntime()
    {
        foreach (var reference in typeof(BffUserDocumentNames).Assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            var isRuntime =
                name == "netstandard"
                || name == "System"
                || name.StartsWith("System.", StringComparison.Ordinal);

            Assert.True(isRuntime, name);
        }
    }
}
