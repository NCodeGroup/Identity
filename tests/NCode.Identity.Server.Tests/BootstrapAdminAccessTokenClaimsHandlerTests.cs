// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Tokens.Commands;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class BootstrapAdminAccessTokenClaimsHandlerTests : BaseTests
{
    private const string BootstrapClientId = "bootstrap-admin";
    private const string BootstrapTenantId = "default";

    private static BootstrapAdminAccessTokenClaimsHandler CreateHandler(
        BootstrapAdminOptions options
    ) => new(Options.Create(options));

    private static BootstrapAdminOptions ConfiguredOptions() =>
        new()
        {
            ClientId = BootstrapClientId,
            ClientSecret = "secret",
            TenantId = BootstrapTenantId,
        };

    private GetAccessTokenPayloadClaimsCommand CreateCommand(
        string clientId,
        IDictionary<string, object> payloadClaims,
        string? tenantId = null
    )
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        if (tenantId is not null)
        {
            var mockTenant = CreateStrictMock<OpenIdTenant>();
            mockTenant.SetupGet(x => x.TenantId).Returns(tenantId).Verifiable();
            mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        }

        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.ClientId).Returns(clientId).Verifiable();

        return new GetAccessTokenPayloadClaimsCommand(
            mockContext.Object,
            mockClient.Object,
            default,
            payloadClaims
        );
    }

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenBootstrapNotConfigured_DoesNotAddRole()
    {
        var handler = CreateHandler(new BootstrapAdminOptions());
        var payloadClaims = new Dictionary<string, object>(StringComparer.Ordinal);

        var mockContext = CreateStrictMock<OpenIdContext>();
        var mockClient = CreateStrictMock<OpenIdClient>();
        var command = new GetAccessTokenPayloadClaimsCommand(
            mockContext.Object,
            mockClient.Object,
            default,
            payloadClaims
        );

        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(payloadClaims);
    }

    [Fact]
    public async Task HandleAsync_WhenClientDoesNotMatch_DoesNotAddRole()
    {
        var handler = CreateHandler(ConfiguredOptions());
        var payloadClaims = new Dictionary<string, object>(StringComparer.Ordinal);
        var command = CreateCommand("some-other-client", payloadClaims);

        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(payloadClaims);
    }

    [Fact]
    public async Task HandleAsync_WhenClientMatchesInAnotherTenant_DoesNotAddRole()
    {
        // A client sharing the bootstrap client's identifier elsewhere is not the bootstrap administrator.
        var handler = CreateHandler(ConfiguredOptions());
        var payloadClaims = new Dictionary<string, object>(StringComparer.Ordinal);
        var command = CreateCommand(BootstrapClientId, payloadClaims, tenantId: "acme");

        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(payloadClaims);
    }

    [Fact]
    public async Task HandleAsync_WhenClientMatchesInTheBootstrapTenant_AddsGlobalAdminRole()
    {
        var handler = CreateHandler(ConfiguredOptions());
        var payloadClaims = new Dictionary<string, object>(StringComparer.Ordinal);
        var command = CreateCommand(BootstrapClientId, payloadClaims, BootstrapTenantId);

        await handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(
            BuiltInRoles.GlobalAdmin,
            Assert.Contains(JoseClaimNames.Payload.Role, payloadClaims)
        );
    }

    #endregion
}
