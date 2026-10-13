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

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Commands;

/// <summary>
/// Describes the outcome of an RP-Initiated Logout operation.
/// </summary>
/// <param name="RedirectUri">The validated <c>post_logout_redirect_uri</c> (with the echoed <c>state</c>) the user
/// agent should be redirected to, or <see langword="null"/> when no valid redirect was supplied and the OP should
/// render its own logged-out confirmation.</param>
/// <param name="FrontChannelLogoutUris">The OpenID Connect Front-Channel Logout 1.0 iframe URIs the OP should load in
/// hidden iframes so participating clients clear their own sessions; empty when front-channel logout is disabled or no
/// participating client has a <c>frontchannel_logout_uri</c> configured.</param>
[PublicAPI]
public readonly record struct EndSessionDisposition(
    string? RedirectUri,
    IReadOnlyList<string> FrontChannelLogoutUris
);
