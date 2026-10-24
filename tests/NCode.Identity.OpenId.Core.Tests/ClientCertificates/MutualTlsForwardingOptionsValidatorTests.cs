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

public sealed class MutualTlsForwardingOptionsValidatorTests
{
    private MutualTlsForwardingOptionsValidator Validator { get; } =
        new([
            new Base64DerClientCertificateHeaderDecoder(),
            new PemClientCertificateHeaderDecoder(),
        ]);

    #region Validate Tests

    [Fact]
    public void Validate_WhenNoHeaderConfigured_Succeeds()
    {
        var options = new MutualTlsForwardingOptions { Format = "Unregistered" };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenFormatRegistered_Succeeds()
    {
        var options = new MutualTlsForwardingOptions
        {
            CertificateHeaderName = "X-Client-Cert",
            Format = ClientCertificateHeaderFormats.Pem,
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenFormatNotRegistered_FailsNamingTheRegisteredFormats()
    {
        var options = new MutualTlsForwardingOptions
        {
            CertificateHeaderName = "X-Client-Cert",
            Format = "base64der",
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("'base64der'", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Base64Der, Pem", result.FailureMessage, StringComparison.Ordinal);
    }

    #endregion
}
