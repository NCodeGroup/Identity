// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// A parameter a request object must carry, and whether a copy sent outside it must equal it or is ignored
/// (RFC 9101 §5, §6.3).
/// </summary>
/// <param name="ParameterName">The name of the parameter.</param>
/// <param name="GetValue">
/// Gets the value of the parameter in its comparable form, or <see langword="null"/> when the parameter is absent.
/// </param>
/// <param name="OuterValueMustMatch">
/// <see langword="true"/> when a copy of the parameter sent outside the request object must equal the object's value;
/// <see langword="false"/> when an outer copy is ignored.
/// </param>
// See ADR-1202.
internal sealed record RequestObjectParameterRule(
    string ParameterName,
    Func<IBaseAuthorizationRequestValues, string?> GetValue,
    bool OuterValueMustMatch
);
