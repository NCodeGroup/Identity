using Microsoft.Extensions.Primitives;
using NCode.Identity.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Repository;
using NCode.Identity.Validation;

namespace NCode.Identity.Flows.Authorization.StageHandlers
{
    /// <summary />
    public interface IRequestValuesValidator
    {
        /// <summary />
        ValueTask<ValidationResult> ValidateRequestValuesAsync(AuthorizationContext context, CancellationToken cancellationToken);
    }

    /// <summary />
    public class ValidateRequestValuesHandler : IAuthorizationStageHandler, IRequestValuesValidator
    {
        private readonly ILogger<ValidateRequestValuesHandler> _logger;
        private readonly AuthorizationOptions _options;
        private readonly IClientRepository _clientRepository;

        /// <summary />
        public ValidateRequestValuesHandler(ILogger<ValidateRequestValuesHandler> logger, IOptions<AuthorizationOptions> options, IClientRepository clientRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _clientRepository = clientRepository ?? throw new ArgumentNullException(nameof(clientRepository));
        }

        /// <inheritdoc />
        public AuthorizationStage Stage => AuthorizationStage.ValidateRequestValues;

        /// <inheritdoc />
        public ValueTask<ValidationResult> HandleAsync(AuthorizationContext context, CancellationToken cancellationToken)
        {
            return ValidateRequestValuesAsync(context, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask<ValidationResult> ValidateOtherAsync(AuthorizationContext context, CancellationToken cancellationToken)
        {
            // TODO: move after request object parsing

            var redirectUri = context.Request.RedirectUri;

            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
                return ValueTask.FromResult<ValidationResult>(ValidationResult.Factory.InvalidRequest<string>(
                    "The value for redirect_uri is invalid."));

            var redirectUris = context.Client.RedirectUris;
            if (!redirectUris.Contains(redirectUri, StringComparer.OrdinalIgnoreCase) && !(context.Client.AllowLoopback && uri.IsLoopback))
                return ValueTask.FromResult<ValidationResult>(ValidationResult.Factory.InvalidRequest<string>(
                    "The value for redirect_uri is invalid."));

            // TODO: move elsewhere...

            var hasOpenIdScope = context.Request.Scopes.Contains(IdentityConstants.ScopeTypes.OpenId);
            var isImplicit = context.Request.GrantType == GrantType.Implicit;
            var isHybrid = context.Request.GrantType == GrantType.Hybrid;

            // TODO: should we have an assertion for grant types?

            // https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16
            if (context.Request.ResponseType.HasFlag(ResponseTypes.Token) && !context.Client.AllowUnsafeTokenResponse)
                return ValueTask.FromResult<ValidationResult>(ValidationResult.Factory.UnauthorizedClient<string>());

            if (context.Request.ResponseType.HasFlag(ResponseTypes.IdToken) && !hasOpenIdScope)
                return ValueTask.FromResult<ValidationResult>(ValidationResult.Factory.InvalidRequest<string>(
                    "The openid scope is required when requesting id tokens."));

            if (hasOpenIdScope && string.IsNullOrEmpty(context.Request.Nonce) && (isImplicit || isHybrid))
                return ValueTask.FromResult<ValidationResult>(ValidationResult.Factory.InvalidRequest<string>(
                    "The nonce parameter is required when using the implicit or hybrid flows for openid requests."));

            var hasCodeChallenge = !string.IsNullOrEmpty(context.Request.CodeChallenge);
            var codeChallengeMethodIsPlain = context.Request.CodeChallengeMethod == CodeChallengeMethod.Plain;

            if (!hasCodeChallenge && context.Client.RequirePkce)
                return ValueTask.FromResult<ValidationResult>(ValidationResult.Factory.InvalidRequest<string>(
                    "Client configuration requires the use of PKCE parameters."));

            if (hasCodeChallenge && codeChallengeMethodIsPlain && !context.Client.AllowPlainCodeChallengeMethod)
                return ValueTask.FromResult<ValidationResult>(ValidationResult.Factory.InvalidRequest<string>(
                    "Client configuration prohibits the plain PKCE method."));

            // TODO: check IdP
            // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Validation/Default/AuthorizeRequestValidator.cs#L784

            // TODO: check session cookie
            // https://github.com/IdentityServer/IdentityServer4/blob/main/src/IdentityServer4/src/Validation/Default/AuthorizeRequestValidator.cs#L801

            return ValueTask.FromResult(ValidationResult.SuccessResult);
        }

        /// <inheritdoc />
        public async ValueTask<ValidationResult> ValidateRequestValuesAsync(AuthorizationContext context, CancellationToken cancellationToken)
        {
            if (!TryLoadRawValues(context.HttpContext, out var rawValuesResult))
                return rawValuesResult;

            context.Request.RawValues = rawValuesResult.Value;

            var parseResult = ParseRawValues(context);
            if (parseResult.HasError)
                return parseResult;

            var client = await _clientRepository.GetClientAsync(context.Request.ClientId, cancellationToken);
            if (client == null)
                return ValidationResult.Factory.InvalidRequest<string>(
                    "Client not found.");
            if (client.Disabled)
                return ValidationResult.Factory.InvalidRequest<string>(
                    "Client disabled.");

            context.Client = client;
            return ValidationResult.SuccessResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryLoadRawValues(HttpContext httpContext, out ValidationResult<IReadOnlyDictionary<string, StringValues>> result)
        {
            IEnumerable<KeyValuePair<string, StringValues>> source;
            if (HttpMethods.IsGet(httpContext.Request.Method))
            {
                source = httpContext.Request.Query;
            }
            else if (HttpMethods.IsPost(httpContext.Request.Method))
            {
                source = httpContext.Request.Form;
            }
            else
            {
                result = ValidationResult.Factory.Error<IReadOnlyDictionary<string, StringValues>>(
                    "TODO: error",
                    "TODO: errorDescription",
                    StatusCodes.Status405MethodNotAllowed);

                return false;
            }

            result = ValidationResult.Factory.Success<IReadOnlyDictionary<string, StringValues>>(source.ToDictionary(
                kvp => kvp.Key,
                kvp => new StringValues(kvp.Value.SelectMany(stringValues => stringValues.Split(" ")).ToArray())));

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ValidationResult ParseRawValues(AuthorizationContext context)
        {
            var request = context.Request;
            var rawValues = request.RawValues;

            if (!TryParseScope(rawValues, out var scopeResult))
                return scopeResult;

            if (!TryParseResponseType(rawValues, out var responseTypeResult))
                return responseTypeResult;

            if (!TryParseClientId(rawValues, out var clientIdResult))
                return clientIdResult;

            if (!TryParseRedirectUri(rawValues, out var redirectUriResult))
                return redirectUriResult;

            if (!TryParseState(rawValues, out var stateResult))
                return stateResult;

            var grantType = responseTypeResult.Value.GrantType;
            if (!TryParseResponseMode(rawValues, grantType, out var responseModeResult))
                return responseModeResult;

            if (!TryParseNonce(rawValues, out var nonceResult))
                return nonceResult;

            if (!TryParseDisplay(rawValues, out var displayResult))
                return displayResult;

            if (!TryParsePrompt(rawValues, out var promptResult))
                return promptResult;

            if (!TryParseMaxAge(rawValues, out var maxAgeResult))
                return maxAgeResult;

            if (!TryParseUiLocales(rawValues, out var uiLocalesResult))
                return uiLocalesResult;

            if (!TryParseIdTokenHint(rawValues, out var idTokenHintResult))
                return idTokenHintResult;

            if (!TryParseLoginHint(rawValues, out var loginHintResult))
                return loginHintResult;

            if (!TryParseAcrValues(rawValues, out var acrValuesResult))
                return acrValuesResult;

            if (!TryParseClaimsLocales(rawValues, out var claimsLocalesResult))
                return claimsLocalesResult;

            if (!TryParseClaims(rawValues, out var claimsResult))
                return claimsResult;

            if (!TryParseCodeChallenge(rawValues, out var codeChallengeResult))
                return codeChallengeResult;

            if (!TryParseCodeChallengeMethod(rawValues, out var codeChallengeMethodResult))
                return codeChallengeMethodResult;

            // TODO: registration
            // https://openid.net/specs/openid-connect-core-1_0.html#RegistrationParameter

            request.Scopes = scopeResult.Value;
            request.ResponseType = responseTypeResult.Value.ResponseType;
            request.GrantType = grantType;
            request.ClientId = clientIdResult.Value;
            request.RedirectUri = redirectUriResult.Value;
            request.State = stateResult.Value;
            request.Nonce = nonceResult.Value;
            request.Display = displayResult.Value ?? DisplayType.Page;
            request.Prompt = promptResult.Value ?? PromptTypes.Unknown;
            request.MaxAge = maxAgeResult.Value;
            request.UiLocales = uiLocalesResult.Value;
            request.IdTokenHint = idTokenHintResult.Value;
            request.LoginHint = loginHintResult.Value;
            request.AcrValues = acrValuesResult.Value;
            request.ClaimsLocales = claimsLocalesResult.Value;
            request.Claims = claimsResult.Value;
            request.CodeChallenge = codeChallengeResult.Value;
            request.CodeChallengeMethod = codeChallengeMethodResult.Value ?? CodeChallengeMethod.Plain;

            return ValidationResult.SuccessResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseScope(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<IReadOnlySet<string>> result)
        {
            const string key = IdentityConstants.Parameters.Scope;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseScope(stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseResponseType(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<(ResponseTypes ResponseType, GrantType GrantType)> result)
        {
            const string key = IdentityConstants.Parameters.ResponseType;

            rawValues.TryGetValue(key, out var stringValues);

            if (!StringValueParser.TryParseResponseType(stringValues, out var responseTypeResult))
            {
                result = ValidationResult.Factory.Error<(ResponseTypes ResponseType, GrantType GrantType)>(responseTypeResult.ErrorDetails);
                return false;
            }

            // https://openid.net/specs/oauth-v2-multiple-response-types-1_0.html

            GrantType grantType;
            var responseType = responseTypeResult.Value;
            if (responseType == ResponseTypes.Code)
            {
                grantType = GrantType.AuthorizationCode;
            }
            else if (responseType.HasFlag(ResponseTypes.Code))
            {
                grantType = GrantType.Hybrid;
            }
            else
            {
                grantType = GrantType.Implicit;
            }

            result = ValidationResult.Factory.Success((responseType, grantType));
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseClientId(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<string> result)
        {
            const string key = IdentityConstants.Parameters.ClientId;
            const bool optional = false;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseString(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseRedirectUri(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<string> result)
        {
            const string key = IdentityConstants.Parameters.RedirectUri;
            const bool optional = false;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseString(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseState(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<string> result)
        {
            const string key = IdentityConstants.Parameters.State;
            const bool optional = true;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseString(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseResponseMode(IReadOnlyDictionary<string, StringValues> rawValues, GrantType grantType, out ValidationResult<ResponseMode> result)
        {
            const string key = IdentityConstants.Parameters.ResponseMode;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseResponseMode(stringValues, grantType, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseNonce(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<string> result)
        {
            const string key = IdentityConstants.Parameters.Nonce;
            const bool optional = true;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseString(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseDisplay(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<DisplayType?> result)
        {
            const string key = IdentityConstants.Parameters.Display;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseDisplay(stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParsePrompt(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<PromptTypes?> result)
        {
            const string key = IdentityConstants.Parameters.Prompt;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParsePrompt(stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseMaxAge(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<TimeSpan?> result)
        {
            const string key = IdentityConstants.Parameters.MaxAge;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseMaxAge(stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseUiLocales(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<IReadOnlyList<string>> result)
        {
            const string key = IdentityConstants.Parameters.UiLocales;
            const bool optional = true;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseStringList(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseIdTokenHint(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<string> result)
        {
            const string key = IdentityConstants.Parameters.IdTokenHint;
            const bool optional = true;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseString(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseLoginHint(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<string> result)
        {
            const string key = IdentityConstants.Parameters.LoginHint;
            const bool optional = true;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseString(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseAcrValues(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<IReadOnlyList<string>> result)
        {
            const string key = IdentityConstants.Parameters.AcrValues;
            const bool optional = true;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseStringList(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseClaimsLocales(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<IReadOnlyList<string>> result)
        {
            const string key = IdentityConstants.Parameters.ClaimsLocales;
            const bool optional = true;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseStringList(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryParseClaims(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<AuthorizationRequestClaims> result)
        {
            const string key = IdentityConstants.Parameters.Claims;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseClaims(stringValues, _logger, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseCodeChallenge(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<string> result)
        {
            const string key = IdentityConstants.Parameters.CodeChallenge;
            const bool optional = true; // validation will occur later

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseString(key, optional, stringValues, out result);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool TryParseCodeChallengeMethod(IReadOnlyDictionary<string, StringValues> rawValues, out ValidationResult<CodeChallengeMethod?> result)
        {
            const string key = IdentityConstants.Parameters.CodeChallengeMethod;

            rawValues.TryGetValue(key, out var stringValues);

            return StringValueParser.TryParseCodeChallengeMethod(stringValues, out result);
        }

    }
}
