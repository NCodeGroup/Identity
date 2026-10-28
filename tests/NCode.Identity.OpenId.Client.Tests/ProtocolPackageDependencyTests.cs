#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
}
