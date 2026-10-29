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
