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
using Microsoft.AspNetCore.Authentication;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Endpoints.UserInfo.Commands;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.UserInfo;

public class DefaultGetLocalAccountUserInfoClaimsHandlerTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string PrincipalId = "principal-1";
    private const string LocalAccountId = "account-1";

    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IFederatedIdentityStore> MockFederatedIdentityStore { get; }
    private Mock<ILocalAccountStore> MockLocalAccountStore { get; }
    private DefaultGetLocalAccountUserInfoClaimsHandler Handler { get; }

    public DefaultGetLocalAccountUserInfoClaimsHandlerTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockFederatedIdentityStore = CreateStrictMock<IFederatedIdentityStore>();
        MockLocalAccountStore = CreateStrictMock<ILocalAccountStore>();
        Handler = new DefaultGetLocalAccountUserInfoClaimsHandler(MockStoreManagerFactory.Object);
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
    )
    {
        var identity = new ClaimsIdentity("test");
        if (scopes.Length > 0)
        {
            identity.AddClaim(new Claim(JoseClaimNames.Payload.Scope, string.Join(' ', scopes)));
        }

        var subjectAuthentication = new SubjectAuthentication(
            "scheme",
            new AuthenticationProperties(),
            new ClaimsPrincipal(identity),
            PrincipalId
        );

        return new GetUserInfoClaimsCommand(
            CreateStrictMock<OpenIdContext>().Object,
            subjectAuthentication,
            claims
        );
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
            OpenIdConstants.ScopeTypes.OpenId,
            OpenIdConstants.ScopeTypes.Profile,
            OpenIdConstants.ScopeTypes.Email
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
            OpenIdConstants.ScopeTypes.OpenId,
            OpenIdConstants.ScopeTypes.Email
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
            OpenIdConstants.ScopeTypes.OpenId,
            OpenIdConstants.ScopeTypes.Profile
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
            OpenIdConstants.ScopeTypes.OpenId,
            OpenIdConstants.ScopeTypes.Email
        );

        await Handler.HandleAsync(command, CancellationToken.None);

        Assert.Equal("existing@localhost", claims[JoseClaimNames.Payload.Email]);
    }

    #endregion
}
