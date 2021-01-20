namespace NIdentity.OpenId
{
    public static class OpenIdConstants
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

            // TODO
            //public const string AccessToken = "access_token";
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
    }
}
