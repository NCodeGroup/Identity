#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;

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
                    "Under the active security profile the 'client_assertion' audience must be the issuer identifier."
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
                    if (
                        item.ValueKind == JsonValueKind.String
                        && item.GetString() is { Length: > 0 } value
                    )
                        list.Add(value);
                }
                return list;

            default:
                return [];
        }
    }
}
