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

using NCode.Identity.OpenId.Management.ResourceServers;
using NCode.Identity.OpenId.ResourceServers;
using Xunit;

namespace NCode.Identity.OpenId.Management.ResourceServers;

public sealed class ControlManagementResourceServerProviderTests
{
    private readonly ControlManagementResourceServerProvider _provider = new();

    [Fact]
    public void GetDescriptor_UsesTheManagementIdentifier()
    {
        var descriptor = _provider.GetDescriptor();

        Assert.Equal(
            OpenIdConstants.SystemResourceServerIdentifiers.Management,
            descriptor.Identifier
        );
    }

    [Fact]
    public void GetDescriptor_TargetsTheControlPlane()
    {
        var descriptor = _provider.GetDescriptor();

        Assert.Equal(SystemResourceServerPlane.Control, descriptor.Plane);
    }

    [Theory]
    [InlineData("read:servers")]
    [InlineData("create:servers")]
    [InlineData("update:servers")]
    [InlineData("delete:servers")]
    [InlineData("read:server_settings")]
    [InlineData("update:server_settings")]
    [InlineData("read:server_secrets")]
    [InlineData("create:server_secrets")]
    [InlineData("update:server_secrets")]
    [InlineData("delete:server_secrets")]
    [InlineData("read:tenants")]
    [InlineData("create:tenants")]
    [InlineData("update:tenants")]
    [InlineData("delete:tenants")]
    public void GetDescriptor_IncludesTheControlPlaneScope(string expectedScope)
    {
        var descriptor = _provider.GetDescriptor();

        Assert.Contains(descriptor.Scopes, scope => scope.Value == expectedScope);
    }

    [Theory]
    [InlineData("create:server_settings")]
    [InlineData("delete:server_settings")]
    public void GetDescriptor_ExcludesCreateAndDeleteForServerSettings(string settingsScope)
    {
        var descriptor = _provider.GetDescriptor();

        Assert.DoesNotContain(descriptor.Scopes, scope => scope.Value == settingsScope);
    }

    [Theory]
    [InlineData("read:clients")]
    [InlineData("read:resource_servers")]
    [InlineData("read:grants")]
    public void GetDescriptor_ExcludesTenantPlaneScopes(string tenantPlaneScope)
    {
        var descriptor = _provider.GetDescriptor();

        Assert.DoesNotContain(descriptor.Scopes, scope => scope.Value == tenantPlaneScope);
    }
}
