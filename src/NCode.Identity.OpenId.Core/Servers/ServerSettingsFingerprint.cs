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
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Computes the fingerprint of a server's effective settings: the Base64Url encoding of a SHA-256 hash over each
/// setting's length-prefixed UTF-8 name and length-prefixed JSON value. The settings are hashed in ordinal order of
/// their names, and a setting whose value is a sequence of strings is hashed with its items in ordinal order, because
/// every rule that reads such a list reads it as a set. Two collections with the same settings and values therefore
/// have the same fingerprint, whatever order they enumerate in.
/// </summary>
// See ADR-0219.
internal static class ServerSettingsFingerprint
{
    /// <summary>
    /// Computes the fingerprint of the specified <paramref name="settings"/>.
    /// </summary>
    /// <param name="settings">The effective settings to fingerprint.</param>
    /// <param name="jsonOptions">The JSON options that serialize each setting's value.</param>
    /// <returns>The Base64Url encoding of the SHA-256 hash.</returns>
    public static string Compute(IEnumerable<Setting> settings, JsonSerializerOptions jsonOptions)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (
            var setting in settings.OrderBy(
                setting => setting.Descriptor.Name,
                StringComparer.Ordinal
            )
        )
        {
            var name = SecureEncoding.UTF8.GetBytes(setting.Descriptor.Name);
            AppendLengthPrefixed(hash, name);

            // A list is a set for every rule that reads it, so its order does not change the fingerprint.
            var value = setting.GetValue();
            if (value is IEnumerable<string> list)
            {
                value = list.Order(StringComparer.Ordinal).ToArray();
            }

            var json = JsonSerializer.SerializeToUtf8Bytes(value, value.GetType(), jsonOptions);
            AppendLengthPrefixed(hash, json);
        }

        var digest = hash.GetHashAndReset();
        return Base64Url.EncodeToString(digest);
    }

    private static void AppendLengthPrefixed(IncrementalHash hash, ReadOnlySpan<byte> bytes)
    {
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
