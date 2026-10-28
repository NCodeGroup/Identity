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

using System.Buffers.Binary;
using System.Numerics;

namespace NCode.Identity.Secrets.KeyWrapping.GoogleCloudKms;

/// <summary>
/// Computes the CRC32C (Castagnoli) checksum that Google Cloud KMS verifies on requests and reports on responses, so a
/// value corrupted in transit is detected.
/// </summary>
// See ADR-0260.
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
