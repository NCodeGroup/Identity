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

using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Passwords;

public sealed class AccountNamePasswordBlocklistTests : BaseTests
{
    private AccountNamePasswordBlocklist Blocklist { get; } = new();

    #region Scaffolding

    private PasswordPolicyContext CreateContext(string? email) =>
        new()
        {
            Settings = CreateLooseMock<IReadOnlySettingCollection>().Object,
            UserName = "alice.example",
            Email = email,
        };

    #endregion

    #region ContainsAsync Tests

    [Fact]
    public async Task ContainsAsync_WhenThePasswordIsTheUserName_ReturnsTrue()
    {
        var context = CreateContext("alice@example.com");
        var password = "ALICE.EXAMPLE".AsMemory();

        var contains = await Blocklist.ContainsAsync(context, password, CancellationToken.None);

        Assert.True(contains);
    }

    [Fact]
    public async Task ContainsAsync_WhenThePasswordIsTheEmail_ReturnsTrue()
    {
        var context = CreateContext("alice@example.com");
        var password = "Alice@Example.com".AsMemory();

        var contains = await Blocklist.ContainsAsync(context, password, CancellationToken.None);

        Assert.True(contains);
    }

    [Fact]
    public async Task ContainsAsync_WhenThePasswordIsTheEmailLocalPart_ReturnsTrue()
    {
        var context = CreateContext("alice@example.com");
        var password = "alice".AsMemory();

        var contains = await Blocklist.ContainsAsync(context, password, CancellationToken.None);

        Assert.True(contains);
    }

    [Fact]
    public async Task ContainsAsync_WhenThePasswordOnlyContainsTheUserName_ReturnsFalse()
    {
        var context = CreateContext("alice@example.com");
        var password = "alice.example-and-more".AsMemory();

        var contains = await Blocklist.ContainsAsync(context, password, CancellationToken.None);

        Assert.False(contains);
    }

    [Fact]
    public async Task ContainsAsync_WhenTheAccountHasNoEmail_ReturnsFalseForAnotherPassword()
    {
        var context = CreateContext(null);
        var password = "alice".AsMemory();

        var contains = await Blocklist.ContainsAsync(context, password, CancellationToken.None);

        Assert.False(contains);
    }

    #endregion
}
