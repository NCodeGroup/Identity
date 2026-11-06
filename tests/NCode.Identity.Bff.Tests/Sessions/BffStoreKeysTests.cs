// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Binary;
using System.Security.Cryptography;
using NCode.Buffers;
using NCode.Encoders;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public class BffStoreKeysTests : BaseTests
{
    private static void AddLengthPrefixed(List<byte> layout, string value)
    {
        var bytes = SecureEncoding.UTF8.GetBytes(value);
        var length = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        layout.AddRange(length);
        layout.AddRange(bytes);
    }

    #region SubjectIndex Tests

    [Fact]
    public void SubjectIndex_IssuerAndSubject_IsLengthPrefixedSha256()
    {
        const string issuer = "https://issuer.example";
        const string subject = "sub-1";

        var layout = new List<byte>();
        AddLengthPrefixed(layout, issuer);
        AddLengthPrefixed(layout, subject);
        var expected = "bff:sub:" + Base64Url.Encode(SHA256.HashData(layout.ToArray()));

        var result = BffStoreKeys.SubjectIndex(issuer, subject);

        Assert.Equal(expected, result);
    }

    #endregion

    #region ClientIndex Tests

    [Fact]
    public void ClientIndex_SameClient_IsStable()
    {
        var first = BffStoreKeys.ClientIndex(
            new BffSessionClient("web", "https://op.example", "client-1")
        );
        var second = BffStoreKeys.ClientIndex(
            new BffSessionClient("web", "https://op.example", "client-1")
        );

        Assert.Equal(first, second);
        Assert.StartsWith("bff:client:", first, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("other", "https://op.example", "client-1")]
    [InlineData("web", "https://other.example", "client-1")]
    [InlineData("web", "https://op.example", "client-2")]
    public void ClientIndex_AnyPartDiffers_Differs(
        string frontend,
        string authority,
        string clientId
    )
    {
        var baseline = BffStoreKeys.ClientIndex(
            new BffSessionClient("web", "https://op.example", "client-1")
        );

        var result = BffStoreKeys.ClientIndex(new BffSessionClient(frontend, authority, clientId));

        Assert.NotEqual(baseline, result);
    }

    #endregion

    #region ClientMember Tests

    [Fact]
    public void ClientMember_Always_RoundTripsThroughTryParseClientMember()
    {
        var client = new BffSessionClient("web", "https://op.example/a|b%7C", "client|id%");
        var absoluteExpiresAt = new DateTimeOffset(2030, 5, 6, 7, 8, 9, TimeSpan.Zero);

        var member = BffStoreKeys.ClientMember(client, absoluteExpiresAt);
        var parsed = BffStoreKeys.TryParseClientMember(member, out var result);

        Assert.True(parsed);
        Assert.Equal(client, result);
        Assert.StartsWith("2030-05-06|web|", member, StringComparison.Ordinal);
    }

    #endregion

    #region ClientMemberExpiresAt Tests

    [Fact]
    public void ClientMemberExpiresAt_Always_IsTheEndOfTheUtcDay()
    {
        var absoluteExpiresAt = new DateTimeOffset(2030, 5, 6, 23, 59, 59, TimeSpan.FromHours(-7));

        var result = BffStoreKeys.ClientMemberExpiresAt(absoluteExpiresAt);

        Assert.Equal(new DateTimeOffset(2030, 5, 8, 0, 0, 0, TimeSpan.Zero), result);
    }

    #endregion

    #region TryParseClientMember Tests

    [Theory]
    [InlineData("")]
    [InlineData("2030-05-06|web|authority")]
    [InlineData("2030-05-06|web|authority|client|extra")]
    public void TryParseClientMember_Malformed_ReturnsFalse(string member)
    {
        var result = BffStoreKeys.TryParseClientMember(member, out var client);

        Assert.False(result);
        Assert.Equal(default, client);
    }

    #endregion

    #region SessionIdIndex Tests

    [Fact]
    public void SessionIdIndex_SwappedValues_Differ()
    {
        var first = BffStoreKeys.SessionIdIndex("ab", "c");
        var second = BffStoreKeys.SessionIdIndex("a", "bc");

        Assert.NotEqual(first, second);
    }

    #endregion
}
