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
using NCode.Identity.OpenId.Accounts.Authentication;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Contexts;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Accounts;

public class PasswordAttemptGateTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string LocalAccountId = "account-1";
    private const string UserName = "alice";

    private Mock<IPasswordAttemptLimiter> MockPasswordAttemptLimiter { get; }
    private Mock<OpenIdContext> MockContext { get; }
    private PasswordAttemptGate Gate { get; }

    public PasswordAttemptGateTests()
    {
        MockPasswordAttemptLimiter = CreateStrictMock<IPasswordAttemptLimiter>();
        MockContext = CreateStrictMock<OpenIdContext>();
        Gate = new PasswordAttemptGate(MockPasswordAttemptLimiter.Object, MockContext.Object);
    }

    #region Scaffolding

    private void SetupLimit(bool isReached) =>
        MockPasswordAttemptLimiter
            .Setup(x =>
                x.IsLimitReachedAsync(MockContext.Object, UserName, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(isReached)
            .Verifiable();

    private static PersistedLocalAccount Account() =>
        new()
        {
            TenantId = TenantId,
            LocalAccountId = LocalAccountId,
            UserName = UserName,
            Email = null,
            EmailVerified = false,
            IsEnabled = true,
            Claims = [],
            ConcurrencyToken = string.Empty,
        };

    #endregion

    #region IsRefusedAsync Tests

    [Fact]
    public async Task IsRefusedAsync_ReturnsTheLimitersAnswer()
    {
        SetupLimit(isReached: true);

        var isRefused = await Gate.IsRefusedAsync(UserName, CancellationToken.None);

        Assert.True(isRefused);
    }

    #endregion

    #region Enforce Tests

    [Fact]
    public void Enforce_WhenTheStoreDidNotAskTheGate_Throws()
    {
        var stored = new LocalAccountVerificationResult
        {
            UserName = UserName,
            Account = Account(),
        };

        Assert.Throws<InvalidOperationException>(() => Gate.Enforce(stored));
    }

    [Fact]
    public async Task Enforce_WhenTheGateRefusedAndTheStoreVerified_ReturnsARefusal()
    {
        SetupLimit(isReached: true);
        await Gate.IsRefusedAsync(UserName, CancellationToken.None);
        var stored = new LocalAccountVerificationResult
        {
            UserName = UserName,
            Account = Account(),
        };

        var result = Gate.Enforce(stored);

        Assert.True(result.IsRefused);
        Assert.Null(result.Account);
        Assert.Equal(UserName, result.UserName);
    }

    [Fact]
    public async Task Enforce_WhenTheGateAllowed_ReturnsTheStoresResult()
    {
        SetupLimit(isReached: false);
        await Gate.IsRefusedAsync(UserName, CancellationToken.None);
        var stored = new LocalAccountVerificationResult
        {
            UserName = UserName,
            Account = Account(),
        };

        var result = Gate.Enforce(stored);

        Assert.Same(stored, result);
    }

    #endregion
}
