// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Server.ProductionSafety;

/// <summary>
/// A single unsafe-for-production configuration finding reported by <c>AddProductionSafety</c>.
/// </summary>
/// <param name="ConfigurationKey">The configuration key that carries the unsafe value.</param>
/// <param name="Problem">What is unsafe about the current value.</param>
/// <param name="Resolution">The change that makes the deployment safe.</param>
[PublicAPI]
public sealed record ProductionSafetyViolation(
    string ConfigurationKey,
    string Problem,
    string Resolution
)
{
    /// <summary>
    /// Returns the finding as a single operator-facing line.
    /// </summary>
    public override string ToString() => $"{ConfigurationKey}: {Problem} {Resolution}";
}
