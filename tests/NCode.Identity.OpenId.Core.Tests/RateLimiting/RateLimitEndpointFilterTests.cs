// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Moq;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.RateLimiting;

public sealed class RateLimitEndpointFilterTests : BaseTests
{
    private const string TenantId = "tenant-1";
    private const string TeamPath = "/team-1/";

    private Mock<IRequestRateLimiter> MockRateLimiter { get; }

    private Mock<ITenantUsageRecorder> MockUsageRecorder { get; }

    private Mock<OpenIdContext> MockContext { get; }

    private DefaultHttpContext HttpContext { get; } = new();

    private RateLimitEndpointFilter Filter { get; }

    public RateLimitEndpointFilterTests()
    {
        MockRateLimiter = CreateStrictMock<IRequestRateLimiter>();
        MockUsageRecorder = CreateStrictMock<ITenantUsageRecorder>();
        MockContext = CreateStrictMock<OpenIdContext>();
        Filter = new RateLimitEndpointFilter(MockRateLimiter.Object, MockUsageRecorder.Object);
    }

    #region Scaffolding

    private void SetupFeature()
    {
        var mockFeature = CreateStrictMock<IOpenIdContextFeature>();
        mockFeature.Setup(x => x.OpenIdContext).Returns(MockContext.Object).Verifiable();
        HttpContext.Features.Set(mockFeature.Object);
    }

    private void SetupTenant()
    {
        var mockTeam = CreateStrictMock<OpenIdTeam>();
        mockTeam.Setup(x => x.Path).Returns(TeamPath).Verifiable();
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        mockTenant.Setup(x => x.Team).Returns(mockTeam.Object).Verifiable();
        MockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
    }

    // The request stage adds the usage meters of the descriptors it judges to the request's rate-limit state.
    private void SetupRequestStage(RateLimitDecision decision, params string[] usageMeters) =>
        MockRateLimiter
            .Setup(x =>
                x.AcquireAsync(
                    RateLimitStages.Request,
                    It.Is<RateLimitContext>(context =>
                        context.OpenIdContext == MockContext.Object && context.Client == null
                    ),
                    HttpContext.RequestAborted
                )
            )
            .Callback(() =>
                RateLimitRequestState.Get(HttpContext).UsageMeters.AddRange(usageMeters)
            )
            .ReturnsAsync(decision)
            .Verifiable();

    private ValueTask<object?> InvokeAsync(
        EndpointFilterDelegate next,
        CancellationToken cancellationToken
    )
    {
        HttpContext.RequestAborted = cancellationToken;
        return Filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(HttpContext), next);
    }

    #endregion

    #region InvokeAsync Tests

    [Fact]
    public async Task InvokeAsync_WithoutAnOpenIdContext_RunsTheEndpointWithoutJudging()
    {
        var expected = new object();
        var endpointRan = false;

        var result = await InvokeAsync(
            _ =>
            {
                endpointRan = true;
                return ValueTask.FromResult<object?>(expected);
            },
            CancellationToken.None
        );

        Assert.True(endpointRan);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_WhenTheRequestStageRefuses_AnswersWithoutRunningTheEndpoint()
    {
        SetupFeature();
        SetupRequestStage(new RateLimitDecision(TimeSpan.FromSeconds(1.5)));
        var mockError = CreateLooseMock<IOpenIdError>();
        mockError.SetupAllProperties();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable))
            .Returns(mockError.Object)
            .Verifiable();
        MockContext.Setup(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
        var endpointRan = false;

        var result = await InvokeAsync(
            _ =>
            {
                endpointRan = true;
                return ValueTask.FromResult<object?>(null);
            },
            CancellationToken.None
        );

        Assert.False(endpointRan);
        Assert.IsAssignableFrom<IResult>(result);
        Assert.Equal(StatusCodes.Status429TooManyRequests, mockError.Object.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenEveryStageAdmits_RecordsTheUsageMetersAfterTheEndpoint()
    {
        SetupFeature();
        SetupTenant();
        SetupRequestStage(RateLimitDecision.Admitted, TenantUsageMeters.Requests);
        MockUsageRecorder
            .Setup(x => x.Record(TeamPath, TenantId, TenantUsageMeters.Requests))
            .Verifiable();
        MockUsageRecorder
            .Setup(x => x.Record(TeamPath, TenantId, TenantUsageMeters.TokenRequests))
            .Verifiable();
        var expected = new object();

        var result = await InvokeAsync(
            _ =>
            {
                // A stage inside the endpoint adds its meters to the same request state.
                RateLimitRequestState
                    .Get(HttpContext)
                    .UsageMeters.Add(TenantUsageMeters.TokenRequests);
                return ValueTask.FromResult<object?>(expected);
            },
            CancellationToken.None
        );

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task InvokeAsync_WhenALaterStageRefuses_RecordsNoUsage()
    {
        SetupFeature();
        SetupRequestStage(RateLimitDecision.Admitted, TenantUsageMeters.Requests);
        var expected = new object();

        var result = await InvokeAsync(
            _ =>
            {
                // A stage inside the endpoint refused the request, so it is not the tenant's usage.
                RateLimitRequestState.Get(HttpContext).IsRefused = true;
                return ValueTask.FromResult<object?>(expected);
            },
            CancellationToken.None
        );

        Assert.Same(expected, result);
    }

    #endregion
}
