// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Subject;

public class DefaultAuthenticationContextReferenceResolverTests : BaseTests
{
    private DefaultAuthenticationContextReferenceResolver Resolver { get; } = new();

    #region Scaffolding

    private static AuthenticationContextReferenceRequest CreateRequest(
        IReadOnlyCollection<string> methods,
        IReadOnlySettingCollection settings
    ) =>
        new()
        {
            AuthenticationMethods = methods,
            IdentityProvider = null,
            Settings = settings,
        };

    #endregion

    #region ResolveAsync Tests

    [Fact]
    public async Task ResolveAsync_WhenCombinedFactors_ReturnsMultiFactorAcr()
    {
        // A knowledge factor combined with a possession factor indicates multi-factor; settings are not consulted.
        var settings = CreateStrictMock<IReadOnlySettingCollection>();
        var request = CreateRequest(
            [
                OpenIdProtocol.AuthenticationMethods.Password,
                OpenIdProtocol.AuthenticationMethods.OneTimePassword,
            ],
            settings.Object
        );

        var acr = await Resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Equal(OpenIdProtocol.AuthenticationContextClassReferences.MultiFactor, acr);
    }

    [Fact]
    public async Task ResolveAsync_WhenExplicitMfaMethod_ReturnsMultiFactorAcr()
    {
        var settings = CreateStrictMock<IReadOnlySettingCollection>();
        var request = CreateRequest(
            [OpenIdProtocol.AuthenticationMethods.MultiFactorAuthentication],
            settings.Object
        );

        var acr = await Resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Equal(OpenIdProtocol.AuthenticationContextClassReferences.MultiFactor, acr);
    }

    [Fact]
    public async Task ResolveAsync_WhenSingleFactorAndBaselineConfigured_ReturnsBaseline()
    {
        const string baseline = "urn:example:loa:1";
        var baselineOut = baseline;
        var settings = CreateStrictMock<IReadOnlySettingCollection>();
        settings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.DefaultAcr, out baselineOut))
            .Returns(true);

        var request = CreateRequest(
            [OpenIdProtocol.AuthenticationMethods.Password],
            settings.Object
        );

        var acr = await Resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Equal(baseline, acr);
    }

    [Fact]
    public async Task ResolveAsync_WhenSingleFactorAndNoBaseline_ReturnsNull()
    {
        string? baselineOut = null;
        var settings = CreateStrictMock<IReadOnlySettingCollection>();
        settings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.DefaultAcr, out baselineOut))
            .Returns(false);

        var request = CreateRequest(
            [OpenIdProtocol.AuthenticationMethods.Password],
            settings.Object
        );

        var acr = await Resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Null(acr);
    }

    #endregion
}
