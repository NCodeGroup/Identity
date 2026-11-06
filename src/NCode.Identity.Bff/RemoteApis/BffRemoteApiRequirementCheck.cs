// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Refuses a set of frontends in which a configured remote API names a token requirement that is not registered.
/// </summary>
/// <param name="requirements">The names of the registered token requirements.</param>
// See ADR-1442.
internal sealed class BffRemoteApiRequirementCheck(IReadOnlySet<string> requirements)
    : IBffFrontendSetCheck
{
    /// <inheritdoc />
    public void Check(BffFrontendSet frontends, ICollection<string> failures)
    {
        ArgumentNullException.ThrowIfNull(frontends);
        ArgumentNullException.ThrowIfNull(failures);

        foreach (var frontend in frontends.Frontends)
        {
            foreach (var (name, api) in frontend.Options.RemoteApis)
            {
                if (!requirements.Contains(api.TokenRequirement))
                {
                    failures.Add(
                        $"Bff:Frontends:{frontend.Name}:RemoteApis:{name}:TokenRequirement '{api.TokenRequirement}' is not a registered token requirement."
                    );
                }
            }
        }
    }
}
