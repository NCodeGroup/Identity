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
using NIdentity.OpenId.Options;

namespace NIdentity.OpenId.Tenants;

/// <summary>
/// Provides a default implementation of the <see cref="OpenIdTenant"/> abstraction.
/// </summary>
public class DefaultOpenIdTenant : OpenIdTenant
{
    // TODO

    public override string TenantId => throw new NotImplementedException();

    public override string DisplayName => throw new NotImplementedException();

    public override string Issuer => throw new NotImplementedException();

    public override UriDescriptor BaseAddress => throw new NotImplementedException();

    public override ISecretKeyProvider SecretKeyProvider => throw new NotImplementedException();

    public override TenantOptions Options => throw new NotImplementedException();
}
