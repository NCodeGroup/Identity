// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.OpenId.Authentication.Persistence;

namespace NCode.Identity.OpenId.Core.Tests.Persistence;

/// <summary>
/// A stand-in for <see cref="IGrantPayloadProtector"/> that keeps the serialized payload readable behind a prefix.
/// </summary>
internal sealed class TestGrantPayloadProtector : IGrantPayloadProtector
{
    private const string Prefix = "test:";

    private static DefaultPersistedPayloadSerializer Serializer { get; } = new([]);

    public static string Protect<TPayload>(TPayload payload, JsonSerializerOptions jsonOptions) =>
        Prefix + Serializer.Serialize(payload, jsonOptions).GetRawText();

    /// <inheritdoc />
    public ValueTask<string> ProtectAsync<TPayload>(
        string tenantId,
        string hashedKey,
        TPayload payload,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(Protect(payload, jsonOptions));

    /// <inheritdoc />
    public ValueTask<GrantPayload<TPayload>?> UnprotectOrDefaultAsync<TPayload>(
        string tenantId,
        string grantType,
        string hashedKey,
        string protectedPayload,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken
    )
    {
        if (!protectedPayload.StartsWith(Prefix, StringComparison.Ordinal))
            return ValueTask.FromResult<GrantPayload<TPayload>?>(null);

        var element = JsonElement.Parse(protectedPayload[Prefix.Length..]);
        var payload = Serializer.Deserialize<TPayload>(element, jsonOptions);
        return ValueTask.FromResult<GrantPayload<TPayload>?>(new GrantPayload<TPayload>(payload));
    }
}
