// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Jose;

namespace NCode.Identity.OpenId;

public static partial class OpenIdConstants
{
    /// <summary>
    /// Contains the standard claims that are included in the <c>OpenID Connect</c> specification.
    /// </summary>
    public static IReadOnlyCollection<string> ProtocolClaims { get; } =
        new HashSet<string>
        {
            JoseClaimNames.Payload.Acr,
            JoseClaimNames.Payload.Act,
            JoseClaimNames.Payload.Amr,
            JoseClaimNames.Payload.AtHash,
            JoseClaimNames.Payload.Aud,
            JoseClaimNames.Payload.AuthTime,
            JoseClaimNames.Payload.Azp,
            JoseClaimNames.Payload.CHash,
            JoseClaimNames.Payload.ClientId,
            JoseClaimNames.Payload.Cnf,
            JoseClaimNames.Payload.Exp,
            JoseClaimNames.Payload.Iat,
            JoseClaimNames.Payload.Idp,
            JoseClaimNames.Payload.Iss,
            JoseClaimNames.Payload.Jti,
            JoseClaimNames.Payload.Nbf,
            JoseClaimNames.Payload.Nonce,
            JoseClaimNames.Payload.Role,
            JoseClaimNames.Payload.SHash,
            JoseClaimNames.Payload.Scope,
            JoseClaimNames.Payload.Sid,
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
        public static IReadOnlyCollection<string> Profile { get; } =
            new HashSet<string>
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
                JoseClaimNames.Payload.UpdatedAt,
            };

        /// <summary>
        /// Contains the standard claims that are included in the <c>email</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Email { get; } =
            new HashSet<string>
            {
                JoseClaimNames.Payload.Email,
                JoseClaimNames.Payload.EmailVerified,
            };

        /// <summary>
        /// Contains the standard claims that are included in the <c>address</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Address { get; } =
            new HashSet<string> { JoseClaimNames.Payload.Address };

        /// <summary>
        /// Contains the standard claims that are included in the <c>phone</c> scope.
        /// </summary>
        public static IReadOnlyCollection<string> Phone { get; } =
            new HashSet<string>
            {
                JoseClaimNames.Payload.PhoneNumber,
                JoseClaimNames.Payload.PhoneNumberVerified,
            };
    }
}
