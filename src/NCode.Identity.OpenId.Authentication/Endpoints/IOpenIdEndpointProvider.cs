#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using NCode.Identity.Endpoints;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Marker for an <see cref="IEndpointProvider"/> that belongs to the OpenID endpoint group, so that the
/// <see cref="OpenIdEndpointGroupProvider"/> can collect and map it into the shared OpenID route group.
/// </summary>
internal interface IOpenIdEndpointProvider : IEndpointProvider;
