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
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Passwords;

public sealed class DefaultPasswordPolicyTests : BaseTests
{
    private const string ComposedPassword = "caf\u00E9-lifecycle-password-changed";
    private const string DecomposedPassword = "cafe\u0301-lifecycle-password-changed";

    #region Scaffolding

    private PasswordPolicyContext CreateContext()
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.PasswordMinLength))
            .Returns(15)
            .Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.PasswordMaxLength))
            .Returns(64)
            .Verifiable();

        return new PasswordPolicyContext { Settings = mockSettings.Object, UserName = "user" };
    }

    private Mock<IPasswordBlocklist> CreateBlocklist(PasswordPolicyContext context, bool contains)
    {
        var mockBlocklist = CreateStrictMock<IPasswordBlocklist>();
        mockBlocklist
            .Setup(x =>
                x.ContainsAsync(
                    context,
                    It.IsAny<ReadOnlyMemory<char>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(contains)
            .Verifiable();

        return mockBlocklist;
    }

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WhenThePasswordIsShorterThanTheMinimum_RejectsItAsTooShort()
    {
        var context = CreateContext();
        var mockBlocklist = CreateStrictMock<IPasswordBlocklist>();
        var policy = new DefaultPasswordPolicy([mockBlocklist.Object]);
        var password = "short-password".AsMemory();

        var result = await policy.ValidateAsync(context, password, CancellationToken.None);

        Assert.True(result.IsRejected);
        Assert.Equal(PasswordPolicyErrors.TooShort, result.Error);
        Assert.Contains("15", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsync_CountsEachCodePointAsOneCharacter()
    {
        var context = CreateContext();
        var policy = new DefaultPasswordPolicy([]);
        var password = string.Concat(Enumerable.Repeat("\U0001F600", 8)).AsMemory();

        var result = await policy.ValidateAsync(context, password, CancellationToken.None);

        Assert.True(result.IsRejected);
        Assert.Equal(PasswordPolicyErrors.TooShort, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_CountsTheCodePointsOfTheComposedForm()
    {
        var context = CreateContext();
        var policy = new DefaultPasswordPolicy([]);
        var password = string.Concat(Enumerable.Repeat("e\u0301", 8)).AsMemory();

        var result = await policy.ValidateAsync(context, password, CancellationToken.None);

        Assert.True(result.IsRejected);
        Assert.Equal(PasswordPolicyErrors.TooShort, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_GivesTheBlocklistsTheComposedForm()
    {
        var context = CreateContext();
        var mockBlocklist = CreateStrictMock<IPasswordBlocklist>();
        mockBlocklist
            .Setup(x =>
                x.ContainsAsync(
                    context,
                    It.Is<ReadOnlyMemory<char>>(password =>
                        password.ToString() == ComposedPassword
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(false)
            .Verifiable();
        var policy = new DefaultPasswordPolicy([mockBlocklist.Object]);
        var password = DecomposedPassword.AsMemory();

        var result = await policy.ValidateAsync(context, password, CancellationToken.None);

        Assert.Same(PasswordPolicyResult.Accepted, result);
    }

    [Fact]
    public async Task ValidateAsync_WhenThePasswordIsLongerThanTheMaximum_RejectsItAsTooLong()
    {
        var context = CreateContext();
        var mockBlocklist = CreateStrictMock<IPasswordBlocklist>();
        var policy = new DefaultPasswordPolicy([mockBlocklist.Object]);
        var password = new string('a', 65).AsMemory();

        var result = await policy.ValidateAsync(context, password, CancellationToken.None);

        Assert.True(result.IsRejected);
        Assert.Equal(PasswordPolicyErrors.TooLong, result.Error);
        Assert.Contains("64", result.ErrorDescription);
    }

    [Fact]
    public async Task ValidateAsync_WhenABlocklistListsThePassword_RejectsItAsBlocklisted()
    {
        var context = CreateContext();
        var mockFirst = CreateBlocklist(context, false);
        var mockSecond = CreateBlocklist(context, true);
        var policy = new DefaultPasswordPolicy([mockFirst.Object, mockSecond.Object]);
        var password = "lifecycle-password-changed".AsMemory();

        var result = await policy.ValidateAsync(context, password, CancellationToken.None);

        Assert.True(result.IsRejected);
        Assert.Equal(PasswordPolicyErrors.Blocklisted, result.Error);
    }

    [Fact]
    public async Task ValidateAsync_WhenNoBlocklistListsThePassword_AcceptsIt()
    {
        var context = CreateContext();
        var mockFirst = CreateBlocklist(context, false);
        var mockSecond = CreateBlocklist(context, false);
        var policy = new DefaultPasswordPolicy([mockFirst.Object, mockSecond.Object]);
        var password = "lifecycle-password-changed".AsMemory();

        var result = await policy.ValidateAsync(context, password, CancellationToken.None);

        Assert.Same(PasswordPolicyResult.Accepted, result);
    }

    #endregion
}
