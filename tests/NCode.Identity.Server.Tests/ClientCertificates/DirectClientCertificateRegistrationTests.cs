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

using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace NCode.Identity.Server.Tests.ClientCertificates;

public sealed class DirectClientCertificateRegistrationTests : BaseTests
{
    #region AddDirectClientCertificates Tests

    [Fact]
    public void AddDirectClientCertificates_WhenCalled_ConfiguresKestrel()
    {
        var serviceCollection = new ServiceCollection();

        var result = serviceCollection.AddDirectClientCertificates();

        Assert.Same(serviceCollection, result);
        Assert.Contains(
            serviceCollection,
            descriptor => descriptor.ServiceType == typeof(IConfigureOptions<KestrelServerOptions>)
        );
    }

    #endregion

    #region ConfigureHttpsDefaults Tests

    [Fact]
    public void ConfigureHttpsDefaults_WhenSelfSignedCertificatePresented_AcceptsIt()
    {
        // A self-signed certificate does not chain to the machine's trust store; the handshake still accepts it, so the
        // self_signed_tls_client_auth handler can match it to the client's registered key.
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=client", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddHours(1)
        );
        using var chain = new X509Chain();
        var httpsOptions = new HttpsConnectionAdapterOptions();

        DirectClientCertificateRegistration.ConfigureHttpsDefaults(httpsOptions);

        Assert.Equal(ClientCertificateMode.NoCertificate, httpsOptions.ClientCertificateMode);
        var validation = Assert.IsType<Func<X509Certificate2, X509Chain?, SslPolicyErrors, bool>>(
            httpsOptions.ClientCertificateValidation,
            exactMatch: false
        );
        Assert.True(validation(certificate, chain, SslPolicyErrors.RemoteCertificateChainErrors));
    }

    #endregion
}
