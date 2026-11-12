// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides an <see cref="EccSecretKey"/> whose private half a key management service holds. The key keeps the
/// service's public half (as a <c>SubjectPublicKeyInfo</c>), so it exports and verifies like any public
/// elliptic-curve key, and it signs by asking the service through its <see cref="RemoteKeyBinding"/>. It agrees keys
/// by asking the service.
/// </summary>
// See ADR-1471.
// See ADR-1471.
internal sealed class RemoteEccSecretKey(
    KeyMetadata metadata,
    string? curveName,
    int curveSizeBits,
    ReadOnlyMemory<byte> subjectPublicKeyInfo,
    RemoteKeyBinding binding
) : EccSecretKey(metadata, curveName, curveSizeBits, certificateRawData: null), IRemoteSecretKey
{
    private ReadOnlyMemory<byte> SubjectPublicKeyInfo { get; } = subjectPublicKeyInfo;

    /// <inheritdoc />
    public RemoteKeyBinding Binding { get; } = binding;

    /// <inheritdoc />
    public override ECDsa ExportECDsa()
    {
        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportSubjectPublicKeyInfo(SubjectPublicKeyInfo.Span, out _);
            return ecdsa;
        }
        catch
        {
            ecdsa.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public override ECDiffieHellman ExportECDiffieHellman()
    {
        var ecDiffieHellman = ECDiffieHellman.Create();
        try
        {
            ecDiffieHellman.ImportSubjectPublicKeyInfo(SubjectPublicKeyInfo.Span, out _);
            return ecDiffieHellman;
        }
        catch
        {
            ecDiffieHellman.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask<int> SignAsync(
        string algorithm,
        ReadOnlyMemory<byte> signingInput,
        Memory<byte> signature,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(algorithm, Binding.Algorithm.Algorithm, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"The remote key is bound to the algorithm '{Binding.Algorithm.Algorithm}' and cannot sign with '{algorithm}'."
            );

        return RemoteKeySigning.SignAsync(Binding, signingInput, signature, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<int> DecryptAsync(
        string algorithm,
        ReadOnlyMemory<byte> ciphertext,
        Memory<byte> plaintext,
        CancellationToken cancellationToken
    ) => throw new InvalidOperationException("An elliptic-curve key does not decrypt.");

    /// <inheritdoc />
    public ValueTask<int> DeriveSharedSecretAsync(
        string algorithm,
        ReadOnlyMemory<byte> peerSubjectPublicKeyInfo,
        Memory<byte> sharedSecret,
        CancellationToken cancellationToken
    )
    {
        if (!string.Equals(algorithm, Binding.Algorithm.Algorithm, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"The remote key is bound to the algorithm '{Binding.Algorithm.Algorithm}' and cannot agree keys with '{algorithm}'."
            );

        return RemoteKeyDecryption.DeriveSharedSecretAsync(
            Binding,
            peerSubjectPublicKeyInfo,
            sharedSecret,
            cancellationToken
        );
    }

    /// <inheritdoc />
    protected override void AppendMembers(StringBuilder builder)
    {
        base.AppendMembers(builder);
        builder.Append(", Provider = '").Append(Binding.Reference.Provider).Append('\'');
    }
}
