// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Keys;

internal sealed class TestSecretKey(KeyMetadata metadata, int keySizeBits = 256) : SecretKey
{
    public override string KeyType => "test";

    public override KeyMetadata Metadata { get; } = metadata;

    public override int KeySizeBits { get; } = keySizeBits;
}
