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

namespace NCode.Identity.OpenId.ResourceServers;

/// <summary>
/// Contributes a reserved, system-owned resource server that is seeded into a tenant. Multiple providers are
/// registered as a collection so that each subsystem (for example, the OpenID Connect identity API and the
/// management API) can contribute its own system resource server.
/// </summary>
[PublicAPI]
public interface ISystemResourceServerProvider
{
    /// <summary>
    /// Gets the descriptor for the system resource server to seed.
    /// </summary>
    /// <returns>The <see cref="SystemResourceServerDescriptor"/> describing the resource server and its scopes.</returns>
    SystemResourceServerDescriptor GetDescriptor();
}
