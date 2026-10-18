#region Copyright Preamble

// Copyright @ 2024 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Net;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Settings;
using NCode.Json;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="LoadAuthorizationRequestCommand"/> message.
/// </summary>
internal class DefaultLoadAuthorizationRequestHandler(
    ILogger<DefaultLoadAuthorizationRequestHandler> logger,
    IJoseSerializer joseSerializer,
    IJwtValidator jwtValidator,
    IHttpClientFactory httpClientFactory,
    IOutboundHttpContentReader outboundHttpContentReader,
    IClientKeyResolver clientKeyResolver
) : ICommandResponseHandler<LoadAuthorizationRequestCommand, IAuthorizationRequest>
{
    private ILogger<DefaultLoadAuthorizationRequestHandler> Logger { get; } = logger;
    private IJoseSerializer JoseSerializer { get; } = joseSerializer;
    private IJwtValidator JwtValidator { get; } = jwtValidator;
    private IHttpClientFactory HttpClientFactory { get; } = httpClientFactory;
    private IOutboundHttpContentReader OutboundHttpContentReader { get; } =
        outboundHttpContentReader;
    private IClientKeyResolver ClientKeyResolver { get; } = clientKeyResolver;

    /// <summary>
    /// The name of the SSRF-guarded HTTP client used to fetch a JAR <c>request_uri</c> payload (RFC 9101).
    /// </summary>
    internal const string HttpClientName = "NCode.Identity.OpenId.RequestUri";

    /// <inheritdoc />
    public async ValueTask<IAuthorizationRequest> HandleAsync(
        LoadAuthorizationRequestCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, openIdClient, openIdRequestValues) = command;

        var openIdEnvironment = openIdContext.Environment;

        // the following will parse string-values into strongly-typed parameters and may throw
        var requestMessage = AuthorizationRequestMessage.Load(
            openIdEnvironment,
            openIdRequestValues
        );
        requestMessage.AuthorizationSourceType = openIdRequestValues.SourceType;

        var requestObject = await LoadRequestObjectAsync(
            openIdEnvironment,
            openIdClient,
            requestMessage,
            cancellationToken
        );

        const bool isContinuation = false;
        var authorizationRequest = new AuthorizationRequest(
            isContinuation,
            requestMessage,
            requestObject
        );

        return authorizationRequest;
    }

    private async ValueTask<IAuthorizationRequestObject?> LoadRequestObjectAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdClient openIdClient,
        AuthorizationRequestMessage requestMessage,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdEnvironment.ErrorFactory;

        var requestJwt = requestMessage.RequestJwt;
        var requestUri = requestMessage.RequestUri;

        RequestObjectSource requestObjectSource;
        string errorCode;

        if (requestUri is not null)
        {
            if (!openIdClient.Settings.GetValue(OpenIdSettingKeys.RequestUriParameterSupported))
                throw errorFactory.RequestUriNotSupported().AsException();

            requestObjectSource = RequestObjectSource.Remote;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestUri;

            if (!string.IsNullOrEmpty(requestJwt))
                throw errorFactory
                    .InvalidRequest(
                        "Both the 'request' and 'request_uri' parameters cannot be present at the same time.",
                        errorCode
                    )
                    .AsException();

            requestJwt = await FetchRequestUriAsync(
                openIdEnvironment,
                openIdClient,
                requestUri,
                cancellationToken
            );
        }
        else if (!string.IsNullOrEmpty(requestJwt))
        {
            if (!openIdClient.Settings.GetValue(OpenIdSettingKeys.RequestParameterSupported))
                throw errorFactory.RequestParameterNotSupported().AsException();

            requestObjectSource = RequestObjectSource.Inline;
            errorCode = OpenIdConstants.ErrorCodes.InvalidRequestJwt;
        }
        else
        {
            return null;
        }

        // OpenID Connect Core 1.0 §6.1: an OP that does not support unsigned request objects must answer exactly
        // 'request_not_supported' (not a generic 'invalid_request_object' decode error) for an 'alg':'none' request
        // object. The OP advertises only asymmetric request-object signing algorithms (RFC 9700 / FAPI), so detect
        // the unsigned case up front and reject it with the precise error the spec requires.
        if (IsUnsignedRequestObject(requestJwt))
        {
            throw errorFactory
                .Create(OpenIdConstants.ErrorCodes.RequestNotSupported)
                .WithDescription(
                    "The authorization server does not support unsigned ('alg':'none') request objects."
                )
                .AsException();
        }

        JsonElement jwtPayload;
        try
        {
            var validationKeys = await ClientKeyResolver.ResolveSecretKeysAsync(
                openIdClient,
                cancellationToken
            );

            var parameters = new JwtValidationParameters()
            {
                // Honor the client's effective clock_skew setting (the FAPI 2.0 profile tightens this to ≤ 60s via the
                // merge-lattice ceiling); without this the JWT validator would fall back to its 5-minute default.
                ClockSkew = openIdClient.Settings.GetValue(OpenIdSettingKeys.ClockSkew),
            }
                .UseValidationKeys(validationKeys)
                .ValidateIssuer(openIdClient.ClientId)
                .ValidateCertificateLifeTime()
                .ValidateTokenLifeTime();

            var expectedAudience = openIdClient.Settings.GetValue(
                OpenIdSettingKeys.RequestObjectExpectedAudience
            );
            if (!string.IsNullOrEmpty(expectedAudience))
                parameters.ValidateAudience(expectedAudience);

            var result = await JwtValidator.ValidateAsync(
                requestJwt,
                parameters,
                cancellationToken
            );

            if (!result.IsValid)
            {
                ExceptionDispatchInfo.Throw(result.Exception);
            }

            jwtPayload = result.DecodedJwt.Payload;
        }
        catch (Exception exception)
        {
            Logger.FailedToDecodeJwt(exception);
            throw errorFactory.FailedToDecodeJwt(errorCode).WithException(exception).AsException();
        }

        try
        {
            // this will deserialize the object using OpenIdMessageJsonConverter
            var requestObject = jwtPayload.Deserialize<AuthorizationRequestObject>(
                openIdEnvironment.JsonSerializerOptions
            );
            if (requestObject == null)
                throw new InvalidOperationException("JSON deserialization returned null.");

            requestObject.RequestObjectSource = requestObjectSource;

            return requestObject;
        }
        catch (Exception exception)
        {
            Logger.FailedToDeserializeJson(exception);
            throw errorFactory
                .FailedToDeserializeJson(errorCode)
                .WithException(exception)
                .AsException();
        }
    }

    private bool IsUnsignedRequestObject(string requestJwt)
    {
        try
        {
            var joseToken = JoseSerializer.Parse(requestJwt);
            if (joseToken.ProtectionType != JoseProtectionTypes.Jws)
            {
                return false;
            }

            return joseToken.Header.TryGetPropertyValue<string>(
                    JoseClaimNames.Header.Alg,
                    out var alg
                )
                && string.Equals(
                    alg,
                    AlgorithmCodes.DigitalSignature.None,
                    StringComparison.OrdinalIgnoreCase
                );
        }
        catch (Exception)
        {
            // A malformed token is not the unsigned case; let the normal validation path report the decode failure as
            // 'invalid_request_object'.
            return false;
        }
    }

    private async ValueTask<string> FetchRequestUriAsync(
        OpenIdEnvironment openIdEnvironment,
        OpenIdClient openIdClient,
        Uri requestUri,
        CancellationToken cancellationToken
    )
    {
        var errorFactory = openIdEnvironment.ErrorFactory;
        var settings = openIdClient.Settings;

        // The OP fetches this client-supplied URL, so it must be https and (when configured) pass the host allow-list;
        // the always-on private/loopback/link-local address block in DefaultOutboundHttpForgeryGuard applies regardless.
        if (!string.Equals(requestUri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
            throw errorFactory
                .InvalidRequestUri("The 'request_uri' must be an absolute https URI.")
                .AsException();

        if (
            settings.TryGetValue(OpenIdSettingKeys.RequestUriAllowedHosts, out var allowedHosts)
            && allowedHosts.Count > 0
            && !allowedHosts.Contains(requestUri.Host, StringComparer.OrdinalIgnoreCase)
        )
            throw errorFactory
                .InvalidRequestUri(
                    $"The 'request_uri' host '{requestUri.Host}' is not in the configured allow-list."
                )
                .AsException();

        var timeout = settings.GetValue(OpenIdSettingKeys.RequestUriFetchTimeout);
        var maxSizeBytes = settings.GetValue(OpenIdSettingKeys.RequestUriMaxSizeBytes);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout > TimeSpan.Zero)
            timeoutCts.CancelAfter(timeout);

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        request.Options.Set(new HttpRequestOptionsKey<OpenIdClient>("OpenIdClient"), openIdClient);

        using var httpClient = HttpClientFactory.CreateClient(HttpClientName);
        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token
            );
            if (response.StatusCode != HttpStatusCode.OK)
                throw errorFactory
                    .InvalidRequestUri(
                        $"The http status code of the response must be 200 OK. Received {(int)response.StatusCode} {response.StatusCode}."
                    )
                    .AsException();

            var contentType = response.Content.Headers.ContentType?.MediaType;
            var expectedContentType = settings.GetValue(
                OpenIdSettingKeys.RequestUriExpectedContentType
            );
            if (
                settings.GetValue(OpenIdSettingKeys.RequestUriRequireStrictContentType)
                && !string.Equals(contentType, expectedContentType, StringComparison.Ordinal)
            )
                throw errorFactory
                    .InvalidRequestUri(
                        $"The content type of the response must be '{expectedContentType}'. Received '{contentType}'."
                    )
                    .AsException();

            var readResult = await OutboundHttpContentReader.ReadCappedAsync(
                response,
                buffer,
                maxSizeBytes,
                timeoutCts.Token
            );
            if (readResult != OutboundHttpReadResult.Completed)
                throw errorFactory
                    .InvalidRequestUri(
                        "The 'request_uri' response exceeds the maximum allowed size."
                    )
                    .AsException();

            using var lease = buffer.GetSpanLease(isSensitive: false);
            return Encoding.UTF8.GetString(lease.Span);
        }
        catch (OpenIdException)
        {
            // A specific validation failure (status / content-type / size) is surfaced as-is, not masked as a fetch error.
            throw;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Genuine caller cancellation (not the fetch timeout) propagates unchanged.
            throw;
        }
        catch (Exception exception)
        {
            Logger.FailedToFetchRequestUri(exception);
            throw errorFactory
                .InvalidRequestUri("Failed to fetch the request URI")
                .WithException(exception)
                .AsException();
        }
    }
}
