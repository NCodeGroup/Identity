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
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Provides the default implementation of <see cref="IGrantPayloadProtector"/> that protects the payload of a grant
/// under the master key of its tenant.
/// </summary>
/// <remarks>
/// The plaintext is written to and read from sensitive buffers and never becomes a string.
/// </remarks>
/// <param name="secretProtectorProvider">The provider of the protector of a tenant.</param>
/// <param name="payloadSerializer">The schema-versioned serializer of payloads.</param>
/// <param name="logger">The logger.</param>
// See ADR-0265.
internal sealed class DefaultGrantPayloadProtector(
    ISecretProtectorProvider secretProtectorProvider,
    IPersistedPayloadSerializer payloadSerializer,
    ILogger<DefaultGrantPayloadProtector> logger
) : IGrantPayloadProtector
{
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private IPersistedPayloadSerializer PayloadSerializer { get; } = payloadSerializer;
    private ILogger<DefaultGrantPayloadProtector> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<string> ProtectAsync<TPayload>(
        string tenantId,
        string hashedKey,
        TPayload payload,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken
    )
    {
        var element = PayloadSerializer.Serialize(payload, jsonOptions);

        using var buffer = BufferFactory.CreatePooledBufferWriter(isSensitive: true);

        using (var writer = new Utf8JsonWriter(buffer))
        {
            element.WriteTo(writer);
        }

        var protector = await SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForTenant(tenantId),
            [],
            cancellationToken
        );

        using var lease = buffer.GetSpanLease(isSensitive: true);

        return protector.ProtectToString(
            lease.Span,
            SecretProtectionContext.ForGrantPayload(hashedKey)
        );
    }

    /// <inheritdoc />
    public async ValueTask<GrantPayload<TPayload>?> UnprotectOrDefaultAsync<TPayload>(
        string tenantId,
        string grantType,
        string hashedKey,
        string protectedPayload,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var protector = await SecretProtectorProvider.GetProtectorAsync(
                SecretOwner.ForTenant(tenantId),
                protectedValues: [protectedPayload],
                cancellationToken
            );

            using var plaintext = BufferFactory.CreatePooledBufferWriter(isSensitive: true);
            IBufferWriter<byte> writer = plaintext;

            protector.Unprotect(
                protectedPayload,
                SecretProtectionContext.ForGrantPayload(hashedKey),
                ref writer
            );

            using var lease = plaintext.GetSpanLease(isSensitive: true);

            var reader = new Utf8JsonReader(lease.Span);
            var element = JsonElement.ParseValue(ref reader);

            return new GrantPayload<TPayload>(
                PayloadSerializer.Deserialize<TPayload>(element, jsonOptions)
            );
        }
        catch (CryptographicException exception)
        {
            Logger.GrantPayloadUnreadable(exception, tenantId, grantType);
            return null;
        }
    }
}
