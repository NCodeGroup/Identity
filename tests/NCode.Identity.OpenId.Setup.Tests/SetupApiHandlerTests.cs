// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Setup.Contracts;

namespace NCode.Identity.OpenId.Setup;

public sealed class SetupApiHandlerTests : IDisposable
{
    private const string Token = "a-setup-token-that-must-never-be-logged";
    private const string UserName = "root";
    private const string Password = "a-long-root-password";
    private const string ConsoleBaseUrl = "https://console.example.com";

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IInstanceSetupService> MockInstanceSetupService { get; }
    private Mock<ISetupTokenService> MockSetupTokenService { get; }
    private Mock<ISetupSurfaceGate> MockSurfaceGate { get; }
    private SetupRateLimiter RateLimiter { get; } = new();

    public SetupApiHandlerTests()
    {
        MockInstanceSetupService = MockRepository.Create<IInstanceSetupService>();
        MockSetupTokenService = MockRepository.Create<ISetupTokenService>();
        MockSurfaceGate = MockRepository.Create<ISetupSurfaceGate>();
    }

    public void Dispose()
    {
        RateLimiter.Dispose();
        MockRepository.Verify();
    }

    #region Helpers

    private SetupApiHandler CreateHandler() =>
        new(
            MockInstanceSetupService.Object,
            MockSetupTokenService.Object,
            RateLimiter,
            MockSurfaceGate.Object
        );

