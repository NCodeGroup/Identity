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
    // TODO
    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>claims</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="IRequestClaims"/> result.
    /// </summary>
    public static readonly KnownParameter<IRequestClaims> Claims =
        new(OpenIdConstants.Parameters.Claims, AuthenticationParameterParsers.RequestClaims)
        {
            AllowMissingStringValues = true,
        };

    // TODO
    /// <summary>
    /// Gets the <see cref="KnownParameter"/> for the <c>$request_object_source</c> message parameter which parsers <see cref="StringValues"/> into an <see cref="RequestObjectSource"/> result.
    /// </summary>
    public static readonly KnownParameter<RequestObjectSource> RequestObjectSource =
        new(OpenIdConstants.Parameters.RequestObjectSource, EnumParser<RequestObjectSource>.Singleton)
        {
            AllowMissingStringValues = true,
            ShouldSerialize = OpenIdCommonParameters.ShouldSerializeAsJsonOnly,
        };
}
