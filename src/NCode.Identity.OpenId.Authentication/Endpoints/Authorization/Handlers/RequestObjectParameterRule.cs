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

using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;

/// <summary>
/// A parameter a request object must carry, and whether a copy sent outside it must equal it or is ignored
/// (RFC 9101 §5, §6.3; ADR-0202).
/// </summary>
/// <param name="ParameterName">The name of the parameter.</param>
/// <param name="GetValue">
/// Gets the value of the parameter in its comparable form, or <see langword="null"/> when the parameter is absent.
/// </param>
/// <param name="OuterValueMustMatch">
/// <see langword="true"/> when a copy of the parameter sent outside the request object must equal the object's value;
/// <see langword="false"/> when an outer copy is ignored.
/// </param>
// See ADR-0202.
internal sealed record RequestObjectParameterRule(
    string ParameterName,
    Func<IBaseAuthorizationRequestValues, string?> GetValue,
    bool OuterValueMustMatch
);
