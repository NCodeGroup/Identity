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
using Moq;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue;
using NCode.Identity.OpenId.Authentication.Endpoints.Continue.Models;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Tenants;
using NCode.Registration.AspNetCore;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Continue;

public class DefaultContinueRefusalServiceTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string State = "state-1";
    private const string ContinueUrl = "https://op.example/oauth2/continue?state=state-1";

    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private Mock<IContinueProviderSelector> MockContinueProviderSelector { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<OpenIdTenant> MockTenant { get; }
    private DefaultContinueRefusalService Service { get; }

    public DefaultContinueRefusalServiceTests()
    {
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        MockContinueProviderSelector = CreateStrictMock<IContinueProviderSelector>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockTenant = CreateStrictMock<OpenIdTenant>();
        Service = new DefaultContinueRefusalService(
            MockPersistedGrantService.Object,
            MockContinueProviderSelector.Object
        );
    }

    #region Scaffolding

    private PersistedGrantId SetupConsume(PersistedGrant<ContinueEnvelope>? grant)
    {
        var persistedGrantId = new PersistedGrantId
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.Continue,
            GrantKey = State,
        };

        MockContext.SetupGet(x => x.Tenant).Returns(MockTenant.Object).Verifiable();
        MockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.Continue, State)
            )
            .Returns(persistedGrantId)
            .Verifiable();
        MockPersistedGrantService
            .Setup(x =>
                x.ConsumeOnceOrDefault<ContinueEnvelope>(
                    MockContext.Object,
                    persistedGrantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PersistedGrant<ContinueEnvelope>?>(grant))
            .Verifiable();

        return persistedGrantId;
    }

    #endregion

    #region RefuseAsync Tests

    [Fact]
    public async Task RefuseAsync_WhenTheUrlNamesAParkedContinuation_ReturnsTheProvidersRefusal()
    {
        var error = CreateLooseMock<IOpenIdError>();
        var payloadJson = JsonElement.Parse("{}");

        var grant = new PersistedGrant<ContinueEnvelope>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = null,
            SessionId = null,
            Payload = new ContinueEnvelope { Code = "authorization", PayloadJson = payloadJson },
        };
        SetupConsume(grant);

        var mockProvider = CreateStrictMock<IContinueProvider>();
        MockContinueProviderSelector
            .Setup(x => x.SelectProvider("authorization"))
            .Returns(mockProvider.Object)
            .Verifiable();
        mockProvider
            .Setup(x =>
                x.RefuseAsync(
                    MockContext.Object,
                    payloadJson,
                    error.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<ReadOnlyEndpointDisposition>(
                    ReadOnlyEndpointDisposition.Handled(TypedResults.Ok())
                )
            )
            .Verifiable();

        var result = await Service.RefuseAsync(
            MockContext.Object,
            ContinueUrl,
            error.Object,
            CancellationToken.None
        );

        Assert.True(result.HasHttpResult);
    }

    [Fact]
    public async Task RefuseAsync_WhenTheUrlCarriesNoState_ReturnsUnhandled()
    {
        var error = CreateLooseMock<IOpenIdError>();

        var result = await Service.RefuseAsync(
            MockContext.Object,
            "/account/sessions",
            error.Object,
            CancellationToken.None
        );

        Assert.False(result.WasHandled);
    }

    [Fact]
    public async Task RefuseAsync_WhenTheContinuationIsSpent_ReturnsUnhandled()
    {
        var error = CreateLooseMock<IOpenIdError>();
        SetupConsume(grant: null);

        var result = await Service.RefuseAsync(
            MockContext.Object,
            ContinueUrl,
            error.Object,
            CancellationToken.None
        );

        Assert.False(result.WasHandled);
    }

    #endregion
}
