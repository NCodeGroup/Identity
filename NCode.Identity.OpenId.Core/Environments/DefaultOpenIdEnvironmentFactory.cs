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

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Serialization;

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEnvironmentFactory"/> abstraction.
/// </summary>
public class DefaultOpenIdEnvironmentFactory(
    ILoggerFactory loggerFactory,
    IDataProtectionProvider dataProtectionProvider,
    IKnownParameterCollectionProvider knownParameterCollectionProvider,
    IOpenIdMessageFactorySelector openIdMessageFactorySelector,
    IEnumerable<IOpenIdJsonConverterProvider> jsonConverterProviders
) : IOpenIdEnvironmentFactory
{
    private EphemeralDataProtectionProvider EphemeralDataProtectionProvider { get; } = new(loggerFactory);

    private IDataProtectionProvider DataProtectionProvider { get; } = dataProtectionProvider;
    private IKnownParameterCollectionProvider KnownParameterCollectionProvider { get; } = knownParameterCollectionProvider;
    private IOpenIdMessageFactorySelector OpenIdMessageFactorySelector { get; } = openIdMessageFactorySelector;
    private IEnumerable<IOpenIdJsonConverterProvider> JsonConverterProviders { get; } = jsonConverterProviders;

    /// <inheritdoc />
    public OpenIdEnvironment Create()
    {
        var dataProtector = DataProtectionProvider.CreateProtector("NCode.Identity.OpenId");
        var ephemeralDataProtector = EphemeralDataProtectionProvider.CreateProtector("NCode.Identity.OpenId");

        var openIdEnvironment = new DefaultOpenIdEnvironment(
            dataProtector,
            ephemeralDataProtector,
            KnownParameterCollectionProvider,
            OpenIdMessageFactorySelector,
            JsonConverterProviders
        );

        return openIdEnvironment;
    }
}
