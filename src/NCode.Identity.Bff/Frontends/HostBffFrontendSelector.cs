// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// The <see cref="IBffFrontendSelector"/> named <c>host</c>, which selects a frontend by the host, and optionally the
/// port, of the request. A value without a port matches any port.
/// </summary>
internal sealed class HostBffFrontendSelector : IBffFrontendSelector
{
    /// <inheritdoc />
    public string Name => BffFrontendSelectorNames.Host;

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(BffFrontendSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.Values.Count == 0)
            return ["Values must name at least one host."];

        List<string> failures = [];
        List<HostString> hosts = [];
        var origin = Uri.TryCreate(selection.PublicOrigin, UriKind.Absolute, out var parsed)
            ? parsed
            : null;
        foreach (var value in selection.Values)
        {
            if (TryParse(value, out var host))
            {
                hosts.Add(host);

                if (origin is not null && host.Port is { } port && port == GetDefaultPort(origin))
                {
                    failures.Add(
                        $"Values entry '{value}' names the default port of PublicOrigin '{selection.PublicOrigin}'; omit it, because a browser's Host header never carries the default port."
                    );
                }

                continue;
            }

            failures.Add(
                $"Values entry '{value}' must be a host name with an optional port, such as 'app.example.com' or 'app.example.com:8443'."
            );
        }

        if (origin is not null && !hosts.Exists(x => IsOriginHost(x, origin)))
        {
            failures.Add(
                $"Values must include the host of PublicOrigin '{selection.PublicOrigin}', so the frontend's callback selects it."
            );
        }

        return failures;
    }

    /// <inheritdoc />
    public bool Overlaps(BffFrontendSelection first, BffFrontendSelection second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        foreach (var firstValue in first.Values)
        {
            if (!TryParse(firstValue, out var firstHost))
                continue;

            foreach (var secondValue in second.Values)
            {
                if (!TryParse(secondValue, out var secondHost))
                    continue;

                if (
                    string.Equals(
                        firstHost.Host,
                        secondHost.Host,
                        StringComparison.OrdinalIgnoreCase
                    )
                    && (
                        firstHost.Port is null
                        || secondHost.Port is null
                        || firstHost.Port == secondHost.Port
                    )
                )
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc />
    public bool IsMatch(HttpRequest request, BffFrontendSelection selection)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(selection);

        var requestHost = request.Host;
        foreach (var value in selection.Values)
        {
            if (
                TryParse(value, out var host)
                && string.Equals(host.Host, requestHost.Host, StringComparison.OrdinalIgnoreCase)
                && (host.Port is null || host.Port == requestHost.Port)
            )
            {
                return true;
            }
        }

        return false;
    }

    private static int? GetDefaultPort(Uri origin) =>
        origin.Scheme switch
        {
            "https" => 443,
            "http" => 80,
            _ => null,
        };

    private static bool IsOriginHost(HostString host, Uri origin) =>
        string.Equals(host.Host, origin.Host, StringComparison.OrdinalIgnoreCase)
        && (host.Port is null || host.Port == origin.Port);

    private static bool TryParse(string? value, out HostString host)
    {
        host = default;

        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsWhiteSpace))
            return false;

        var candidate = new HostString(value);
        if (
            Uri.CheckHostName(candidate.Host) is not (UriHostNameType.Dns or UriHostNameType.IPv4)
            || candidate.Port is < 1 or > 65535
            || !string.Equals(candidate.ToUriComponent(), value, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                candidate.Port is { } port ? $"{candidate.Host}:{port}" : candidate.Host,
                value,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return false;
        }

        host = candidate;
        return true;
    }
}
