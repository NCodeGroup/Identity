#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Security.Claims;
using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Json;

namespace NCode.Identity.Jwt.Claims;

/// <summary>
/// Provides the default implementation of <see cref="IClaimsIdentityFactory"/>, which types each claim by its JSON
/// value through <see cref="ClaimValueMapper"/>.
/// </summary>
// See ADR-0122.
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
