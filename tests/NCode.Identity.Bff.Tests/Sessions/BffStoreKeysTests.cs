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
