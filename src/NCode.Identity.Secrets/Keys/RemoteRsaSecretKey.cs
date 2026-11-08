// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.RemoteKeys;

namespace NCode.Identity.Secrets.Keys;

/// <summary>
/// Provides an <see cref="RsaSecretKey"/> whose private half a key management service holds. The key keeps the
/// service's public half (as a <c>SubjectPublicKeyInfo</c>), so it exports and verifies like any public <c>RSA</c> key,
/// and it signs by asking the service through its <see cref="RemoteKeyBinding"/>.
/// </summary>
// See ADR-1471.
internal sealed class RemoteRsaSecretKey(
    KeyMetadata metadata,
    int modulusSizeBits,
    ReadOnlyMemory<byte> subjectPublicKeyInfo,
    RemoteKeyBinding binding
) : RsaSecretKey(metadata, modulusSizeBits, certificateRawData: null), IRemoteSecretKey
{
    private ReadOnlyMemory<byte> SubjectPublicKeyInfo { get; } = subjectPublicKeyInfo;

    /// <inheritdoc />
    public RemoteKeyBinding Binding { get; } = binding;

    /// <inheritdoc />
    public override RSA ExportRSA()
    {
        var rsa = RSA.Create();
        try
        {
            rsa.ImportSubjectPublicKeyInfo(SubjectPublicKeyInfo.Span, out _);
            return rsa;
        }
        catch
        {
            rsa.Dispose();
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
    protected override string FormatToString() =>
        $"{base.FormatToString()[..^2]}, Provider = '{Binding.Reference.Provider}' }}";
}
