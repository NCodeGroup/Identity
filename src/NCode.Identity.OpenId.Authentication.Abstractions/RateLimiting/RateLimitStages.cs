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

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Defines the names of the stages at which the server judges the <see cref="RateLimitDescriptor"/> instances that
/// registered for them. A stage is an open name: a package that judges a stage of its own names it in the
/// <see cref="RateLimitDescriptor.Stage"/> of its descriptors and passes the same name when it asks the request rate
/// limiter to judge it.
/// </summary>
// See ADR-0246.
[PublicAPI]
public static class RateLimitStages
{
    /// <summary>
    /// Every request to the OpenID endpoint group, judged before its endpoint runs.
    /// </summary>
    public const string Request = "request";

    /// <summary>
    /// A request from a confidential client, judged once the client has authenticated.
    /// </summary>
    public const string ClientAuthentication = "client_authentication";

    /// <summary>
    /// A token request from a confidential client, judged once the client has authenticated.
    /// </summary>
    public const string TokenRequest = "token_request";
}
