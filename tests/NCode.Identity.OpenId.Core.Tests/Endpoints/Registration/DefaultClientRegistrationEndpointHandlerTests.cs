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
using Microsoft.AspNetCore.Routing;
using Moq;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Messages;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Registration;

public class DefaultClientRegistrationEndpointHandlerTests : BaseTests
{
    #region Scaffold

    private DefaultClientRegistrationEndpointHandler CreateHandler() =>
        CreateHandler(CreateStrictMock<IClientScopeService>().Object);

    private DefaultClientRegistrationEndpointHandler CreateHandler(
        IClientScopeService clientScopeService
    ) =>
        new(
            TimeProvider.System,
            CreateStrictMock<LinkGenerator>().Object,
            CreateStrictMock<ICryptoService>().Object,
            CreateStrictMock<ISecretGenerator>().Object,
            CreateStrictMock<ISecretImporter>().Object,
            CreateStrictMock<IStoreManagerFactory>().Object,
            CreateStrictMock<IPersistedGrantService>().Object,
            CreateStrictMock<IInitialAccessTokenValidator>().Object,
            CreateStrictMock<ISectorIdentifierValidator>().Object,
            clientScopeService,
            CreateStrictMock<ISettingSerializer>().Object
        );

    private (IOpenIdErrorFactory factory, Func<string?> capturedCode) CreateErrorFactory()
    {
        string? capturedCode = null;
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Callback<string>(code => capturedCode = code)
            .Returns(mockError.Object);
        return (mockErrorFactory.Object, () => capturedCode);
    }

    #endregion

    #region ValidateMetadata

    [Fact]
    public void ValidateMetadata_UnsupportedAuthMethod_ReturnsInvalidClientMetadata()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var request = new RegisterClientRequest { TokenEndpointAuthMethod = "private_key_jwt" };

        var error = handler.ValidateMetadata(factory, request, out _);

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public void ValidateMetadata_AuthorizationCodeWithoutRedirectUris_ReturnsInvalidRedirectUri()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var request = new RegisterClientRequest();

