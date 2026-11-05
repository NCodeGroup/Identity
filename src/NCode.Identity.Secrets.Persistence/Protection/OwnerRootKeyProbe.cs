// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Validates a key that an owner names by wrapping a random value through it and unwrapping the result. Success proves
/// that the key exists, is enabled, and that the deployment's identity holds both the encrypt and the decrypt
/// permission, through any key wrapper and without a provider-specific call.
/// </summary>
/// <remarks>
/// The probe value is wrapped under its own context label, with the layout of the root context, so a probe ciphertext
/// can never be presented as a wrapped master key. Both the probe value and the unwrapped value are zeroed. Any failure
/// of the key wrapper, transient or definitive, is a failure of the probe, so the owner fails closed.
/// </remarks>
// See ADR-1411.
internal static class OwnerRootKeyProbe
{
    private const int ProbeSizeBytes = 32;

    /// <summary>
    /// Wraps and unwraps a random value through the named key.
    /// </summary>
    /// <param name="wrapper">The key wrapper.</param>
    /// <param name="keyReference">The reference to the key.</param>
    /// <param name="owner">The owner that names the key.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="null"/> when the key passes, otherwise the description of the failure.</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled.</exception>
    public static async ValueTask<string?> ProbeAsync(
        IReferencedKeyWrapper wrapper,
        string keyReference,
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(wrapper);
        ArgumentNullException.ThrowIfNull(keyReference);

        using var probeLease = BufferFactory.Rent(
            ProbeSizeBytes,
            isSensitive: true,
            out Memory<byte> probe
        );
        RandomNumberGenerator.Fill(probe.Span);

        using var contextLease = BufferFactory.Rent(
            SecretProtectionFormat.GetProbeContextSize(owner),
            isSensitive: false,
            out Memory<byte> contextBuffer
        );
        var contextWritten = SecretProtectionFormat.WriteProbeContext(
            Guid.NewGuid(),
            owner,
            contextBuffer.Span
        );
        var context = contextBuffer[..contextWritten];

        try
        {
            var wrapped = await wrapper.WrapAsync(keyReference, probe, context, cancellationToken);

            using var destination = BufferFactory.CreatePooledBufferWriter(
                isSensitive: true,
                ProbeSizeBytes
            );
            await wrapper.UnwrapAsync(wrapped, context, destination, cancellationToken);

            using var unwrapped = destination.GetSpanLease(isSensitive: true);

            return CryptographicOperations.FixedTimeEquals(probe.Span, unwrapped.Span)
                ? null
                : "the key unwrapped a value other than the one it wrapped";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return Truncate($"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static string Truncate(string failure) =>
        failure.Length <= PersistedOwnerRootKey.MaxFailureLength
            ? failure
            : failure[..PersistedOwnerRootKey.MaxFailureLength];
}
