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
/// Contains the options for one key every tenant is provisioned with. The key's type and intended use follow from its
/// algorithm: a signature algorithm yields a signing key, a key-management algorithm an encryption key.
/// </summary>
[PublicAPI]
public sealed class OpenIdTenantKeyOptions
{
    /// <summary>
    /// Gets or sets the code of the JSON Web Algorithm the key is generated for and bound to (for example
    /// <c>ES256</c>). It must name a registered signature or key-management algorithm whose key type is asymmetric.
    /// </summary>
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size, in bits, of the key to generate, or <c>null</c> to generate the smallest size the
    /// algorithm accepts. The size must be one the algorithm accepts.
    /// </summary>
    public int? KeySizeBits { get; set; }
}
