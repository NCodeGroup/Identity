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
/// Identifies the deployment plane into which a system-owned resource server is seeded.
/// </summary>
[PublicAPI]
public enum SystemResourceServerPlane
{
    /// <summary>
    /// The resource server belongs to the tenant plane and is seeded into every tenant (for example, the OpenID
    /// Connect identity API and the tenant-plane management API).
    /// </summary>
    Tenant = 0,

    /// <summary>
    /// The resource server belongs to the control plane and is seeded only into the root tenant (for example, the
    /// control-plane management API that administers servers and tenants).
    /// </summary>
    Control = 1,
}
