// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NCode.Identity.OpenId.Management.Contracts;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class RetryAfterProblemTests
{
    #region Create Tests

    [Fact]
    public void Create_WhenTheWaitIsFractional_RoundsUpToWholeSecondsInHeaderAndMember()
    {
        var httpContext = new DefaultHttpContext();

        var result = RetryAfterProblem.Create(
            httpContext,
            TimeSpan.FromSeconds(90.2),
            new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Detail = "Wait for the replicas.",
            }
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("Wait for the replicas.", problem.ProblemDetails.Detail);
        Assert.Equal("91", httpContext.Response.Headers.RetryAfter.ToString());
        Assert.Equal(
            91L,
            problem.ProblemDetails.Extensions[ManagementProblemMembers.RetryAfterSeconds]
        );
    }

    [Fact]
    public void Create_WhenTheWaitIsUnderOneSecond_WaitsOneSecond()
    {
        var httpContext = new DefaultHttpContext();

        var result = RetryAfterProblem.Create(
            httpContext,
            TimeSpan.FromMilliseconds(10),
            new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable }
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal("1", httpContext.Response.Headers.RetryAfter.ToString());
        Assert.Equal(
            1L,
            problem.ProblemDetails.Extensions[ManagementProblemMembers.RetryAfterSeconds]
        );
    }

    [Fact]
    public void Create_WhenTheProblemHasAnExtension_KeepsIt()
    {
        var httpContext = new DefaultHttpContext();
        var details = new ProblemDetails { Status = StatusCodes.Status503ServiceUnavailable };
        details.Extensions[ManagementProblemMembers.OwnerId] = "tenant-1";

        var result = RetryAfterProblem.Create(httpContext, TimeSpan.FromSeconds(5), details);

        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(
            "tenant-1",
            problem.ProblemDetails.Extensions[ManagementProblemMembers.OwnerId]
        );
        Assert.Equal(
            5L,
            problem.ProblemDetails.Extensions[ManagementProblemMembers.RetryAfterSeconds]
        );
    }

    #endregion
}
