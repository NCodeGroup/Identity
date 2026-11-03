// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Buffers;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Core.Tests.Clients.Handlers;

/// <summary>
/// Provides a shared secret the client holds as a key, as a client issued a secret for <c>client_secret_jwt</c> does.
/// </summary>
internal sealed class FakeSymmetricSecretKey(string keyId, string secret) : SymmetricSecretKey
{
    private byte[] Secret { get; } = SecureEncoding.UTF8.GetBytes(secret);

    public override KeyMetadata Metadata { get; } = new() { KeyId = keyId };

    public override int KeySizeBits => Secret.Length * 8;

    public override void ExportPrivateKey<TWriter>(ref TWriter destination)
    {
        Secret.CopyTo(destination.GetSpan(Secret.Length));
        destination.Advance(Secret.Length);
    }
}
