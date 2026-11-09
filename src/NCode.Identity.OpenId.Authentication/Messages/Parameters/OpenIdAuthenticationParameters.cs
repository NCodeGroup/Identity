// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Messages.Parsers;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Authentication.Messages.Parameters;

/// <summary>
/// Contains constants for various <see cref="OpenIdAuthenticationParameters"/> used by <c>OAuth</c> and <c>OpenID Connect</c> messages.
/// </summary>
[PublicAPI]
public static class OpenIdAuthenticationParameters
{
    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>claims</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="IRequestClaims"/> result.
    /// </summary>
    public static KnownParameter<IRequestClaims> Claims { get; } =
        new(OpenIdProtocol.Parameters.Claims, AuthenticationParameterParsers.RequestClaims)
        {
            AllowMissingStringValues = true,
        };

    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>$request_object_source</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="RequestObjectSource"/> result.
    /// </summary>
    public static KnownParameter<RequestObjectSource> RequestObjectSource { get; } =
        new(
            OpenIdConstants.InternalParameters.RequestObjectSource,
            EnumParser<RequestObjectSource>.Singleton
        )
        {
            AllowMissingStringValues = true,
            ShouldSerialize = OpenIdCommonParameters.ShouldSerializeAsJsonOnly,
        };
}
