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
