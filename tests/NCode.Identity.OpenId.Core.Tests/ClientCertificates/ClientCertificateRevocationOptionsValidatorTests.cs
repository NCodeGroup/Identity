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

public sealed class ClientCertificateRevocationOptionsValidatorTests
{
    private ClientCertificateRevocationOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenTheDefaultsAreKept_Succeeds()
    {
        var options = new ClientCertificateRevocationOptions();

        var result = Validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheFetchTimeoutIsZero_FailsNamingItsKey()
    {
        var options = new ClientCertificateRevocationOptions { FetchTimeout = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'ClientCertificateRevocation:FetchTimeout'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheMaxResponseSizeIsZero_FailsNamingItsKey()
    {
        var options = new ClientCertificateRevocationOptions { MaxResponseSizeBytes = 0 };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'ClientCertificateRevocation:MaxResponseSizeBytes'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheMaxCacheDurationIsZero_FailsNamingItsKey()
    {
        var options = new ClientCertificateRevocationOptions { MaxCacheDuration = TimeSpan.Zero };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'ClientCertificateRevocation:MaxCacheDuration'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Validate_WhenTheFailureCacheDurationIsZero_FailsNamingItsKey()
    {
        var options = new ClientCertificateRevocationOptions
        {
            FailureCacheDuration = TimeSpan.Zero,
        };

        var result = Validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(
            "'ClientCertificateRevocation:FailureCacheDuration'",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
