// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Setup.Contracts;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Answers the setup page's three API calls: the status of an uninitialized instance, the check of the setup token
/// and the creation of its root administrator. All answer <c>404</c> once the instance is initialized, and in
/// Production when the request is not HTTPS, so the surface does not exist for any. Checking the token and creating
/// the root are limited per client address, need the setup token, and never write the token, the password or any
/// other part of the request to a log.
/// </summary>
internal sealed class SetupApiHandler(
    IInstanceSetupService instanceSetupService,
    ISetupTokenService setupTokenService,
    SetupRateLimiter rateLimiter,
    ISetupSurfaceGate surfaceGate,
    IOptions<SetupOptions> optionsAccessor
)
{
    private IInstanceSetupService InstanceSetupService { get; } = instanceSetupService;

    private ISetupTokenService SetupTokenService { get; } = setupTokenService;

    private SetupRateLimiter RateLimiter { get; } = rateLimiter;

    private ISetupSurfaceGate SurfaceGate { get; } = surfaceGate;

    private IOptions<SetupOptions> OptionsAccessor { get; } = optionsAccessor;

    /// <summary>
    /// Describes the setup an uninitialized instance expects: where its setup token comes from and the shortest root
    /// password it accepts.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The <see cref="SetupStatusResponse"/>, or <c>404</c> when the surface is not served.</returns>
    public async ValueTask<IResult> GetStatusAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (!await SurfaceGate.IsServedAsync(httpContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok(
            new SetupStatusResponse
            {
                TokenSource = SetupTokenService.TokenSource,
                MinimumPasswordLength = SetupDefaults.MinimumRootPasswordLength,
            }
        );
    }

    /// <summary>
    /// Checks the setup token and answers the management console that the deployment names for the wizard's console
    /// step. The handler reads the <see cref="CheckSetupTokenRequest"/> from the request body only after it checks
    /// that the surface is served, so the request is refused with <c>404</c> when the surface is not served whatever
    /// the body, <c>429</c> when the client address has used its attempts of the current minute, <c>400</c> when the
    /// body is not a JSON <see cref="CheckSetupTokenRequest"/>, and <c>403</c> when the setup token is not the
    /// current one. The hint is shown only to a holder of the token, so the deployment's layout is shown only to the
    /// operator.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request, whose body carries the
    /// setup token.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><c>200</c> with the <see cref="CheckSetupTokenResponse"/>, or the problem that refuses the
    /// request.</returns>
    public async ValueTask<IResult> CheckTokenAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (!await SurfaceGate.IsServedAsync(httpContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (await RateLimiter.AcquireAsync(httpContext, cancellationToken) is { } retryAfter)
        {
            return RefuseRateLimited(httpContext, retryAfter);
        }

        var request = await ReadRequestOrDefaultAsync<CheckSetupTokenRequest>(
            httpContext,
            static request => request.Token is not null,
            cancellationToken
        );
        if (request is null)
        {
            return TypedResults.Problem(
                detail: "The setup request is not valid.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        if (!await SetupTokenService.VerifyAsync(request.Token, cancellationToken))
        {
            return TypedResults.Problem(
                detail: "The setup token is not valid.",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        var console = OptionsAccessor.Value.Wizard.ManagementConsole;
        return TypedResults.Ok(
            new CheckSetupTokenResponse
            {
                ManagementConsole = console.IsConfigured
                    ? new SetupManagementConsoleHint
                    {
                        BaseUrl = console.BaseUrl,
                        ClientId = console.ClientId,
                    }
                    : null,
            }
        );
    }

    /// <summary>
    /// Creates the root administrator and marks the instance initialized. The handler reads the
    /// <see cref="CreateRootRequest"/> from the request body only after it checks that the surface is served, so the
    /// request is refused with <c>404</c> when the surface is not served whatever the body, <c>429</c> when the client
    /// address has used its attempts of the current minute, <c>400</c> when the body is not a JSON
    /// <see cref="CreateRootRequest"/>, and <c>403</c> when the setup token is not the current one; a management
    /// console that breaks <see cref="SetupManagementConsoleRules"/> is refused with <c>400</c> listing each broken
    /// rule in the wizard's terms, and a failed precondition of the setup is answered with its own status.
    /// </summary>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request, whose body carries the
    /// setup token and the root administrator.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><c>201</c> with the <see cref="CreateRootResponse"/>, or the problem that refuses the request.</returns>
    public async ValueTask<IResult> CreateRootAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (!await SurfaceGate.IsServedAsync(httpContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (await RateLimiter.AcquireAsync(httpContext, cancellationToken) is { } retryAfter)
        {
            return RefuseRateLimited(httpContext, retryAfter);
        }

        var request = await ReadRequestOrDefaultAsync<CreateRootRequest>(
            httpContext,
            // a body that names a required field as JSON null leaves it null despite the non-nullable type
            static request =>
                request is { Token: not null, UserName: not null, Password: not null },
            cancellationToken
        );
        if (request is null)
        {
            return TypedResults.Problem(
                detail: "The setup request is not valid.",
                statusCode: StatusCodes.Status400BadRequest
            );
        }

        if (!await SetupTokenService.VerifyAsync(request.Token, cancellationToken))
        {
            return TypedResults.Problem(
                detail: "The setup token is not valid.",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        var console = request.ManagementConsole is { } requested ? MapConsole(requested) : null;
        if (console is not null)
        {
            var failures = SetupManagementConsoleRules.Validate(
                console,
                namesConfigurationKeys: false
            );
            if (failures.Count > 0)
            {
                return TypedResults.Problem(
                    detail: string.Join(' ', failures),
                    statusCode: StatusCodes.Status400BadRequest
                );
            }
        }

        var result = await InstanceSetupService.SetUpAsync(
            new InstanceSetupRequest
            {
                Root = new SetupRootOptions
                {
                    UserName = request.UserName,
                    Email = request.Email,
                    Password = request.Password,
                },
                ManagementConsole = console,
            },
            cancellationToken
        );
        if (!result.Succeeded)
        {
            return TypedResults.Problem(
                detail: result.Error.Detail,
                statusCode: result.Error.StatusCode
            );
        }

        return TypedResults.Json(result.Root, statusCode: StatusCodes.Status201Created);
    }

    private static ProblemHttpResult RefuseRateLimited(HttpContext httpContext, TimeSpan retryAfter)
    {
        // Retry-After counts whole seconds, so round up and never tell the caller to retry at once.
        var seconds = Math.Max(1L, (long)Math.Ceiling(retryAfter.TotalSeconds));
        httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        httpContext.Response.Headers.CacheControl = "no-store";

        return TypedResults.Problem(
            detail: GetRateLimitedDetail(seconds),
            statusCode: StatusCodes.Status429TooManyRequests
        );
    }

    /// <summary>
    /// Words the problem detail that tells a rate-limited caller how many whole seconds to wait.
    /// </summary>
    internal static string GetRateLimitedDetail(long seconds) =>
        seconds == 1
            ? "Too many setup attempts. Try again in 1 second."
            : string.Create(
                CultureInfo.InvariantCulture,
                $"Too many setup attempts. Try again in {seconds} seconds."
            );

    private static SetupManagementConsoleOptions MapConsole(CreateRootManagementConsole console) =>
        new()
        {
            ClientId = console.ClientId ?? SetupManagementConsoleOptions.DefaultClientId,
            BaseUrl = console.BaseUrl,
            Jwks = console.Jwks.GetRawText(),
        };

    private static async ValueTask<TRequest?> ReadRequestOrDefaultAsync<TRequest>(
        HttpContext httpContext,
        Func<TRequest, bool> isComplete,
        CancellationToken cancellationToken
    )
        where TRequest : class
    {
        if (!httpContext.Request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            var request = await httpContext.Request.ReadFromJsonAsync<TRequest>(cancellationToken);

            return request is not null && isComplete(request) ? request : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
