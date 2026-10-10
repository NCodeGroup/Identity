#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.Credentials;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Contexts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;

public sealed class DefaultLocalAccountSourceTests : IDisposable
{
    private const string UserName = "alice";
    private const string Subject = "account-1";
    private static readonly byte[] PasswordBytes = "correct-horse"u8.ToArray();

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly ServiceProvider _hasherProvider;
    private readonly IPasswordHasher _passwordHasher;
    private readonly string _passwordHash;

    public DefaultLocalAccountSourceTests()
    {
        _hasherProvider = new ServiceCollection().AddDefaultPasswordHasher().BuildServiceProvider();
        _passwordHasher = _hasherProvider.GetRequiredService<IPasswordHasher>();
        _passwordHash = _passwordHasher.HashPassword(PasswordBytes);
    }

    public void Dispose()
    {
        _mocks.Verify();
        _hasherProvider.Dispose();
    }

    #region Helpers

    private (DefaultLocalAccountSource Source, Mock<ILocalAccountStore> Store) CreateSource()
    {
        var factory = _mocks.Create<IStoreManagerFactory>();
        var manager = _mocks.Create<IStoreManager>();
        var store = _mocks.Create<ILocalAccountStore>();

        factory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(manager.Object)
            .Verifiable();
        manager.Setup(x => x.GetStore<ILocalAccountStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        return (new DefaultLocalAccountSource(factory.Object, _passwordHasher), store);
    }

    private PersistedLocalAccount Account(bool isEnabled = true, string? passwordHash = null) =>
        new()
        {
            TenantId = "tenant-1",
            LocalAccountId = Subject,
            UserName = UserName,
            Email = null,
            EmailVerified = false,
            PasswordHash = passwordHash ?? _passwordHash,
            SecurityStamp = "stamp",
            IsEnabled = isEnabled,
            Claims = [new PersistedLocalAccountClaim { Type = "role", Value = "admin" }],
            ConcurrencyToken = string.Empty,
        };

    #endregion

    #region ValidateCredentialsAsync Tests

    [Fact]
    public async Task ValidateCredentialsAsync_WhenValidPassword_ReturnsLocalAccount()
    {
        var (source, store) = CreateSource();
        store
            .Setup(x => x.GetByUserNameOrDefaultAsync(UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account())
            .Verifiable();

        var result = await source.ValidateCredentialsAsync(
            Mock.Of<OpenIdContext>(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(Subject, result.Subject);
        Assert.True(result.IsEnabled);
        var claim = Assert.Single(result.Claims);
        Assert.Equal("role", claim.Type);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_MapsMetadataBags()
    {
        var (source, store) = CreateSource();
        var account = Account();
        account.ProfileMetadata = JsonElement.Parse("""{"theme":"dark"}""");
        account.SystemMetadata = JsonElement.Parse("""{"plan":"gold"}""");
        store
            .Setup(x => x.GetByUserNameOrDefaultAsync(UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(account)
            .Verifiable();

        var result = await source.ValidateCredentialsAsync(
            Mock.Of<OpenIdContext>(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal("dark", result.ProfileMetadata.GetProperty("theme").GetString());
        Assert.Equal("gold", result.SystemMetadata.GetProperty("plan").GetString());
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenAccountMissing_ReturnsNull()
    {
        var (source, store) = CreateSource();
        store
            .Setup(x => x.GetByUserNameOrDefaultAsync(UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedLocalAccount?)null)
            .Verifiable();

        var result = await source.ValidateCredentialsAsync(
            Mock.Of<OpenIdContext>(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenDisabled_ReturnsNull()
    {
        var (source, store) = CreateSource();
        store
            .Setup(x => x.GetByUserNameOrDefaultAsync(UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account(isEnabled: false))
            .Verifiable();

        var result = await source.ValidateCredentialsAsync(
            Mock.Of<OpenIdContext>(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenNoPasswordHash_ReturnsNull()
    {
        var (source, store) = CreateSource();
        store
            .Setup(x => x.GetByUserNameOrDefaultAsync(UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account(passwordHash: string.Empty))
            .Verifiable();

        var result = await source.ValidateCredentialsAsync(
            Mock.Of<OpenIdContext>(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenWrongPassword_ReturnsNull()
    {
        var (source, store) = CreateSource();
        store
            .Setup(x => x.GetByUserNameOrDefaultAsync(UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account())
            .Verifiable();

        var result = await source.ValidateCredentialsAsync(
            Mock.Of<OpenIdContext>(),
            UserName,
            "wrong-password"u8.ToArray(),
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region FindBySubjectAsync Tests

    [Fact]
    public async Task FindBySubjectAsync_WhenFound_ReturnsLocalAccount()
    {
        var (source, store) = CreateSource();
        store
            .Setup(x => x.GetByIdOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Account())
            .Verifiable();

        var result = await source.FindBySubjectAsync(
            Mock.Of<OpenIdContext>(),
            Subject,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(Subject, result.Subject);
    }

    [Fact]
    public async Task FindBySubjectAsync_WhenMissing_ReturnsNull()
    {
        var (source, store) = CreateSource();
        store
            .Setup(x => x.GetByIdOrDefaultAsync(Subject, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PersistedLocalAccount?)null)
            .Verifiable();

        var result = await source.FindBySubjectAsync(
            Mock.Of<OpenIdContext>(),
            Subject,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion
}
