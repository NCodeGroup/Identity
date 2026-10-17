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

using NCode.Identity.OpenId.Management.Contracts.Capabilities;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// Contributes the <c>eccCurves</c> section to the management capabilities document, projecting each supported
/// <see cref="EccCurveSpecification"/> so clients can offer a curve picker for <c>ecc</c> secrets.
/// </summary>
// See ADR-0068.
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
