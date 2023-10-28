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

using NCode.Jose.SecretKeys;

namespace NCode.Jose.Tests.SecretKeys;

public class SecretKeyExpiresWhenComparerTests : BaseTests
{
    private Mock<SecretKey> MockSecretKeyLeft { get; }
    private Mock<SecretKey> MockSecretKeyRight { get; }

    public SecretKeyExpiresWhenComparerTests()
    {
        MockSecretKeyLeft = CreateStrictMock<SecretKey>();
        MockSecretKeyRight = CreateStrictMock<SecretKey>();
    }

    [Fact]
    public void Compare_ExpiresWhen_LeftIsNewer()
    {
        var expiresWhenLeft = DateTimeOffset.UtcNow;
        var expiresWhenRight = expiresWhenLeft.AddDays(-1);

        var metadataLeft = new KeyMetadata { ExpiresWhen = expiresWhenLeft };
        MockSecretKeyLeft
            .Setup(x => x.Metadata)
            .Returns(metadataLeft)
            .Verifiable();

        var metadataRight = new KeyMetadata { ExpiresWhen = expiresWhenRight };
        MockSecretKeyRight
            .Setup(x => x.Metadata)
            .Returns(metadataRight)
            .Verifiable();

        var result = SecretKeyExpiresWhenComparer.Singleton.Compare(
            MockSecretKeyLeft.Object,
            MockSecretKeyRight.Object);
        Assert.Equal(-1, result);
    }

    [Fact]
    public void Compare_ExpiresWhen_RightIsNewer()
    {
        var expiresWhenLeft = DateTimeOffset.UtcNow;
        var expiresWhenRight = expiresWhenLeft.AddDays(1);

        var metadataLeft = new KeyMetadata { ExpiresWhen = expiresWhenLeft };
        MockSecretKeyLeft
            .Setup(x => x.Metadata)
            .Returns(metadataLeft)
            .Verifiable();

        var metadataRight = new KeyMetadata { ExpiresWhen = expiresWhenRight };
        MockSecretKeyRight
            .Setup(x => x.Metadata)
            .Returns(metadataRight)
            .Verifiable();

        var result = SecretKeyExpiresWhenComparer.Singleton.Compare(
            MockSecretKeyLeft.Object,
            MockSecretKeyRight.Object);
        Assert.Equal(1, result);
    }
}
