// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Jose.SdJwt.Digests;
using NCode.Identity.Jose.SdJwt.Processing;
using NCode.Identity.Jose.SdJwt.Verification;

namespace NCode.Identity.Jose.SdJwt.Tests.Processing;

public class SdJwtPayloadProcessorTests
{
    #region Process Tests

    [Theory]
    [InlineData("[]")]
    [InlineData("\"x\"")]
    [InlineData("null")]
    public void Process_WhenPayloadIsNotAnObject_Throws(string payloadJson)
    {
        var processor = new SdJwtPayloadProcessor(
            SdJwtDigestAlgorithms.Sha256,
            maxNestingDepth: 32,
            profile: null
        );

        var exception = Assert.Throws<SdJwtException>(() =>
            processor.Process(JsonElement.Parse(payloadJson), [])
        );

        Assert.Equal(SdJwtVerificationFailureCodes.InvalidSdJwt, exception.FailureCode);
    }

    #endregion
}
