// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Passwords;

public sealed class CommonPasswordBlocklistTests : BaseTests
{
    private CommonPasswordBlocklist Blocklist { get; } = new();

    #region Scaffolding

    private PasswordPolicyContext CreateContext() =>
        new()
        {
            Settings = CreateLooseMock<IReadOnlySettingCollection>().Object,
            UserName = "user",
        };

    #endregion

    #region ContainsAsync Tests

    [Fact]
    public async Task ContainsAsync_WhenThePasswordIsOnTheList_ReturnsTrue()
    {
        var context = CreateContext();
        var password = "passwordpassword".AsMemory();

        var contains = await Blocklist.ContainsAsync(context, password, CancellationToken.None);

        Assert.True(contains);
    }

    [Fact]
    public async Task ContainsAsync_WhenThePasswordDiffersOnlyInCase_ReturnsTrue()
    {
        var context = CreateContext();
        var password = "PassWordPassWord".AsMemory();

        var contains = await Blocklist.ContainsAsync(context, password, CancellationToken.None);

        Assert.True(contains);
    }

    [Fact]
    public async Task ContainsAsync_WhenThePasswordIsNotOnTheList_ReturnsFalse()
    {
        var context = CreateContext();
        var password = "lifecycle-password-changed".AsMemory();

        var contains = await Blocklist.ContainsAsync(context, password, CancellationToken.None);

        Assert.False(contains);
    }

    [Fact]
    public async Task ContainsAsync_WhenAListedPasswordIsOnlyPartOfThePassword_ReturnsFalse()
    {
        var context = CreateContext();
        var password = "passwordpassword-unlisted-suffix".AsMemory();

        var contains = await Blocklist.ContainsAsync(context, password, CancellationToken.None);

        Assert.False(contains);
    }

    #endregion

    #region LoadPasswords Tests

    [Fact]
    public void LoadPasswords_ReadsEveryEntryOfTheEmbeddedList()
    {
        var passwords = CommonPasswordBlocklist.LoadPasswords();

        Assert.Equal(46_483, passwords.Count);
    }

    #endregion
}
