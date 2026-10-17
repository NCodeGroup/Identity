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
/// Factory class that provides the ability to create a <see cref="ClaimsIdentity"/> instance from a JSON Web Token (JWT) payload.
/// </summary>
internal static class DefaultClaimsIdentityFactory
{
    /// <summary>
    /// Factory method that provides the ability to create a <see cref="ClaimsIdentity"/> instance from a JSON Web Token (JWT) payload.
    /// </summary>
    /// <param name="authenticationType">A <see cref="string"/> for the <c>AuthenticationType</c> that is used when creating a <see cref="ClaimsIdentity"/> instance.</param>
    /// <param name="nameClaimType">A <see cref="string"/> that specifies which <see cref="Claim.Type"/> is used to store the <c>Name</c>
    /// claim for a <see cref="ClaimsIdentity"/> instance.</param>
    /// <param name="roleClaimType">A <see cref="string"/> that specifies which <see cref="Claim.Type"/> is used to store the <c>Role</c>
    /// claim for a <see cref="ClaimsIdentity"/> instance.</param>
    /// <param name="payload">A <see cref="JsonElement"/> that contains the JSON Web Token (JWT) payload.</param>
    /// <returns>The newly created <see cref="ClaimsIdentity"/> instance.</returns>
    public static ClaimsIdentity CreateClaimsIdentity(
        string authenticationType,
        string nameClaimType,
        string roleClaimType,
        JsonElement payload
    )
    {
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
            !payload.TryGetPropertyValue<string>(JoseClaimNames.Payload.Iss, out var issuer)
            || string.IsNullOrEmpty(issuer)
        )
        {
            issuer = ClaimsIdentity.DefaultIssuer;
        }

        foreach (var property in payload.EnumerateObject())
        {
            var name = property.Name;
            var value = property.Value;

            if (value.ValueKind == JsonValueKind.Array)
            {
                subject.AddClaims(
                    value
                        .EnumerateArray()
                        .Select(item => ClaimValueMapper.ToClaim(name, item, issuer, subject))
                );
            }
            else
            {
                var claim = ClaimValueMapper.ToClaim(name, value, issuer, subject);
                subject.AddClaim(claim);
            }
        }

        return subject;
    }
}
