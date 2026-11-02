// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Identity;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

public sealed class AspNetIdentityDummyPasswordHashTests : IDisposable
{
    private const string DummyPasswordHash = "dummy-hash";

    private readonly MockRepository _mocks = new(MockBehavior.Strict);

    public void Dispose() => _mocks.Verify();

    #region GetOrCreate Tests

    [Fact]
    public void GetOrCreate_WhenCalledTwice_HashesOnce()
    {
        var passwordHasher = _mocks.Create<IPasswordHasher<IdentityUser>>();
        passwordHasher
            .Setup(x => x.HashPassword(It.IsAny<IdentityUser>(), It.IsAny<string>()))
            .Returns(DummyPasswordHash)
            .Verifiable(Times.Once());
        var holder = new AspNetIdentityDummyPasswordHash<IdentityUser>();

        var first = holder.GetOrCreate(passwordHasher.Object);
        var second = holder.GetOrCreate(passwordHasher.Object);

        Assert.Equal(DummyPasswordHash, first);
        Assert.Equal(DummyPasswordHash, second);
    }

    [Fact]
    public void GetOrCreate_WhenCreating_HashesARandomPasswordForAUserWithoutAName()
    {
        IdentityUser? hashedUser = null;
        string? hashedPassword = null;
        var passwordHasher = _mocks.Create<IPasswordHasher<IdentityUser>>();
        passwordHasher
            .Setup(x => x.HashPassword(It.IsAny<IdentityUser>(), It.IsAny<string>()))
            .Callback<IdentityUser, string>(
                (user, password) =>
                {
                    hashedUser = user;
                    hashedPassword = password;
                }
            )
            .Returns(DummyPasswordHash)
            .Verifiable();
        var holder = new AspNetIdentityDummyPasswordHash<IdentityUser>();

        _ = holder.GetOrCreate(passwordHasher.Object);

        Assert.NotNull(hashedUser);
        Assert.Null(hashedUser.UserName);
        Assert.NotNull(hashedPassword);
        Assert.Equal(32, hashedPassword.Length);
    }

    #endregion
}
