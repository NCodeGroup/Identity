#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The known reasons that a BFF login failed.
/// </summary>
/// <remarks>
/// The reasons are an open vocabulary: a login may fail for a reason that is not listed here. Reasons are compared
/// ordinally.
/// </remarks>
[PublicAPI]
public static class BffLoginFailureReasons
{
    /// <summary>
    /// The <c>state</c> of the callback matches no pending login transaction.
    /// </summary>
    public const string UnknownState = "unknown_state";

    /// <summary>
    /// The browser that returned is not the browser that started the login.
    /// </summary>
    public const string InitiatorMismatch = "initiator_mismatch";

    /// <summary>
    /// The <c>iss</c> parameter of the callback is not the issuer of the provider.
    /// </summary>
    public const string IssuerMismatch = "issuer_mismatch";

    /// <summary>
    /// The provider answered the authorization request with an error.
    /// </summary>
    public const string AuthorizationError = "authorization_error";

    /// <summary>
    /// The authorization request could not be pushed to the provider.
    /// </summary>
    public const string AuthorizationRequestFailed = "authorization_request_failed";

    /// <summary>
    /// The provider could not be reached, or its metadata or keys could not be read.
    /// </summary>
    public const string ProviderUnavailable = "provider_unavailable";

    /// <summary>
    /// The callback is malformed.
    /// </summary>
    public const string InvalidResponse = "invalid_response";

    /// <summary>
    /// The provider answered the code exchange with an error.
    /// </summary>
    public const string TokenError = "token_error";

    /// <summary>
    /// The ID token is invalid.
    /// </summary>
    public const string IdTokenInvalid = "id_token_invalid";

    /// <summary>
    /// The <c>nonce</c> of the ID token is not the nonce of the request.
    /// </summary>
    public const string NonceMismatch = "nonce_mismatch";

    /// <summary>
    /// A login gate rejected the login.
    /// </summary>
    public const string RejectedByGate = "rejected_by_gate";
}
