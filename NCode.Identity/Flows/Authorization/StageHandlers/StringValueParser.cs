using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Identity.DataContracts;
using NCode.Identity.Validation;

namespace NCode.Identity.Flows.Authorization.StageHandlers
{
    /// <summary />
    public static class StringValueParser
    {
        /// <summary />
        public static bool TryParseString(string key, bool optional, StringValues stringValues, out ValidationResult<string> result)
        {
            switch (stringValues.Count)
            {
                case 0 when optional:
                    result = ValidationResult.Factory.Success<string>(null);
                    return true;

                case 0:
                    result = ValidationResult.Factory.MissingParameter(key).As<string>();
                    return false;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues(key).As<string>();
                    return false;

                default:
                    result = ValidationResult.Factory.Success(stringValues[0]);
                    return true;
            }
        }

        /// <summary />
        public static bool TryParseStringList(string key, bool optional, StringValues stringValues, out ValidationResult<IReadOnlyList<string>> result)
        {
            if (!optional && stringValues.Count == 0)
            {
                result = ValidationResult.Factory.MissingParameter(key).As<IReadOnlyList<string>>();
                return false;
            }

            result = ValidationResult.Factory.Success<IReadOnlyList<string>>(stringValues.ToList());
            return true;
        }

        /// <summary />
        public static bool TryParseScope(StringValues stringValues, out ValidationResult<IReadOnlySet<string>> result)
        {
            const string key = IdentityConstants.Parameters.Scope;

            if (stringValues.Count == 0)
            {
                result = ValidationResult.Factory.MissingParameter(key).As<IReadOnlySet<string>>();
                return false;
            }

            result = ValidationResult.Factory.Success<IReadOnlySet<string>>(stringValues.ToHashSet(StringComparer.Ordinal));
            return true;
        }

        /// <summary />
        public static bool TryParseResponseType(StringValues stringValues, out ValidationResult<ResponseTypes> result)
        {
            const string key = IdentityConstants.Parameters.ResponseMode;

            if (stringValues.Count == 0)
            {
                result = ValidationResult.Factory.MissingParameter(key, IdentityConstants.ErrorCodes.UnsupportedResponseType).As<ResponseTypes>();
                return false;
            }

            var responseType = ResponseTypes.Unknown;
            foreach (var stringValue in stringValues)
            {
                switch (stringValue)
                {
                    case IdentityConstants.ResponseTypes.Code:
                        responseType |= ResponseTypes.Code;
                        break;

                    case IdentityConstants.ResponseTypes.Token:
                        responseType |= ResponseTypes.Token;
                        break;

                    case IdentityConstants.ResponseTypes.IdToken:
                        responseType |= ResponseTypes.IdToken;
                        break;

                    default:
                        result = ValidationResult.Factory.InvalidParameterValue(key, IdentityConstants.ErrorCodes.UnsupportedResponseType).As<ResponseTypes>();
                        return false;
                }
            }

            result = ValidationResult.Factory.Success(responseType);
            return true;
        }

        /// <summary />
        public static bool TryParseResponseMode(StringValues stringValues, GrantType grantType, out ValidationResult<ResponseMode> result)
        {
            const string key = IdentityConstants.Parameters.ResponseMode;

            // https://openid.net/specs/oauth-v2-multiple-response-types-1_0.html

            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success(
                        grantType == GrantType.AuthorizationCode ? ResponseMode.Query : ResponseMode.Fragment);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues(key).As<ResponseMode>();
                    return false;
            }

            ResponseMode responseMode;
            switch (stringValues[0])
            {
                case IdentityConstants.ResponseModes.Query:
                    if (grantType != GrantType.AuthorizationCode)
                    {
                        result = ValidationResult.Factory.InvalidRequest("The query encoding is only allowed for the authorization code grant.").As<ResponseMode>();
                        return false;
                    }

                    responseMode = ResponseMode.Query;
                    break;

                case IdentityConstants.ResponseModes.Fragment:
                    responseMode = ResponseMode.Fragment;
                    break;

                case IdentityConstants.ResponseModes.FormPost:
                    responseMode = ResponseMode.FormPost;
                    break;

                default:
                    result = ValidationResult.Factory.InvalidParameterValue(key).As<ResponseMode>();
                    return false;
            }

            result = ValidationResult.Factory.Success(responseMode);
            return true;
        }

        /// <summary />
        public static bool TryParseDisplay(StringValues stringValues, out ValidationResult<DisplayType?> result)
        {
            const string key = IdentityConstants.Parameters.Display;

            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<DisplayType?>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues(key).As<DisplayType?>();
                    return false;
            }

