// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class ErrorStatusCodesTests
{
    #region Contains Tests

    [Theory]
    [InlineData(StatusCodes.Status400BadRequest - 1, false)]
    [InlineData(StatusCodes.Status400BadRequest, true)]
    [InlineData(ErrorStatusCodes.LastServerError, true)]
    [InlineData(ErrorStatusCodes.LastServerError + 1, false)]
    public void Contains_ForAStatusCode_ReturnsWhetherItReportsAnError(
        int statusCode,
        bool expected
    )
    {
        Assert.Equal(expected, ErrorStatusCodes.Contains(statusCode));
    }

    #endregion
}
