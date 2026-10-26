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

using System.Buffers;
using System.Security.Cryptography;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Core.Tests.Persistence;

/// <summary>
/// A reversible stand-in for <see cref="ISecretProtector"/> that records the context of each protected value and
/// refuses to unprotect a value for any other context.
/// </summary>
internal sealed class FakeSecretProtector(SecretOwner owner) : ISecretProtector
{
    private const string Prefix = "fake.";

    public SecretOwner Owner { get; } = owner;

    public Dictionary<string, SecretProtectionContext> Contexts { get; } = new();

    public int GetProtectedLength(int plaintextLength) => 5 + 4 * ((plaintextLength + 2) / 3);

    public int GetMaxPlaintextLength(int protectedLength) =>
        protectedLength < 5 ? 0 : (protectedLength - 5) / 4 * 3;

    public void Protect<TWriter>(
        ReadOnlySpan<byte> plaintext,
        SecretProtectionContext context,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<char>, allows ref struct
    {
        var value = Prefix + Convert.ToBase64String(plaintext);
        Contexts[value] = context;

        var span = destination.GetSpan(value.Length);
        value.CopyTo(span);
        destination.Advance(value.Length);
    }

    public void Unprotect<TWriter>(
        ReadOnlySpan<char> protectedValue,
        SecretProtectionContext context,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var key = protectedValue.ToString();
        if (!Contexts.TryGetValue(key, out var recorded) || recorded != context)
            throw new CryptographicException("The value was not protected for this context.");

        var bytes = Convert.FromBase64String(key[Prefix.Length..]);
        var span = destination.GetSpan(bytes.Length);
        bytes.CopyTo(span);
        destination.Advance(bytes.Length);
    }
}
