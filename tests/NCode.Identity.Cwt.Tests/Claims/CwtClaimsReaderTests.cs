// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose;
using NCode.Identity.Cwt.Claims;
using NCode.Identity.Cwt.Validation;

namespace NCode.Identity.Cwt.Tests.Claims;

public class CwtClaimsReaderTests
{
    private static CwtClaimsSet Read(CwtTestHost host, string hex) =>
        CwtClaimsReader.Read(
            host.Serializer.DecodeMap(Convert.FromHexString(hex)),
            host.Serializer
        );

    #region Read Tests

    [Fact]
    public async Task Read_RegisteredClaimsExample_FillsEveryTypedMember()
    {
        await using var host = new CwtTestHost();

        var claims = Read(host, CwtTestHost.ClaimsA1);

        Assert.Equal("coap://as.example.com", claims.Issuer);
        Assert.Equal("erikw", claims.Subject);
        Assert.Equal(["coap://light.example.com"], claims.Audiences);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1444064944), claims.ExpiresAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1443944944), claims.NotBefore);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1443944944), claims.IssuedAt);
        Assert.Equal(Convert.FromHexString("0B71"), claims.CwtId?.ToArray());
        Assert.Null(claims.ConfirmationKey);
        Assert.Equal(7, claims.Claims.Count);
    }

    [Fact]
    public async Task Read_FractionalIssuedAt_KeepsTheFraction()
    {
        await using var host = new CwtTestHost();

        var claims = Read(host, "A106FB41D584367C200000");

        Assert.Equal(DateTimeOffset.UnixEpoch.AddTicks(14439449445000000L), claims.IssuedAt);
    }

    [Fact]
    public async Task Read_AudienceArray_ReadsEachAudience()
    {
        await using var host = new CwtTestHost();

        var claims = Read(host, "A10382616161" + "62");

        Assert.Equal(["a", "b"], claims.Audiences);
    }

    [Fact]
    public async Task Read_ConfirmationClaim_KeepsTheEncodedCoseKey()
    {
        await using var host = new CwtTestHost();

        var claims = Read(host, "A108A101A10102");

        Assert.Equal(Convert.FromHexString("A10102"), claims.ConfirmationKey?.ToArray());
    }

    [Fact]
    public async Task Read_ConfirmationWithoutACoseKey_HasNoConfirmationKey()
    {
        await using var host = new CwtTestHost();

        var claims = Read(host, "A108A10201");

        Assert.Null(claims.ConfirmationKey);
    }

    [Theory]
    [InlineData("A10101", "iss as an integer")]
    [InlineData("A10380", "aud as an empty array")]
    [InlineData("A10382016161", "aud with a non-text element")]
    [InlineData("A104C11A5612AEB0", "exp tagged")]
    [InlineData("A104F97E00", "exp as NaN")]
    [InlineData("A10761" + "61", "cti as text")]
    [InlineData("A10801", "cnf as an integer")]
    [InlineData("A108A101" + "01", "cnf key that is not a map")]
    [InlineData("A1046161", "exp as text")]
    [InlineData("A1041B7FFFFFFFFFFFFFFF", "exp beyond the DateTimeOffset range")]
    [InlineData("A108A1410101", "cnf with a byte-string label")]
    [InlineData("A10201", "sub as an integer")]
    public async Task Read_ClaimOfTheWrongForm_ThrowsInvalidClaim(string hex, string description)
    {
        await using var host = new CwtTestHost();

        var exception = Assert.Throws<CoseException>(() => Read(host, hex));

        Assert.True(exception.FailureCode == CwtValidationFailureCodes.InvalidClaim, description);
    }

    #endregion
}
