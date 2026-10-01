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
using Xunit;

namespace NCode.Identity.OpenId.Management.ResourceServers;

public sealed class ManagementResourceServerProviderTests
{
    private readonly ManagementResourceServerProvider _provider = new();

    [Fact]
    public void GetDescriptor_UsesTheManagementIdentifier()
    {
        var descriptor = _provider.GetDescriptor();

        Assert.Equal(
            OpenIdConstants.SystemResourceServerIdentifiers.Management,
            descriptor.Identifier
        );
    }

    [Theory]
    [InlineData("read:tenant_settings")]
    [InlineData("update:tenant_settings")]
    [InlineData("read:tenant_secrets")]
    [InlineData("create:tenant_secrets")]
    [InlineData("update:tenant_secrets")]
    [InlineData("delete:tenant_secrets")]
    [InlineData("read:clients")]
    [InlineData("create:clients")]
    [InlineData("update:clients")]
    [InlineData("delete:clients")]
    [InlineData("read:client_secrets")]
    [InlineData("create:client_secrets")]
    [InlineData("update:client_secrets")]
    [InlineData("delete:client_secrets")]
    [InlineData("read:resource_servers")]
    [InlineData("create:resource_servers")]
    [InlineData("read:client_grants")]
    [InlineData("read:grants")]
    [InlineData("delete:grants")]
    public void GetDescriptor_IncludesTheTenantPlaneScope(string expectedScope)
    {
        var descriptor = _provider.GetDescriptor();

        Assert.Contains(descriptor.Scopes, scope => scope.Value == expectedScope);
    }

    [Theory]
    [InlineData("create:tenant_settings")]
    [InlineData("delete:tenant_settings")]
    public void GetDescriptor_ExcludesCreateAndDeleteForTenantSettings(string settingsScope)
    {
        var descriptor = _provider.GetDescriptor();

        Assert.DoesNotContain(descriptor.Scopes, scope => scope.Value == settingsScope);
    }

    [Theory]
    [InlineData("read:servers")]
    [InlineData("read:tenants")]
    [InlineData("create:tenants")]
    public void GetDescriptor_ExcludesControlPlaneScopes(string controlPlaneScope)
    {
        var descriptor = _provider.GetDescriptor();

        Assert.DoesNotContain(descriptor.Scopes, scope => scope.Value == controlPlaneScope);
    }

    [Fact]
    public void GetDescriptor_CreateGrantScopeIsNotEmitted()
    {
        var descriptor = _provider.GetDescriptor();

        // Grants are read + revoke only; there is no create/update of a grant through the management API.
        Assert.DoesNotContain(descriptor.Scopes, scope => scope.Value == "create:grants");
        Assert.DoesNotContain(descriptor.Scopes, scope => scope.Value == "update:grants");
    }
}
