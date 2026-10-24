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

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace NCode.Identity.Secrets.Persistence.AzureKeyVault;

/// <summary>
/// Contains the rules for the identifier of a key in Azure Key Vault or Managed HSM: whether a value is one, and whether
/// it names a key version.
/// </summary>
// See ADR-0260.
internal static partial class AzureKeyVaultKeyId
{
    // '\z' rather than '$', which also matches before a trailing newline; the patterns accept ASCII letters and digits
    // only.
    [GeneratedRegex(
        @"^https://[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+/keys/[A-Za-z0-9-]{1,127}(/[A-Za-z0-9]{32})?\z"
    )]
    private static partial Regex KeyPattern();

    [GeneratedRegex(
        @"^https://[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+/keys/[A-Za-z0-9-]{1,127}/[A-Za-z0-9]{32}\z"
    )]
    private static partial Regex KeyVersionPattern();

    /// <summary>
    /// Determines whether the specified value is the identifier of a Key Vault or Managed HSM key, with or without a
    /// version.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a key identifier; otherwise, <see langword="false"/>.</returns>
    public static bool IsKeyId([NotNullWhen(true)] string? value) =>
        value is not null && KeyPattern().IsMatch(value);

    /// <summary>
    /// Determines whether the specified value is the identifier of a Key Vault or Managed HSM key version.
    /// </summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> if the value is a key version identifier; otherwise, <see langword="false"/>.</returns>
    public static bool IsKeyVersionId([NotNullWhen(true)] string? value) =>
        value is not null && KeyVersionPattern().IsMatch(value);
}
