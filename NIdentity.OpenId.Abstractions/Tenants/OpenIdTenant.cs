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

using NCode.Jose.SecretKeys;

namespace NIdentity.OpenId.Tenants;

/// <summary>
/// Provides configuration details about an OpenID tenant.
/// </summary>
public abstract class OpenIdTenant
{
    /// <summary>
    /// Gets the unique identifier for the tenant.
    /// </summary>
    public abstract string TenantId { get; }

    /// <summary>
    /// Gets the display name for the tenant.
    /// </summary>
    public abstract string DisplayName { get; }

    /// <summary>
    /// Gets the issuer identifier for the tenant.
    /// </summary>
    public abstract string Issuer { get; }

    /// <summary>
    /// Gets the base address for the tenant.
    /// </summary>
    public abstract UriDescriptor BaseAddress { get; }

    /// <summary>
    /// Gets the <see cref="ISecretKeyProvider"/> for the tenant.
    /// </summary>
    public abstract ISecretKeyProvider SecretKeyProvider { get; }
}
