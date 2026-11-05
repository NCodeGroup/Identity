// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Server.Tests;

/// <summary>
/// A hand-written <see cref="ISecretProtectorProvider"/> for tests of the key ring services, because a mock cannot set
/// up a method that takes a span. A value reads <c>k:&lt;key id&gt;|payload</c>; only the keys passed to the constructor
/// load.
/// </summary>
internal sealed class FakeRingProvider(params Guid[] loadableKeys) : ISecretProtectorProvider
{
    private HashSet<Guid> LoadableKeys { get; } = [.. loadableKeys];

    public List<(SecretOwner Owner, IReadOnlyCollection<string> Values)> Requests { get; } = [];

    public List<SecretOwner> Evicted { get; } = [];

    public int EvictAllCount { get; private set; }

    /// <summary>
    /// Gets or sets an exception that every <see cref="GetProtectorAsync"/> call throws, or <see langword="null"/>.
    /// </summary>
    public Exception? Failure { get; set; }

    /// <summary>
    /// Gets a semaphore that every <see cref="GetProtectorAsync"/> call releases once, before it throws or returns.
    /// </summary>
    public SemaphoreSlim Requested { get; } = new(0);

    public static string Value(Guid keyId, string payload = "payload") => $"k:{keyId:D}|{payload}";

    public bool TryReadMasterKeyId(ReadOnlySpan<char> protectedValue, out Guid masterKeyId)
    {
        masterKeyId = Guid.Empty;
        return protectedValue.StartsWith("k:", StringComparison.Ordinal)
            && protectedValue.Length >= 38
            && Guid.TryParse(protectedValue.Slice(2, 36), out masterKeyId);
    }

    public string GetMasterKeyIdPrefix(Guid masterKeyId) => $"k:{masterKeyId:D}";

    public ValueTask<ISecretProtector> GetProtectorAsync(
        SecretOwner owner,
        IReadOnlyCollection<string> protectedValues,
        CancellationToken cancellationToken
    )
    {
        Requests.Add((owner, protectedValues));
        Requested.Release();

        if (Failure is { } failure)
            throw failure;

        foreach (var value in protectedValues)
        {
            if (!TryReadMasterKeyId(value, out var keyId) || !LoadableKeys.Contains(keyId))
                throw new CryptographicException("The master key of the value is not available.");
        }

        return ValueTask.FromResult<ISecretProtector>(new Protector(owner));
    }

    public void Evict(SecretOwner owner) => Evicted.Add(owner);

    public void EvictAll() => EvictAllCount++;

    private sealed class Protector(SecretOwner owner) : ISecretProtector
    {
        public SecretOwner Owner { get; } = owner;

        public int GetProtectedLength(int plaintextLength) => throw new NotSupportedException();

        public int GetMaxPlaintextLength(int protectedLength) => throw new NotSupportedException();

        public void Protect<TWriter>(
            ReadOnlySpan<byte> plaintext,
            SecretProtectionContext context,
            ref TWriter destination
        )
            where TWriter : IBufferWriter<char>, allows ref struct =>
            throw new NotSupportedException();

        public void Unprotect<TWriter>(
            ReadOnlySpan<char> protectedValue,
            SecretProtectionContext context,
            ref TWriter destination
        )
            where TWriter : IBufferWriter<byte>, allows ref struct =>
            throw new NotSupportedException();

        public void Rewrap<TWriter>(
            ReadOnlySpan<char> protectedValue,
            SecretProtectionContext context,
            ref TWriter destination
        )
            where TWriter : IBufferWriter<char>, allows ref struct =>
            throw new NotSupportedException();
    }
}
