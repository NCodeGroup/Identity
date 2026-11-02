// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Numerics;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms;

/// <summary>
/// Computes the CRC32C (Castagnoli) checksum that Google Cloud KMS verifies on requests and reports on responses, so a
/// value corrupted in transit is detected.
/// </summary>
// See ADR-1260.
internal static class GoogleCloudKmsChecksum
{
    /// <summary>
    /// Computes the CRC32C (Castagnoli) checksum of the specified data.
    /// </summary>
    /// <param name="data">The data to checksum.</param>
    /// <returns>The CRC32C checksum of the data, as the unsigned 32-bit value Cloud KMS expects.</returns>
    public static long Compute(ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;

        while (data.Length >= sizeof(ulong))
        {
            crc = BitOperations.Crc32C(crc, BinaryPrimitives.ReadUInt64LittleEndian(data));
            data = data[sizeof(ulong)..];
        }

        foreach (var value in data)
        {
            crc = BitOperations.Crc32C(crc, value);
        }

        return ~crc;
    }
}
