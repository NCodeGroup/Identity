// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Serialization;

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEnvironmentFactory"/> abstraction.
/// </summary>
internal class DefaultOpenIdEnvironmentFactory(
    ILoggerFactory loggerFactory,
    IOptions<OpenIdOptions> optionsAccessor,
    IDataProtectionProvider dataProtectionProvider,
    IKnownParameterCollectionProvider knownParameterCollectionProvider,
    IOpenIdMessageFactorySelector openIdMessageFactorySelector,
    IEnumerable<IOpenIdJsonConverterProvider> jsonConverterProviders
) : IOpenIdEnvironmentFactory
{
    private EphemeralDataProtectionProvider EphemeralDataProtectionProvider { get; } =
        new(loggerFactory);

    // Read on use: Value runs the OpenIdOptions validators, which construct the client-authentication handlers, which depend on this factory (ADR-1204).
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private OpenIdOptions Options => OptionsAccessor.Value;
    private IDataProtectionProvider DataProtectionProvider { get; } = dataProtectionProvider;
    private IKnownParameterCollectionProvider KnownParameterCollectionProvider { get; } =
        knownParameterCollectionProvider;
    private IOpenIdMessageFactorySelector OpenIdMessageFactorySelector { get; } =
        openIdMessageFactorySelector;
    private IEnumerable<IOpenIdJsonConverterProvider> JsonConverterProviders { get; } =
        jsonConverterProviders;

    /// <inheritdoc />
    public OpenIdEnvironment Create()
    {
        var dataProtector = DataProtectionProvider.CreateProtector("NCode.Identity.OpenId");
        var ephemeralDataProtector = EphemeralDataProtectionProvider.CreateProtector(
            "NCode.Identity.OpenId"
        );

        var openIdEnvironment = new DefaultOpenIdEnvironment(
            dataProtector,
            ephemeralDataProtector,
            KnownParameterCollectionProvider,
            OpenIdMessageFactorySelector,
            JsonConverterProviders,
            Options.GetSubjectId,
            Options.GetSubjectIdentity,
            Options.NormalizeSubjectId
        );

        return openIdEnvironment;
    }
}
