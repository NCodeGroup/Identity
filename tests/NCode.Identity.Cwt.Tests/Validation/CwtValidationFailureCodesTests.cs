// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose;
using NCode.Identity.Cwt.Validation;
using NCode.Identity.Jwt.Validation;

namespace NCode.Identity.Cwt.Tests.Validation;

public class CwtValidationFailureCodesTests
{
    #region SharedCode Tests

    [Theory]
    [InlineData(CwtValidationFailureCodes.InvalidToken, JwtValidationFailureCodes.InvalidToken)]
    [InlineData(
        CwtValidationFailureCodes.InvalidTokenType,
        JwtValidationFailureCodes.InvalidTokenType
    )]
    [InlineData(CwtValidationFailureCodes.InvalidClaim, JwtValidationFailureCodes.InvalidClaim)]
    [InlineData(CwtValidationFailureCodes.InvalidIssuer, JwtValidationFailureCodes.InvalidIssuer)]
    [InlineData(
        CwtValidationFailureCodes.InvalidAudience,
        JwtValidationFailureCodes.InvalidAudience
    )]
    [InlineData(CwtValidationFailureCodes.Expired, JwtValidationFailureCodes.Expired)]
    [InlineData(CwtValidationFailureCodes.NotYetValid, JwtValidationFailureCodes.NotYetValid)]
    [InlineData(
        CwtValidationFailureCodes.InvalidLifetime,
        JwtValidationFailureCodes.InvalidLifetime
    )]
    [InlineData(
        CoseFailureCodes.AlgorithmNotPermitted,
        JwtValidationFailureCodes.AlgorithmNotPermitted
    )]
    [InlineData(CoseFailureCodes.KeyNotFound, JwtValidationFailureCodes.KeyNotFound)]
    [InlineData(CoseFailureCodes.InvalidSignature, JwtValidationFailureCodes.InvalidSignature)]
    [InlineData(CoseFailureCodes.DecryptionFailed, JwtValidationFailureCodes.DecryptionFailed)]
    public void SharedCode_EqualsTheJwtValidatorCode(string cwtOrCoseCode, string jwtCode)
    {
        Assert.Equal(jwtCode, cwtOrCoseCode);
    }

    #endregion
}
