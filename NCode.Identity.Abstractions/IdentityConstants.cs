namespace NCode.Identity
{
    public static class IdentityConstants
    {
        public static class Parameters
        {
            public const string Scope = "scope";
            public const string ResponseType = "response_type";
            public const string ClientId = "client_id";
            public const string RedirectUri = "redirect_uri";
            public const string State = "state";
            public const string ResponseMode = "response_mode";
            public const string Nonce = "nonce";
            public const string Display = "display";
            public const string Prompt = "prompt";
            public const string MaxAge = "max_age";
            public const string UiLocales = "ui_locales";
            public const string IdTokenHint = "id_token_hint";
            public const string LoginHint = "login_hint";
            public const string AcrValues = "acr_values";
            public const string ClaimsLocales = "claims_locales";
            public const string Claims = "claims";
            public const string Request = "request";
            public const string RequestUri = "request_uri";

            // TODO
            public const string CodeVerifier = "code_verifier";
            public const string CodeChallenge = "code_challenge";
            public const string CodeChallengeMethod = "code_challenge_method";
        }

        public static class ResponseTypes
        {
            public const string Code = "code";
            public const string Token = "token";
            public const string IdToken = "id_token";
        }

        public static class ResponseModes
        {
            public const string Query = "query";
            public const string Fragment = "fragment";
            public const string FormPost = "form_post";
        }

        public static class DisplayTypes
        {
            public const string Page = "page";
            public const string Popup = "popup";
            public const string Touch = "touch";
            public const string Wap = "wap";
        }

        public static class PromptTypes
        {
            public const string None = "none";
            public const string Login = "login";
            public const string Consent = "consent";
            public const string SelectAccount = "select_account";
        }

        public static class GrantTypes
        {
            public const string AuthorizationCode = "authorization_code";
            public const string Implicit = "implicit";
            public const string Hybrid = "hybrid";
            public const string ResourceOwnerPassword = "password";
            public const string ClientCredentials = "client_credentials";
            public const string RefreshToken = "refresh_token";
            public const string DeviceFlow = "urn:ietf:params:oauth:grant-type:device_code";
        }

        public static class CodeChallengeMethods
        {
            public const string Plain = "plain";
            public const string S256 = "S256";
        }

        public static class ErrorCodes
        {
            // OAuth 2.0 Authorization Error Response
            // https://tools.ietf.org/html/rfc6749#section-4.1.2.1
            public const string InvalidRequest = "invalid_request";
            public const string UnauthorizedClient = "unauthorized_client";
            public const string AccessDenied = "access_denied";
            public const string UnsupportedResponseType = "unsupported_response_type";
            public const string InvalidScope = "invalid_scope";
            public const string ServerError = "server_error";
            public const string TemporarilyUnavailable = "temporarily_unavailable";

            // https://openid.net/specs/openid-connect-core-1_0.html#AuthError
            public const string InteractionRequired = "interaction_required";
            public const string LoginRequired = "login_required";
            public const string AccountSelectionRequired = "account_selection_required";
            public const string ConsentRequired = "consent_required";
            public const string InvalidRequestUri = "invalid_request_uri";
            public const string InvalidRequestObject = "invalid_request_object";
            public const string RequestNotSupported = "request_not_supported";
            public const string RequestUriNotSupported = "request_uri_not_supported";
            public const string RegistrationNotSupported = "registration_not_supported";
        }

        public static class ScopeTypes
        {
            public const string OpenId = "openid";
            public const string Profile = "profile";
            public const string Email = "email";
            public const string Address = "address";
            public const string Phone = "phone";
            public const string OfflineAccess = "offline_access";
        }

    }
}
