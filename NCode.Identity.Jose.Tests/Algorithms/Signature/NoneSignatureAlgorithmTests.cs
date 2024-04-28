#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.Signature;
using NCode.Identity.Secrets;

namespace NCode.Jose.Tests.Algorithms.Signature;

public class NoneSignatureAlgorithmTests
{
    private static SignatureAlgorithm Algorithm => NoneSignatureAlgorithm.Singleton;

    [Fact]
    public void Code_Valid()
    {
        Assert.Equal("none", Algorithm.Code);
    }

    [Fact]
    public void KeyType_Valid()
    {
        Assert.Equal(typeof(SecretKey), Algorithm.KeyType);
    }

    [Fact]
    public void KeyBitSizes_Valid()
    {
        Assert.Empty(Algorithm.KeyBitSizes);
    }

    [Fact]
    public void SignatureSizeBits_Valid()
    {
        var keySizeBits = Random.Shared.Next();
        var result = Algorithm.GetSignatureSizeBytes(keySizeBits);
        Assert.Equal(0, result);
    }

    [Fact]
    public void TrySign_Valid()
    {
        var result = Algorithm.TrySign(null!, Span<byte>.Empty, Span<byte>.Empty, out var bytesWritten);
        Assert.True(result);
        Assert.Equal(0, bytesWritten);
    }
}
