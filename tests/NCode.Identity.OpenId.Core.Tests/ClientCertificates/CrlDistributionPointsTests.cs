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

using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class CrlDistributionPointsTests
{
    #region GetUris Tests

    [Fact]
    public void GetUris_ReturnsTheHttpUris()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var certificate = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions
            {
                CrlDistributionPoints =
                [
                    "http://crl.example.com/a.crl",
                    "https://crl.example.com/b.crl",
                ],
            }
        );

        var result = CrlDistributionPoints.GetUris(certificate);

        Assert.Equal(["http://crl.example.com/a.crl", "https://crl.example.com/b.crl"], result);
    }

    [Fact]
    public void GetUris_IgnoresANonHttpUri()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var certificate = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = ["ldap://x/y"] }
        );

        var result = CrlDistributionPoints.GetUris(certificate);

        Assert.Empty(result);
    }

    [Fact]
    public void GetUris_WithoutTheExtension_ReturnsEmpty()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var certificate = TestCertificates.CreateClientCertificate(root, "CN=c");

        var result = CrlDistributionPoints.GetUris(certificate);

        Assert.Empty(result);
    }

    #endregion
}
