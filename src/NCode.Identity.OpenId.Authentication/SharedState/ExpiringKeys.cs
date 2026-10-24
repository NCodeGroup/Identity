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
using System.Security.Cryptography;
using System.Text;
using NCode.Buffers;
using NCode.Encoders;

namespace NCode.Identity.OpenId.Authentication.SharedState;

// See ADR-0246.
/// <summary>
/// Builds the keys this library records in the shared <c>IExpiringKeySet</c>. A key is
/// <c>{purpose}:{tenant in braces}:{digest}</c>, where the digest is the base64url SHA-256 of the length-prefixed UTF-8
/// components. The purpose and the tenant keep one tenant's records, and one kind of record, from colliding with another
/// on a store every tenant shares. The braces make the tenant the key's hash tag, so a clustered store keeps one
/// tenant's keys on one node and one script may touch several of them. The length prefixes keep distinct component lists
/// distinct — <c>("a:b", "c")</c> and <c>("a", "b:c")</c> never meet — and the digest bounds the key's length, since the
/// components come from the request.
/// </summary>
internal static class ExpiringKeys
{
    /// <summary>
    /// Builds the key for a record of the given purpose, scoped to a tenant, from its identifying components.
    /// </summary>
    /// <param name="purpose">The kind of record, which never contains a colon.</param>
    /// <param name="tenantId">The identifier of the tenant the record belongs to.</param>
    /// <param name="components">The values that identify the record within its purpose and tenant.</param>
    /// <returns>The key to record in the shared expiring key set.</returns>
    public static string Create(
        string purpose,
        string tenantId,
        params ReadOnlySpan<string> components
    )
    {
        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: false);

        foreach (var component in components)
        {
            var length = Encoding.UTF8.GetByteCount(component);
            BinaryPrimitives.WriteInt32BigEndian(buffer.GetSpan(sizeof(int)), length);
            buffer.Advance(sizeof(int));
            Encoding.UTF8.GetBytes(component, buffer);
        }

        using var lease = buffer.GetSpanLease(isSensitive: false);
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(lease.Span, digest);

        var encodedDigest = Base64Url.Encode(digest);

        return $"{purpose}:{{{tenantId}}}:{encodedDigest}";
    }
}
