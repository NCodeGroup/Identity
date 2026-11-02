// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Buffers;
using NCode.Identity.OpenId.Client.Tokens;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests;

public class ProtocolPackageDependencyTests : BaseTests
{
    [Fact]
    public void ProtocolAssembly_ReferencesOnlyTheRuntime()
    {
        foreach (var reference in typeof(OpenIdProtocol).Assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            var isRuntime =
                name == "netstandard"
                || name == "System"
                || name.StartsWith("System.", StringComparison.Ordinal);

            Assert.True(isRuntime, name);
        }
    }

    [Theory]
    [InlineData(typeof(ClientRegistration))]
    [InlineData(typeof(DefaultTokenClient))]
    public void ClientAssembly_ReferencesNoServerPackage(Type type)
    {
        foreach (var reference in type.Assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            if (!name.StartsWith("NCode.Identity.OpenId.", StringComparison.Ordinal))
                continue;

            var isAllowed =
                name.StartsWith("NCode.Identity.OpenId.Client", StringComparison.Ordinal)
                || name == "NCode.Identity.OpenId.Protocol.Abstractions";

            Assert.True(isAllowed, name);
        }
    }

    [Theory]
    [InlineData(typeof(ClientRegistration))]
    [InlineData(typeof(DefaultTokenClient))]
    public void ClientAssembly_ReferencesNoAspNetCoreAssembly(Type type)
    {
        foreach (var reference in type.Assembly.GetReferencedAssemblies())
        {
            Assert.False(
                (reference.Name ?? string.Empty).StartsWith(
                    "Microsoft.AspNetCore",
                    StringComparison.Ordinal
                ),
                reference.Name
            );
        }
    }

    [Fact]
    public void ClientPackage_RunsWithoutTheAspNetCoreSharedFramework()
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            typeof(ProtocolPackageDependencyTests).Assembly.GetName().Name + ".runtimeconfig.json"
        );

        Assert.True(File.Exists(path), path);
        Assert.DoesNotContain(
            "Microsoft.AspNetCore.App",
            File.ReadAllText(path, SecureEncoding.UTF8),
            StringComparison.Ordinal
        );
    }
}
