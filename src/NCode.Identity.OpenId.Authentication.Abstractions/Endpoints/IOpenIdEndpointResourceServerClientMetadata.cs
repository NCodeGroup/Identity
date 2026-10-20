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
/// Represents endpoint metadata stating whether an endpoint accepts a client bound to a resource server. Such a client
/// is the resource server's credential for the calls the resource server makes to the authorization server, and an
/// endpoint refuses it unless the endpoint carries this metadata with <see cref="AcceptsResourceServerClients"/> set
/// to <c>true</c>: a client bound to a resource server is not resolved anywhere else.
/// </summary>
[PublicAPI]
public interface IOpenIdEndpointResourceServerClientMetadata
{
    /// <summary>
    /// Gets a value indicating whether the associated endpoint accepts a client bound to a resource server.
    /// </summary>
    bool AcceptsResourceServerClients { get; }
}
