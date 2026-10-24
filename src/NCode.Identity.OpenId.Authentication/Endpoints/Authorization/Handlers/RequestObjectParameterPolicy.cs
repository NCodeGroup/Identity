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

using System.Collections.Immutable;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// Declares which parameters a request object must carry and how a copy sent outside the request object relates to
/// them, and validates a request object against that table (RFC 9101 §5, §6.3).
/// </summary>
/// <remarks>
/// Only the request object's parameters are used (RFC 9101 §6.3). A parameter whose outer copy must match is
/// compared in a single comparable form, as ordinal strings.
/// </remarks>
// See ADR-0202.
internal static class RequestObjectParameterPolicy
{
    /// <summary>
    /// Gets the parameters a request object must carry, in the order in which they are validated.
    /// </summary>
    public static ImmutableArray<RequestObjectParameterRule> Rules { get; } =
    [
        // The response type in its canonical form, so the order of its names does not matter.
        new RequestObjectParameterRule(
            OpenIdConstants.Parameters.ResponseType,
            values =>
                values.ResponseTypes is { Count: > 0 } names
                    ? ResponseTypeValues.Canonicalize(names)
                    : null,
            OuterValueMustMatch: true
        ),
        // An outer scope is ignored, as OpenID Connect Core §6.1 lets the object's value supersede it.
        new RequestObjectParameterRule(
            OpenIdConstants.Parameters.Scope,
            values =>
                values.Scopes is { Count: > 0 } scopes
                    ? string.Join(OpenIdConstants.ParameterSeparatorChar, scopes)
                    : null,
            OuterValueMustMatch: false
        ),
        // The redirect URI as written (ADR-0203).
        new RequestObjectParameterRule(
            OpenIdConstants.Parameters.RedirectUri,
            values => string.IsNullOrEmpty(values.RedirectUri) ? null : values.RedirectUri,
            OuterValueMustMatch: true
        ),
        new RequestObjectParameterRule(
            OpenIdConstants.Parameters.ClientId,
            values => string.IsNullOrEmpty(values.ClientId) ? null : values.ClientId,
            OuterValueMustMatch: true
        ),
    ];

    /// <summary>
    /// Validates that a request object carries every parameter in <see cref="Rules"/>, and that every outer copy that
    /// must match equals the request object's value.
    /// </summary>
    /// <param name="errorFactory">The factory used to create the error.</param>
    /// <param name="errorCode">The error code to report for an invalid request object.</param>
    /// <param name="requestMessage">The parameters sent outside the request object.</param>
    /// <param name="requestObject">The parameters of the request object.</param>
    /// <exception cref="OpenIdException">A required parameter is absent from the request object, or an outer copy
    /// differs from the request object's value.</exception>
    public static void Validate(
        IOpenIdErrorFactory errorFactory,
        string errorCode,
        IAuthorizationRequestMessage requestMessage,
        IAuthorizationRequestObject requestObject
    )
    {
        foreach (var rule in Rules)
        {
            var objectValue = rule.GetValue(requestObject);
            if (objectValue is null)
                throw errorFactory
                    .InvalidRequest(
                        $"The JWT request object must contain the '{rule.ParameterName}' parameter.",
                        errorCode
                    )
                    .AsException();

            if (!rule.OuterValueMustMatch)
                continue;

            var messageValue = rule.GetValue(requestMessage);
            if (
                messageValue is not null
                && !string.Equals(messageValue, objectValue, StringComparison.Ordinal)
            )
                throw errorFactory
                    .InvalidRequest(
                        $"The '{rule.ParameterName}' parameter in the JWT request object must match the same value from the request message.",
                        errorCode
                    )
                    .AsException();
        }
    }
}
