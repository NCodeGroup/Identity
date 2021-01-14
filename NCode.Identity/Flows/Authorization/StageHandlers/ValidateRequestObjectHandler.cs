using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Logic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NCode.Identity.DataContracts;
using NCode.Identity.Validation;

namespace NCode.Identity.Flows.Authorization.StageHandlers
{
    /// <summary />
    public class ValidateRequestObjectHandler : IAuthorizationStageHandler
    {
        private readonly ILogger<ValidateRequestObjectHandler> _logger;
        private readonly AuthorizationOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ISecretService _secretService;
        private readonly IJwtDecoder _jwtDecoder;

        /// <summary />
        public ValidateRequestObjectHandler(ILogger<ValidateRequestObjectHandler> logger, IOptions<AuthorizationOptions> options, IHttpClientFactory httpClientFactory, ISecretService secretService, IJwtDecoder jwtDecoder)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _secretService = secretService ?? throw new ArgumentNullException(nameof(secretService));
            _jwtDecoder = jwtDecoder ?? throw new ArgumentNullException(nameof(jwtDecoder));
        }

        /// <inheritdoc />
        public AuthorizationStage Stage => AuthorizationStage.ValidateRequestObject;

        /// <inheritdoc />
        public async ValueTask<ValidationResult> HandleAsync(AuthorizationContext context, CancellationToken cancellationToken)
        {
            // https://tools.ietf.org/html/draft-ietf-oauth-jwsreq-30
            // https://openid.net/specs/openid-connect-core-1_0.html#JWTRequests

            var jwtResult = await GetJwtAsync(context.Client, context.Request.RawValues, cancellationToken);
            if (jwtResult.HasError)
                return jwtResult;

            var (jwt, useErrorCode) = jwtResult.Value;
            if (jwt == null)
                return ValidationResult.SuccessResult;

            var issuer = context.Client.ClientId;
            var audience = _options.JwtRequest.Audience;

            var securityKeys = _secretService.LoadSecurityKeys(context.Client.Secrets);

            if (!_jwtDecoder.TryDecode(jwt, issuer, audience, securityKeys, useErrorCode, out var decodeResult))
                return decodeResult;

            var json = decodeResult.Value;
            if (!TryDeserialize(json, useErrorCode, out var deserializeResult))
                return deserializeResult;

            if (!TryValidate(context.Request, deserializeResult.Value, useErrorCode, out var validateResult))
                return validateResult;

            return ValidationResult.SuccessResult;
        }

        private async ValueTask<ValidationResult<(string jwt, string useErrorCode)>> GetJwtAsync(Client client, IReadOnlyDictionary<string, StringValues> rawValues, CancellationToken cancellationToken)
        {
            var requestStringValues = rawValues[IdentityConstants.Parameters.Request];
            var requestUriStringValues = rawValues[IdentityConstants.Parameters.RequestUri];

            if (requestStringValues.Count + requestUriStringValues.Count > 1)
                return ValidationResult.Factory.InvalidRequest<(string jwt, string useErrorCode)>(
                    "Both the request and request_uri parameters cannot be present at the same time.");

            string jwt = null;
            string useErrorCode = null;
            if (requestStringValues.Count == 1)
            {
                if (!_options.JwtRequest.RequestEnabled)
                    return ValidationResult.Factory.BadRequest<(string jwt, string useErrorCode)>(
                        IdentityConstants.ErrorCodes.RequestNotSupported,
                        "The request parameter is not supported.");

                jwt = requestStringValues[0];
                useErrorCode = IdentityConstants.ErrorCodes.InvalidRequestObject;
            }
            else if (requestUriStringValues.Count == 1)
            {
                if (!_options.JwtRequest.RequestUriEnabled)
                    return ValidationResult.Factory.BadRequest<(string jwt, string useErrorCode)>(
                        IdentityConstants.ErrorCodes.RequestUriNotSupported,
                        "The request_uri parameter is not supported.");

                var requestUri = requestUriStringValues[0];
                var requestUriMaxLength = _options.JwtRequest.RequestUriMaxLength;
                if (requestUri.Length > requestUriMaxLength)
                    return ValidationResult.Factory.BadRequest<(string jwt, string useErrorCode)>(
                        IdentityConstants.ErrorCodes.InvalidRequestUri,
                        $"The request_uri parameter must not exceed {requestUriMaxLength} characters.");

                var jwtResult = await GetJwtAsync(client, requestUri, cancellationToken);
                if (jwtResult.HasError)
                    return ValidationResult.Factory.Error<(string jwt, string useErrorCode)>(
                        jwtResult.ErrorDetails);

                jwt = jwtResult.Value;
                useErrorCode = IdentityConstants.ErrorCodes.InvalidRequestUri;
            }
            else if (client.RequireRequestObject)
            {
                return ValidationResult.Factory.InvalidRequest<(string jwt, string useErrorCode)>(
                    "Client configuration requires the use of request or request_uri parameters.");
            }

            return ValidationResult.Factory.Success((jwt, useErrorCode));
        }

