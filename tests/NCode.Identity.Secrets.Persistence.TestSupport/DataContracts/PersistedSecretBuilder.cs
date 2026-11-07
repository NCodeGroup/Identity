// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Builds a <see cref="PersistedSecret"/> for tests. Each property holds a value a test may care about; the defaults
/// describe a generated 256-bit symmetric key active from the Unix epoch for one year.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record PersistedSecretBuilder
{
    /// <summary>
    /// Gets the identifier of the secret. Defaults to <c>secret-1</c>.
    /// </summary>
    public string SecretId { get; init; } = "secret-1";

    /// <summary>
    /// Gets the key identifier of the secret. Defaults to <see langword="null"/>.
    /// </summary>
    public string? KeyId { get; init; }

    /// <summary>
    /// Gets the concurrency token of the secret. Defaults to an empty string.
    /// </summary>
    public string ConcurrencyToken { get; init; } = string.Empty;

    /// <summary>
    /// Gets the intended use of the secret. Defaults to <see langword="null"/>.
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm of the secret. Defaults to <see langword="null"/>.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets when the secret was created. Defaults to <see cref="DateTimeOffset.UnixEpoch"/>.
    /// </summary>
    public DateTimeOffset CreatedWhen { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets when the secret's active window opens. Defaults to <see cref="DateTimeOffset.UnixEpoch"/>.
    /// </summary>
    public DateTimeOffset ActivatesWhen { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Gets when the secret's active window closes. Defaults to one year after <see cref="DateTimeOffset.UnixEpoch"/>.
    /// </summary>
    public DateTimeOffset RetiresWhen { get; init; } = DateTimeOffset.UnixEpoch.AddYears(1);

    /// <summary>
    /// Gets when the secret expires. Defaults to one year after <see cref="DateTimeOffset.UnixEpoch"/>.
    /// </summary>
    public DateTimeOffset ExpiresWhen { get; init; } = DateTimeOffset.UnixEpoch.AddYears(1);

    /// <summary>
    /// Gets the type of the secret. Defaults to <see cref="SecretTypes.Symmetric"/>.
    /// </summary>
    public string SecretType { get; init; } = SecretTypes.Symmetric;

    /// <summary>
    /// Gets how the stored key material is encoded. Defaults to <see cref="SecretEncodings.SymmetricRaw"/>.
    /// </summary>
    public string SecretEncoding { get; init; } = SecretEncodings.SymmetricRaw;

    /// <summary>
    /// Gets where the secret's material came from. Defaults to <see cref="SecretOrigins.Generated"/>.
    /// </summary>
    public string Origin { get; init; } = SecretOrigins.Generated;

    /// <summary>
    /// Gets the name of the component that manages the secret's lifecycle. Defaults to <see langword="null"/>.
    /// </summary>
    public string? ManagedBy { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key material. Defaults to <c>256</c>.
    /// </summary>
    public int KeySizeBits { get; init; } = 256;

    /// <summary>
    /// Gets the stored value of the secret. Defaults to <c>AA</c>.
    /// </summary>
    public string EncodedValue { get; init; } = "AA";

    /// <summary>
    /// Gets the base64url-encoded public key of a private asymmetric key. Defaults to <see langword="null"/>.
    /// </summary>
    public string? PublicKey { get; init; }

    /// <summary>
    /// Builds the <see cref="PersistedSecret"/>.
    /// </summary>
    /// <returns>A new <see cref="PersistedSecret"/> that carries every property of this builder.</returns>
    public PersistedSecret Build() =>
        new()
        {
            SecretId = SecretId,
            KeyId = KeyId,
            ConcurrencyToken = ConcurrencyToken,
            Use = Use,
            Algorithm = Algorithm,
            CreatedWhen = CreatedWhen,
            ActivatesWhen = ActivatesWhen,
            RetiresWhen = RetiresWhen,
            ExpiresWhen = ExpiresWhen,
            SecretType = SecretType,
            SecretEncoding = SecretEncoding,
            Origin = Origin,
            ManagedBy = ManagedBy,
            KeySizeBits = KeySizeBits,
            EncodedValue = EncodedValue,
            PublicKey = PublicKey,
        };
}
