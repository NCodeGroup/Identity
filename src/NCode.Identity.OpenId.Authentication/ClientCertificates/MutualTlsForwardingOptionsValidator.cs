// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Validates <see cref="MutualTlsForwardingOptions"/> when the host starts: a forwarded header must name a format some
/// registered <see cref="IClientCertificateHeaderDecoder"/> reads, so a misspelled or unregistered format stops the host
/// instead of failing every request that carries a certificate.
/// </summary>
// See ADR-1137 (fail at startup) and ADR-1172 (formats are registered decoders).
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
