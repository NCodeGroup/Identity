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

using Microsoft.Extensions.Options;
using NCode.Identity.Claims;
using NCode.Identity.DataProtection;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Authentication.Messages.Parameters;
using NCode.Identity.OpenId.Authentication.Options;

namespace NCode.Identity.OpenId.Authentication.Environments;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEnvironmentFactory"/> abstraction.
/// </summary>
public class DefaultOpenIdEnvironmentFactory(
    IOptions<OpenIdOptions> openIdOptionsAccessor,
    ISecureDataProtectionProvider secureDataProtectionProvider,
    IKnownParameterCollectionProvider knownParameterCollectionProvider,
    IOpenIdMessageFactorySelector openIdMessageFactorySelector,
    IClaimsSerializer claimsSerializer
) : IOpenIdEnvironmentFactory
{
    private OpenIdOptions OpenIdOptions { get; } = openIdOptionsAccessor.Value;
    private ISecureDataProtectionProvider SecureDataProtectionProvider { get; } = secureDataProtectionProvider;
    private IKnownParameterCollectionProvider KnownParameterCollectionProvider { get; } = knownParameterCollectionProvider;
    private IOpenIdMessageFactorySelector OpenIdMessageFactorySelector { get; } = openIdMessageFactorySelector;
    private IClaimsSerializer ClaimsSerializer { get; } = claimsSerializer;

    /// <inheritdoc />
    public OpenIdEnvironment Create()
    {
        // TODO: configure purpose from options
        var secureDataProtector = SecureDataProtectionProvider.CreateProtector("NCode.Identity.OpenId");

        var openIdEnvironment = new DefaultOpenIdEnvironment(
            OpenIdOptions,
            secureDataProtector,
            KnownParameterCollectionProvider,
            OpenIdMessageFactorySelector,
            ClaimsSerializer
        );

        return openIdEnvironment;
    }
}
