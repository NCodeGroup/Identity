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
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;

public sealed class DefaultLocalAccountProvisionerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private static readonly byte[] PasswordBytes = "initial-password"u8.ToArray();

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly ServiceProvider _hasherProvider;
    private readonly IPasswordHasher _passwordHasher;

    public DefaultLocalAccountProvisionerTests()
    {
        _hasherProvider = new ServiceCollection().AddDefaultPasswordHasher().BuildServiceProvider();
        _passwordHasher = _hasherProvider.GetRequiredService<IPasswordHasher>();
    }

    public void Dispose()
    {
        _mocks.Verify();
        _hasherProvider.Dispose();
    }

    private static OpenIdContext CreateContext(MockRepository mocks)
    {
        var tenant = mocks.Create<OpenIdTenant>();
        tenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();

        var context = mocks.Create<OpenIdContext>();
        context.Setup(x => x.Tenant).Returns(tenant.Object).Verifiable();
        return context.Object;
    }

    [Fact]
    public async Task CreateAsync_WhenCalled_StagesHashedAccountWithPrincipalAndSelfIssuedIdentity()
    {
        var manager = _mocks.Create<IStoreManager>();
        var accountStore = _mocks.Create<ILocalAccountStore>();
        var principalStore = _mocks.Create<IFederatedPrincipalStore>();
        var identityStore = _mocks.Create<IFederatedIdentityStore>();
        var crypto = _mocks.Create<ICryptoService>();

        // The provisioner generates ids for: local account, principal, self-issued identity, and security stamp.
        crypto
            .SetupSequence(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
            .Returns("local-account-id")
            .Returns("principal-id")
            .Returns("federated-identity-id")
            .Returns("security-stamp");

        manager
            .Setup(x => x.GetStore<ILocalAccountStore>())
            .Returns(accountStore.Object)
            .Verifiable();
        manager
            .Setup(x => x.GetStore<IFederatedPrincipalStore>())
            .Returns(principalStore.Object)
            .Verifiable();
        manager
            .Setup(x => x.GetStore<IFederatedIdentityStore>())
            .Returns(identityStore.Object)
            .Verifiable();

        PersistedLocalAccount? capturedAccount = null;
        accountStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedLocalAccount>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedLocalAccount account, CancellationToken _) => capturedAccount = account
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        PersistedFederatedPrincipal? capturedPrincipal = null;
        principalStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedPrincipal>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedFederatedPrincipal principal, CancellationToken _) =>
                    capturedPrincipal = principal
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        PersistedFederatedIdentity? capturedIdentity = null;
        identityStore
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedFederatedIdentity>(), It.IsAny<CancellationToken>())
            )
            .Callback(
                (PersistedFederatedIdentity identity, CancellationToken _) =>
                    capturedIdentity = identity
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var provisioner = new DefaultLocalAccountProvisioner(_passwordHasher, crypto.Object);

        var result = await provisioner.CreateAsync(
            CreateContext(_mocks),
            manager.Object,
            new LocalAccountCreationRequest
            {
                UserName = "alice",
                Password = PasswordBytes,
                Email = "alice@example.com",
                EmailVerified = true,
            },
            CancellationToken.None
        );

        Assert.Equal("local-account-id", result);

        Assert.NotNull(capturedAccount);
        Assert.Equal(TenantId, capturedAccount.TenantId);
        Assert.Equal("local-account-id", capturedAccount.LocalAccountId);
        Assert.Equal("alice", capturedAccount.UserName);
        Assert.Equal("alice@example.com", capturedAccount.Email);
        Assert.True(capturedAccount.EmailVerified);
        Assert.True(capturedAccount.IsEnabled);
        Assert.Equal("security-stamp", capturedAccount.SecurityStamp);

        // The stored hash must verify against the original credential bytes.
        Assert.Equal(
            PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(capturedAccount.PasswordHash!, PasswordBytes)
        );

        Assert.NotNull(capturedPrincipal);
        Assert.Equal(TenantId, capturedPrincipal.TenantId);
        Assert.Equal("principal-id", capturedPrincipal.PrincipalId);

        Assert.NotNull(capturedIdentity);
        Assert.Equal(TenantId, capturedIdentity.TenantId);
        Assert.Equal("federated-identity-id", capturedIdentity.FederatedIdentityId);
        Assert.Equal("principal-id", capturedIdentity.PrincipalId);
        Assert.Equal(AccountConstants.SelfIssuer, capturedIdentity.Issuer);
        Assert.Equal("local-account-id", capturedIdentity.Subject);
    }
}
