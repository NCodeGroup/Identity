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

namespace NCode.Identity.Secrets;

public class SecretEncodingsTests
{
    #region IsPublic Tests

    [Fact]
    public void IsPublic_Spki_ReturnsTrue()
    {
        Assert.True(SecretEncodings.IsPublic(SecretEncodings.Spki));
    }

    [Fact]
    public void IsPublic_X509_ReturnsTrue()
    {
        Assert.True(SecretEncodings.IsPublic(SecretEncodings.X509));
    }

    [Fact]
    public void IsPublic_Pkcs8_ReturnsFalse()
    {
        Assert.False(SecretEncodings.IsPublic(SecretEncodings.Pkcs8));
    }

    [Fact]
    public void IsPublic_SymmetricRaw_ReturnsFalse()
    {
        Assert.False(SecretEncodings.IsPublic(SecretEncodings.SymmetricRaw));
    }

    [Fact]
    public void IsPublic_Unknown_ReturnsFalse()
    {
        Assert.False(SecretEncodings.IsPublic("unknown"));
    }

    [Fact]
    public void IsPublic_DifferentCase_ReturnsFalse()
    {
        Assert.False(SecretEncodings.IsPublic("SPKI"));
        Assert.False(SecretEncodings.IsPublic("X509"));
    }

    [Fact]
    public void IsPublic_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SecretEncodings.IsPublic(null!));
    }

    #endregion
}
