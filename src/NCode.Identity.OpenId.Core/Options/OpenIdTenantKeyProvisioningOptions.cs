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

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options that decide which keys every tenant is provisioned with. Each time a tenant is seeded, the
/// server generates and persists a key for every configured algorithm the tenant holds no usable key for, so every
/// tenant — the root tenant, a tenant created at runtime, and an existing tenant when the configuration gains an
/// algorithm — can sign without an operator importing a key first.
/// </summary>
/// <remarks>
/// Provisioning only fills gaps: a key the tenant already holds for an algorithm, whether imported or generated
/// earlier, suppresses generation for that algorithm and is never replaced, rotated, or removed — not even when it can
/// no longer be unprotected, which signals a lost or changed Data Protection key ring and fails the tenant instead.
/// A deployment that brings its own keys either imports one for each configured algorithm or disables provisioning.
/// </remarks>
[PublicAPI]
public sealed class OpenIdTenantKeyProvisioningOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether tenants are provisioned with keys when they are seeded.
    /// The default value is <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the keys every tenant is provisioned with, one per algorithm. When empty, every tenant holds an
    /// <c>RS256</c> key (the algorithm OpenID Connect requires every provider to support) and a <c>PS256</c> key, both
    /// 3072-bit RSA, and an <c>ES256</c> key on the P-256 curve.
    /// </summary>
    /// <remarks>
    /// Any registered signature or key-management algorithm whose key type is asymmetric may be listed — for example
    /// <c>EdDSA</c> when its package is registered, or <c>RSA-OAEP-256</c> to give each tenant an encryption key.
    /// Configuring the list replaces the default set rather than adding to it, and every entry is validated when the
    /// host starts.
    /// </remarks>
    public List<OpenIdTenantKeyOptions> Keys { get; set; } = [];
}
