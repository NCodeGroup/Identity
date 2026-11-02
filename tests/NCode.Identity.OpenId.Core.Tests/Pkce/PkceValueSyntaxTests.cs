// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Pkce;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Pkce;

public class PkceValueSyntaxTests
{
    #region IsWellFormed Tests

    [Theory]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")]
    [InlineData("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM")]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQ0123456789-._~")]
    public void IsWellFormed_WhenUnreservedCharactersWithinBounds_ReturnsTrue(string value)
    {
        Assert.True(PkceValueSyntax.IsWellFormed(value));
    }

    [Fact]
    public void IsWellFormed_WhenAtTheMaximumLength_ReturnsTrue()
    {
        var value = new string('a', 128);

        Assert.True(PkceValueSyntax.IsWellFormed(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjX")]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEj+k")]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEj k")]
    [InlineData("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEj=k")]
    public void IsWellFormed_WhenTooShortOrReservedCharacters_ReturnsFalse(string value)
    {
        Assert.False(PkceValueSyntax.IsWellFormed(value));
    }

    [Fact]
    public void IsWellFormed_WhenLongerThanTheMaximum_ReturnsFalse()
    {
        var value = new string('a', 129);

        Assert.False(PkceValueSyntax.IsWellFormed(value));
    }

    #endregion
}
