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
