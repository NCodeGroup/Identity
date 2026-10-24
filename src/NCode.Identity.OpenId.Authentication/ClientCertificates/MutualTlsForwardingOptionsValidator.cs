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

using System.Collections.Frozen;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Validates <see cref="MutualTlsForwardingOptions"/> when the host starts: a forwarded header must name a format some
/// registered <see cref="IClientCertificateHeaderDecoder"/> reads, so a misspelled or unregistered format stops the host
/// instead of failing every request that carries a certificate.
/// </summary>
// See ADR-0137 (fail at startup) and ADR-0172 (formats are registered decoders).
internal class MutualTlsForwardingOptionsValidator(
    IEnumerable<IClientCertificateHeaderDecoder> decoders
) : IValidateOptions<MutualTlsForwardingOptions>
{
    private FrozenSet<string> Formats { get; } =
        decoders.Select(decoder => decoder.Format).ToFrozenSet(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, MutualTlsForwardingOptions options)
    {
        if (string.IsNullOrEmpty(options.CertificateHeaderName))
            return ValidateOptionsResult.Success;

        var format = options.Format;
        if (Formats.Contains(format))
            return ValidateOptionsResult.Success;

        var known = string.Join(", ", Formats.Order(StringComparer.Ordinal));
        return ValidateOptionsResult.Fail(
            $"{nameof(MutualTlsForwardingOptions.Format)} '{format}' is not a registered client certificate header format; the registered formats are: {known}."
        );
    }
}
