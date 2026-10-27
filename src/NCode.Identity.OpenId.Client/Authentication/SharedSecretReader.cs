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
using NCode.Buffers;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Reads the shared secret of a client from its key ring.
/// </summary>
internal static class SharedSecretReader
{
    /// <summary>
    /// Reads the first <see cref="SymmetricSecretKey"/> of the key ring of the specified client as a string.
    /// </summary>
    /// <param name="keyRingProvider">The <see cref="IClientKeyRingProvider"/> that provides the key ring.</param>
    /// <param name="registration">The client whose secret to read.</param>
    /// <exception cref="InvalidOperationException">The client has no key ring or the key ring has no symmetric key.</exception>
    public static string ReadSecret(
        IClientKeyRingProvider keyRingProvider,
        ClientRegistration registration
    )
    {
        var keyRing = registration.KeyRing;
        if (string.IsNullOrEmpty(keyRing))
            throw new InvalidOperationException(
                $"The client '{registration.ClientId}' has no key ring for its shared secret."
            );

        var secretKey =
            keyRingProvider.GetKeyRing(keyRing).OfType<SymmetricSecretKey>().FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"The key ring '{keyRing}' of the client '{registration.ClientId}' has no symmetric key."
            );

        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
        IBufferWriter<byte> writer = buffer;
        secretKey.ExportPrivateKey(ref writer);

        using var spanLease = buffer.GetSpanLease(isSensitive: true);
        return SecureEncoding.UTF8.GetString(spanLease.Span);
    }
}
