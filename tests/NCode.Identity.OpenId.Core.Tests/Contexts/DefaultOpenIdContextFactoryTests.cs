// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Contexts;

public class DefaultOpenIdContextFactoryTests : BaseTests
{
    #region CreateForBackgroundAsync Tests

    [Fact]
    public async Task CreateForBackgroundAsync_CreatesTheTenantsContextFromTheServicesWithoutARequest()
    {
        const string tenantId = "tenant-1";
        var cancellationToken = CancellationToken.None;
        var mockMediator = CreateStrictMock<IMediator>();
        var mockContext = CreateStrictMock<OpenIdContext>();
        await using var serviceProvider = new ServiceCollection()
            .AddSingleton(mockMediator.Object)
            .BuildServiceProvider();
        var factory = CreatePartialMock<DefaultOpenIdContextFactory>(
            CreateStrictMock<IOpenIdEnvironmentProvider>().Object,
            CreateStrictMock<IOpenIdPlatformProvider>().Object,
            CreateStrictMock<IOpenIdTenantFactory>().Object
        );
        factory
            .Setup(x =>
                x.CreateForTenantAsync(
                    It.Is<HttpContext>(h =>
                        h.RequestServices == serviceProvider && h.GetEndpoint() == null
                    ),
                    tenantId,
                    mockMediator.Object,
                    cancellationToken
                )
            )
            .ReturnsAsync(mockContext.Object)
            .Verifiable();

        var result = await factory.Object.CreateForBackgroundAsync(
            serviceProvider,
            tenantId,
            cancellationToken
        );

        Assert.Same(mockContext.Object, result);
    }

    #endregion
}
