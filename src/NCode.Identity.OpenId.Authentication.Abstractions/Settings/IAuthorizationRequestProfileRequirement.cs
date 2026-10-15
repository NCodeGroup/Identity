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

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// A <see cref="ISecurityProfileRequirement"/> evaluated while validating an authorization request. The active
/// profile's requirements of this phase are invoked at the authorization endpoint; an implementation throws an
/// <c>OpenId</c> exception to reject the request.
/// </summary>
[PublicAPI]
public interface IAuthorizationRequestProfileRequirement
    : ISecurityProfileRequirement<AuthorizationRequestProfileContext>;