            DisplayType? displayType;
            switch (stringValues[0])
            {
                case IdentityConstants.DisplayTypes.Page:
                    displayType = DisplayType.Page;
                    break;

                case IdentityConstants.DisplayTypes.Popup:
                    displayType = DisplayType.Popup;
                    break;

                case IdentityConstants.DisplayTypes.Touch:
                    displayType = DisplayType.Touch;
                    break;

                case IdentityConstants.DisplayTypes.Wap:
                    displayType = DisplayType.Wap;
                    break;

                default:
                    // TODO: ignore unsupported values
                    result = ValidationResult.Factory.InvalidParameterValue(key).As<DisplayType?>();
                    return false;
            }

            result = ValidationResult.Factory.Success(displayType);
            return true;
        }

        /// <summary />
        public static bool TryParsePrompt(StringValues stringValues, out ValidationResult<PromptTypes?> result)
        {
            const string key = IdentityConstants.Parameters.Prompt;

            if (stringValues.Count == 0)
            {
                result = ValidationResult.Factory.Success<PromptTypes?>(null);
                return true;
            }

            var promptTypes = PromptTypes.Unknown;
            foreach (var stringValue in stringValues)
            {
                // 'none' must be by itself
                if (promptTypes.HasFlag(PromptTypes.None))
                {
                    result = ValidationResult.Factory.InvalidRequest("The none value for prompt must not be combined with other values.").As<PromptTypes?>();
                    return false;
                }

                switch (stringValue)
                {
                    case IdentityConstants.PromptTypes.None:
                        promptTypes |= PromptTypes.None;
                        break;

                    case IdentityConstants.PromptTypes.Login:
                        promptTypes |= PromptTypes.Login;
                        break;

                    case IdentityConstants.PromptTypes.Consent:
                        promptTypes |= PromptTypes.Consent;
                        break;

                    case IdentityConstants.PromptTypes.SelectAccount:
                        promptTypes |= PromptTypes.SelectAccount;
                        break;

                    default:
                        // TODO: ignore unsupported values
                        result = ValidationResult.Factory.InvalidParameterValue(key).As<PromptTypes?>();
                        return false;
                }
            }

            result = ValidationResult.Factory.Success<PromptTypes?>(promptTypes);
            return true;
        }

        /// <summary />
        public static bool TryParseMaxAge(StringValues stringValues, out ValidationResult<TimeSpan?> result)
        {
            const string key = IdentityConstants.Parameters.MaxAge;

            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<TimeSpan?>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues(key).As<TimeSpan?>();
                    return false;
            }

            if (!int.TryParse(stringValues[0], out var seconds) || seconds < 0)
            {
                result = ValidationResult.Factory.InvalidParameterValue(key).As<TimeSpan?>();
                return false;
            }

            result = ValidationResult.Factory.Success<TimeSpan?>(TimeSpan.FromSeconds(seconds));
            return true;
        }

        /// <summary />
        public static bool TryParseClaims(StringValues stringValues, ILogger logger, out ValidationResult<AuthorizationRequestClaims> result)
        {
            // https://openid.net/specs/openid-connect-core-1_0.html#ClaimsParameter

            const string key = IdentityConstants.Parameters.Claims;

            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<AuthorizationRequestClaims>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues(key).As<AuthorizationRequestClaims>();
                    return false;
            }

            var json = WebUtility.UrlDecode(stringValues[0]) ?? stringValues[0];

            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            try
            {
                var claims = JsonSerializer.Deserialize<AuthorizationRequestClaims>(json, options);
                result = ValidationResult.Factory.Success(claims);
                return true;
            }
            catch (Exception exception)
            {
                const string errorCode = IdentityConstants.ErrorCodes.InvalidRequest;
                result = ValidationResult.Factory.FailedToDeserializeJson(errorCode).As<AuthorizationRequestClaims>();
                logger.LogError(exception, result.ErrorDetails.ErrorDescription);
                return false;
            }
        }

        /// <summary />
        public static bool TryParseCodeChallengeMethod(StringValues stringValues, out ValidationResult<CodeChallengeMethod?> result)
        {
            const string key = IdentityConstants.Parameters.CodeChallengeMethod;

            switch (stringValues.Count)
            {
                case 0:
                    result = ValidationResult.Factory.Success<CodeChallengeMethod?>(null);
                    return true;

                case > 1:
                    result = ValidationResult.Factory.TooManyParameterValues(key).As<CodeChallengeMethod?>();
                    return false;
            }

            CodeChallengeMethod? codeChallengeMethod;
            switch (stringValues[0])
            {
                case IdentityConstants.CodeChallengeMethods.Plain:
                    codeChallengeMethod = CodeChallengeMethod.Plain;
                    break;

                case IdentityConstants.CodeChallengeMethods.S256:
                    codeChallengeMethod = CodeChallengeMethod.S256;
                    break;

                default:
                    // TODO: ignore unsupported values
                    result = ValidationResult.Factory.InvalidParameterValue(key).As<CodeChallengeMethod?>();
                    return false;
            }

            result = ValidationResult.Factory.Success(codeChallengeMethod);
            return true;
        }
    }
}