        private async ValueTask<ValidationResult<string>> GetJwtAsync(Client client, string requestUri, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            request.Options.Set(new HttpRequestOptionsKey<Client>("Client"), client);

            using var httpClient = _httpClientFactory.CreateClient();

            try
            {
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                    return ValidationResult.Factory.BadRequest<string>(
                        IdentityConstants.ErrorCodes.InvalidRequestUri,
                        $"The http status code of the response must be OK. Received {response.StatusCode}.");

                var contentType = response.Content.Headers.ContentType?.MediaType;
                var expectedContentType = _options.JwtRequest.ExpectedContentType;
                if (_options.JwtRequest.StrictContentType && contentType != expectedContentType)
                    return ValidationResult.Factory.BadRequest<string>(
                        IdentityConstants.ErrorCodes.InvalidRequestUri,
                        $"The content type of the response must be \"{expectedContentType}\". Received \"{contentType}\".");

                var jwt = await response.Content.ReadAsStringAsync(cancellationToken);
                return ValidationResult.Factory.Success(jwt);
            }
            catch (Exception exception)
            {
                var result = ValidationResult.Factory.BadRequest<string>(
                    IdentityConstants.ErrorCodes.InvalidRequestUri,
                    "An error occurred while attempting to fetch request_uri.");
                _logger.LogError(exception, result.ErrorDetails.ErrorDescription);
                return result;
            }
        }

