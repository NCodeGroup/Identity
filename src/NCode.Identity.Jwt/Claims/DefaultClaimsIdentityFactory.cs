// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Json;

namespace NCode.Identity.Jwt.Claims;

/// <summary>
/// Provides the default implementation of <see cref="IClaimsIdentityFactory"/>, which types each claim by its JSON
/// value through <see cref="ClaimValueMapper"/>.
/// </summary>
// See ADR-1122.
internal sealed class DefaultClaimsIdentityFactory : IClaimsIdentityFactory
{
    /// <inheritdoc />
    public ClaimsIdentity CreateClaimsIdentity(
        JsonElement claimSet,
        string authenticationType,
        string? nameClaimType = null,
        string? roleClaimType = null,
        string? issuer = null
    )
    {
        if (claimSet.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("The claim set must be a JSON object.", nameof(claimSet));

        var effectiveNameClaimType = string.IsNullOrEmpty(nameClaimType)
            ? ClaimsIdentity.DefaultNameClaimType
            : nameClaimType;

        var effectiveRoleClaimType = string.IsNullOrEmpty(roleClaimType)
            ? ClaimsIdentity.DefaultRoleClaimType
            : roleClaimType;

        var subject = new ClaimsIdentity(
            authenticationType,
            effectiveNameClaimType,
            effectiveRoleClaimType
        );

        if (
            !claimSet.TryGetPropertyValue<string>(
                JoseClaimNames.Payload.Iss,
                out var effectiveIssuer
            ) || string.IsNullOrEmpty(effectiveIssuer)
        )
        {
            effectiveIssuer = string.IsNullOrEmpty(issuer) ? ClaimsIdentity.DefaultIssuer : issuer;
        }

        foreach (var property in claimSet.EnumerateObject())
        {
            var name = property.Name;
            var value = property.Value;

            if (value.ValueKind == JsonValueKind.Array)
            {
                subject.AddClaims(
                    value
                        .EnumerateArray()
                        .Select(item =>
                            ClaimValueMapper.ToClaim(name, item, effectiveIssuer, subject)
                        )
                );
            }
            else
            {
                var claim = ClaimValueMapper.ToClaim(name, value, effectiveIssuer, subject);
                subject.AddClaim(claim);
            }
        }

        return subject;
    }
}
