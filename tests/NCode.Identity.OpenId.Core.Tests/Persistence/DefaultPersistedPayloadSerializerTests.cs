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
using System.Text.Json.Nodes;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.Sessions;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Persistence;

public class DefaultPersistedPayloadSerializerTests
{
    private JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web);

    #region Serialize Tests

    [Fact]
    public void Serialize_WhenNoVersionAttribute_WrapsInEnvelopeAtVersionOne()
    {
        var serializer = new DefaultPersistedPayloadSerializer([]);

        var element = serializer.Serialize(new SimplePayload { Name = "abc" }, JsonOptions);

        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Equal(1, element.GetProperty("$ver").GetInt32());
        Assert.Equal("abc", element.GetProperty("$payload").GetProperty("name").GetString());
    }

    [Fact]
    public void Serialize_WhenVersionAttributePresent_StampsDeclaredVersion()
    {
        var serializer = new DefaultPersistedPayloadSerializer([]);

        var element = serializer.Serialize(new VersionedPayload { Value = "x" }, JsonOptions);

        Assert.Equal(3, element.GetProperty("$ver").GetInt32());
    }

    #endregion

    #region Deserialize Tests

    [Fact]
    public void Deserialize_WhenRoundTripped_ReturnsEquivalentPayload()
    {
        var serializer = new DefaultPersistedPayloadSerializer([]);

        var element = serializer.Serialize(
            new SimplePayload { Name = "abc", Count = 5 },
            JsonOptions
        );
        var result = serializer.Deserialize<SimplePayload>(element, JsonOptions);

        Assert.Equal("abc", result.Name);
        Assert.Equal(5, result.Count);
    }

    [Fact]
    public void Deserialize_WhenDocumentLacksEnvelope_ReadsAsVersionOne()
    {
        var serializer = new DefaultPersistedPayloadSerializer([]);
        var legacy = JsonSerializer.SerializeToElement(new { name = "abc" }, JsonOptions);

        var result = serializer.Deserialize<SimplePayload>(legacy, JsonOptions);

        Assert.Equal("abc", result.Name);
    }

    [Fact]
    public void Deserialize_WhenStoredVersionOlder_AppliesUpcasterChain()
    {
        var serializer = new DefaultPersistedPayloadSerializer([
            new RenamePropertyUpcaster(typeof(VersionedPayload), 1, "v1name", "v2name"),
            new RenamePropertyUpcaster(typeof(VersionedPayload), 2, "v2name", "value"),
        ]);
        var stored = ParseDocument(
            """
            { "$ver": 1, "$payload": { "v1name": "x" } }
            """
        );

        var result = serializer.Deserialize<VersionedPayload>(stored, JsonOptions);

        Assert.Equal("x", result.Value);
    }

    [Fact]
    public void Deserialize_WhenUpcasterStepMissing_Throws()
    {
        var serializer = new DefaultPersistedPayloadSerializer([
            new RenamePropertyUpcaster(typeof(VersionedPayload), 1, "v1name", "v2name"),
        ]);
        var stored = ParseDocument(
            """
            { "$ver": 1, "$payload": { "v1name": "x" } }
            """
        );

        Assert.Throws<JsonException>(() =>
            serializer.Deserialize<VersionedPayload>(stored, JsonOptions)
        );
    }

    [Fact]
    public void Deserialize_WhenStoredVersionNewer_IgnoresUnknownMembers()
    {
        var serializer = new DefaultPersistedPayloadSerializer([]);
        var stored = ParseDocument(
            """
            { "$ver": 5, "$payload": { "name": "abc", "futureField": 123 } }
            """
        );

        var result = serializer.Deserialize<SimplePayload>(stored, JsonOptions);

        Assert.Equal("abc", result.Name);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenDuplicateUpcasterStep_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DefaultPersistedPayloadSerializer([
                new RenamePropertyUpcaster(typeof(VersionedPayload), 1, "a", "b"),
                new RenamePropertyUpcaster(typeof(VersionedPayload), 1, "c", "d"),
            ])
        );
    }

    #endregion

    #region Extension Data Tests

    [Fact]
    public void Deserialize_WhenStructPayloadHasUnknownMember_PreservesItThroughRewrite()
    {
        var serializer = new DefaultPersistedPayloadSerializer([]);
        var stored = ParseDocument(
            """
            {
                "$ver": 1,
                "$payload": {
                    "authTime": "2026-01-01T00:00:00+00:00",
                    "clientIds": ["client-1"],
                    "lastActivityWhen": "2026-01-01T00:00:00+00:00",
                    "futureMember": "keep-me"
                }
            }
            """
        );

        var session = serializer.Deserialize<SessionGrant>(stored, JsonOptions);

        Assert.NotNull(session.ExtensionData);
        Assert.True(session.ExtensionData!.ContainsKey("futureMember"));

        var rewritten = serializer.Serialize(session, JsonOptions);
        Assert.Equal(
            "keep-me",
            rewritten.GetProperty("$payload").GetProperty("futureMember").GetString()
        );
    }

    #endregion

    private static JsonElement ParseDocument(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    private sealed record SimplePayload
    {
        public required string Name { get; init; }
        public int? Count { get; init; }
    }

    [PersistedPayloadVersion(3)]
    private sealed record VersionedPayload
    {
        public required string Value { get; init; }
    }

    private sealed class RenamePropertyUpcaster(
        Type payloadType,
        int fromVersion,
        string from,
        string to
    ) : IPersistedPayloadUpcaster
    {
        public Type PayloadType => payloadType;
        public int FromVersion => fromVersion;

        public JsonNode? Upcast(JsonNode? payload)
        {
            var obj = payload!.AsObject();
            if (obj.Remove(from, out var node))
            {
                obj[to] = node;
            }

            return obj;
        }
    }
}
