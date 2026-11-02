// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// Contributes the <c>eccCurves</c> section to the management capabilities document, projecting each supported
/// <see cref="EccCurveSpecification"/> so clients can offer a curve picker for <c>ecc</c> secrets.
/// </summary>
// See ADR-1068.
internal sealed class EccCurveCapabilitiesProvider(IEccCurveCatalog eccCurveCatalog)
    : ICommandHandler<DiscoverManagementCapabilitiesCommand>
{
    /// <summary>
    /// The capability section name under which the supported elliptic curves are published.
    /// </summary>
    public const string SectionName = "eccCurves";

    private IEccCurveCatalog EccCurveCatalog { get; } = eccCurveCatalog;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        DiscoverManagementCapabilitiesCommand command,
        CancellationToken cancellationToken
    )
    {
        var curves = EccCurveCatalog
            .Curves.Select(curve => new EccCurveCapabilityResource
            {
                CurveName = curve.CurveName,
                CurveSizeBits = curve.CurveSizeBits,
            })
            .ToList();

        command.Capabilities.TryAdd(SectionName, curves);

        return ValueTask.CompletedTask;
    }
}
