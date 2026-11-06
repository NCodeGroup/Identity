// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// Refuses a set of frontends in which a frontend's configured client authentication method has no registered
/// <see cref="IClientAuthenticator"/>.
/// </summary>
/// <param name="authenticators">The registered client authenticators.</param>
// See ADR-1442.
internal sealed class BffClientAuthenticationCheck(IEnumerable<IClientAuthenticator> authenticators)
    : IBffFrontendSetCheck
{
    private FrozenSet<string> Methods { get; } =
        authenticators.Select(x => x.MethodName).ToFrozenSet(StringComparer.Ordinal);

    /// <inheritdoc />
    public void Check(BffFrontendSet frontends, ICollection<string> failures)
    {
        ArgumentNullException.ThrowIfNull(frontends);
        ArgumentNullException.ThrowIfNull(failures);

        foreach (var frontend in frontends.Frontends)
        {
            var method = frontend.Options.ClientAuthentication.Method;
            if (!Methods.Contains(method))
            {
                failures.Add(
                    $"No client authenticator is registered for the method '{method}' configured at Bff:Frontends:{frontend.Name}:ClientAuthentication:Method."
                );
            }
        }
    }
}
