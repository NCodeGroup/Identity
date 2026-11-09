// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class ManagementProblemsTests
{
    #region ResourceNotFound Tests

    [Fact]
    public void ResourceNotFound_Always_ReturnsANotFoundProblem()
    {
        var result = ManagementProblems.ResourceNotFound();

        Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
        Assert.Equal(StatusCodes.Status404NotFound, result.ProblemDetails.Status);
        Assert.Equal("The resource does not exist.", result.ProblemDetails.Detail);
    }

    #endregion
}
