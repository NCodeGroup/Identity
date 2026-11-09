// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using NCode.Identity.Cryptography;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Management.Endpoints;

public sealed class ManagementApiEndpointHandlerTests : IDisposable
{
    private const string ConcurrencyToken = "token-1";

    private MockRepository MockRepository { get; }
    private Mock<IAuthorizationService> MockAuthorizationService { get; }
    private Mock<ICryptoService> MockCryptoService { get; }
    private TestHandler Handler { get; }

    public ManagementApiEndpointHandlerTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockAuthorizationService = MockRepository.Create<IAuthorizationService>();
        MockCryptoService = MockRepository.Create<ICryptoService>();

        Handler = new TestHandler(MockAuthorizationService.Object, MockCryptoService.Object);
    }

    public void Dispose() => MockRepository.VerifyAll();

    private sealed record TestValue(string Name) : ISupportConcurrencyToken
    {
        public string ConcurrencyToken { get; } =
            ManagementApiEndpointHandlerTests.ConcurrencyToken;
    }

    private sealed class TestHandler(
        IAuthorizationService authorizationService,
        ICryptoService cryptoService
    ) : ManagementApiEndpointHandler
    {
        protected override IAuthorizationService AuthorizationService => authorizationService;

        protected override ICryptoService CryptoService => cryptoService;

        public ValueTask<IResult?> InvokeGetAuthorizationFailureAsync(
            HttpContext httpContext,
            object authorizationResource,
            IAuthorizationRequirement authorizationRequirement,
            CancellationToken cancellationToken
        ) =>
            GetAuthorizationFailureAsync(
                httpContext,
                authorizationResource,
                authorizationRequirement,
                cancellationToken
            );

        public ValueTask<IResult> InvokeProcessGetAsync<TValue, TResponse>(
            HttpContext httpContext,
            object authorizationResource,
            IAuthorizationRequirement authorizationRequirement,
            Func<CancellationToken, ValueTask<TValue?>> readAsync,
            Func<TValue, TResponse> mapper,
            CancellationToken cancellationToken
        ) =>
            ProcessGetAsync(
                httpContext,
                authorizationResource,
                authorizationRequirement,
                readAsync,
                mapper,
                cancellationToken
            );
    }

    private void SetupAuthorization(AuthorizationResult result) =>
        MockAuthorizationService
            .Setup(x =>
                x.AuthorizeAsync(
                    It.IsAny<ClaimsPrincipal>(),
                    It.IsAny<object?>(),
                    It.IsAny<IEnumerable<IAuthorizationRequirement>>()
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    private static DefaultHttpContext CreateHttpContext(bool authenticated)
    {
        var identity = authenticated
            ? new ClaimsIdentity(authenticationType: "test")
            : new ClaimsIdentity();
        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    #region ProcessGetAsync Tests

    [Fact]
    public async Task ProcessGetAsync_WhenForbidden_ReturnsForbiddenWithoutReading()
    {
        SetupAuthorization(AuthorizationResult.Failed());
        var readCount = 0;

        var result = await Handler.InvokeProcessGetAsync(
            CreateHttpContext(authenticated: true),
            new object(),
            Operations.Read,
            _ =>
            {
                readCount++;
                return ValueTask.FromResult<TestValue?>(new TestValue("a"));
            },
            value => value.Name,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
        Assert.Equal(0, readCount);
    }

    [Fact]
    public async Task ProcessGetAsync_WhenAnonymousAndRefused_ReturnsUnauthorized()
    {
        SetupAuthorization(AuthorizationResult.Failed());
        var readCount = 0;

        var result = await Handler.InvokeProcessGetAsync(
            CreateHttpContext(authenticated: false),
            new object(),
            Operations.Read,
            _ =>
            {
                readCount++;
                return ValueTask.FromResult<TestValue?>(new TestValue("a"));
            },
            value => value.Name,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        Assert.Equal(0, readCount);
    }

    [Fact]
    public async Task ProcessGetAsync_WhenAuthorizedAndMissing_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.InvokeProcessGetAsync(
            CreateHttpContext(authenticated: true),
            new object(),
            Operations.Read,
            _ => ValueTask.FromResult<TestValue?>(null),
            value => value.Name,
            CancellationToken.None
        );

        var notFound = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    [Fact]
    public async Task ProcessGetAsync_WhenAuthorizedAndFound_ReturnsJsonWithTheETag()
    {
        SetupAuthorization(AuthorizationResult.Success());
        var httpContext = CreateHttpContext(authenticated: true);

        var result = await Handler.InvokeProcessGetAsync(
            httpContext,
            new object(),
            Operations.Read,
            _ => ValueTask.FromResult<TestValue?>(new TestValue("a")),
            value => value.Name,
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<string>>(result);

        Assert.Equal("a", json.Value);
        Assert.Equal(ConcurrencyToken, httpContext.Response.Headers.ETag.ToString());
    }

    [Fact]
    public async Task ProcessGetAsync_WhenIfNoneMatchMatches_ReturnsNotModified()
    {
        SetupAuthorization(AuthorizationResult.Success());
        var httpContext = CreateHttpContext(authenticated: true);
        httpContext.Request.Headers.IfNoneMatch = ConcurrencyToken;

        var result = await Handler.InvokeProcessGetAsync(
            httpContext,
            new object(),
            Operations.Read,
            _ => ValueTask.FromResult<TestValue?>(new TestValue("a")),
            value => value.Name,
            CancellationToken.None
        );

        var status = Assert.IsType<StatusCodeHttpResult>(result);

        Assert.Equal(StatusCodes.Status304NotModified, status.StatusCode);
    }

    [Fact]
    public async Task ProcessGetAsync_WhenTheProjectionIsNull_ReturnsNotFound()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.InvokeProcessGetAsync<TestValue, string?>(
            CreateHttpContext(authenticated: true),
            new object(),
            Operations.Read,
            _ => ValueTask.FromResult<TestValue?>(new TestValue("a")),
            _ => null,
            CancellationToken.None
        );

        var notFound = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
    }

    #endregion

    #region GetAuthorizationFailureAsync Tests

    [Fact]
    public async Task GetAuthorizationFailureAsync_WhenAuthorized_ReturnsNull()
    {
        SetupAuthorization(AuthorizationResult.Success());

        var result = await Handler.InvokeGetAuthorizationFailureAsync(
            CreateHttpContext(authenticated: true),
            new object(),
            Operations.Read,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAuthorizationFailureAsync_WhenForbidden_ReturnsForbidden()
    {
        SetupAuthorization(AuthorizationResult.Failed());

        var result = await Handler.InvokeGetAuthorizationFailureAsync(
            CreateHttpContext(authenticated: true),
            new object(),
            Operations.Read,
            CancellationToken.None
        );

        var problem = Assert.IsType<ProblemHttpResult>(result);

        Assert.Equal(StatusCodes.Status403Forbidden, problem.StatusCode);
    }

    #endregion
}
