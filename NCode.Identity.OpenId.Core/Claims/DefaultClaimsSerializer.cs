#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Claims;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Claims;

/// <summary>
/// Provides a default implementation of the <see cref="IClaimsSerializer"/> abstraction.
/// </summary>
[PublicAPI]
public class DefaultClaimsSerializer : IClaimsSerializer
{
    /// <summary>
    /// Gets a singleton instance for <see cref="DefaultClaimsSerializer"/>.
    /// </summary>
    public static DefaultClaimsSerializer Singleton { get; } = new();

    [return: NotNullIfNotNull(nameof(value))]
    private static string? GetReferenceId(object? value) =>
        value?.GetHashCode().ToString(CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public SerializableClaim SerializeClaim(Claim claim) =>
        new()
        {
            Type = claim.Type,
            Value = claim.Value,
            ValueType = claim.ValueType,
            Issuer = claim.Issuer,
            OriginalIssuer = claim.OriginalIssuer,
            Properties = claim.Properties,
            SubjectRef = GetReferenceId(claim.Subject)
        };

    /// <inheritdoc />
    public SerializableClaimsIdentity SerializeIdentity(ClaimsIdentity identity)
    {
        var referenceId = GetReferenceId(identity);
        var actor = identity.Actor != null ? SerializeIdentity(identity.Actor) : null;

        var claims = identity.Claims.Select(claim => new SerializableClaim
        {
            Type = claim.Type,
            Value = claim.Value,
            ValueType = claim.ValueType,
            Issuer = claim.Issuer,
            OriginalIssuer = claim.OriginalIssuer,
            Properties = claim.Properties,
            SubjectRef = claim.Subject == identity ? referenceId : null,
        }).ToList();

        return new SerializableClaimsIdentity
        {
            ReferenceId = referenceId,
            Label = identity.Label,
            BootstrapContext = identity.BootstrapContext as string, // we only allow strings
            AuthenticationType = identity.AuthenticationType,
            NameClaimType = identity.NameClaimType,
            RoleClaimType = identity.RoleClaimType,
            Actor = actor,
            Claims = claims,
        };
    }

    /// <inheritdoc />
    public SerializableClaimsPrincipal SerializePrincipal(ClaimsPrincipal principal) =>
        new()
        {
            Identities = principal.Identities.Select(SerializeIdentity).ToList()
        };

    /// <inheritdoc />
    public Claim DeserializeClaim(SerializableClaim serializableClaim) =>
        DeserializeClaim(serializableClaim, null);

    /// <inheritdoc />
    public Claim DeserializeClaim(SerializableClaim serializableClaim, ClaimsIdentity? subject)
    {
        var claim = new Claim(
            serializableClaim.Type,
            serializableClaim.Value,
            serializableClaim.ValueType,
            serializableClaim.Issuer,
            serializableClaim.OriginalIssuer,
            subject
        );

        foreach (var (key, value) in serializableClaim.Properties)
        {
            claim.Properties[key] = value;
        }

        return claim;
    }

    /// <inheritdoc />
    public ClaimsIdentity DeserializeIdentity(SerializableClaimsIdentity serializableIdentity)
    {
        var actor = serializableIdentity.Actor != null ? DeserializeIdentity(serializableIdentity.Actor) : null;

        var claimsIdentity = new ClaimsIdentity(
            identity: null,
            claims: null,
            serializableIdentity.AuthenticationType,
            serializableIdentity.NameClaimType,
            serializableIdentity.RoleClaimType)
        {
            Label = serializableIdentity.Label,
            BootstrapContext = serializableIdentity.BootstrapContext,
            Actor = actor,
        };

        foreach (var serializableClaim in serializableIdentity.Claims)
        {
            var subject = serializableClaim.SubjectRef == serializableIdentity.ReferenceId ? claimsIdentity : null;

            var claim = DeserializeClaim(serializableClaim, subject);
            claimsIdentity.AddClaim(claim);
        }

        return claimsIdentity;
    }

    /// <inheritdoc />
    public ClaimsPrincipal DeserializePrincipal(SerializableClaimsPrincipal serializablePrincipal) =>
        new(serializablePrincipal.Identities.Select(DeserializeIdentity));
}
