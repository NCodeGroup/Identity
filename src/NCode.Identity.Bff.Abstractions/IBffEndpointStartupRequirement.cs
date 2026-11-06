// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Provides endpoint metadata that the BFF's startup check evaluates once the host has started, so a package that
/// marks endpoints can refuse to start when its own registration is missing.
/// </summary>
[PublicAPI]
public interface IBffEndpointStartupRequirement
{
    /// <summary>
    /// Checks the requirement of the endpoint that carries this metadata.
    /// </summary>
    /// <param name="services">The application's services.</param>
    /// <returns>A message that describes the failure, or <see langword="null"/> when the requirement holds.</returns>
    string? Validate(IServiceProvider services);
}