        private bool TryDeserialize(string json, string useErrorCode, out ValidationResult<JwtAuthorizationRequest> result)
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            try
            {
                var requestObject = JsonSerializer.Deserialize<JwtAuthorizationRequest>(json, options);
                result = ValidationResult.Factory.Success(requestObject);
                return true;
            }
            catch (Exception exception)
            {
                result = ValidationResult.Factory.FailedToDeserializeJson<JwtAuthorizationRequest>(useErrorCode);
                _logger.LogError(exception, result.ErrorDetails.ErrorDescription);
                return false;
            }
        }

        private static bool TryValidate(AuthorizationRequest request, JwtAuthorizationRequest requestObject, string useErrorCode, out ValidationResult result)
        {
            /*
             * request and request_uri parameters MUST NOT be included in Request Objects.
             */

            if (requestObject.Request.ValueKind != JsonValueKind.Undefined)
            {
                result = ValidationResult.Factory.BadRequest(useErrorCode, "The JWT request object must not contain the request parameter.");
                return false;
            }

            if (requestObject.RequestUri.ValueKind != JsonValueKind.Undefined)
            {
                result = ValidationResult.Factory.BadRequest(useErrorCode, "The JWT request object must not contain the request_uri parameter.");
                return false;
            }

            /*
             * So that the request is a valid OAuth 2.0 Authorization Request, values for the response_type and client_id parameters MUST
             * be included using the OAuth 2.0 request syntax, since they are REQUIRED by OAuth 2.0. The values for these parameters MUST
             * match those in the Request Object, if present.
             */

            if (!string.IsNullOrEmpty(requestObject.ResponseType))
            {
                if (!StringValueParser.TryParseResponseType(requestObject.ResponseType.Split(" "), out var responseTypeResult))
                {
                    result = responseTypeResult;
                    return false;
                }

                if (responseTypeResult.Value != request.ResponseType)
                {
                    result = ValidationResult.Factory.BadRequest(useErrorCode, "The response_type parameter in the JWT request object must match the same value from the request.");
                    return false;
                }
            }

            /*
             * The Client ID values in the "client_id" request parameter and in the Request Object "client_id" claim MUST be identical.
             */

            if (string.IsNullOrEmpty(requestObject.ClientId))
            {
                result = ValidationResult.Factory.BadRequest(useErrorCode, "The client_id parameter in the JWT request object is missing.");
                return false;
            }

            if (requestObject.ClientId != request.ClientId)
            {
                result = ValidationResult.Factory.BadRequest(useErrorCode, "The client_id parameter in the JWT request object must match the same value from the request.");
                return false;
            }

            /*
             * The Authorization Server MUST assemble the set of Authorization Request parameters to be used from the Request Object value and the
             * OAuth 2.0 Authorization Request parameters (minus the request or request_uri parameters). If the same parameter exists both in the
             * Request Object and the OAuth Authorization Request parameters, the parameter in the Request Object is used. Using the assembled set
             * of Authorization Request parameters, the Authorization Server then validates the request the normal manner for the flow being used,
             * as specified in Sections 3.1.2.2, 3.2.2.2, or 3.3.2.2.
             */

            if (!string.IsNullOrEmpty(requestObject.Scope))
            {
                request.Scopes = requestObject.Scope.Split(" ").ToHashSet();
            }

            if (!string.IsNullOrEmpty(requestObject.RedirectUri))
            {
                request.RedirectUri = requestObject.RedirectUri;
            }

            if (!string.IsNullOrEmpty(requestObject.State))
            {
                request.State = requestObject.State;
            }

            if (!string.IsNullOrEmpty(requestObject.ResponseMode))
            {
                if (!StringValueParser.TryParseResponseMode(requestObject.ResponseMode, request.GrantType, out var responseModeResult))
                {
                    result = responseModeResult;
                    return false;
                }

                request.ResponseMode = responseModeResult.Value;
            }

            if (!string.IsNullOrEmpty(requestObject.Nonce))
            {
                request.Nonce = requestObject.Nonce;
            }

            if (!string.IsNullOrEmpty(requestObject.Display))
            {
                if (!StringValueParser.TryParseDisplay(requestObject.Display, out var displayResult))
                {
                    result = displayResult;
                    return false;
                }

                request.Display = displayResult.Value ?? request.Display;
            }

            if (!string.IsNullOrEmpty(requestObject.Prompt))
            {
                if (!StringValueParser.TryParsePrompt(requestObject.Prompt.Split(" "), out var promptResult))
                {
                    result = promptResult;
                    return false;
                }

                request.Prompt = promptResult.Value ?? request.Prompt;
            }

            if (requestObject.MaxAgeSeconds.HasValue)
            {
                request.MaxAge = TimeSpan.FromSeconds(requestObject.MaxAgeSeconds.Value);
            }

            if (!string.IsNullOrEmpty(requestObject.UiLocales))
            {
                request.UiLocales = requestObject.UiLocales.Split(" ");
            }

            if (!string.IsNullOrEmpty(requestObject.IdTokenHint))
            {
                request.IdTokenHint = requestObject.IdTokenHint;
            }

            if (!string.IsNullOrEmpty(requestObject.LoginHint))
            {
                request.LoginHint = requestObject.LoginHint;
            }

            if (!string.IsNullOrEmpty(requestObject.AcrValues))
            {
                request.AcrValues = requestObject.AcrValues.Split(" ");
            }

            if (!string.IsNullOrEmpty(requestObject.ClaimsLocales))
            {
                request.ClaimsLocales = requestObject.ClaimsLocales.Split(" ");
            }

            if (requestObject.Claims != null)
            {
                request.Claims = requestObject.Claims;
            }

            if (!string.IsNullOrEmpty(requestObject.CodeChallenge))
            {
                const string key = IdentityConstants.Parameters.CodeChallenge;
                const bool optional = true; // validation will occur later

                if (!StringValueParser.TryParseString(key, optional, requestObject.CodeChallenge.Split(" "), out var codeChallengeResult))
                {
                    result = codeChallengeResult;
                    return false;
                }

                request.CodeChallenge = codeChallengeResult.Value ?? request.CodeChallenge;
            }

            if (!string.IsNullOrEmpty(requestObject.CodeChallengeMethod))
            {
                if (!StringValueParser.TryParseCodeChallengeMethod(requestObject.CodeChallengeMethod.Split(" "), out var codeChallengeMethodResult))
                {
                    result = codeChallengeMethodResult;
                    return false;
                }

                request.CodeChallengeMethod = codeChallengeMethodResult.Value ?? request.CodeChallengeMethod;
            }

            result = ValidationResult.SuccessResult;
            return true;
        }

    }
}
