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
    public void BffAssembly_ReferencesOnlyAbstractionPackages()
    {
        var assembly = Assembly.Load(new AssemblyName("NCode.Identity.Bff"));

        foreach (var reference in assembly.GetReferencedAssemblies())
        {
            var name = reference.Name ?? string.Empty;
            if (!name.StartsWith("NCode.Identity.", StringComparison.Ordinal))
                continue;

            Assert.True(name.EndsWith(".Abstractions", StringComparison.Ordinal), name);
        }
    }
}
