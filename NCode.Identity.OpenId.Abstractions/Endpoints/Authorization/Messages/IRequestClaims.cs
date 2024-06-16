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

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Provides the ability for the authorization request to enable requesting individual <c>Claims</c> and specifying
/// parameters that apply to the requested <c>Claims</c>.
/// </summary>
[PublicAPI]
public interface IRequestClaims
{
    /// <summary>
    /// Gets the information that specifies which specific Claims should be returned from the UserInfo endpoint.
    /// If present, the listed Claims are being requested to be added to any Claims that are being requested using
    /// the <c>scope</c> parameter. If not present, the Claims being requested from the UserInfo endpoint are only
    /// those requested using the <c>scope</c> parameter.
    /// </summary>
    IReadOnlyDictionary<string, IRequestClaim?>? UserInfo { get; }

    /// <summary>
    /// Gets the information that specifies which specific Claims should be returned in the ID Token. If present,
    /// the listed Claims are being requested to be added to the default Claims in the ID Token. If not present,
    /// the default ID Token Claims are requested, as per the ID Token specification and additional per-flow ID
    /// Token requirements.
    /// </summary>
    IReadOnlyDictionary<string, IRequestClaim?>? IdToken { get; }
}
