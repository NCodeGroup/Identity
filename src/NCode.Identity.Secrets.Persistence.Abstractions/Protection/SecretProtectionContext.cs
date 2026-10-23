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

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Identifies what a protected value is for by the purpose of the value and the identifier of the item that the
/// value belongs to. A value can only be unprotected with the same context that it was protected with.
/// </summary>
// See ADR-0235.
[PublicAPI]
public readonly record struct SecretProtectionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SecretProtectionContext"/> struct.
    /// </summary>
    /// <param name="purpose">The purpose of the value, such as one of the values in <see cref="SecretProtectionPurposes"/>.</param>
    /// <param name="itemId">The identifier of the item that the value belongs to.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="purpose"/> or <paramref name="itemId"/> is <see langword="null"/>, empty or
    /// whitespace.
    /// </exception>
    public SecretProtectionContext(string purpose, string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);

        Purpose = purpose;
        ItemId = itemId;
    }

    /// <summary>
    /// Gets the purpose of the value, such as one of the values in <see cref="SecretProtectionPurposes"/>.
    /// </summary>
    public string Purpose { get; }

    /// <summary>
    /// Gets the identifier of the item that the value belongs to.
    /// </summary>
    public string ItemId { get; }

    /// <summary>
    /// Creates the context for the private or symmetric key material of a single persisted secret.
    /// </summary>
    /// <param name="secretId">The identifier of the persisted secret.</param>
    /// <returns>The <see cref="SecretProtectionContext"/> for the persisted secret.</returns>
    public static SecretProtectionContext ForPersistedSecret(string secretId) =>
        new(SecretProtectionPurposes.PersistedSecret, secretId);
}