    private void SetupIsServed(bool isServed, Times? times = null) =>
        MockSurfaceGate
            .Setup(x => x.IsServedAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(isServed)
            .Verifiable(times ?? Times.Once());

    private void SetupVerify(bool isValid, Times? times = null) =>
        MockSetupTokenService
            .Setup(x => x.VerifyAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(isValid)
            .Verifiable(times ?? Times.Once());

    private static DefaultHttpContext CreateHttpContext(
        string remoteAddress = "10.0.0.1",
        ILoggerProvider? loggerProvider = null,
        string? body = null,
        string? contentType = "application/json"
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging(builder =>
        {
            if (loggerProvider is not null)
            {
                builder.AddProvider(loggerProvider);
            }
        });

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceCollection.BuildServiceProvider(),
        };
        httpContext.Request.Scheme = Uri.UriSchemeHttps;
        httpContext.Request.ContentType = contentType;
        httpContext.Request.Body = new MemoryStream(
            SecureEncoding.UTF8.GetBytes(body ?? CreateRequestJson())
        );
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        return httpContext;
    }

    private static string CreateRequestJson() =>
        JsonSerializer.Serialize(
            new CreateRootRequest
            {
                Token = Token,
                UserName = UserName,
                Email = "root@example.com",
                Password = Password,
            },
            JsonSerializerOptions.Web
        );

    private static string CreateConsoleRequestJson(
        string? clientId = "console-1",
        string baseUrl = ConsoleBaseUrl,
        string jwks = """{ "keys": [ { "kty": "EC", "crv": "P-256", "x": "x", "y": "y" } ] }"""
    ) =>
        JsonSerializer.Serialize(
            new CreateRootRequest
            {
                Token = Token,
                UserName = UserName,
                Password = Password,
                ManagementConsole = new CreateRootManagementConsole
                {
                    ClientId = clientId,
                    BaseUrl = baseUrl,
                    Jwks = JsonElement.Parse(jwks),
                },
            },
            JsonSerializerOptions.Web
        );

    private static InstanceSetupResult CreateSuccess() =>
        new()
        {
            Root = new CreateRootResponse
            {
                TenantId = "control-plane",
                PrincipalId = "principal-1",
                UserName = UserName,
            },
        };

    private static ProblemHttpResult AssertProblem(IResult result, int statusCode)
    {
        var problem = Assert.IsType<ProblemHttpResult>(result);
        Assert.Equal(statusCode, problem.StatusCode);
        return problem;
    }

    #endregion

    #region GetStatusAsync Tests

    [Fact]
    public async Task GetStatusAsync_WhenTheSurfaceIsServed_AnswersTheTokenSourceAndMinimumPasswordLength()
    {
        SetupIsServed(true);
        MockSetupTokenService
            .Setup(x => x.TokenSource)
            .Returns(SetupTokenSources.Generated)
            .Verifiable();
        var handler = CreateHandler();

        var result = await handler.GetStatusAsync(CreateHttpContext(), CancellationToken.None);

        var ok = Assert.IsType<Ok<SetupStatusResponse>>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(SetupTokenSources.Generated, ok.Value.TokenSource);
        Assert.Equal(SetupDefaults.MinimumRootPasswordLength, ok.Value.MinimumPasswordLength);
    }

    [Fact]
    public async Task GetStatusAsync_WhenTheSurfaceIsNotServed_Answers404()
    {
        SetupIsServed(false);
        var handler = CreateHandler();

        var result = await handler.GetStatusAsync(CreateHttpContext(), CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    #endregion

    #region CreateRootAsync Tests

    [Fact]
    public async Task CreateRootAsync_WhenTheSurfaceIsNotServed_Answers404WithoutVerifyingTheToken()
    {
        SetupIsServed(false);
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(CreateHttpContext(), CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task CreateRootAsync_WhenTheSurfaceIsNotServedAndTheBodyIsMalformed_Answers404()
    {
        SetupIsServed(false);
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(
            CreateHttpContext(body: "{ not json"),
            CancellationToken.None
        );

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task CreateRootAsync_WhenTheContentTypeIsNotJson_Answers400WithoutVerifyingTheToken()
    {
        SetupIsServed(true);
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(
            CreateHttpContext(contentType: "text/plain"),
            CancellationToken.None
        );

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Equal("The setup request is not valid.", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task CreateRootAsync_WhenTheBodyIsMalformedJson_Answers400WithoutVerifyingTheToken()
    {
        SetupIsServed(true);
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(
            CreateHttpContext(body: "{ not json"),
            CancellationToken.None
        );

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Equal("The setup request is not valid.", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task CreateRootAsync_WhenTheBodyIsJsonNull_Answers400WithoutVerifyingTheToken()
    {
        SetupIsServed(true);
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(
            CreateHttpContext(body: "null"),
            CancellationToken.None
        );

        AssertProblem(result, StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("userName")]
    [InlineData("password")]
    public async Task CreateRootAsync_WhenTheBodyOmitsARequiredField_Answers400WithoutVerifyingTheToken(
        string field
    )
    {
        SetupIsServed(true);
        var handler = CreateHandler();
        var body = new Dictionary<string, string?>
        {
            ["token"] = Token,
            ["userName"] = UserName,
            ["password"] = Password,
        };
        body.Remove(field);

        var result = await handler.CreateRootAsync(
            CreateHttpContext(body: JsonSerializer.Serialize(body)),
            CancellationToken.None
        );

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Equal("The setup request is not valid.", problem.ProblemDetails.Detail);
    }

    [Theory]
    [InlineData("token")]
    [InlineData("userName")]
    [InlineData("password")]
    public async Task CreateRootAsync_WhenTheBodyNamesARequiredFieldAsNull_Answers400WithoutVerifyingTheToken(
        string field
    )
    {
        SetupIsServed(true);
        var handler = CreateHandler();
        var body = new Dictionary<string, string?>
        {
            ["token"] = Token,
            ["userName"] = UserName,
            ["password"] = Password,
        };
        body[field] = null;

        var result = await handler.CreateRootAsync(
            CreateHttpContext(body: JsonSerializer.Serialize(body)),
            CancellationToken.None
        );

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Equal("The setup request is not valid.", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task CreateRootAsync_WhenTheTokenIsNotValid_Answers403WithoutCreatingTheRoot()
    {
        SetupIsServed(true);
        SetupVerify(false);
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(CreateHttpContext(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task CreateRootAsync_WhenTheClientHasUsedItsAttempts_Answers429WithoutVerifyingTheToken()
    {
        SetupIsServed(true, Times.Exactly(SetupDefaults.RootAttemptsPerMinute + 1));
        SetupVerify(false, Times.Exactly(SetupDefaults.RootAttemptsPerMinute));
        var handler = CreateHandler();
        for (var attempt = 0; attempt < SetupDefaults.RootAttemptsPerMinute; ++attempt)
        {
            await handler.CreateRootAsync(CreateHttpContext(), CancellationToken.None);
        }

        var result = await handler.CreateRootAsync(CreateHttpContext(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task CreateRootAsync_WhenAnotherClientHasUsedItsAttempts_StillVerifiesTheToken()
    {
        SetupIsServed(true, Times.Exactly(SetupDefaults.RootAttemptsPerMinute + 1));
        SetupVerify(false, Times.Exactly(SetupDefaults.RootAttemptsPerMinute + 1));
        var handler = CreateHandler();
        for (var attempt = 0; attempt < SetupDefaults.RootAttemptsPerMinute; ++attempt)
        {
            await handler.CreateRootAsync(
                CreateHttpContext(remoteAddress: "10.0.0.1"),
                CancellationToken.None
            );
        }

        var result = await handler.CreateRootAsync(
            CreateHttpContext(remoteAddress: "10.0.0.2"),
            CancellationToken.None
        );

        AssertProblem(result, StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task CreateRootAsync_WhenTheSetupFails_AnswersItsErrorAsAProblem()
    {
        SetupIsServed(true);
        SetupVerify(true);
        MockInstanceSetupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new InstanceSetupResult
                {
                    Error = new ManagementError
                    {
                        StatusCode = StatusCodes.Status400BadRequest,
                        Detail = "The password is too weak.",
                    },
                }
            )
            .Verifiable();
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(CreateHttpContext(), CancellationToken.None);

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Equal("The password is too weak.", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task CreateRootAsync_WhenAConcurrentSetupWon_Answers404()
    {
        SetupIsServed(true);
        SetupVerify(true);
        MockInstanceSetupService
            .Setup(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(
                new InstanceSetupResult
                {
                    Error = new ManagementError
                    {
                        StatusCode = StatusCodes.Status404NotFound,
                        Detail = "The instance is already set up.",
                    },
                }
            )
            .Verifiable();
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(CreateHttpContext(), CancellationToken.None);

        AssertProblem(result, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task CreateRootAsync_WhenTheSetupSucceeds_Answers201WithTheRootAndPassesTheRequestedAdministrator()
    {
        var success = CreateSuccess();
        SetupIsServed(true);
        SetupVerify(true);
        MockInstanceSetupService
            .Setup(x =>
                x.SetUpAsync(
                    It.Is<InstanceSetupRequest>(request =>
                        request.Root.UserName == UserName
                        && request.Root.Email == "root@example.com"
                        && request.Root.Password == Password
                        && request.ManagementConsole == null
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(success)
            .Verifiable();
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(CreateHttpContext(), CancellationToken.None);

        var json = Assert.IsType<JsonHttpResult<CreateRootResponse>>(result);
        Assert.Equal(StatusCodes.Status201Created, json.StatusCode);
        Assert.NotNull(json.Value);
        Assert.NotNull(success.Root);
        Assert.Equal(success.Root.TenantId, json.Value.TenantId);
        Assert.Equal(success.Root.PrincipalId, json.Value.PrincipalId);
        Assert.Equal(success.Root.UserName, json.Value.UserName);
        Assert.Null(json.Value.ManagementConsoleClientId);
    }

    [Fact]
    public async Task CreateRootAsync_WithManagementConsole_PassesConsoleAndReturnsClientId()
    {
        var success = CreateSuccess();
        SetupIsServed(true);
        SetupVerify(true);
        MockInstanceSetupService
            .Setup(x =>
                x.SetUpAsync(
                    It.Is<InstanceSetupRequest>(request =>
                        request.Root.UserName == UserName
                        && request.ManagementConsole != null
                        && request.ManagementConsole.ClientId == "console-1"
                        && request.ManagementConsole.BaseUrl == ConsoleBaseUrl
                        && request.ManagementConsole.Jwks != null
                        && JsonElement
                            .Parse(request.ManagementConsole.Jwks)
                            .GetProperty("keys")
                            .GetArrayLength() == 1
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new InstanceSetupResult
                {
                    Root = success.Root,
                    ManagementConsoleClientId = "console-1",
                }
            )
            .Verifiable();
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(
            CreateHttpContext(body: CreateConsoleRequestJson(clientId: "console-1")),
            CancellationToken.None
        );

        var json = Assert.IsType<JsonHttpResult<CreateRootResponse>>(result);
        Assert.Equal(StatusCodes.Status201Created, json.StatusCode);
        Assert.NotNull(json.Value);
        Assert.NotNull(success.Root);
        Assert.Equal("console-1", json.Value.ManagementConsoleClientId);
        Assert.Equal(success.Root.PrincipalId, json.Value.PrincipalId);
    }

    [Fact]
    public async Task CreateRootAsync_WithManagementConsoleWithoutClientId_PassesTheDefaultClientId()
    {
        SetupIsServed(true);
        SetupVerify(true);
        MockInstanceSetupService
            .Setup(x =>
                x.SetUpAsync(
                    It.Is<InstanceSetupRequest>(request =>
                        request.ManagementConsole != null
                        && request.ManagementConsole.ClientId
                            == SetupManagementConsoleOptions.DefaultClientId
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(CreateSuccess())
            .Verifiable();
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(
            CreateHttpContext(body: CreateConsoleRequestJson(clientId: null)),
            CancellationToken.None
        );

        Assert.IsType<JsonHttpResult<CreateRootResponse>>(result);
    }

    [Fact]
    public async Task CreateRootAsync_ManagementConsoleInvalid_Returns400()
    {
        SetupIsServed(true);
        SetupVerify(true);
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(
            CreateHttpContext(
                body: CreateConsoleRequestJson(
                    baseUrl: "http://console.example.com",
                    jwks: """{ "keys": [] }"""
                )
            ),
            CancellationToken.None
        );

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Contains("console URL", problem.ProblemDetails.Detail, StringComparison.Ordinal);
        Assert.Contains("JWK Set", problem.ProblemDetails.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Setup:", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(
        "https://console.example.com/app",
        null,
        "The console URL must be an absolute https origin"
    )]
    [InlineData(
        ConsoleBaseUrl,
        """{ "other": [] }""",
        "The public JWK Set must be a JSON object with a non-empty keys array."
    )]
    public async Task CreateRootAsync_ManagementConsoleBreaksARule_AnswersInTheWizardsTerms(
        string baseUrl,
        string? jwks,
        string expected
    )
    {
        SetupIsServed(true);
        SetupVerify(true);
        var handler = CreateHandler();
        var body = jwks is null
            ? CreateConsoleRequestJson(baseUrl: baseUrl)
            : CreateConsoleRequestJson(baseUrl: baseUrl, jwks: jwks);

        var result = await handler.CreateRootAsync(
            CreateHttpContext(body: body),
            CancellationToken.None
        );

        var problem = AssertProblem(result, StatusCodes.Status400BadRequest);
        Assert.Contains(expected, problem.ProblemDetails.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Setup:", problem.ProblemDetails.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateRootAsync_ManagementConsoleRefused_ReturnsServiceStatus()
    {
        SetupIsServed(true);
        SetupVerify(true);
        MockInstanceSetupService
            .Setup(x =>
                x.SetUpAsync(
                    It.Is<InstanceSetupRequest>(request => request.ManagementConsole != null),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new InstanceSetupResult
                {
                    Error = new ManagementError
                    {
                        StatusCode = StatusCodes.Status409Conflict,
                        Detail = "The management console's client is refused.",
                    },
                }
            )
            .Verifiable();
        var handler = CreateHandler();

        var result = await handler.CreateRootAsync(
            CreateHttpContext(body: CreateConsoleRequestJson()),
            CancellationToken.None
        );

        var problem = AssertProblem(result, StatusCodes.Status409Conflict);
        Assert.Equal("The management console's client is refused.", problem.ProblemDetails.Detail);
    }

    [Fact]
    public async Task CreateRootAsync_Always_NeverPassesTheTokenToALogger()
    {
        using var loggerProvider = new RecordingLoggerProvider();
        SetupIsServed(true, Times.Exactly(3));
        MockSetupTokenService
            .SetupSequence(x => x.VerifyAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true)
            .ReturnsAsync(true);
        MockInstanceSetupService
            .SetupSequence(x =>
                x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(CreateSuccess())
            .ReturnsAsync(
                new InstanceSetupResult
                {
                    Error = new ManagementError
                    {
                        StatusCode = StatusCodes.Status400BadRequest,
                        Detail = "The password is too weak.",
                    },
                }
            );
        var handler = CreateHandler();

        for (var attempt = 0; attempt < 3; ++attempt)
        {
            await handler.CreateRootAsync(
                CreateHttpContext(loggerProvider: loggerProvider),
                CancellationToken.None
            );
        }

        MockSetupTokenService.Verify(
            x => x.VerifyAsync(Token, It.IsAny<CancellationToken>()),
            Times.Exactly(3)
        );
        MockInstanceSetupService.Verify(
            x => x.SetUpAsync(It.IsAny<InstanceSetupRequest>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2)
        );

        Assert.DoesNotContain(
            loggerProvider.Messages,
            message => message.Contains(Token, StringComparison.Ordinal)
        );
        Assert.DoesNotContain(
            loggerProvider.Messages,
            message => message.Contains(Password, StringComparison.Ordinal)
        );
    }

    #endregion

    private sealed class RecordingLoggerProvider : ILoggerProvider, ILogger
    {
        public List<string> Messages { get; } = [];

        public ILogger CreateLogger(string categoryName) => this;

        public void Dispose() { }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Messages.Add(formatter(state, exception));
    }
}
