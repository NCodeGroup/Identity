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

    #region PreconditionFailed Tests

    [Fact]
    public void PreconditionFailed_Always_ReturnsAPreconditionFailedProblemNamingTheResource()
    {
        var result = ManagementProblems.PreconditionFailed("secret");

        Assert.Equal(StatusCodes.Status412PreconditionFailed, result.StatusCode);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, result.ProblemDetails.Status);
        Assert.Equal(
            "The supplied If-Match token does not match the current secret.",
            result.ProblemDetails.Detail
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void PreconditionFailed_WhenTheResourceIsBlank_Throws(string resource)
    {
        Assert.Throws<ArgumentException>(() => ManagementProblems.PreconditionFailed(resource));
    }

    #endregion
}
