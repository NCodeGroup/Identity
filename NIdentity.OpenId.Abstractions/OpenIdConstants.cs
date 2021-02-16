namespace NIdentity.OpenId
{
    /// <summary>
    /// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> implementations.
    /// </summary>
    public static class OpenIdConstants
    {
        /// <summary>
        /// Contains the space ' ' character which is used as the separator in string lists.
        /// </summary>
        public const string ParameterSeparator = " ";

        /// <summary>
        /// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> parameters.
        /// </summary>
        public static class Parameters
        {
            /// <summary>
            /// Contains the name of <c>access_token</c> parameter.
            /// </summary>
            public const string AccessToken = "access_token";

            /// <summary>
            /// Contains the name of <c>acr_values</c> parameter.
            /// </summary>
            public const string AcrValues = "acr_values";

            /// <summary>
            /// Contains the name of <c>assertion</c> parameter.
            /// </summary>
            public const string Assertion = "assertion";

            /// <summary>
            /// Contains the name of <c>audience</c> parameter.
            /// </summary>
            public const string Audience = "audience";

            /// <summary>
            /// Contains the name of <c>claims</c> parameter.
            /// </summary>
            public const string Claims = "claims";

            /// <summary>
            /// Contains the name of <c>claims_locales</c> parameter.
            /// </summary>
            public const string ClaimsLocales = "claims_locales";

            /// <summary>
            /// Contains the name of <c>client_assertion</c> parameter.
            /// </summary>
            public const string ClientAssertion = "client_assertion";

            /// <summary>
            /// Contains the name of <c>client_assertion_type</c> parameter.
            /// </summary>
            public const string ClientAssertionType = "client_assertion_type";

            /// <summary>
            /// Contains the name of <c>client_id</c> parameter.
            /// </summary>
            public const string ClientId = "client_id";

            /// <summary>
            /// Contains the name of <c>client_secret</c> parameter.
            /// </summary>
            public const string ClientSecret = "client_secret";

            /// <summary>
            /// Contains the name of <c>code</c> parameter.
            /// </summary>
            public const string Code = "code";

            /// <summary>
            /// Contains the name of <c>code_challenge</c> parameter.
            /// </summary>
            public const string CodeChallenge = "code_challenge";

            /// <summary>
            /// Contains the name of <c>code_challenge_method</c> parameter.
            /// </summary>
            public const string CodeChallengeMethod = "code_challenge_method";

            /// <summary>
            /// Contains the name of <c>code_verifier</c> parameter.
            /// </summary>
            public const string CodeVerifier = "code_verifier";

            /// <summary>
            /// Contains the name of <c>device_code</c> parameter.
            /// </summary>
            public const string DeviceCode = "device_code";

            /// <summary>
            /// Contains the name of <c>display</c> parameter.
            /// </summary>
            public const string Display = "display";

            /// <summary>
            /// Contains the name of <c>essential</c> parameter.
            /// </summary>
            public const string Essential = "essential";

            /// <summary>
            /// Contains the name of <c>grant_type</c> parameter.
            /// </summary>
            public const string GrantType = "grant_type";

            /// <summary>
            /// Contains the name of <c>identity_provider</c> parameter.
            /// </summary>
            public const string IdentityProvider = "identity_provider";

            /// <summary>
            /// Contains the name of <c>id_token</c> parameter.
            /// </summary>
            public const string IdToken = "id_token";

            /// <summary>
            /// Contains the name of <c>id_token_hint</c> parameter.
            /// </summary>
            public const string IdTokenHint = "id_token_hint";

            /// <summary>
            /// Contains the name of <c>login_hint</c> parameter.
            /// </summary>
            public const string LoginHint = "login_hint";

            /// <summary>
            /// Contains the name of <c>max_age</c> parameter.
            /// </summary>
            public const string MaxAge = "max_age";

            /// <summary>
            /// Contains the name of <c>nonce</c> parameter.
            /// </summary>
            public const string Nonce = "nonce";

            /// <summary>
            /// Contains the name of <c>password</c> parameter.
            /// </summary>
            public const string Password = "password";

            /// <summary>
            /// Contains the name of <c>post_logout_redirect_uri</c> parameter.
            /// </summary>
            public const string PostLogoutRedirectUri = "post_logout_redirect_uri";

            /// <summary>
            /// Contains the name of <c>prompt</c> parameter.
            /// </summary>
            public const string Prompt = "prompt";

            /// <summary>
            /// Contains the name of <c>redirect_uri</c> parameter.
            /// </summary>
            public const string RedirectUri = "redirect_uri";

            /// <summary>
            /// Contains the name of <c>refresh_token</c> parameter.
            /// </summary>
            public const string RefreshToken = "refresh_token";

            /// <summary>
            /// Contains the name of <c>registration</c> parameter.
            /// </summary>
            public const string Registration = "registration";

            /// <summary>
            /// Contains the name of <c>request</c> parameter.
            /// </summary>
            public const string Request = "request";

            /// <summary>
            /// Contains the name of <c>request_id</c> parameter.
            /// </summary>
            public const string RequestId = "request_id";

            /// <summary>
            /// Contains the name of <c>request_uri</c> parameter.
            /// </summary>
            public const string RequestUri = "request_uri";

            /// <summary>
            /// Contains the name of <c>resource</c> parameter.
            /// </summary>
            public const string Resource = "resource";

            /// <summary>
            /// Contains the name of <c>response_mode</c> parameter.
            /// </summary>
            public const string ResponseMode = "response_mode";

            /// <summary>
            /// Contains the name of <c>response_type</c> parameter.
            /// </summary>
            public const string ResponseType = "response_type";

            /// <summary>
            /// Contains the name of <c>scope</c> parameter.
            /// </summary>
            public const string Scope = "scope";

            /// <summary>
            /// Contains the name of <c>state</c> parameter.
            /// </summary>
            public const string State = "state";

            /// <summary>
            /// Contains the name of <c>token</c> parameter.
            /// </summary>
            public const string Token = "token";

            /// <summary>
            /// Contains the name of <c>token_type_hint</c> parameter.
            /// </summary>
            public const string TokenTypeHint = "token_type_hint";

            /// <summary>
            /// Contains the name of <c>ui_locales</c> parameter.
            /// </summary>
            public const string UiLocales = "ui_locales";

            /// <summary>
            /// Contains the name of <c>user_code</c> parameter.
            /// </summary>
            public const string UserCode = "user_code";

            /// <summary>
            /// Contains the name of <c>userinfo</c> parameter.
            /// </summary>
            public const string UserInfo = "userinfo";

            /// <summary>
            /// Contains the name of <c>username</c> parameter.
            /// </summary>
            public const string Username = "username";

            /// <summary>
            /// Contains the name of <c>value</c> parameter.
            /// </summary>
            public const string Value = "value";

            /// <summary>
            /// Contains the name of <c>values</c> parameter.
            /// </summary>
            public const string Values = "values";
        }

        /// <summary>
        /// Contains constants for various <c>OAuth</c> and <c>OpenID Connect</c> error codes.
        /// </summary>
        public static class ErrorCodes
        {
            // OAuth 2.0 Authorization Error Response
            // https://tools.ietf.org/html/rfc6749#section-4.1.2.1

            /// <summary>
            /// Contains the error code for <c>invalid_request</c>.
            /// </summary>
            public const string InvalidRequest = "invalid_request";

            /// <summary>
            /// Contains the error code for <c>unauthorized_client</c>.
            /// </summary>
            public const string UnauthorizedClient = "unauthorized_client";

            /// <summary>
            /// Contains the error code for <c>access_denied</c>.
            /// </summary>
            public const string AccessDenied = "access_denied";

            /// <summary>
            /// Contains the error code for <c>unsupported_response_type</c>.
            /// </summary>
            public const string UnsupportedResponseType = "unsupported_response_type";

            /// <summary>
            /// Contains the error code for <c>invalid_scope</c>.
            /// </summary>
            public const string InvalidScope = "invalid_scope";

            /// <summary>
            /// Contains the error code for <c>server_error</c>.
            /// </summary>
            public const string ServerError = "server_error";

            /// <summary>
            /// Contains the error code for <c>temporarily_unavailable</c>.
            /// </summary>
            public const string TemporarilyUnavailable = "temporarily_unavailable";

            // https://openid.net/specs/openid-connect-core-1_0.html#AuthError

            /// <summary>
            /// Contains the error code for <c>interaction_required</c>.
            /// </summary>
            public const string InteractionRequired = "interaction_required";

            /// <summary>
            /// Contains the error code for <c>login_required</c>.
            /// </summary>
            public const string LoginRequired = "login_required";

            /// <summary>
            /// Contains the error code for <c>account_selection_required</c>.
            /// </summary>
            public const string AccountSelectionRequired = "account_selection_required";

            /// <summary>
            /// Contains the error code for <c>consent_required</c>.
            /// </summary>
            public const string ConsentRequired = "consent_required";

            /// <summary>
            /// Contains the error code for <c>invalid_request_uri</c>.
            /// </summary>
            public const string InvalidRequestUri = "invalid_request_uri";

            /// <summary>
            /// Contains the error code for <c>invalid_request_object</c>.
            /// </summary>
            public const string InvalidRequestJwt = "invalid_request_object";

            /// <summary>
            /// Contains the error code for <c>request_not_supported</c>.
            /// </summary>
            public const string RequestNotSupported = "request_not_supported";

            /// <summary>
            /// Contains the error code for <c>request_uri_not_supported</c>.
            /// </summary>
            public const string RequestUriNotSupported = "request_uri_not_supported";

            /// <summary>
            /// Contains the error code for <c>registration_not_supported</c>.
            /// </summary>
            public const string RegistrationNotSupported = "registration_not_supported";
        }

        /// <summary>
        /// Contains constants for possible values of the <c>response_type</c> parameter.
        /// </summary>
        public static class ResponseTypes
        {
            /// <summary>
            /// Contains the <c>response_type</c> parameter value for <c>none</c>.
            /// </summary>
            public const string None = "none";

            /// <summary>
            /// Contains the <c>response_type</c> parameter value for <c>code</c>.
            /// </summary>
            public const string Code = "code";

            /// <summary>
            /// Contains the <c>response_type</c> parameter value for <c>id_token</c>.
            /// </summary>
            public const string IdToken = "id_token";

            /// <summary>
            /// Contains the <c>response_type</c> parameter value for <c>token</c>.
            /// </summary>
            public const string Token = "token";
        }

        /// <summary>
        /// Contains constants for possible values of the <c>response_mode</c> parameter.
        /// </summary>
        public static class ResponseModes
        {
            /// <summary>
            /// Contains the <c>response_mode</c> parameter value for <c>query</c>.
            /// </summary>
            public const string Query = "query";

            /// <summary>
            /// Contains the <c>response_mode</c> parameter value for <c>fragment</c>.
            /// </summary>
            public const string Fragment = "fragment";

            /// <summary>
            /// Contains the <c>response_mode</c> parameter value for <c>form_post</c>.
            /// </summary>
            public const string FormPost = "form_post";
        }

        /// <summary>
        /// Contains constants for possible values of the <c>display</c> parameter.
        /// </summary>
        public static class DisplayTypes
        {
            /// <summary>
            /// Contains the <c>display</c> parameter value for <c>page</c>.
            /// </summary>
            public const string Page = "page";

            /// <summary>
            /// Contains the <c>display</c> parameter value for <c>popup</c>.
            /// </summary>
            public const string Popup = "popup";

            /// <summary>
            /// Contains the <c>display</c> parameter value for <c>touch</c>.
            /// </summary>
            public const string Touch = "touch";

            /// <summary>
            /// Contains the <c>display</c> parameter value for <c>wap</c>.
            /// </summary>
            public const string Wap = "wap";
        }

        /// <summary>
        /// Contains constants for possible values of the <c>prompt</c> parameter.
        /// </summary>
        public static class PromptTypes
        {
            /// <summary>
            /// Contains the <c>prompt</c> parameter value for <c>none</c>.
            /// </summary>
            public const string None = "none";

            /// <summary>
            /// Contains the <c>prompt</c> parameter value for <c>login</c>.
            /// </summary>
            public const string Login = "login";

            /// <summary>
            /// Contains the <c>prompt</c> parameter value for <c>consent</c>.
            /// </summary>
            public const string Consent = "consent";

            /// <summary>
            /// Contains the <c>prompt</c> parameter value for <c>select_account</c>.
            /// </summary>
            public const string SelectAccount = "select_account";
        }

        /// <summary>
        /// Contains constants for possible values of the <c>code_challenge_method</c> parameter.
        /// </summary>
        public static class CodeChallengeMethods
        {
            /// <summary>
            /// Contains the <c>code_challenge_method</c> parameter value for <c>plain</c>.
            /// </summary>
            public const string Plain = "plain";

            /// <summary>
            /// Contains the <c>code_challenge_method</c> parameter value for <c>S256</c>.
            /// </summary>
            public const string S256 = "S256";
        }
    }
}
