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
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

public class DefaultClientResponseAlgorithmValidatorTests : IDisposable
{
    private const string TenantId = "tenant-1";

    private MockRepository MockRepository { get; }
    private Mock<IAmbientTenantAccessor> MockAmbientTenantAccessor { get; }
    private Mock<ISecurityProfileCatalog> MockSecurityProfileCatalog { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IClientStore> MockClientStore { get; }
    private DefaultClientResponseAlgorithmValidator Validator { get; }

    public DefaultClientResponseAlgorithmValidatorTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAmbientTenantAccessor = MockRepository.Create<IAmbientTenantAccessor>();
        MockSecurityProfileCatalog = MockRepository.Create<ISecurityProfileCatalog>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockClientStore = MockRepository.Create<IClientStore>();

        Validator = new DefaultClientResponseAlgorithmValidator(
            MockAmbientTenantAccessor.Object,
            MockSecurityProfileCatalog.Object
        );
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private Mock<IDisposable> SetupClients(params PersistedClient[] clients)
    {
        MockStoreManager
            .Setup(x => x.GetStore<IClientStore>())
            .Returns(MockClientStore.Object)
            .Verifiable();

        var mockScope = MockRepository.Create<IDisposable>();
        mockScope.Setup(x => x.Dispose()).Verifiable();
        MockAmbientTenantAccessor
            .Setup(x => x.BeginScope(TenantId))
            .Returns(mockScope.Object)
            .Verifiable();

        MockSecurityProfileCatalog
            .Setup(x =>
                x.Expand(
                    It.IsAny<JsonElement>(),
                    OpenIdSettingScopes.Client,
                    It.IsAny<JsonSerializerOptions>()
                )
            )
            .Returns(Array.Empty<Setting>());

        // Two pages, so the validator follows the cursor rather than stopping after the first page.
        MockClientStore
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == null),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new PagedResult<PersistedClient> { Items = [], NextCursor = "next" })
            .Verifiable();
        MockClientStore
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == "next"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new PagedResult<PersistedClient> { Items = clients, NextCursor = null })
            .Verifiable();

        return mockScope;
    }

    private static ISettingCollection CreateIdTokenSigningSettings(
        string registered,
        IReadOnlyCollection<string> permitted
    )
    {
        string? registeredValue = registered;
        IReadOnlyCollection<string>? permittedValues = permitted;
        var mockSettings = new Mock<ISettingCollection>(MockBehavior.Loose);
        mockSettings
            .Setup(x => x.Merge(It.IsAny<IEnumerable<Setting>>()))
            .Returns(mockSettings.Object);
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.IdTokenSignedResponseAlg, out registeredValue)
            )
            .Returns(true);
        mockSettings
            .Setup(x =>
                x.TryGetValue(
                    OpenIdSettingKeys.IdTokenSigningAlgValuesSupported,
                    out permittedValues
                )
            )
            .Returns(true);
        return mockSettings.Object;
    }

    private static PersistedClient CreateClient(string clientId) =>
        new()
        {
            TenantId = TenantId,
            ClientId = clientId,
            ConcurrencyToken = "client-ct",
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = clientId,
                ConcurrencyToken = "settings-ct",
                Value = JsonSerializer.SerializeToElement(new { }),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = clientId,
                ConcurrencyToken = "secrets-ct",
                Value = [],
            },
        };

    #endregion

    #region FindIntroducedViolationsAsync Tests

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenTheChangeExcludesAnAlgorithm_ReturnsTheFinding()
    {
        SetupClients(CreateClient("client-1"));

        var findings = await Validator.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TenantId,
            CreateIdTokenSigningSettings("RS256", ["RS256", "PS256"]),
            CreateIdTokenSigningSettings("RS256", ["PS256"]),
            JsonSerializerOptions.Default,
            CancellationToken.None
        );

        var finding = Assert.Single(findings);
        Assert.Equal("client-1", finding.ClientId);
        Assert.Equal(
            new ClientResponseAlgorithmViolation(
                OpenIdSettingKeys.IdTokenSignedResponseAlg,
                "RS256",
                OpenIdSettingKeys.IdTokenSigningAlgValuesSupported
            ),
            finding.Violation
        );
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenTheAlgorithmWasAlreadyExcluded_ReturnsEmpty()
    {
        SetupClients(CreateClient("client-1"));

        var findings = await Validator.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TenantId,
            CreateIdTokenSigningSettings("RS256", ["PS256"]),
            CreateIdTokenSigningSettings("RS256", ["PS256"]),
            JsonSerializerOptions.Default,
            CancellationToken.None
        );

        Assert.Empty(findings);
    }

    [Fact]
    public async Task FindIntroducedViolationsAsync_WhenTheAlgorithmStaysPermitted_ReturnsEmpty()
    {
        SetupClients(CreateClient("client-1"));

        var findings = await Validator.FindIntroducedViolationsAsync(
            MockStoreManager.Object,
            TenantId,
            CreateIdTokenSigningSettings("RS256", ["RS256"]),
            CreateIdTokenSigningSettings("RS256", ["RS256", "PS256"]),
            JsonSerializerOptions.Default,
            CancellationToken.None
        );

        Assert.Empty(findings);
    }

    #endregion
}
