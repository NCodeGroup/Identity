using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages.Authorization
{
    [JsonConverter(typeof(OpenIdMessageJsonConverter<JsonAuthorizationRequestObject>))]
    internal class JsonAuthorizationRequestObject : OpenIdMessage, IAuthorizationRequestObject
    {
        /// <inheritdoc />
        public IEnumerable<string>? AcrValues
        {
            get => GetKnownParameter(KnownParameters.AcrValues);
            set => SetKnownParameter(KnownParameters.AcrValues, value);
        }

        /// <inheritdoc />
        public IRequestClaims? Claims
        {
            get => GetKnownParameter(KnownParameters.Claims);
            set => SetKnownParameter(KnownParameters.Claims, value);
        }

        /// <inheritdoc />
        public IEnumerable<string>? ClaimsLocales
        {
            get => GetKnownParameter(KnownParameters.ClaimsLocales);
            set => SetKnownParameter(KnownParameters.ClaimsLocales, value);
        }

        /// <inheritdoc />
        public string? ClientId
        {
            get => GetKnownParameter(KnownParameters.ClientId);
            set => SetKnownParameter(KnownParameters.ClientId, value);
        }

        /// <inheritdoc />
        public string? CodeChallenge
        {
            get => GetKnownParameter(KnownParameters.CodeChallenge);
            set => SetKnownParameter(KnownParameters.CodeChallenge, value);
        }

        /// <inheritdoc />
        public CodeChallengeMethod? CodeChallengeMethod
        {
            get => GetKnownParameter(KnownParameters.CodeChallengeMethod);
            set => SetKnownParameter(KnownParameters.CodeChallengeMethod, value);
        }

        /// <inheritdoc />
        public string? CodeVerifier
        {
            get => GetKnownParameter(KnownParameters.CodeVerifier);
            set => SetKnownParameter(KnownParameters.CodeVerifier, value);
        }

        /// <inheritdoc />
        public DisplayType? Display
        {
            get => GetKnownParameter(KnownParameters.Display);
            set => SetKnownParameter(KnownParameters.Display, value);
        }

        /// <inheritdoc />
        public string? IdTokenHint
        {
            get => GetKnownParameter(KnownParameters.IdTokenHint);
            set => SetKnownParameter(KnownParameters.IdTokenHint, value);
        }

        /// <inheritdoc />
        public string? LoginHint
        {
            get => GetKnownParameter(KnownParameters.LoginHint);
            set => SetKnownParameter(KnownParameters.LoginHint, value);
        }

        /// <inheritdoc />
        public TimeSpan? MaxAge
        {
            get => GetKnownParameter(KnownParameters.MaxAge);
            set => SetKnownParameter(KnownParameters.MaxAge, value);
        }

        /// <inheritdoc />
        public string? Nonce
        {
            get => GetKnownParameter(KnownParameters.Nonce);
            set => SetKnownParameter(KnownParameters.Nonce, value);
        }

        /// <inheritdoc />
        public PromptTypes? Prompt
        {
            get => GetKnownParameter(KnownParameters.Prompt);
            set => SetKnownParameter(KnownParameters.Prompt, value);
        }

        /// <inheritdoc />
        public string? RedirectUri
        {
            get => GetKnownParameter(KnownParameters.RedirectUri);
            set => SetKnownParameter(KnownParameters.RedirectUri, value);
        }

        //public string? Request
        //{
        //    get => GetKnownParameter(KnownParameters.Request);
        //    set => SetKnownParameter(KnownParameters.Request, value);
        //}

        //public string? RequestUri
        //{
        //    get => GetKnownParameter(KnownParameters.RequestUri);
        //    set => SetKnownParameter(KnownParameters.RequestUri, value);
        //}

        /// <inheritdoc />
        public ResponseMode? ResponseMode
        {
            get => GetKnownParameter(KnownParameters.ResponseMode);
            set => SetKnownParameter(KnownParameters.ResponseMode, value);
        }

        /// <inheritdoc />
        public ResponseTypes? ResponseType
        {
            get => GetKnownParameter(KnownParameters.ResponseType);
            set => SetKnownParameter(KnownParameters.ResponseType, value);
        }

        /// <inheritdoc />
        public IEnumerable<string>? Scopes
        {
            get => GetKnownParameter(KnownParameters.Scope);
            set => SetKnownParameter(KnownParameters.Scope, value);
        }

        /// <inheritdoc />
        public string? State
        {
            get => GetKnownParameter(KnownParameters.State);
            set => SetKnownParameter(KnownParameters.State, value);
        }

        /// <inheritdoc />
        public IEnumerable<string>? UiLocales
        {
            get => GetKnownParameter(KnownParameters.UiLocales);
            set => SetKnownParameter(KnownParameters.UiLocales, value);
        }
    }
}
