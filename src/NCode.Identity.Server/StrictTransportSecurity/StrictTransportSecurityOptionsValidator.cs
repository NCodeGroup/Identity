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

using Microsoft.Extensions.Options;

namespace NCode.Identity.Server.StrictTransportSecurity;

/// <summary>
/// Validates <see cref="StrictTransportSecurityOptions"/>, so a policy that a browser or the HSTS preload list would
/// reject, or an excluded host that the middleware could never match, stops the host when it starts instead of being
/// ignored.
/// </summary>
// See ADR-0137 and ADR-0258.
internal sealed class StrictTransportSecurityOptionsValidator
    : IValidateOptions<StrictTransportSecurityOptions>
{
    private const string Prefix = StrictTransportSecurityOptions.DefaultSectionName;

    private static TimeSpan PreloadMinimumMaxAge { get; } = TimeSpan.FromDays(365);

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, StrictTransportSecurityOptions options)
    {
        var failures = new List<string>();

        if (options.MaxAge < TimeSpan.Zero)
        {
            failures.Add(
                $"{Prefix}:{nameof(options.MaxAge)} '{options.MaxAge}' must not be negative; set it to 00:00:00 to tell browsers to forget the policy."
            );
        }

        if (options.Preload && !options.IncludeSubDomains)
        {
            failures.Add(
                $"{Prefix}:{nameof(options.Preload)} requires {Prefix}:{nameof(options.IncludeSubDomains)}, which the HSTS preload list requires."
            );
        }

        if (options.Preload && options.MaxAge < PreloadMinimumMaxAge)
        {
            failures.Add(
                $"{Prefix}:{nameof(options.Preload)} requires a {Prefix}:{nameof(options.MaxAge)} of at least 365.00:00:00, which the HSTS preload list requires; it is '{options.MaxAge}'."
            );
        }

        for (var index = 0; index < options.ExcludedHosts.Length; index++)
        {
            var entry = options.ExcludedHosts[index];
            if (!IsHostName(entry))
            {
                failures.Add(
                    $"{Prefix}:{nameof(options.ExcludedHosts)}:{index} '{entry}' is not a host name; write a DNS name, an IPv4 address, or an IPv6 address in brackets, without a scheme or a port."
                );
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    // The middleware compares an entry with the request's host, so a scheme, a port, or an unbracketed IPv6 address
    // could never match.
    private static bool IsHostName(string entry) =>
        Uri.CheckHostName(entry) switch
        {
            UriHostNameType.Dns or UriHostNameType.IPv4 => true,
            UriHostNameType.IPv6 => entry.StartsWith('['),
            _ => false,
        };
}
