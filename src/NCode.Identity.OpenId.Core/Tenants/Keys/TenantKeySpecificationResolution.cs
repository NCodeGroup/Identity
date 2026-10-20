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

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Contains the keys every tenant is provisioned with, resolved from the configured provisioning options, and the
/// reasons any configured key cannot be provisioned.
/// </summary>
internal sealed class TenantKeySpecificationResolution
{
    /// <summary>
    /// Gets the keys every tenant is provisioned with; empty when provisioning is disabled.
    /// </summary>
    public required List<TenantKeySpecification> Specifications { get; init; }

    /// <summary>
    /// Gets a human-readable reason for every configured key that cannot be provisioned; empty when every key can be.
    /// </summary>
    public required List<string> Errors { get; init; }
}
