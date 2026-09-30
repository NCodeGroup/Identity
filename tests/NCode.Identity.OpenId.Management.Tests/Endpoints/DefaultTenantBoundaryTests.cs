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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Management.Endpoints;

public sealed class DefaultTenantBoundaryTests : IDisposable
{
    private const string ResourceTenantId = "tenant-1";

    private MockRepository MockRepository { get; }
    private Mock<ITenantResolver> MockTenantResolver { get; }

    public DefaultTenantBoundaryTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private DefaultTenantBoundary CreateBoundary(string providerCode) =>
        new(
            MockTenantResolver.Object,
            Options.Create(new TenantResolutionOptions { ProviderCode = providerCode })
        );

    private static HttpContext CreateHttpContext() => new DefaultHttpContext();

    private void SetupResolvedTenant(TenantDescriptor? descriptor) =>
        MockTenantResolver
            .Setup(x =>
                x.ResolveDescriptorAsync(
                    It.IsAny<HttpContext>(),
                    It.IsAny<IPropertyBag>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(descriptor)
            .Verifiable();

    #endregion

    [Fact]
    public async Task CheckAsync_WhenStaticSingle_ReturnsNullWithoutResolving()
    {
        var boundary = CreateBoundary(OpenIdConstants.TenantProviderCodes.StaticSingle);

        var error = await boundary.CheckAsync(
            CreateHttpContext(),
            ResourceTenantId,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task CheckAsync_WhenScopedAndTenantMatches_ReturnsNull()
    {
        SetupResolvedTenant(
            new TenantDescriptor { TenantId = ResourceTenantId, DisplayName = "Tenant One" }
        );
        var boundary = CreateBoundary(OpenIdConstants.TenantProviderCodes.DynamicByHost);

        var error = await boundary.CheckAsync(
            CreateHttpContext(),
            ResourceTenantId,
            CancellationToken.None
        );

        Assert.Null(error);
    }

    [Fact]
    public async Task CheckAsync_WhenScopedAndTenantMismatch_Returns404()
    {
        SetupResolvedTenant(
            new TenantDescriptor { TenantId = "other-tenant", DisplayName = "Other" }
        );
        var boundary = CreateBoundary(OpenIdConstants.TenantProviderCodes.DynamicByPath);

        var error = await boundary.CheckAsync(
            CreateHttpContext(),
            ResourceTenantId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status404NotFound, error.StatusCode);
    }

    [Fact]
    public async Task CheckAsync_WhenScopedAndNoAmbientTenant_ReturnsNull()
    {
        SetupResolvedTenant(null);
        var boundary = CreateBoundary(OpenIdConstants.TenantProviderCodes.DynamicByHost);

        var error = await boundary.CheckAsync(
            CreateHttpContext(),
            ResourceTenantId,
            CancellationToken.None
        );

        Assert.Null(error);
    }
}
