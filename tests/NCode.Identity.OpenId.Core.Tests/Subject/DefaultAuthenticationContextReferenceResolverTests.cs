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

using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Subject;

public class DefaultAuthenticationContextReferenceResolverTests : BaseTests
{
    private DefaultAuthenticationContextReferenceResolver Resolver { get; } = new();

    #region Scaffolding

    private AuthenticationContextReferenceRequest CreateRequest(
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
                OpenIdConstants.AuthenticationMethods.Password,
                OpenIdConstants.AuthenticationMethods.OneTimePassword,
            ],
            settings.Object
        );

        var acr = await Resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Equal(OpenIdConstants.AuthenticationContextClassReferences.MultiFactor, acr);
    }

    [Fact]
    public async Task ResolveAsync_WhenExplicitMfaMethod_ReturnsMultiFactorAcr()
    {
        var settings = CreateStrictMock<IReadOnlySettingCollection>();
        var request = CreateRequest(
            [OpenIdConstants.AuthenticationMethods.MultiFactorAuthentication],
            settings.Object
        );

        var acr = await Resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Equal(OpenIdConstants.AuthenticationContextClassReferences.MultiFactor, acr);
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
            [OpenIdConstants.AuthenticationMethods.Password],
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
            [OpenIdConstants.AuthenticationMethods.Password],
            settings.Object
        );

        var acr = await Resolver.ResolveAsync(request, CancellationToken.None);

        Assert.Null(acr);
    }

    #endregion
}
