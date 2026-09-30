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

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Management.Endpoints;

public sealed class DefaultTenantBoundaryTests : IDisposable
{
    private const string ResourceTenantId = "tenant-1";

    private MockRepository MockRepository { get; }
    private Mock<ITenantSelector> MockTenantSelector { get; }

    public DefaultTenantBoundaryTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockTenantSelector = MockRepository.Create<ITenantSelector>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private DefaultTenantBoundary CreateBoundary(string strategyCode) =>
        new(
            MockTenantSelector.Object,
            Options.Create(new TenantResolutionOptions { StrategyCode = strategyCode })
        );

    private static HttpContext CreateHttpContext() => new DefaultHttpContext();

    private void SetupResolvedTenant(PersistedTenant tenant) =>
        MockTenantSelector
            .Setup(x =>
                x.ResolveTenantAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(tenant)
            .Verifiable();

    private static PersistedTenant CreateTenant(string tenantId) =>
        new()
        {
            TenantId = tenantId,
            ConcurrencyToken = string.Empty,
            DomainName = null,
            IsDisabled = false,
            DisplayName = "Tenant",
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(new Dictionary<string, object>()),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    #endregion

    [Fact]
    public async Task CheckAsync_WhenStaticSingle_ReturnsNullWithoutResolving()
    {
        var boundary = CreateBoundary(OpenIdConstants.TenantStrategyCodes.StaticSingle);

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
        SetupResolvedTenant(CreateTenant(ResourceTenantId));
        var boundary = CreateBoundary(OpenIdConstants.TenantStrategyCodes.DynamicByHost);

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
        SetupResolvedTenant(CreateTenant("other-tenant"));
        var boundary = CreateBoundary(OpenIdConstants.TenantStrategyCodes.DynamicByPath);

        var error = await boundary.CheckAsync(
            CreateHttpContext(),
            ResourceTenantId,
            CancellationToken.None
        );

        Assert.NotNull(error);
        Assert.Equal(StatusCodes.Status404NotFound, error.StatusCode);
    }
}
