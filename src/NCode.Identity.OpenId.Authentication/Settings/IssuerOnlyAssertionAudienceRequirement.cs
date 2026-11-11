// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.Json;
using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Requires the <c>client_assertion</c> to target only the issuer identifier as its audience (FAPI 2.0 §5.3.2.1),
/// rejecting the token-endpoint URL that OpenID Connect Core §9 otherwise permits.
/// </summary>
internal sealed class IssuerOnlyAssertionAudienceRequirement
    : IClientAuthenticationProfileRequirement
{
    /// <inheritdoc />
    public ValueTask EvaluateAsync(
        ClientAuthenticationProfileContext context,
        CancellationToken cancellationToken
    )
    {
        var issuer = context.OpenIdContext.Tenant.Issuer;
        var audiences = GetAudiences(context.AssertionPayload);

        if (
            audiences.Count == 0
            || audiences.Any(audience => !string.Equals(audience, issuer, StringComparison.Ordinal))
        )
        {
            throw context
                .OpenIdContext.ErrorFactory.InvalidClient()
                .WithDescription(
                    "Under a selected security profile the 'client_assertion' audience must be the issuer identifier."
                )
                .AsException();
        }

        return ValueTask.CompletedTask;
    }

    private static List<string> GetAudiences(JsonElement payload)
    {
        if (!payload.TryGetProperty(JoseClaimNames.Payload.Aud, out var aud))
            return [];

        switch (aud.ValueKind)
        {
            case JsonValueKind.String:
                return aud.GetString() is { Length: > 0 } single ? [single] : [];

            case JsonValueKind.Array:
                var list = new List<string>(aud.GetArrayLength());
                foreach (var item in aud.EnumerateArray())
                {
                    if (item.TryGetValue<string>(out var value) && value is { Length: > 0 })
                        list.Add(value);
                }
                return list;

            default:
                return [];
        }
    }
}
