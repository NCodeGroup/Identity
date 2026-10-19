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

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace NCode.Identity.Server.ReverseProxy;

/// <summary>
/// Validates <see cref="ReverseProxyOptions"/>, so a trusted-proxy entry the server cannot honor stops the host when it
/// starts. Skipping such an entry would leave a deployment that meant to trust its proxy trusting nothing (forwarded
/// headers silently ignored, so the server publishes an <c>http://</c> issuer for an internal host), and the IPv4
/// parser's shorthand forms (<c>10.0.0</c>, <c>010.0.0.1</c>) would trust an address other than the one written.
/// </summary>
// See ADR-0137.
internal class ReverseProxyOptionsValidator : IValidateOptions<ReverseProxyOptions>
{
    private const string Prefix = ReverseProxyOptions.DefaultSectionName;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ReverseProxyOptions options)
    {
        var failures = new List<string>();

        for (var index = 0; index < options.KnownProxies.Length; index++)
        {
            var entry = options.KnownProxies[index];
            if (!TryParseAddress(entry, out _))
            {
                failures.Add(
                    $"{Prefix}:{nameof(options.KnownProxies)}:{index} '{entry}' is not an IP address; write an IPv4 "
                        + "address as four decimal octets (for example '10.0.0.4')."
                );
            }
        }

        for (var index = 0; index < options.KnownNetworks.Length; index++)
        {
            var entry = options.KnownNetworks[index];
            if (!TryParseNetwork(entry))
            {
                failures.Add(
                    $"{Prefix}:{nameof(options.KnownNetworks)}:{index} '{entry}' is not a network in CIDR notation; "
                        + "the address must be the network's first address, with no bits set past the prefix length "
                        + "(for example '172.16.0.0/12')."
                );
            }
        }

        if (options.ForwardLimit < 1)
        {
            failures.Add(
                $"{Prefix}:{nameof(options.ForwardLimit)} '{options.ForwardLimit}' must be at least 1; a lower limit "
                    + "consults no forwarded entries at all."
            );
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool TryParseNetwork(string entry)
    {
        var separatorIndex = entry.IndexOf('/', StringComparison.Ordinal);
        if (separatorIndex < 0)
            return false;

        // The parser masks bits set past the prefix length, so '10.0.0.4/8' would trust 10.0.0.0/8; require the
        // address as written to be the network's base address.
        var addressText = entry[..separatorIndex];
        return TryParseAddress(addressText, out var address)
            && IPNetwork.TryParse(entry, out var network)
            && network.BaseAddress.Equals(address);
    }

    // IPv4 must be written canonically: the parser also accepts shorthand and octal forms that denote another address.
    private static bool TryParseAddress(string entry, [NotNullWhen(true)] out IPAddress? address) =>
        IPAddress.TryParse(entry, out address)
        && (
            address.AddressFamily != AddressFamily.InterNetwork
            || string.Equals(address.ToString(), entry, StringComparison.Ordinal)
        );
}
