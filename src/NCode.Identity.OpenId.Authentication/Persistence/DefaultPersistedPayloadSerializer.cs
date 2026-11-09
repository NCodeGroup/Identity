// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Json;

namespace NCode.Identity.OpenId.Authentication.Persistence;

/// <summary>
/// Provides the default implementation of <see cref="IPersistedPayloadSerializer"/> that wraps every persisted-grant
/// payload in a self-describing, schema-versioned envelope and migrates older stored documents up to the current schema
/// version through registered <see cref="IPersistedPayloadUpcaster"/> steps.
/// </summary>
internal sealed class DefaultPersistedPayloadSerializer : IPersistedPayloadSerializer
{
    // The '$' sentinel reserves these envelope keys; payload members must never begin with '$'.
    internal const string VersionPropertyName = "$ver";
    internal const string PayloadPropertyName = "$payload";

    private const int DefaultVersion = 1;

    private static ConcurrentDictionary<Type, int> CurrentVersionCache { get; } = new();

    // Keyed by (payload type, from-version); each entry migrates that one version step.
    private FrozenDictionary<
        (Type PayloadType, int FromVersion),
        IPersistedPayloadUpcaster
    > Upcasters { get; }

    public DefaultPersistedPayloadSerializer(IEnumerable<IPersistedPayloadUpcaster> upcasters)
    {
        var map = new Dictionary<(Type, int), IPersistedPayloadUpcaster>();
        foreach (var upcaster in upcasters)
        {
            var key = (upcaster.PayloadType, upcaster.FromVersion);
            if (!map.TryAdd(key, upcaster))
            {
                throw new InvalidOperationException(
                    $"More than one {nameof(IPersistedPayloadUpcaster)} is registered to migrate payload "
                        + $"'{upcaster.PayloadType}' from schema version {upcaster.FromVersion}."
                );
            }
        }

        Upcasters = map.ToFrozenDictionary();
    }

    /// <inheritdoc />
    public JsonElement Serialize<TPayload>(TPayload payload, JsonSerializerOptions jsonOptions)
    {
        var version = GetCurrentVersion(typeof(TPayload));
        var payloadNode = JsonSerializer.SerializeToNode(payload, jsonOptions);

        var envelope = new JsonObject
        {
            [VersionPropertyName] = version,
            [PayloadPropertyName] = payloadNode,
        };

        return JsonSerializer.SerializeToElement(envelope);
    }

    /// <inheritdoc />
    public TPayload Deserialize<TPayload>(
        JsonElement jsonElement,
        JsonSerializerOptions jsonOptions
    )
    {
        if (
            jsonElement.ValueKind != JsonValueKind.Object
            || !jsonElement.TryGetPropertyValue<int>(VersionPropertyName, out var storedVersion)
            || !jsonElement.TryGetProperty(PayloadPropertyName, out var payloadProperty)
        )
        {
            throw new JsonException(
                $"The persisted payload of type '{typeof(TPayload)}' is not a schema-versioned envelope: it must be "
                    + $"an object with an integer '{VersionPropertyName}' and a '{PayloadPropertyName}'."
            );
        }

        var payloadNode = JsonSerializer.SerializeToNode(payloadProperty, jsonOptions);
        var currentVersion = GetCurrentVersion(typeof(TPayload));
        payloadNode = Upcast(typeof(TPayload), payloadNode, storedVersion, currentVersion);

        var payload = payloadNode.Deserialize<TPayload>(jsonOptions);
        if (payload is null)
        {
            throw new JsonException(
                $"The persisted payload of type '{typeof(TPayload)}' deserialized to null."
            );
        }

        return payload;
    }

    private JsonNode? Upcast(
        Type payloadType,
        JsonNode? payloadNode,
        int fromVersion,
        int toVersion
    )
    {
        // A stored version newer than the current version (a rollback) is read forward-compatibly: the newer members
        // are simply ignored, so no upcasting is performed in that direction.
        for (var version = fromVersion; version < toVersion; version++)
        {
            if (!Upcasters.TryGetValue((payloadType, version), out var upcaster))
            {
                throw new JsonException(
                    $"No {nameof(IPersistedPayloadUpcaster)} is registered to migrate payload '{payloadType}' "
                        + $"from schema version {version} to {version + 1}."
                );
            }

            payloadNode = upcaster.Upcast(payloadNode);
        }

        return payloadNode;
    }

    private static int GetCurrentVersion(Type payloadType) =>
        CurrentVersionCache.GetOrAdd(
            payloadType,
            static type =>
                type.GetCustomAttribute<PersistedPayloadVersionAttribute>()?.Version
                ?? DefaultVersion
        );
}
