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

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Provides the default implementation of <see cref="IClientCertificateRevocationChecker"/>. It asks each registered
/// <see cref="IClientCertificateRevocationSource"/> in registration order and returns the first answer that is not
/// <see cref="ClientCertificateRevocationStatus.Unknown"/>; the status is
/// <see cref="ClientCertificateRevocationStatus.Unknown"/> when every source is inconclusive or none is registered.
/// </summary>
// See ADR-0259.
internal sealed class DefaultClientCertificateRevocationChecker(
    IEnumerable<IClientCertificateRevocationSource> sources
) : IClientCertificateRevocationChecker
{
    private IReadOnlyList<IClientCertificateRevocationSource> Sources { get; } = [.. sources];

    /// <inheritdoc />
    public async ValueTask<ClientCertificateRevocationStatus> CheckAsync(
        ClientCertificateRevocationContext context,
        CancellationToken cancellationToken
    )
    {
        foreach (var source in Sources)
        {
            var status = await source.CheckAsync(context, cancellationToken);
            if (status != ClientCertificateRevocationStatus.Unknown)
                return status;
        }

        return ClientCertificateRevocationStatus.Unknown;
    }
}
