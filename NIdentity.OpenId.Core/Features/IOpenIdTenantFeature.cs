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
using NIdentity.OpenId.Tenants;

namespace NIdentity.OpenId.Features;

/// <summary>
/// Provides access to the <see cref="IOpenIdTenant"/> for the current request.
/// </summary>
public interface IOpenIdTenantFeature
{
    /// <summary>
    /// Gets the <see cref="IOpenIdTenant"/> for the current request.
    /// </summary>
    IOpenIdTenant Tenant { get; }
}

public class DefaultOpenIdTenant : IOpenIdTenant
{
    public string TenantId => throw new NotImplementedException();

    public string Issuer => throw new NotImplementedException();

    public UriDescriptor BaseAddress => throw new NotImplementedException();

    public ISecretKeyProvider SecretKeyProvider { get; }

    public DefaultOpenIdTenant(ISecretKeyProvider secretKeyProvider)
    {
        SecretKeyProvider = secretKeyProvider;
    }
}
