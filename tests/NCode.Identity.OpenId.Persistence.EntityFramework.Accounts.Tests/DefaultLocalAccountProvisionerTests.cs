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
using NCode.Identity.OpenId.Tenants;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;

public sealed class DefaultLocalAccountProvisionerTests : IDisposable
{
    private const string TenantId = "tenant-1";
    private const string GeneratedId = "generated-id";
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
    public async Task CreateAsync_WhenCalled_PersistsHashedAccountAndReturnsGeneratedId()
    {
        var factory = _mocks.Create<IStoreManagerFactory>();
        var manager = _mocks.Create<IStoreManager>();
        var store = _mocks.Create<ILocalAccountStore>();
        var crypto = _mocks.Create<ICryptoService>();

        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object)
            .Verifiable();
        manager.Setup(x => x.GetStore<ILocalAccountStore>()).Returns(store.Object).Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        crypto
            .Setup(x => x.GenerateKey(16, BinaryEncodingType.Base64Url))
            .Returns(GeneratedId)
            .Verifiable();

        PersistedLocalAccount? captured = null;
        store
            .Setup(x =>
                x.AddAsync(It.IsAny<PersistedLocalAccount>(), It.IsAny<CancellationToken>())
            )
            .Callback((PersistedLocalAccount account, CancellationToken _) => captured = account)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var provisioner = new DefaultLocalAccountProvisioner(
            factory.Object,
            _passwordHasher,
            crypto.Object
        );

        var result = await provisioner.CreateAsync(
            CreateContext(_mocks),
            new LocalAccountCreationRequest
            {
                UserName = "alice",
                Password = PasswordBytes,
                Email = "alice@example.com",
                EmailVerified = true,
            },
            CancellationToken.None
        );

        Assert.Equal(GeneratedId, result);

        Assert.NotNull(captured);
        Assert.Equal(TenantId, captured.TenantId);
        Assert.Equal(GeneratedId, captured.LocalAccountId);
        Assert.Equal("alice", captured.UserName);
        Assert.Equal("alice@example.com", captured.Email);
        Assert.True(captured.EmailVerified);
        Assert.True(captured.IsEnabled);
        Assert.NotNull(captured.SecurityStamp);

        // The stored hash must verify against the original credential bytes.
        Assert.Equal(
            PasswordVerificationResult.Success,
            _passwordHasher.VerifyHashedPassword(captured.PasswordHash!, PasswordBytes)
        );
    }
}
