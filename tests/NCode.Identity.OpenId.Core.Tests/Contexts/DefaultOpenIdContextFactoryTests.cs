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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Servers;
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
            CreateStrictMock<IOpenIdServerProvider>().Object,
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
