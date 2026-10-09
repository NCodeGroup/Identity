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
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

public sealed class AspNetIdentityLocalAccountSourceTests : IDisposable
{
    private const string UserName = "alice";
    private const string Subject = "user-1";
    private const string Password = "correct-horse";
    private const string SecurityStamp = "stamp";
    private static readonly byte[] PasswordBytes = "correct-horse"u8.ToArray();
    private static readonly Claim RoleClaim = new("role", "admin");

    private readonly MockRepository _mocks = new(MockBehavior.Strict);

    public void Dispose() => _mocks.Verify();

    #region Helpers

    private (
        AspNetIdentityLocalAccountSource<IdentityUser> Source,
        Mock<UserManager<IdentityUser>> UserManager
    ) CreateSource()
    {
        // UserManager's constructor needs only a user store; the remaining dependencies are unused by this adapter.
        object?[] constructorArguments =
        [
            Mock.Of<IUserStore<IdentityUser>>(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
        ];
        var userManager = _mocks.Create<UserManager<IdentityUser>>((object[])constructorArguments);

        // UserManager's constructor assigns its virtual Logger property; allow that set on the strict mock.
        userManager.SetupSet(x => x.Logger = It.IsAny<ILogger>());

        var serviceProvider = _mocks.Create<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(UserManager<IdentityUser>)))
            .Returns(userManager.Object)
            .Verifiable();

        // The source opens a DI scope per call to resolve the scoped UserManager; mock the plumbing so the
        // strict UserManager mock is never disposed (a non-virtual Dispose would escape the mock).
        var scope = _mocks.Create<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object).Verifiable();
        scope.Setup(x => x.Dispose()).Verifiable();

        var scopeFactory = _mocks.Create<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object).Verifiable();

        return (
            new AspNetIdentityLocalAccountSource<IdentityUser>(scopeFactory.Object),
            userManager
        );
    }

    private OpenIdContext Context() => _mocks.Create<OpenIdContext>().Object;

    private static void SetupMapping(
        Mock<UserManager<IdentityUser>> userManager,
        IdentityUser user,
        bool lockoutSupported = true,
        bool lockedOut = false,
        bool stampSupported = true
    )
    {
        userManager.Setup(x => x.GetUserIdAsync(user)).ReturnsAsync(Subject).Verifiable();
        userManager
            .Setup(x => x.GetClaimsAsync(user))
            .ReturnsAsync(new List<Claim> { RoleClaim })
            .Verifiable();

        userManager.SetupGet(x => x.SupportsUserLockout).Returns(lockoutSupported).Verifiable();
        if (lockoutSupported)
        {
            userManager.Setup(x => x.IsLockedOutAsync(user)).ReturnsAsync(lockedOut).Verifiable();
        }

        userManager.SetupGet(x => x.SupportsUserSecurityStamp).Returns(stampSupported).Verifiable();
        if (stampSupported)
        {
            userManager
                .Setup(x => x.GetSecurityStampAsync(user))
                .ReturnsAsync(SecurityStamp)
                .Verifiable();
        }
    }

    #endregion

    #region ValidateCredentialsAsync Tests

    [Fact]
    public async Task ValidateCredentialsAsync_WhenUserNotFound_ReturnsNull()
    {
        var (source, userManager) = CreateSource();
        userManager
            .Setup(x => x.FindByNameAsync(UserName))
            .ReturnsAsync((IdentityUser?)null)
            .Verifiable();

        var result = await source.ValidateCredentialsAsync(
            Context(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenPasswordInvalid_ReturnsNull()
    {
        var (source, userManager) = CreateSource();
        var user = new IdentityUser(UserName);
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        userManager
            .Setup(x => x.CheckPasswordAsync(user, Password))
            .ReturnsAsync(false)
            .Verifiable();

        var result = await source.ValidateCredentialsAsync(
            Context(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenValid_ReturnsMappedAccount()
    {
        var (source, userManager) = CreateSource();
        var user = new IdentityUser(UserName);
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        userManager
            .Setup(x => x.CheckPasswordAsync(user, Password))
            .ReturnsAsync(true)
            .Verifiable();
        SetupMapping(userManager, user);

        var result = await source.ValidateCredentialsAsync(
            Context(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Equal(Subject, result.Subject);
        Assert.True(result.IsEnabled);
        Assert.Equal(SecurityStamp, result.SecurityStamp);
        var claim = Assert.Single(result.Claims);
        Assert.Equal("role", claim.Type);
        Assert.Equal("admin", claim.Value);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenLockedOut_MapsNotEnabled()
    {
        var (source, userManager) = CreateSource();
        var user = new IdentityUser(UserName);
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        userManager
            .Setup(x => x.CheckPasswordAsync(user, Password))
            .ReturnsAsync(true)
            .Verifiable();
        SetupMapping(userManager, user, lockedOut: true);

        var result = await source.ValidateCredentialsAsync(
            Context(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.False(result.IsEnabled);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_WhenLockoutAndStampUnsupported_MapsEnabledWithoutStamp()
    {
        var (source, userManager) = CreateSource();
        var user = new IdentityUser(UserName);
        userManager.Setup(x => x.FindByNameAsync(UserName)).ReturnsAsync(user).Verifiable();
        userManager
            .Setup(x => x.CheckPasswordAsync(user, Password))
            .ReturnsAsync(true)
            .Verifiable();
        // A minimal store supports neither capability; the source must feature-detect and not call the optional APIs.
        SetupMapping(userManager, user, lockoutSupported: false, stampSupported: false);

        var result = await source.ValidateCredentialsAsync(
            Context(),
            UserName,
            PasswordBytes,
            CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.True(result.IsEnabled);
        Assert.Null(result.SecurityStamp);
    }

    #endregion

    #region FindBySubjectAsync Tests

    [Fact]
    public async Task FindBySubjectAsync_WhenNotFound_ReturnsNull()
    {
        var (source, userManager) = CreateSource();
        userManager
            .Setup(x => x.FindByIdAsync(Subject))
            .ReturnsAsync((IdentityUser?)null)
            .Verifiable();

        var result = await source.FindBySubjectAsync(Context(), Subject, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FindBySubjectAsync_WhenFound_ReturnsMappedAccount()
    {
        var (source, userManager) = CreateSource();
        var user = new IdentityUser(UserName);
        userManager.Setup(x => x.FindByIdAsync(Subject)).ReturnsAsync(user).Verifiable();
        SetupMapping(userManager, user);

        var result = await source.FindBySubjectAsync(Context(), Subject, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(Subject, result.Subject);
        Assert.True(result.IsEnabled);
        Assert.Equal(SecurityStamp, result.SecurityStamp);
        Assert.Single(result.Claims);
    }

    #endregion
}
