// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// Refuses a set of frontends in which a frontend's client-authentication or DPoP key ring names a key ring that is not
/// registered.
/// </summary>
/// <param name="keyRings">The provider that says which key rings are registered.</param>
// See ADR-1439.
internal sealed class BffKeyRingCheck(IClientKeyRingProvider keyRings) : IBffFrontendSetCheck
{
    private IClientKeyRingProvider KeyRings { get; } = keyRings;

    /// <inheritdoc />
    public void Check(BffFrontendSet frontends, ICollection<string> failures)
    {
        ArgumentNullException.ThrowIfNull(frontends);
        ArgumentNullException.ThrowIfNull(failures);

        foreach (var frontend in frontends.Frontends)
        {
            CheckKeyRing(
                frontend.Options.ClientAuthentication.KeyRing,
                $"Bff:Frontends:{frontend.Name}:ClientAuthentication:KeyRing",
                failures
            );
            CheckKeyRing(
                frontend.Options.DPoP.KeyRing,
                $"Bff:Frontends:{frontend.Name}:DPoP:KeyRing",
                failures
            );
        }
    }

    private void CheckKeyRing(string? keyRing, string key, ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(keyRing) || KeyRings.HasKeyRing(keyRing))
            return;

        failures.Add($"No key ring is registered under the name '{keyRing}' configured at {key}.");
    }
}