        var error = handler.ValidateMetadata(factory, request, out _);

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRedirectUri, capturedCode());
    }

    [Fact]
    public void ValidateMetadata_InvalidRedirectUri_ReturnsInvalidRedirectUri()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var request = new RegisterClientRequest { RedirectUris = ["not-an-absolute-uri"] };

        var error = handler.ValidateMetadata(factory, request, out _);

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRedirectUri, capturedCode());
    }

    [Fact]
    public void ValidateMetadata_RedirectUriWithFragment_ReturnsInvalidRedirectUri()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback#fragment"],
        };

        var error = handler.ValidateMetadata(factory, request, out _);

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRedirectUri, capturedCode());
    }

    [Fact]
    public void ValidateMetadata_ValidMinimal_AppliesDefaults()
    {
        var handler = CreateHandler();
        var (factory, _) = CreateErrorFactory();
        var request = new RegisterClientRequest { RedirectUris = ["https://example.com/callback"] };

        var error = handler.ValidateMetadata(factory, request, out var resolved);

        Assert.Null(error);
        Assert.Equal(
            OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            resolved.TokenEndpointAuthMethod
        );
        Assert.Equal([OpenIdConstants.GrantTypes.AuthorizationCode], resolved.GrantTypes);
        Assert.Equal([OpenIdConstants.ResponseTypes.Code], resolved.ResponseTypes);
    }

    [Fact]
    public void ValidateMetadata_ClientCredentials_NoRedirectRequired()
    {
        var handler = CreateHandler();
        var (factory, _) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            GrantTypes = [OpenIdConstants.GrantTypes.ClientCredentials],
            TokenEndpointAuthMethod = OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost,
        };

        var error = handler.ValidateMetadata(factory, request, out var resolved);

        Assert.Null(error);
        Assert.Empty(resolved.RedirectUris);
        Assert.Empty(resolved.ResponseTypes);
    }

    [Fact]
    public void ValidateMetadata_ResponseTypeNamesInAnyOrder_ResolvesTheCanonicalForm()
    {
        var handler = CreateHandler();
        var (factory, _) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            GrantTypes =
            [
                OpenIdConstants.GrantTypes.AuthorizationCode,
                OpenIdConstants.GrantTypes.Implicit,
            ],
            ResponseTypes = ["id_token code", OpenIdConstants.ResponseTypes.CodeIdToken],
        };

        var error = handler.ValidateMetadata(factory, request, out var resolved);

        Assert.Null(error);
        Assert.Equal([OpenIdConstants.ResponseTypes.CodeIdToken], resolved.ResponseTypes);
    }

    [Fact]
    public void ValidateMetadata_ResponseTypesWithoutGrantTypes_DefaultsToTheGrantTypesTheyUse()
    {
        var handler = CreateHandler();
        var (factory, _) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            ResponseTypes = [OpenIdConstants.ResponseTypes.CodeIdToken],
        };

        var error = handler.ValidateMetadata(factory, request, out var resolved);

        Assert.Null(error);
        Assert.Equal(
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit],
            resolved.GrantTypes
        );
    }

    [Fact]
    public void ValidateMetadata_GrantTypeOfAResponseTypeNotRegistered_ReturnsInvalidClientMetadata()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            GrantTypes = [OpenIdConstants.GrantTypes.AuthorizationCode],
            ResponseTypes = [OpenIdConstants.ResponseTypes.CodeIdToken],
        };

        var error = handler.ValidateMetadata(factory, request, out _);

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public void ValidateMetadata_ImplicitGrantWithoutResponseTypes_ReturnsInvalidClientMetadata()
    {
        // The default response type is 'code', which uses the authorization_code grant the client did not register.
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            GrantTypes = [OpenIdConstants.GrantTypes.Implicit],
        };

        var error = handler.ValidateMetadata(factory, request, out _);

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public void ValidateMetadata_EmptyResponseTypeValue_ReturnsInvalidClientMetadata()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            ResponseTypes = [string.Empty],
        };

        var error = handler.ValidateMetadata(factory, request, out _);

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public void ValidateMetadata_UnsupportedSubjectType_ReturnsInvalidClientMetadata()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            SubjectType = "bogus",
        };

        var error = handler.ValidateMetadata(factory, request, out _);

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public void ValidateMetadata_PairwiseMultipleHostsWithoutSectorUri_ReturnsInvalidClientMetadata()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://a.example.com/callback", "https://b.example.com/callback"],
            SubjectType = OpenIdConstants.SubjectTypes.Pairwise,
        };

        var error = handler.ValidateMetadata(factory, request, out _);

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public void ValidateMetadata_PairwiseSingleHostWithoutSectorUri_IsValid()
    {
        var handler = CreateHandler();
        var (factory, _) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/one", "https://example.com/two"],
            SubjectType = OpenIdConstants.SubjectTypes.Pairwise,
        };

        var error = handler.ValidateMetadata(factory, request, out var resolved);

        Assert.Null(error);
        Assert.Equal(OpenIdConstants.SubjectTypes.Pairwise, resolved.SubjectType);
    }

    [Fact]
    public void ValidateMetadata_PairwiseWithSectorUri_ResolvesSectorUri()
    {
        var handler = CreateHandler();
        var (factory, _) = CreateErrorFactory();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://a.example.com/callback", "https://b.example.com/callback"],
            SubjectType = OpenIdConstants.SubjectTypes.Pairwise,
            SectorIdentifierUri = "https://sector.example.com/uris.json",
        };

        var error = handler.ValidateMetadata(factory, request, out var resolved);

        Assert.Null(error);
        Assert.Equal("https://sector.example.com/uris.json", resolved.SectorIdentifierUri);
    }

    #endregion

    #region ValidateSupportedMetadata

    private static ResolvedMetadata CreateHybridMetadata() =>
        new()
        {
            TokenEndpointAuthMethod = OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            GrantTypes =
            [
                OpenIdConstants.GrantTypes.AuthorizationCode,
                OpenIdConstants.GrantTypes.Implicit,
            ],
            ResponseTypes = [OpenIdConstants.ResponseTypes.CodeIdToken],
            RedirectUris = ["https://example.com/callback"],
        };

    private Mock<IReadOnlySettingCollection> CreateTenantSettings(
        IReadOnlyCollection<string>? grantTypesSupported,
        IReadOnlyCollection<string>? responseTypesSupported,
        IReadOnlyCollection<string>? tokenEndpointAuthMethodsSupported = null
    )
    {
        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.GrantTypesSupported, out grantTypesSupported)
            )
            .Returns(true);
        mockSettings
            .Setup(x =>
                x.TryGetValue(OpenIdSettingKeys.ResponseTypesSupported, out responseTypesSupported)
            )
            .Returns(true);
        mockSettings
            .Setup(x =>
                x.TryGetValue(
                    OpenIdSettingKeys.TokenEndpointAuthMethodsSupported,
                    out tokenEndpointAuthMethodsSupported
                )
            )
            .Returns(tokenEndpointAuthMethodsSupported is not null);
        return mockSettings;
    }

    [Fact]
    public void ValidateSupportedMetadata_WhenTheTenantPermitsTheAuthMethod_ReturnsNull()
    {
        var handler = CreateHandler();
        var (factory, _) = CreateErrorFactory();
        var mockTenantSettings = CreateTenantSettings(
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit],
            [OpenIdConstants.ResponseTypes.CodeIdToken],
            [
                OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
                OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt,
            ]
        );

        var error = handler.ValidateSupportedMetadata(
            factory,
            mockTenantSettings.Object,
            CreateHybridMetadata()
        );

        Assert.Null(error);
    }

    [Fact]
    public void ValidateSupportedMetadata_WhenTheTenantDoesNotPermitTheAuthMethod_ReturnsInvalidClientMetadata()
    {
        // A client registered outside the tenant's ceiling could never authenticate.
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var mockTenantSettings = CreateTenantSettings(
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit],
            [OpenIdConstants.ResponseTypes.CodeIdToken],
            [OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt]
        );

        var error = handler.ValidateSupportedMetadata(
            factory,
            mockTenantSettings.Object,
            CreateHybridMetadata()
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public void ValidateSupportedMetadata_WhenTheTenantSupportsEveryValue_ReturnsNull()
    {
        var handler = CreateHandler();
        var (factory, _) = CreateErrorFactory();
        var mockTenantSettings = CreateTenantSettings(
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit],
            [OpenIdConstants.ResponseTypes.Code, OpenIdConstants.ResponseTypes.CodeIdToken]
        );

        var error = handler.ValidateSupportedMetadata(
            factory,
            mockTenantSettings.Object,
            CreateHybridMetadata()
        );

        Assert.Null(error);
    }

    [Fact]
    public void ValidateSupportedMetadata_WhenTheTenantDoesNotSupportAResponseType_ReturnsInvalidClientMetadata()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var mockTenantSettings = CreateTenantSettings(
            [OpenIdConstants.GrantTypes.AuthorizationCode, OpenIdConstants.GrantTypes.Implicit],
            [OpenIdConstants.ResponseTypes.Code, OpenIdConstants.ResponseTypes.IdToken]
        );

        var error = handler.ValidateSupportedMetadata(
            factory,
            mockTenantSettings.Object,
            CreateHybridMetadata()
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public void ValidateSupportedMetadata_WhenTheTenantDoesNotSupportAGrantType_ReturnsInvalidClientMetadata()
    {
        var handler = CreateHandler();
        var (factory, capturedCode) = CreateErrorFactory();
        var mockTenantSettings = CreateTenantSettings(
            [OpenIdConstants.GrantTypes.AuthorizationCode],
            [OpenIdConstants.ResponseTypes.CodeIdToken]
        );

        var error = handler.ValidateSupportedMetadata(
            factory,
            mockTenantSettings.Object,
            CreateHybridMetadata()
        );

        Assert.NotNull(error);
        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidClientMetadata, capturedCode());
    }

    [Fact]
    public void ValidateSupportedMetadata_WhenTheTenantListsAreUnset_ReturnsNull()
    {
        var handler = CreateHandler();
        var (factory, _) = CreateErrorFactory();
        var mockTenantSettings = CreateLooseMock<IReadOnlySettingCollection>();

        var error = handler.ValidateSupportedMetadata(
            factory,
            mockTenantSettings.Object,
            CreateHybridMetadata()
        );

        Assert.Null(error);
    }

    #endregion

    #region BuildSettingsObject

    [Fact]
    public void BuildSettingsObject_MapsFunctionalAndInformationalFields()
    {
        var handler = CreateHandler();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            Scope = "openid profile email",
            ClientName = "My Client",
            Contacts = ["admin@example.com"],
        };
        var resolved = new ResolvedMetadata
        {
            TokenEndpointAuthMethod = OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            GrantTypes = [OpenIdConstants.GrantTypes.AuthorizationCode],
            ResponseTypes = [OpenIdConstants.ResponseTypes.Code],
            RedirectUris = ["https://example.com/callback"],
        };

        var settings = handler.BuildSettingsObject(request, resolved);

        Assert.Equal(
            "client_secret_basic",
            settings[OpenIdSettingNames.TokenEndpointAuthMethod]!.GetValue<string>()
        );
        Assert.Equal("My Client", settings[OpenIdSettingNames.ClientName]!.GetValue<string>());
        Assert.NotNull(settings[OpenIdSettingNames.RedirectUris]);
        Assert.NotNull(settings[OpenIdSettingNames.Contacts]);
    }

    [Fact]
    public void BuildSettingsObject_WhenScopeRequested_DoesNotStoreIt()
    {
        var handler = CreateHandler();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            Scope = "openid profile email",
        };
        var resolved = new ResolvedMetadata
        {
            TokenEndpointAuthMethod = OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            GrantTypes = [OpenIdConstants.GrantTypes.AuthorizationCode],
            ResponseTypes = [OpenIdConstants.ResponseTypes.Code],
            RedirectUris = ["https://example.com/callback"],
        };

        var settings = handler.BuildSettingsObject(request, resolved);

        // The scopes a client may request come from resource servers and grants, never from a stored setting.
        Assert.DoesNotContain(
            settings,
            pair => pair.Key.Contains("scope", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void BuildSettingsObject_MapsSubjectTypeAndSectorIdentifierUri()
    {
        var handler = CreateHandler();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            SubjectType = OpenIdConstants.SubjectTypes.Pairwise,
            SectorIdentifierUri = "https://sector.example.com/uris.json",
        };
        var resolved = new ResolvedMetadata
        {
            TokenEndpointAuthMethod = OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            GrantTypes = [OpenIdConstants.GrantTypes.AuthorizationCode],
            ResponseTypes = [OpenIdConstants.ResponseTypes.Code],
            RedirectUris = ["https://example.com/callback"],
            SubjectType = OpenIdConstants.SubjectTypes.Pairwise,
            SectorIdentifierUri = "https://sector.example.com/uris.json",
        };

        var settings = handler.BuildSettingsObject(request, resolved);

        Assert.Equal(
            OpenIdConstants.SubjectTypes.Pairwise,
            settings[OpenIdSettingNames.SubjectType]!.GetValue<string>()
        );
        Assert.Equal(
            "https://sector.example.com/uris.json",
            settings[OpenIdSettingNames.SectorIdentifierUri]!.GetValue<string>()
        );
    }

    #endregion

    #region ToResponse

    [Fact]
    public void ToResponse_ReconstructsMetadataFromSettings()
    {
        var handler = CreateHandler();
        var request = new RegisterClientRequest
        {
            RedirectUris = ["https://example.com/callback"],
            Scope = "openid profile",
            ClientName = "My Client",
            LogoUri = "https://example.com/logo.png",
        };
        var resolved = new ResolvedMetadata
        {
            TokenEndpointAuthMethod = OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            GrantTypes = [OpenIdConstants.GrantTypes.AuthorizationCode],
            ResponseTypes = [OpenIdConstants.ResponseTypes.Code],
            RedirectUris = ["https://example.com/callback"],
        };
        var settings = handler.BuildSettingsObject(request, resolved);

        var client = new PersistedClient
        {
            TenantId = "tenant-1",
            ClientId = "client-1",
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = "tenant-1",
                ClientId = "client-1",
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(settings),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = "tenant-1",
                ClientId = "client-1",
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

        var response = handler.ToResponse(client);

        Assert.Equal("client-1", response.ClientId);
        Assert.Equal("client_secret_basic", response.TokenEndpointAuthMethod);
        Assert.Equal("My Client", response.ClientName);
        Assert.Equal("https://example.com/logo.png", response.LogoUri);
        Assert.Null(response.Scope);
        Assert.Equal(["https://example.com/callback"], response.RedirectUris);
        Assert.Equal([OpenIdConstants.GrantTypes.AuthorizationCode], response.GrantTypes);
        Assert.Equal([OpenIdConstants.ResponseTypes.Code], response.ResponseTypes);
    }

    [Fact]
    public void ToResponse_WhenNoResponseTypeIsRegistered_ReportsAnEmptyList()
    {
        var handler = CreateHandler();
        var request = new RegisterClientRequest();
        var resolved = new ResolvedMetadata
        {
            TokenEndpointAuthMethod = OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
            GrantTypes = [OpenIdConstants.GrantTypes.ClientCredentials],
            ResponseTypes = [],
            RedirectUris = [],
        };
        var settings = handler.BuildSettingsObject(request, resolved);
        var client = CreateClient(JsonSerializer.SerializeToElement(settings));

        var response = handler.ToResponse(client);

        // An omitted response_types would read as the RFC 7591 default ('code').
        Assert.NotNull(response.ResponseTypes);
        Assert.Empty(response.ResponseTypes);
    }

    #endregion

    #region ToResponseAsync

    [Fact]
    public async Task ToResponseAsync_ReportsTheScopesTheClientMayRequest()
    {
        var mockClientScopeService = CreateStrictMock<IClientScopeService>();
        mockClientScopeService
            .Setup(x =>
                x.GetAllowedScopesAsync("tenant-1", "client-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(["profile", "openid", "api:read"])
            .Verifiable();
        var handler = CreateHandler(mockClientScopeService.Object);
        var client = CreateClient(
            JsonSerializer.SerializeToElement(new Dictionary<string, object>())
        );

        var response = await handler.ToResponseAsync(client, CancellationToken.None);

        Assert.Equal("api:read openid profile", response.Scope);
        Assert.Equal("client-1", response.ClientId);
    }

    [Fact]
    public async Task ToResponseAsync_WhenNoScopeIsAllowed_OmitsScope()
    {
        var mockClientScopeService = CreateStrictMock<IClientScopeService>();
        mockClientScopeService
            .Setup(x =>
                x.GetAllowedScopesAsync("tenant-1", "client-1", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync([])
            .Verifiable();
        var handler = CreateHandler(mockClientScopeService.Object);
        var client = CreateClient(
            JsonSerializer.SerializeToElement(new Dictionary<string, object>())
        );

        var response = await handler.ToResponseAsync(client, CancellationToken.None);

        Assert.Null(response.Scope);
    }

    private static PersistedClient CreateClient(JsonElement settings) =>
        new()
        {
            TenantId = "tenant-1",
            ClientId = "client-1",
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = "tenant-1",
                ClientId = "client-1",
                ConcurrencyToken = string.Empty,
                Value = settings,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = "tenant-1",
                ClientId = "client-1",
                ConcurrencyToken = string.Empty,
                Value = [],
            },
        };

    #endregion
}
