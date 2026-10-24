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

using Moq;
using NCode.Identity.Jose;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens;

public class DefaultIssuedTokenValidatorTests : BaseTests
{
    private const string Issuer = "https://issuer.example.com";
    private const string Token = "token-value";
    private const string SessionId = "session-xyz";

    private static IReadOnlyCollection<string> AccessTokenSigningAlgorithms { get; } =
    ["PS256", "ES256"];

    private static IReadOnlyCollection<string> AccessTokenEncryptionAlgorithms { get; } =
    ["RSA-OAEP-256"];

    private static IReadOnlyCollection<string> AccessTokenContentEncryptionAlgorithms { get; } =
    ["A256GCM"];

    private static IReadOnlyCollection<string> IdTokenSigningAlgorithms { get; } = ["RS256"];
    private static IReadOnlyCollection<string> IdTokenEncryptionAlgorithms { get; } =
    ["RSA-OAEP-256"];
    private static IReadOnlyCollection<string> IdTokenContentEncryptionAlgorithms { get; } =
    ["A256GCM"];

    private Mock<IJwtValidator> MockJwtValidator { get; }
    private Mock<ISessionService> MockSessionService { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private Mock<IReadOnlySettingCollection> MockSettings { get; }
    private SecretKey TenantKey { get; }
    private SecretKey ReaderKey { get; }
    private DefaultIssuedTokenValidator Validator { get; }
    private JwtValidationParameters? CapturedParameters { get; set; }

    public DefaultIssuedTokenValidatorTests()
    {
        MockJwtValidator = CreateStrictMock<IJwtValidator>();
        MockSessionService = CreateStrictMock<ISessionService>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        TenantKey = CreateLooseMock<SecretKey>().Object;
        ReaderKey = CreateLooseMock<SecretKey>().Object;

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        var mockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        var mockSecretsProvider = CreateStrictMock<ISecretKeyCollectionProvider>();
        var mockTenantKeys = CreateLooseMock<ISecretKeyCollection>();
        mockTenantKeys.SetupGet(x => x.Count).Returns(1);
        mockTenantKeys
            .Setup(x => x.GetEnumerator())
            .Returns(() => ((IEnumerable<SecretKey>)[TenantKey]).GetEnumerator());

        MockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        mockTenant.SetupGet(x => x.Issuer).Returns(Issuer).Verifiable();
        mockTenant
            .SetupGet(x => x.SettingsProvider)
            .Returns(mockSettingsProvider.Object)
            .Verifiable();
        mockTenant
            .SetupGet(x => x.SecretsProvider)
            .Returns(mockSecretsProvider.Object)
            .Verifiable();
        mockSettingsProvider.SetupGet(x => x.Collection).Returns(MockSettings.Object).Verifiable();
        mockSecretsProvider.SetupGet(x => x.Collection).Returns(mockTenantKeys.Object).Verifiable();

        Validator = new DefaultIssuedTokenValidator(
            MockJwtValidator.Object,
            MockSessionService.Object
        );
    }

    #region Scaffolding

    private void SetupAlgorithms(
        SettingKey<IReadOnlyCollection<string>> key,
        IReadOnlyCollection<string> algorithms
    )
    {
        IReadOnlyCollection<string>? value = algorithms;
        MockSettings.Setup(x => x.TryGetValue(key, out value)).Returns(true).Verifiable();
    }

    private void SetupAccessTokenAlgorithms()
    {
        SetupAlgorithms(
            OpenIdSettingKeys.AccessTokenSigningAlgValuesSupported,
            AccessTokenSigningAlgorithms
        );
        SetupAlgorithms(
            OpenIdSettingKeys.AccessTokenEncryptionAlgValuesSupported,
            AccessTokenEncryptionAlgorithms
        );
        SetupAlgorithms(
            OpenIdSettingKeys.AccessTokenEncryptionEncValuesSupported,
            AccessTokenContentEncryptionAlgorithms
        );
    }

    private void SetupJwtValidation(JwtValidationResult result) =>
        MockJwtValidator
            .Setup(x =>
                x.ValidateAsync(
                    Token,
                    It.IsAny<JwtValidationParameters>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, JwtValidationParameters, CancellationToken>(
                (_, parameters, _) => CapturedParameters = parameters
            )
            .ReturnsAsync(result)
            .Verifiable();

    private void SetupSession(PersistedGrantStatus? status) =>
        MockSessionService
            .Setup(x =>
                x.GetOrDefaultAsync(MockContext.Object, SessionId, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                status is { } value
                    ? new PersistedGrant<SessionGrant>
                    {
                        Status = value,
                        TenantId = "tenant-1",
                        ClientId = null,
                        SubjectId = "subject-1",
                        SessionId = SessionId,
                        Payload = new SessionGrant
                        {
                            AuthTime = DateTimeOffset.UnixEpoch,
                            AuthenticationMethods = null,
                            IdentityProvider = null,
                            ClientIds = [],
                            LastActivityWhen = DateTimeOffset.UnixEpoch,
                        },
                    }
                    : null
            )
            .Verifiable();

    private static JwtValidationResult ValidWithSession() =>
        TestJwtValidationResults.Valid(
            new Dictionary<string, object> { [JoseClaimNames.Payload.Sid] = SessionId }
        );

    private async Task<JwtValidationResult> ValidateAccessTokenAsync(
        AccessTokenValidationOptions? options = null
    ) =>
        await Validator.ValidateAccessTokenAsync(
            MockContext.Object,
            Token,
            options ?? new AccessTokenValidationOptions(),
            CancellationToken.None
        );

    private JwtValidationParameters AssertCaptured() =>
        CapturedParameters ?? throw new InvalidOperationException("No parameters were captured.");

    private static T AssertSingleRequirement<T>(JwtValidationParameters parameters)
        where T : IJwtValidationRequirement => Assert.Single(parameters.Requirements.OfType<T>());

    private static ClaimValueRequirement AssertClaimRequirement(
        JwtValidationParameters parameters,
        string claimName
    ) =>
        Assert.Single(
            parameters.Requirements.OfType<ClaimValueRequirement>(),
            requirement => requirement.ClaimName == claimName
        );

    #endregion

    #region ValidateAccessTokenAsync Tests

    [Fact]
    public async Task ValidateAccessTokenAsync_StatesTheTenantsAccessTokenPolicy()
    {
        SetupAccessTokenAlgorithms();
        SetupJwtValidation(TestJwtValidationResults.Valid(new Dictionary<string, object>()));

        var result = await ValidateAccessTokenAsync();

        Assert.True(result.IsValid);
        var parameters = AssertCaptured();
        Assert.Equal(JoseClaimNames.Payload.Role, parameters.RoleClaimType);
        Assert.Equal(AccessTokenSigningAlgorithms, parameters.ValidSignatureAlgorithms);
        Assert.Equal(AccessTokenEncryptionAlgorithms, parameters.ValidKeyManagementAlgorithms);
        Assert.Equal(
            AccessTokenContentEncryptionAlgorithms,
            parameters.ValidContentEncryptionAlgorithms
        );
        Assert.Empty(
            Assert.IsAssignableFrom<IEnumerable<string>>(parameters.ValidCompressionAlgorithms)
        );
        Assert.Equal([TenantKey], parameters.ValidationKeys);
        Assert.Equal([TenantKey], parameters.DecryptionKeys);
        Assert.Equal(
            [JoseTokenTypes.AccessTokenJwt],
            AssertSingleRequirement<TokenTypeRequirement>(parameters).ValidTokenTypes
        );
        Assert.Equal(
            [Issuer],
            AssertClaimRequirement(parameters, JoseClaimNames.Payload.Iss).ValidValues
        );
        AssertSingleRequirement<TokenLifetimeRequirement>(parameters);
        Assert.DoesNotContain(
            parameters.Requirements.OfType<ClaimValueRequirement>(),
            requirement => requirement.ClaimName == JoseClaimNames.Payload.Aud
        );
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_WhenTheReaderRequiresAudiencesAndAddsKeys_ValidatesThemToo()
    {
        SetupAccessTokenAlgorithms();
        SetupJwtValidation(TestJwtValidationResults.Valid(new Dictionary<string, object>()));

        await ValidateAccessTokenAsync(
            new AccessTokenValidationOptions
            {
                RequiredAudiences = ["urn:api"],
                AdditionalDecryptionKeys = [ReaderKey],
            }
        );

        var parameters = AssertCaptured();
        Assert.Equal([TenantKey], parameters.ValidationKeys);
        Assert.Equal([TenantKey, ReaderKey], parameters.DecryptionKeys);
        Assert.Equal(
            ["urn:api"],
            AssertClaimRequirement(parameters, JoseClaimNames.Payload.Aud).ValidValues
        );
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_WhenTheTokenFailsValidation_ReturnsTheFailureWithoutASessionLookup()
    {
        SetupAccessTokenAlgorithms();
        SetupJwtValidation(TestJwtValidationResults.Invalid(Token));

        var result = await ValidateAccessTokenAsync();

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_WhenTheValidatorThrows_ReturnsAFailure()
    {
        SetupAccessTokenAlgorithms();
        MockJwtValidator
            .Setup(x =>
                x.ValidateAsync(
                    Token,
                    It.IsAny<JwtValidationParameters>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new JwtDecodeException("malformed"))
            .Verifiable();

        var result = await ValidateAccessTokenAsync();

        Assert.False(result.IsValid);
        Assert.IsType<JwtDecodeException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_WhenTheRequestIsCancelled_Throws()
    {
        SetupAccessTokenAlgorithms();
        MockJwtValidator
            .Setup(x =>
                x.ValidateAsync(
                    Token,
                    It.IsAny<JwtValidationParameters>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new OperationCanceledException())
            .Verifiable();

        await Assert.ThrowsAsync<OperationCanceledException>(() => ValidateAccessTokenAsync());
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_WhenTheSessionIsRevoked_ReturnsAFailure()
    {
        SetupAccessTokenAlgorithms();
        SetupJwtValidation(ValidWithSession());
        SetupSession(PersistedGrantStatus.Revoked);

        var result = await ValidateAccessTokenAsync();

        Assert.False(result.IsValid);
        Assert.IsType<JwtValidationException>(result.Exception);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_WhenTheSessionIsActive_ReturnsTheToken()
    {
        SetupAccessTokenAlgorithms();
        SetupJwtValidation(ValidWithSession());
        SetupSession(PersistedGrantStatus.Active);

        var result = await ValidateAccessTokenAsync();

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAccessTokenAsync_WhenTheSessionHasAgedOut_ReturnsTheToken()
    {
        SetupAccessTokenAlgorithms();
        SetupJwtValidation(ValidWithSession());
        SetupSession(status: null);

        var result = await ValidateAccessTokenAsync();

        Assert.True(result.IsValid);
    }

    #endregion

    #region ValidateIdTokenAsync Tests

    [Fact]
    public async Task ValidateIdTokenAsync_StatesTheTenantsIdTokenPolicy()
    {
        SetupAlgorithms(
            OpenIdSettingKeys.IdTokenSigningAlgValuesSupported,
            IdTokenSigningAlgorithms
        );
        SetupJwtValidation(TestJwtValidationResults.Valid(new Dictionary<string, object>()));

        var result = await Validator.ValidateIdTokenAsync(
            MockContext.Object,
            Token,
            CancellationToken.None
        );

        Assert.True(result.IsValid);
        var parameters = AssertCaptured();
        Assert.Equal(IdTokenSigningAlgorithms, parameters.ValidSignatureAlgorithms);
        Assert.Equal([TenantKey], parameters.ValidationKeys);
        Assert.Null(parameters.DecryptionKeys);
        Assert.Equal(
            [JoseTokenTypes.Jwt],
            AssertSingleRequirement<TokenTypeRequirement>(parameters).ValidTokenTypes
        );
        Assert.Equal(
            [Issuer],
            AssertClaimRequirement(parameters, JoseClaimNames.Payload.Iss).ValidValues
        );
        AssertSingleRequirement<TokenLifetimeRequirement>(parameters);
    }

    #endregion

    #region ValidateIdTokenHintAsync Tests

    [Fact]
    public async Task ValidateIdTokenHintAsync_DoesNotValidateTheLifetime()
    {
        SetupIdTokenHintAlgorithms();
        SetupJwtValidation(TestJwtValidationResults.Valid(new Dictionary<string, object>()));

        var result = await Validator.ValidateIdTokenHintAsync(
            MockContext.Object,
            Token,
            CancellationToken.None
        );

        Assert.True(result.IsValid);
        var parameters = AssertCaptured();
        Assert.Equal(IdTokenSigningAlgorithms, parameters.ValidSignatureAlgorithms);
        Assert.Empty(parameters.Requirements.OfType<TokenLifetimeRequirement>());
        Assert.Equal(
            [JoseTokenTypes.Jwt],
            AssertSingleRequirement<TokenTypeRequirement>(parameters).ValidTokenTypes
        );
    }

    [Fact]
    public async Task ValidateIdTokenHintAsync_DecryptsAHintReEncryptedToTheTenant()
    {
        SetupIdTokenHintAlgorithms();
        SetupJwtValidation(TestJwtValidationResults.Valid(new Dictionary<string, object>()));

        var result = await Validator.ValidateIdTokenHintAsync(
            MockContext.Object,
            Token,
            CancellationToken.None
        );

        // A relying party may re-encrypt the hint to the server, under the tenant's advertised ID-token encryption lists.
        Assert.True(result.IsValid);
        var parameters = AssertCaptured();
        Assert.Equal([TenantKey], parameters.DecryptionKeys);
        Assert.Equal(IdTokenEncryptionAlgorithms, parameters.ValidKeyManagementAlgorithms);
        Assert.Equal(
            IdTokenContentEncryptionAlgorithms,
            parameters.ValidContentEncryptionAlgorithms
        );
        Assert.Empty(
            Assert.IsAssignableFrom<IEnumerable<string>>(parameters.ValidCompressionAlgorithms)
        );
        Assert.Equal(
            [Issuer],
            AssertClaimRequirement(parameters, JoseClaimNames.Payload.Iss).ValidValues
        );
    }

    private void SetupIdTokenHintAlgorithms()
    {
        SetupAlgorithms(
            OpenIdSettingKeys.IdTokenSigningAlgValuesSupported,
            IdTokenSigningAlgorithms
        );
        SetupAlgorithms(
            OpenIdSettingKeys.IdTokenEncryptionAlgValuesSupported,
            IdTokenEncryptionAlgorithms
        );
        SetupAlgorithms(
            OpenIdSettingKeys.IdTokenEncryptionEncValuesSupported,
            IdTokenContentEncryptionAlgorithms
        );
    }

    #endregion
}
