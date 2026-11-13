// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using NCode.Identity.OpenId.Setup.Contracts;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Answers the setup page's two API calls: the status of an uninitialized instance and the creation of its root
/// administrator. Both answer <c>404</c> once the instance is initialized, and in Production when the request is not
/// HTTPS, so the surface does not exist for either. Creating the root is limited per client address, needs the setup
/// token, and never writes the token, the password or any other part of the request to a log.
/// </summary>
internal sealed class SetupApiHandler(
    IInstanceSetupService instanceSetupService,
    ISetupTokenService setupTokenService,
    SetupRateLimiter rateLimiter,
    IHostEnvironment hostEnvironment
)
{
    private IInstanceSetupService InstanceSetupService { get; } = instanceSetupService;

    private ISetupTokenService SetupTokenService { get; } = setupTokenService;

    private SetupRateLimiter RateLimiter { get; } = rateLimiter;

    private IHostEnvironment HostEnvironment { get; } = hostEnvironment;

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
        if (!await IsServedAsync(httpContext, cancellationToken))
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
        if (!await IsServedAsync(httpContext, cancellationToken))
        {
            return TypedResults.NotFound();
        }

        if (!await RateLimiter.TryAcquireAsync(httpContext, cancellationToken))
        {
            return TypedResults.Problem(
                detail: "Too many setup attempts. Try again in a minute.",
                statusCode: StatusCodes.Status429TooManyRequests
            );
        }

        var request = await ReadRequestOrDefaultAsync(httpContext, cancellationToken);
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

        return TypedResults.Json(
            new CreateRootResponse
            {
                TenantId = result.Root.TenantId,
                PrincipalId = result.Root.PrincipalId,
                UserName = result.Root.UserName,
                ManagementConsoleClientId = result.ManagementConsoleClientId,
            },
            statusCode: StatusCodes.Status201Created
        );
    }

    private static SetupManagementConsoleOptions MapConsole(CreateRootManagementConsole console) =>
        new()
        {
            ClientId = console.ClientId ?? SetupManagementConsoleOptions.DefaultClientId,
            BaseUrl = console.BaseUrl,
            Jwks = console.Jwks.GetRawText(),
        };

    private static async ValueTask<CreateRootRequest?> ReadRequestOrDefaultAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (!httpContext.Request.HasJsonContentType())
        {
            return null;
        }

        try
        {
            return await httpContext.Request.ReadFromJsonAsync<CreateRootRequest>(
                cancellationToken
            );
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async ValueTask<bool> IsServedAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (HostEnvironment.IsProduction() && !httpContext.Request.IsHttps)
        {
            return false;
        }

        return !await InstanceSetupService.IsInitializedAsync(cancellationToken);
    }
}
