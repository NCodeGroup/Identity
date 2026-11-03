// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Validates the options for retrieving and caching revocation information when the host starts, so a timeout that a
/// timer refuses, a limit that is not positive, or a selection of sources that names an unregistered source or one
/// source twice stops the host instead of failing the first time a certificate is checked. The sources are read from the
/// source catalog, which is built from descriptors, so validation constructs no source.
/// </summary>
/// <param name="catalog">The catalog of the registered revocation-source descriptors.</param>
// See ADR-1252 and ADR-1400.
internal sealed class ClientCertificateRevocationOptionsValidator(
    IClientCertificateRevocationSourceCatalog catalog
) : IValidateOptions<ClientCertificateRevocationOptions>
{
    private IClientCertificateRevocationSourceCatalog Catalog { get; } = catalog;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ClientCertificateRevocationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        List<string> failures = [];

        if (!TimerPeriod.IsValid(options.FetchTimeout))
        {
            failures.Add(
                $"{nameof(ClientCertificateRevocationOptions)}.{nameof(ClientCertificateRevocationOptions.FetchTimeout)} ('{ClientCertificateRevocationOptions.SectionName}:{nameof(ClientCertificateRevocationOptions.FetchTimeout)}') must be between {TimerPeriod.MinValue} and {TimerPeriod.MaxValue}, the periods a timer accepts; it is {options.FetchTimeout}."
            );
        }

        if (options.MaxResponseSizeBytes < 1)
        {
            failures.Add(
                $"{nameof(ClientCertificateRevocationOptions)}.{nameof(ClientCertificateRevocationOptions.MaxResponseSizeBytes)} ('{ClientCertificateRevocationOptions.SectionName}:{nameof(ClientCertificateRevocationOptions.MaxResponseSizeBytes)}') must be at least 1; it is {options.MaxResponseSizeBytes}."
            );
        }

        if (options.MaxCacheDuration <= TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(ClientCertificateRevocationOptions)}.{nameof(ClientCertificateRevocationOptions.MaxCacheDuration)} ('{ClientCertificateRevocationOptions.SectionName}:{nameof(ClientCertificateRevocationOptions.MaxCacheDuration)}') must be positive; it is {options.MaxCacheDuration}."
            );
        }

        if (options.FailureCacheDuration <= TimeSpan.Zero)
        {
            failures.Add(
                $"{nameof(ClientCertificateRevocationOptions)}.{nameof(ClientCertificateRevocationOptions.FailureCacheDuration)} ('{ClientCertificateRevocationOptions.SectionName}:{nameof(ClientCertificateRevocationOptions.FailureCacheDuration)}') must be positive; it is {options.FailureCacheDuration}."
            );
        }

        ValidateSources(options.Sources, failures);

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private void ValidateSources(List<string>? sources, List<string> failures)
    {
        const string property =
            $"{nameof(ClientCertificateRevocationOptions)}.{nameof(ClientCertificateRevocationOptions.Sources)} ('{ClientCertificateRevocationOptions.SectionName}:{nameof(ClientCertificateRevocationOptions.Sources)}')";

        if (sources is null)
        {
            failures.Add($"{property} must not be null.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in sources)
        {
            if (name is null || !Catalog.TryGet(name, out _))
            {
                var registered = string.Join(", ", Catalog.SourceNames.Select(n => $"'{n}'"));
                failures.Add(
                    $"{property} names '{name}', which is not a registered revocation source; the registered sources are {registered}."
                );
            }
            else if (!seen.Add(name))
            {
                failures.Add($"{property} names '{name}' more than once.");
            }
        }
    }
}
