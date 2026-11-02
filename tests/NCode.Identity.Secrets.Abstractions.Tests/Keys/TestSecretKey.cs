// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Keys;

internal sealed class TestSecretKey(
    KeyMetadata metadata,
    int keySizeBits = 256,
    string keyType = "test"
) : SecretKey
{
    public override string KeyType { get; } = keyType;

    public override KeyMetadata Metadata { get; } = metadata;

    public override int KeySizeBits { get; } = keySizeBits;
}
