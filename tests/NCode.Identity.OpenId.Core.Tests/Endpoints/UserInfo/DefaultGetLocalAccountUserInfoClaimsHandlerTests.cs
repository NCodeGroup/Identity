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
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.UserInfo;

public class DefaultGetLocalAccountUserInfoClaimsHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string PrincipalId = "principal-1";
    private const string LocalAccountId = "account-1";
    private const string AccessTokenId = "jti-1";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IFederatedIdentityStore> MockFederatedIdentityStore { get; }
    private Mock<ILocalAccountStore> MockLocalAccountStore { get; }
    private Mock<IPersistedGrantService> MockPersistedGrantService { get; }
    private DefaultGetLocalAccountUserInfoClaimsHandler Handler { get; }

    public DefaultGetLocalAccountUserInfoClaimsHandlerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockFederatedIdentityStore = CreateStrictMock<IFederatedIdentityStore>();
        MockLocalAccountStore = CreateStrictMock<ILocalAccountStore>();
        MockPersistedGrantService = CreateStrictMock<IPersistedGrantService>();
        Handler = new DefaultGetLocalAccountUserInfoClaimsHandler(
            MockStoreManagerFactory.Object,
            MockPersistedGrantService.Object
        );
    }

    #region Scaffolding

    private void SetupStoreManager()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(MockFederatedIdentityStore.Object)
            .Verifiable();
    }

    private void SetupIdentities(params PersistedFederatedIdentity[] identities) =>
        MockFederatedIdentityStore
            .Setup(x => x.GetByPrincipalAsync(PrincipalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(identities)
            .Verifiable();

    private void SetupAccount(PersistedLocalAccount? account)
    {
        MockStoreManager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(MockLocalAccountStore.Object)
            .Verifiable();
        MockLocalAccountStore
            .Setup(x => x.GetByIdOrDefaultAsync(LocalAccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account)
            .Verifiable();
    }

    private static PersistedFederatedIdentity SelfIssuedIdentity() =>
        new()
        {
            TenantId = TenantId,
            FederatedIdentityId = "fed-1",
            PrincipalId = PrincipalId,
            Issuer = AccountConstants.SelfIssuer,
            Subject = LocalAccountId,
            JoinKey = null,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedFederatedIdentity ExternalIdentity() =>
        new()
        {
            TenantId = TenantId,
            FederatedIdentityId = "fed-ext",
            PrincipalId = PrincipalId,
            Issuer = "https://external.example.com",
            Subject = "external-subject",
            JoinKey = null,
            ConcurrencyToken = string.Empty,
        };

    private static PersistedLocalAccount Account(
        string? email,
        bool emailVerified,
        params PersistedLocalAccountClaim[] claims
    ) =>
        new()
        {
            TenantId = TenantId,
            LocalAccountId = LocalAccountId,
            UserName = "user",
            Email = email,
            EmailVerified = emailVerified,
            IsEnabled = true,
            Claims = claims,
            ConcurrencyToken = string.Empty,
        };

    private GetUserInfoClaimsCommand CreateCommand(
        IDictionary<string, object> claims,
        params string[] scopes
    ) => CreateCommandCore(claims, jti: null, scopes);

    private GetUserInfoClaimsCommand CreateCommandWithJti(
        IDictionary<string, object> claims,
        string jti,
        params string[] scopes
    ) => CreateCommandCore(claims, jti, scopes);

    private GetUserInfoClaimsCommand CreateCommandCore(
        IDictionary<string, object> claims,
        string? jti,
        string[] scopes
    )
    {
        var identity = new ClaimsIdentity("test");
        if (scopes.Length > 0)
        {
            identity.AddClaim(new Claim(JoseClaimNames.Payload.Scope, string.Join(' ', scopes)));
        }

        if (!string.IsNullOrEmpty(jti))
        {
            identity.AddClaim(new Claim(JoseClaimNames.Payload.Jti, jti));
        }

        var subjectAuthentication = new SubjectAuthentication(
            "scheme",
            new AuthenticationProperties(),
            new ClaimsPrincipal(identity),
            PrincipalId
        );

        var mockContext = CreateStrictMock<OpenIdContext>();
        if (!string.IsNullOrEmpty(jti))
        {
            var mockTenant = CreateStrictMock<OpenIdTenant>();
            mockTenant.SetupGet(x => x.TenantId).Returns(TenantId).Verifiable();
            mockContext.SetupGet(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
        }

        return new GetUserInfoClaimsCommand(mockContext.Object, subjectAuthentication, claims);
    }

    private void SetupRequestedUserInfoClaims(string jti, params string[] claimTypes)
    {
        var grantId = new PersistedGrantId
        {
            TenantId = TenantId,
            GrantType = OpenIdConstants.PersistedGrantTypes.UserInfoClaims,
            GrantKey = jti,
        };
        MockPersistedGrantService
            .Setup(x =>
                x.CreateGrantId(TenantId, OpenIdConstants.PersistedGrantTypes.UserInfoClaims, jti)
            )
            .Returns(grantId)
            .Verifiable();

        var userInfo = new RequestClaimDictionary();
        foreach (var claimType in claimTypes)
        {
            userInfo[claimType] = null;
        }

        var grant = new PersistedGrant<IRequestClaims>
        {
            TenantId = TenantId,
            ClientId = "client-1",
            SubjectId = PrincipalId,
            SessionId = null,
            Payload = new RequestClaims { UserInfo = userInfo },
        };
        MockPersistedGrantService
            .Setup(x =>
                x.GetOrDefaultAsync<IRequestClaims>(
                    It.IsAny<OpenIdContext>(),
                    grantId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(grant)
            .Verifiable();
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenNoScopes_DoesNothing()
    {
        var claims = new Dictionary<string, object>();
        var command = CreateCommand(claims);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(claims);
    }

    [Fact]
    public async Task HandleAsync_WhenProfileAndEmailScopes_AddsAccountClaims()
    {
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(
            Account(
                "user@localhost",
                emailVerified: true,
                new PersistedLocalAccountClaim
                {
                    Type = JoseClaimNames.Payload.Name,
                    Value = "Test User",
                },
                new PersistedLocalAccountClaim
                {
                    Type = JoseClaimNames.Payload.GivenName,
                    Value = "Test",
                }
            )
        );

        var claims = new Dictionary<string, object>();
        var command = CreateCommand(
            claims,
            OpenIdProtocol.ScopeTypes.OpenId,
            OpenIdProtocol.ScopeTypes.Profile,
            OpenIdProtocol.ScopeTypes.Email
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("Test User", claims[JoseClaimNames.Payload.Name]);
        Assert.Equal("Test", claims[JoseClaimNames.Payload.GivenName]);
        Assert.Equal("user@localhost", claims[JoseClaimNames.Payload.Email]);
        Assert.Equal(true, claims[JoseClaimNames.Payload.EmailVerified]);
    }

    [Fact]
    public async Task HandleAsync_WhenOnlyEmailScope_OmitsProfileClaims()
    {
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(
            Account(
                "user@localhost",
                emailVerified: false,
                new PersistedLocalAccountClaim
                {
                    Type = JoseClaimNames.Payload.Name,
                    Value = "Test User",
                }
            )
        );

        var claims = new Dictionary<string, object>();
        var command = CreateCommand(
            claims,
            OpenIdProtocol.ScopeTypes.OpenId,
            OpenIdProtocol.ScopeTypes.Email
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.False(claims.ContainsKey(JoseClaimNames.Payload.Name));
        Assert.Equal("user@localhost", claims[JoseClaimNames.Payload.Email]);
        Assert.Equal(false, claims[JoseClaimNames.Payload.EmailVerified]);
    }

    [Fact]
    public async Task HandleAsync_WhenNoSelfIssuedConnection_DoesNotResolveAccount()
    {
        SetupStoreManager();
        SetupIdentities(ExternalIdentity());

        var claims = new Dictionary<string, object>();
        var command = CreateCommand(
            claims,
            OpenIdProtocol.ScopeTypes.OpenId,
            OpenIdProtocol.ScopeTypes.Profile
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Empty(claims);
    }

    [Fact]
    public async Task HandleAsync_DoesNotOverrideExistingClaim()
    {
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(Account("new@localhost", emailVerified: true));

        var claims = new Dictionary<string, object>
        {
            [JoseClaimNames.Payload.Email] = "existing@localhost",
        };
        var command = CreateCommand(
            claims,
            OpenIdProtocol.ScopeTypes.OpenId,
            OpenIdProtocol.ScopeTypes.Email
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("existing@localhost", claims[JoseClaimNames.Payload.Email]);
    }

    [Fact]
    public async Task HandleAsync_WhenClaimsParameterRequestsName_AddsFromAccountWithoutProfileScope()
    {
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(
            Account(
                email: null,
                emailVerified: false,
                new PersistedLocalAccountClaim
                {
                    Type = JoseClaimNames.Payload.Name,
                    Value = "Test User",
                }
            )
        );
        SetupRequestedUserInfoClaims(AccessTokenId, JoseClaimNames.Payload.Name);

        var claims = new Dictionary<string, object>();
        var command = CreateCommandWithJti(claims, AccessTokenId, OpenIdProtocol.ScopeTypes.OpenId);

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("Test User", claims[JoseClaimNames.Payload.Name]);
    }

    [Fact]
    public async Task HandleAsync_WhenAddressScope_EmitsAddressAsJsonObject()
    {
        const string addressJson = """{"locality":"Springfield","country":"US"}""";
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(
            Account(
                email: null,
                emailVerified: false,
                new PersistedLocalAccountClaim
                {
                    Type = JoseClaimNames.Payload.Address,
                    Value = addressJson,
                }
            )
        );

        var claims = new Dictionary<string, object>();
        var command = CreateCommand(
            claims,
            OpenIdProtocol.ScopeTypes.OpenId,
            OpenIdProtocol.ScopeTypes.Address
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        var address = Assert.IsAssignableFrom<JsonNode>(claims[JoseClaimNames.Payload.Address]);
        Assert.Equal("Springfield", (string?)address["locality"]);
        Assert.Equal("US", (string?)address["country"]);
    }

    [Fact]
    public async Task HandleAsync_WhenPhoneScope_EmitsTypedPhoneClaims()
    {
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(
            Account(
                email: null,
                emailVerified: false,
                new PersistedLocalAccountClaim
                {
                    Type = JoseClaimNames.Payload.PhoneNumber,
                    Value = "+1 555 0100",
                },
                new PersistedLocalAccountClaim
                {
                    Type = JoseClaimNames.Payload.PhoneNumberVerified,
                    Value = "true",
                }
            )
        );

        var claims = new Dictionary<string, object>();
        var command = CreateCommand(
            claims,
            OpenIdProtocol.ScopeTypes.OpenId,
            OpenIdProtocol.ScopeTypes.Phone
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("+1 555 0100", claims[JoseClaimNames.Payload.PhoneNumber]);
        Assert.Equal(true, claims[JoseClaimNames.Payload.PhoneNumberVerified]);
    }

    [Fact]
    public async Task HandleAsync_WhenProfileScope_EmitsUpdatedAtAsNumber()
    {
        SetupStoreManager();
        SetupIdentities(SelfIssuedIdentity());
        SetupAccount(
            Account(
                email: null,
                emailVerified: false,
                new PersistedLocalAccountClaim
                {
                    Type = JoseClaimNames.Payload.UpdatedAt,
                    Value = "1700000000",
                }
            )
        );

        var claims = new Dictionary<string, object>();
        var command = CreateCommand(
            claims,
            OpenIdProtocol.ScopeTypes.OpenId,
            OpenIdProtocol.ScopeTypes.Profile
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal(1700000000L, claims[JoseClaimNames.Payload.UpdatedAt]);
    }

    #endregion
}
