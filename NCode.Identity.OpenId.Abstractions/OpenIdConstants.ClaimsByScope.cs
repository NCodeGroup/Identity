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

using NCode.Identity.Jose;

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the standard claims that are included in the <c>OpenID Connect</c> specification.
    /// </summary>
    public static IReadOnlyCollection<string> ProtocolClaims { get; } = new HashSet<string>
    {
        JoseClaimNames.Payload.Acr,
        // TODO: act
        JoseClaimNames.Payload.Amr,
        JoseClaimNames.Payload.AtHash,
        JoseClaimNames.Payload.Aud,
        JoseClaimNames.Payload.AuthTime,
        // TODO: azp
        JoseClaimNames.Payload.CHash,
        JoseClaimNames.Payload.ClientId,
        // TODO: cnf
        JoseClaimNames.Payload.Exp,
        JoseClaimNames.Payload.Iat,
        JoseClaimNames.Payload.Idp,
        JoseClaimNames.Payload.Iss,
        JoseClaimNames.Payload.Jti,
        JoseClaimNames.Payload.Nbf,
        JoseClaimNames.Payload.Nonce,
        // TODO: role
        JoseClaimNames.Payload.SHash,
        // TODO: sid
        JoseClaimNames.Payload.Sub,
        JoseClaimNames.Payload.Tid,
    };

    /// <summary>
    /// Contains the standard claims that are included in various <c>OpenID Connect</c> scopes.
    /// </summary>
    public static class ClaimsByScope
    {
        /// <summary>
        /// Contains the standard claims that are included in the <c>profile</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Profile { get; } = new HashSet<string>
        {
            JoseClaimNames.Payload.Name,
            JoseClaimNames.Payload.FamilyName,
            JoseClaimNames.Payload.GivenName,
            JoseClaimNames.Payload.MiddleName,
            JoseClaimNames.Payload.Nickname,
            JoseClaimNames.Payload.PreferredUsername,
            JoseClaimNames.Payload.Profile,
            JoseClaimNames.Payload.Picture,
            JoseClaimNames.Payload.Website,
            JoseClaimNames.Payload.Gender,
            JoseClaimNames.Payload.Birthdate,
            JoseClaimNames.Payload.Zoneinfo,
            JoseClaimNames.Payload.Locale,
            JoseClaimNames.Payload.UpdatedAt
        };

        /// <summary>
        /// Contains the standard claims that are included in the <c>email</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Email { get; } = new HashSet<string>
        {
            JoseClaimNames.Payload.Email,
            JoseClaimNames.Payload.EmailVerified
        };

        /// <summary>
        /// Contains the standard claims that are included in the <c>address</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Address { get; } = new HashSet<string>
        {
            JoseClaimNames.Payload.Address
        };

        /// <summary>
        /// Contains the standard claims that are included in the <c>phone</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Phone { get; } = new HashSet<string>
        {
            JoseClaimNames.Payload.PhoneNumber,
            JoseClaimNames.Payload.PhoneNumberVerified
        };
    }
}
