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
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants.Keys;
using NCode.Identity.Secrets;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class ServerSettingsChangedTenantKeyHandlerTests : BaseTests
{
    private const string ServerId = "default";

    private static TenantKeySpecification Rs256 { get; } =
        new()
        {
            Algorithm = "RS256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Rsa,
            KeySizeBits = 3072,
        };

    private static TenantKeySpecification Es256 { get; } =
        new()
        {
            Algorithm = "ES256",
            Use = SecretKeyUses.Signature,
            SecretType = SecretKeyTypes.Ecc,
            KeySizeBits = 256,
        };

    private OpenIdOptions OpenIdOptions { get; } = new();
    private Mock<ITenantKeyPolicy> MockPolicy { get; }
    private Mock<IEffectiveSettingsResolver> MockEffectiveSettingsResolver { get; }
    private Mock<IJobDispatcher> MockJobDispatcher { get; }
    private ServerSettingsChangedTenantKeyHandler Handler { get; }

    public ServerSettingsChangedTenantKeyHandlerTests()
    {
        MockPolicy = CreateStrictMock<ITenantKeyPolicy>();
        MockEffectiveSettingsResolver = CreateStrictMock<IEffectiveSettingsResolver>();
        MockJobDispatcher = CreateStrictMock<IJobDispatcher>();

        Handler = new ServerSettingsChangedTenantKeyHandler(
            Options.Create(OpenIdOptions),
            MockPolicy.Object,
            MockEffectiveSettingsResolver.Object,
            MockJobDispatcher.Object
        );
    }

    private OpenIdTenantKeyProvisioningOptions KeyProvisioning =>
        OpenIdOptions.Tenant.KeyProvisioning;

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenAListGainsAConfiguredAlgorithm_DispatchesAReseedJob()
    {
        // ES256 is configured and was already listed, PS256 is listed now but not configured, and RS256 is the one the
        // change permits.
        SetupSpecifications(Rs256, Es256);
        SetupServerSettings(previous: ["ES256"], current: ["ES256", "PS256", "RS256"]);
        MockJobDispatcher
            .Setup(x =>
                x.DispatchAsync(
                    It.Is<TenantKeyReseedJob>(job =>
                        HasVersion(job.PreviousServerSettings, 1)
                        && HasVersion(job.ServerSettings, 2)
                        && job.Algorithms.SequenceEqual(new[] { "RS256" })
                    ),
                    It.IsAny<DispatchOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenAServerListIsLifted_DispatchesAReseedJob()
    {
        // The server's list named only ES256 and the change removes it, so every configured algorithm it did not name is
        // permitted; ES256 is not among them.
        SetupSpecifications(Rs256, Es256);
        SetupServerSettings(previous: ["ES256"], current: null);
        MockJobDispatcher
            .Setup(x =>
                x.DispatchAsync(
                    It.Is<TenantKeyReseedJob>(job =>
                        HasVersion(job.PreviousServerSettings, 1)
                        && HasVersion(job.ServerSettings, 2)
                        && job.Algorithms.SequenceEqual(new[] { "RS256" })
                    ),
                    It.IsAny<DispatchOptions?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenNoListGainsAConfiguredAlgorithm_DispatchesNothing()
    {
        // The strict dispatcher has no setup: dispatching a job fails the test.
        SetupSpecifications(Rs256, Es256);
        SetupServerSettings(previous: ["RS256"], current: ["RS256", "PS256"]);

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenProvisioningIsDisabled_DispatchesNothing()
    {
        // Every mock is strict with no setup: resolving the specifications or the settings fails the test.
        KeyProvisioning.Enabled = false;

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    #endregion

    #region Scaffolding

    private static bool HasVersion(JsonElement element, int version) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty("v", out var value)
        && value.GetInt32() == version;

    private static ServerSettingsChangedCommand CreateCommand() =>
        new(ServerId, JsonElement.Parse("""{"v":1}"""), JsonElement.Parse("""{"v":2}"""));

    private void SetupSpecifications(params TenantKeySpecification[] specifications) =>
        MockPolicy
            .Setup(x => x.ResolveSpecifications(KeyProvisioning))
            .Returns(
                new TenantKeySpecificationResolution
                {
                    Specifications = [.. specifications],
                    Errors = [],
                }
            )
            .Verifiable();

    private void SetupServerSettings(string[] previous, string[]? current)
    {
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveServerAsync(
                    It.Is<JsonElement>(e => HasVersion(e, 1)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSettings(previous))
            .Verifiable();
        MockEffectiveSettingsResolver
            .Setup(x =>
                x.ResolveServerAsync(
                    It.Is<JsonElement>(e => HasVersion(e, 2)),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSettings(current))
            .Verifiable();
    }

    private IReadOnlySettingCollection CreateSettings(string[]? algorithms)
    {
        // The server lists its algorithms under one setting only; every other lookup finds nothing, and a null list
        // sets nothing at all.
        var mockSettings = CreateLooseMock<ISettingCollection>();
        if (algorithms is null)
        {
            return mockSettings.Object;
        }

        object? value = algorithms.ToList();
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingNames.IdTokenSigningAlgValuesSupported, out value)
            )
            .Returns(true);
        return mockSettings.Object;
    }

    #endregion
}
