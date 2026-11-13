// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public sealed class DefaultClientJwksUriCheckerTests
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IOutboundHostChecker> MockOutboundHostChecker { get; }

    private DefaultClientJwksUriChecker Checker { get; }

    public DefaultClientJwksUriCheckerTests()
    {
        MockOutboundHostChecker = MockRepository.Create<IOutboundHostChecker>();
        Checker = new DefaultClientJwksUriChecker(MockOutboundHostChecker.Object);
    }

    private void SetupHostCheck(string host, bool isRefused) =>
        MockOutboundHostChecker
            .Setup(x => x.IsRefusedAsync(host, CancellationToken.None))
            .ReturnsAsync(isRefused)
            .Verifiable();

    #region GetRefusalAsync Tests

    [Fact]
    public async Task GetRefusalAsync_WhenTheSettingsAreNotAnObject_ReturnsNull()
    {
        var refusal = await Checker.GetRefusalAsync(
            JsonElement.Parse("[]"),
            null,
            CancellationToken.None
        );

        Assert.Null(refusal);
        MockRepository.Verify();
    }

    [Fact]
    public async Task GetRefusalAsync_WhenTheJwksUriIsMissing_ReturnsNull()
    {
        var refusal = await Checker.GetRefusalAsync(
            JsonElement.Parse("{}"),
            null,
            CancellationToken.None
        );

        Assert.Null(refusal);
        MockRepository.Verify();
    }

    [Fact]
    public async Task GetRefusalAsync_WhenTheJwksUriIsUnchanged_ReturnsNullWithoutAskingTheHostChecker()
    {
        var settings = JsonElement.Parse("""{"jwks_uri":"https://client.example/jwks"}""");
        var storedSettings = JsonElement.Parse("""{"jwks_uri":"https://client.example/jwks"}""");

        var refusal = await Checker.GetRefusalAsync(
            settings,
            storedSettings,
            CancellationToken.None
        );

        Assert.Null(refusal);
        MockRepository.Verify();
    }

    [Fact]
    public async Task GetRefusalAsync_WhenTheJwksUriIsNotHttps_ReturnsNullWithoutAskingTheHostChecker()
    {
        var refusal = await Checker.GetRefusalAsync(
            JsonElement.Parse("""{"jwks_uri":"http://client.example/jwks"}"""),
            null,
            CancellationToken.None
        );

        Assert.Null(refusal);
        MockRepository.Verify();
    }

    [Fact]
    public async Task GetRefusalAsync_WhenTheHostIsAnIpv6Literal_AsksTheHostCheckerForTheAddress()
    {
        SetupHostCheck("::1", isRefused: false);

        var refusal = await Checker.GetRefusalAsync(
            JsonElement.Parse("""{"jwks_uri":"https://[::1]/jwks"}"""),
            null,
            CancellationToken.None
        );

        Assert.Null(refusal);
        MockRepository.Verify();
    }

    [Fact]
    public async Task GetRefusalAsync_WhenTheJwksUriDiffersFromTheStoredOne_AsksTheHostChecker()
    {
        SetupHostCheck("client.example", isRefused: false);

        var refusal = await Checker.GetRefusalAsync(
            JsonElement.Parse("""{"jwks_uri":"https://client.example/jwks"}"""),
            JsonElement.Parse("""{"jwks_uri":"https://other.example/jwks"}"""),
            CancellationToken.None
        );

        Assert.Null(refusal);
        MockRepository.Verify();
    }

    [Fact]
    public async Task GetRefusalAsync_WhenTheHostIsRefused_ReturnsAnErrorNamingTheJwksUri()
    {
        SetupHostCheck("client.example", isRefused: true);

        var refusal = await Checker.GetRefusalAsync(
            JsonElement.Parse("""{"jwks_uri":"https://client.example/jwks"}"""),
            null,
            CancellationToken.None
        );

        Assert.NotNull(refusal);
        Assert.Equal(OpenIdSettingNames.JwksUri, refusal.Value.SettingName);
        Assert.Contains("secrets/import", refusal.Value.Message);
        MockRepository.Verify();
    }

    [Fact]
    public async Task GetRefusalAsync_WhenTheHostIsAllowed_ReturnsNull()
    {
        SetupHostCheck("client.example", isRefused: false);

        var refusal = await Checker.GetRefusalAsync(
            JsonElement.Parse("""{"jwks_uri":"https://client.example/jwks"}"""),
            null,
            CancellationToken.None
        );

        Assert.Null(refusal);
        MockRepository.Verify();
    }

    #endregion
}
