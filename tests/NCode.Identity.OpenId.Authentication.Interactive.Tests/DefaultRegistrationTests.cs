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

using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.OpenId.Authentication.Interactive.Tests;

public sealed class DefaultRegistrationTests : IDisposable
{
    private static string ContentRootPath { get; } = Path.Combine(Path.GetTempPath(), "host");

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose() => MockRepository.Verify();

    private InteractiveAuthenticationOptions ResolveOptions(
        Dictionary<string, string?> configurationData,
        Action<InteractiveAuthenticationOptions>? configureOptions = null
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();

        var hostEnvironment = MockRepository.Create<IHostEnvironment>();
        hostEnvironment.Setup(x => x.ContentRootPath).Returns(ContentRootPath);

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .NewBuilder<OpenIdAuthenticationLibrary>()
            .AddInteractiveAuthentication(configuration, hostEnvironment.Object, configureOptions);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider
            .GetRequiredService<IOptions<InteractiveAuthenticationOptions>>()
            .Value;
    }

    #region AddInteractiveAuthentication Tests

    [Fact]
    public void AddInteractiveAuthentication_WhenSectionIsAbsent_LeavesTheBundleUnset()
    {
        var options = ResolveOptions([]);

        Assert.Null(options.BundleRootPath);
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenBundleRootPathIsRelative_ResolvesItAgainstTheContentRoot()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?>
            {
                ["InteractiveAuthentication:BundleRootPath"] = "../frontend/apps/auth/dist",
            }
        );

        Assert.Equal(
            Path.Combine(ContentRootPath, "../frontend/apps/auth/dist"),
            options.BundleRootPath
        );
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenBundleRootPathIsRooted_UsesItAsIs()
    {
        var rooted = Path.Combine(Path.GetTempPath(), "bundle");

        var options = ResolveOptions(
            new Dictionary<string, string?>
            {
                ["InteractiveAuthentication:BundleRootPath"] = rooted,
            }
        );

        Assert.Equal(rooted, options.BundleRootPath);
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenConfiguredInCode_OverridesConfiguration()
    {
        var options = ResolveOptions(
            new Dictionary<string, string?> { ["InteractiveAuthentication:BundleRootPath"] = "a" },
            configure => configure.BundleRootPath = "b"
        );

        Assert.Equal(Path.Combine(ContentRootPath, "b"), options.BundleRootPath);
    }

    [Fact]
    public void AddInteractiveAuthentication_WhenSectionHasUnknownKey_Throws()
    {
        // A misspelled key would otherwise leave the surface without its bundle and no sign of why.
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ResolveOptions(
                new Dictionary<string, string?>
                {
                    ["InteractiveAuthentication:BundlePath"] = "../frontend/apps/auth/dist",
                }
            )
        );

        Assert.Contains("'InteractiveAuthentication'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("'BundlePath'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddInteractiveAuthentication_WhenNoBackendOffersAPasswordChange_ReportsItNotSupported()
    {
        var configuration = new ConfigurationBuilder().Build();
        var hostEnvironment = MockRepository.Create<IHostEnvironment>();

        var serviceCollection = new ServiceCollection();
        serviceCollection
            .NewBuilder<OpenIdAuthenticationLibrary>()
            .AddInteractiveAuthentication(configuration, hostEnvironment.Object);

        using var serviceProvider = serviceCollection.BuildServiceProvider();
        var passwordChanger = serviceProvider.GetRequiredService<IInteractivePasswordChanger>();

        var result = await passwordChanger.ChangePasswordAsync(
            null!,
            new ClaimsPrincipal(),
            "current",
            "new",
            CancellationToken.None
        );

        Assert.Equal(InteractivePasswordChangeResult.NotSupported, result);
    }

    #endregion
}
