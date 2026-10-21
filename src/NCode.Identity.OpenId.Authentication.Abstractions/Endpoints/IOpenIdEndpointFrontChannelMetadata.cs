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

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Represents endpoint metadata marking a front-channel endpoint: one the user agent navigates to (the authorization,
/// continue, and end-session endpoints and the interactive login/consent surface) rather than one a client calls from
/// script. Such an endpoint never answers a cross-origin request with CORS headers (FAPI 2.0 Security Profile §5.3.2.1),
/// so the host applies no CORS policy to an endpoint that carries this metadata with <see cref="IsFrontChannel"/> set to
/// <c>true</c>. It is not CORS metadata, so a host that adds no CORS middleware serves the endpoint unchanged.
/// </summary>
[PublicAPI]
public interface IOpenIdEndpointFrontChannelMetadata
{
    /// <summary>
    /// Gets a value indicating whether the associated endpoint is a front-channel endpoint that no CORS policy applies to.
    /// </summary>
    bool IsFrontChannel { get; }
}
