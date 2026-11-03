// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Contains the descriptive metadata of a shared secret that <see cref="DefaultSecretGenerator"/> stores, taken from
/// a <see cref="GenerateSecretRequest"/> or an <see cref="ImportSharedSecretRequest"/>, so the stored shape is built
/// from one group of values that cannot be passed in the wrong order.
/// </summary>
/// <param name="SecretId">The natural identifier of the secret.</param>
/// <param name="Use">The intended use of the secret, or <see langword="null"/> for any compatible use.</param>
/// <param name="Algorithm">The intended algorithm of the secret, or <see langword="null"/> for any compatible
/// algorithm.</param>
/// <param name="CreatedWhen">When the secret is considered created.</param>
/// <param name="ExpiresWhen">When the secret expires.</param>
// See ADR-1110 and ADR-1391.
internal readonly record struct SharedSecretMetadata(
    string SecretId,
    string? Use,
    string? Algorithm,
    DateTimeOffset CreatedWhen,
    DateTimeOffset ExpiresWhen
)
{
    /// <summary>
    /// Gets the metadata of a shared secret to generate.
    /// </summary>
    /// <param name="request">The <see cref="GenerateSecretRequest"/> describing the secret.</param>
    /// <returns>The <see cref="SharedSecretMetadata"/> of the request.</returns>
    public static SharedSecretMetadata From(GenerateSecretRequest request) =>
        new(
            request.SecretId,
            request.Use,
            request.Algorithm,
            request.CreatedWhen,
            request.ExpiresWhen
        );

    /// <summary>
    /// Gets the metadata of a shared secret to import.
    /// </summary>
    /// <param name="request">The <see cref="ImportSharedSecretRequest"/> describing the secret.</param>
    /// <returns>The <see cref="SharedSecretMetadata"/> of the request.</returns>
    public static SharedSecretMetadata From(ImportSharedSecretRequest request) =>
        new(
            request.SecretId,
            request.Use,
            request.Algorithm,
            request.CreatedWhen,
            request.ExpiresWhen
        );
}
