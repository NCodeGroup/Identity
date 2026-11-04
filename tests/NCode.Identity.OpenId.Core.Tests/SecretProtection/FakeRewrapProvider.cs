// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Core.SecretProtection;

/// <summary>
/// A hand-written <see cref="ISecretProtectorProvider"/> for the value-source tests, because a mock cannot set up a
/// method that takes a span. A value reads <c>k:&lt;key id&gt;|payload</c>, and a rewrap swaps the key id for the active
/// one.
/// </summary>
internal sealed class FakeRewrapProvider(Guid activeKeyId) : ISecretProtectorProvider
{
    public Guid ActiveKeyId { get; } = activeKeyId;

    public List<IReadOnlyCollection<string>> Requests { get; } = [];

    public List<SecretProtectionContext> Contexts { get; } = [];

    /// <summary>
    /// Gets the payloads whose rewrap throws a <see cref="CryptographicException"/>.
    /// </summary>
    public HashSet<string> Unrewrappable { get; } = [];

    public static string Value(Guid keyId, string payload) => $"k:{keyId:D}|{payload}";

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
        Requests.Add(protectedValues);
        return ValueTask.FromResult<ISecretProtector>(
            new Protector(owner, ActiveKeyId, Contexts, Unrewrappable)
        );
    }

    public void Evict(SecretOwner owner) { }

    public void EvictAll() { }

    private sealed class Protector(
        SecretOwner owner,
        Guid activeKeyId,
        List<SecretProtectionContext> contexts,
        HashSet<string> unrewrappable
    ) : ISecretProtector
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
            where TWriter : IBufferWriter<char>, allows ref struct
        {
            if (unrewrappable.Contains(protectedValue[39..].ToString()))
                throw new CryptographicException("The value cannot be rewrapped.");

            // The context is recorded so that a test sees which context the source passed; the rewritten value keeps
            // the length of the original, as a real rewrap does.
            contexts.Add(context);
            var rewritten = $"k:{activeKeyId:D}{protectedValue[38..]}";
            var chars = destination.GetSpan(rewritten.Length);
            rewritten.CopyTo(chars);
            destination.Advance(rewritten.Length);
        }
    }
}
