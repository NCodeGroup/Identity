#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using Microsoft.AspNetCore.Authentication;

namespace NIdentity.OpenId.Settings;

// TODO: better xmldoc descriptions

/// <summary>
/// Provides a wrapper on top of <see cref="ISettingCollection"/> that provides strongly typed access to known settings.
/// </summary>
public interface IKnownSettingCollection : ISettingCollection
{
    /// <summary>
    /// Gets or sets the value for the 'access_token_encryption_alg_values_supported' setting.
    /// </summary>
    IReadOnlyCollection<string> AccessTokenEncryptionAlgValuesSupported { get; set; }

    /// <summary>
    /// Gets or sets the value for the 'access_token_encryption_enc_values_supported' setting.
    /// </summary>
    IReadOnlyCollection<string> AccessTokenEncryptionEncValuesSupported { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Access Tokens must be encrypted.
    /// </summary>
    bool AccessTokenEncryptionRequired { get; set; }

    /// <summary>
    /// Gets or sets the value for the 'access_token_encryption_zip_values_supported' setting.
    /// </summary>
    IReadOnlyCollection<string> AccessTokenEncryptionZipValuesSupported { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of access tokens issued to clients.
    /// </summary>
    TimeSpan AccessTokenLifetime { get; set; }

    /// <summary>
    /// Gets or sets the value for the 'access_token_signing_alg_values_supported' setting.
    /// </summary>
    IReadOnlyCollection<string> AccessTokenSigningAlgValuesSupported { get; set; }

    /// <summary>
    /// Gets or sets the value for the 'access_token_type' setting.
    /// </summary>
    string AccessTokenType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the loopback address (i.e. 127.0.0.1 or localhost) is allowed to be
    /// used as a redirect address without being explicitly registered.
    /// </summary>
    bool AllowLoopbackRedirect { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the use of the 'plain' PKCE challenge method is allowed during authorization.
    /// </summary>
    bool AllowPlainCodeChallengeMethod { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether unsafe token responses are allowed.
    /// See https://tools.ietf.org/html/draft-ietf-oauth-security-topics-16 for more information.
    /// </summary>
    bool AllowUnsafeTokenResponse { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of authorization codes issued to clients.
    /// </summary>
    TimeSpan AuthorizationCodeLifetime { get; set; }

    /// <summary>
    /// Gets or sets the authentication scheme corresponding to the middleware
    /// responsible of persisting user's identity after a successful authentication.
    /// This value typically corresponds to a cookie middleware registered in the Startup class.
    /// When omitted, <see cref="AuthenticationOptions.DefaultSignInScheme"/> is used as a fallback value.
    /// </summary>
    string AuthorizationSignInScheme { get; set; }

    /// <summary>
    /// Gets or sets the amount of time to allow for clock skew when validating <see cref="DateTime"/> claims.
    /// The default is <c>300</c> seconds (5 minutes).
    /// </summary>
    TimeSpan ClockSkew { get; set; }

    /// <summary>
    /// Gets or sets the value for the 'id_token_encryption_alg_values_supported' setting.
    /// </summary>
    IReadOnlyCollection<string> IdTokenEncryptionAlgValuesSupported { get; set; }

    /// <summary>
    /// Gets or sets the value for the 'id_token_encryption_enc_values_supported' setting.
    /// </summary>
    IReadOnlyCollection<string> IdTokenEncryptionEncValuesSupported { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether ID Tokens must be encrypted.
    /// </summary>
    bool IdTokenEncryptionRequired { get; set; }

    /// <summary>
    /// Gets or sets the value for the 'id_token_encryption_zip_values_supported' setting.
    /// </summary>
    IReadOnlyCollection<string> IdTokenEncryptionZipValuesSupported { get; set; }

    /// <summary>
    /// Gets or sets the lifetime of ID tokens issued to clients.
    /// </summary>
    TimeSpan IdTokenLifetime { get; set; }

    /// <summary>
    /// Gets or sets the value for the 'id_token_signing_alg_values_supported' setting.
    /// </summary>
    IReadOnlyCollection<string> IdTokenSigningAlgValuesSupported { get; set; }

    /// <summary>
    /// Gets or sets the value to use when validating the <c>audience</c> JWT claim when fetching the request object
    /// from <c>request_uri</c>.
    /// </summary>
    string RequestObjectExpectedAudience { get; set; }

    /// <summary>
    /// Gets or sets the value of the 'request_parameter_supported' setting.
    /// </summary>
    bool RequestParameterSupported { get; set; }

    /// <summary>
    /// Gets or sets the value of the 'request_uri_parameter_supported' setting.
    /// </summary>
    bool RequestUriParameterSupported { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>Content-Type</c> returned from fetching the <c>request_uri</c>
    /// must match the value configured in <see cref="RequestUriExpectedContentType"/>.
    /// </summary>
    bool RequestUriRequireStrictContentType { get; set; }

    /// <summary>
    /// Gets or sets the expected <c>Content-Type</c> that should be returned from <c>request_uri</c>. Defaults to
    /// <c>application/oauth-authz-req+jwt</c>. Only used if <see cref="RequestUriExpectedContentType"/> is <c>true</c>.
    /// </summary>
    string RequestUriExpectedContentType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the use of PKCE is required during authorization.
    /// </summary>
    bool RequireCodeChallenge { get; set; }
}
