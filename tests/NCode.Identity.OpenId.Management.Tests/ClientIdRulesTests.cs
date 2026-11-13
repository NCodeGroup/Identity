// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Management;

public class ClientIdRulesTests
{
    #region MaxLength Tests

    [Fact]
    public void MaxLength_Always_Is255() => Assert.Equal(255, ClientIdRules.MaxLength);

    #endregion

    #region IsValid Tests

    [Fact]
    public void IsValid_WhenNull_ReturnsFalse() => Assert.False(ClientIdRules.IsValid(null));

    [Fact]
    public void IsValid_WhenEmpty_ReturnsFalse() =>
        Assert.False(ClientIdRules.IsValid(string.Empty));

    [Fact]
    public void IsValid_WhenMaxLength_ReturnsTrue() =>
        Assert.True(ClientIdRules.IsValid(new string('a', ClientIdRules.MaxLength)));

    [Fact]
    public void IsValid_WhenOneOverMaxLength_ReturnsFalse() =>
        Assert.False(ClientIdRules.IsValid(new string('a', ClientIdRules.MaxLength + 1)));

    [Theory]
    [InlineData("client id")]
    [InlineData("client/id")]
    [InlineData("client:id")]
    [InlineData("client@id")]
    [InlineData("client%20id")]
    public void IsValid_WhenReservedCharacter_ReturnsFalse(string clientId) =>
        Assert.False(ClientIdRules.IsValid(clientId));

    [Theory]
    [InlineData("mgmt-console")]
    [InlineData("Client.Id_1~2")]
    [InlineData("0")]
    public void IsValid_WhenUnreservedCharacters_ReturnsTrue(string clientId) =>
        Assert.True(ClientIdRules.IsValid(clientId));

    #endregion
}
