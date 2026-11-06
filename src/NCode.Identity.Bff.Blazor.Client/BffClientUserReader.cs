// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using NCode.Json;

namespace NCode.Identity.Bff.Blazor.Client;

/// <summary>
/// Reads the document the BFF's user endpoint writes into a <see cref="BffClientUser"/>.
/// </summary>
internal static class BffClientUserReader
{
    private const string SubjectClaimType = "sub";
    private const string NameClaimType = "name";
    private const string RoleClaimType = "role";

    /// <summary>
    /// Reads <paramref name="document"/> into a user.
    /// </summary>
    /// <param name="document">The user document.</param>
    /// <param name="user">The user, when the document is an object with a string <c>sub</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the document describes a user; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryRead(JsonElement document, [NotNullWhen(true)] out BffClientUser? user)
    {
        user = null;

        if (document.ValueKind != JsonValueKind.Object)
            return false;

        if (
            !document.TryGetProperty(SubjectClaimType, out var subject)
            || subject.ValueKind != JsonValueKind.String
            || subject.GetString() is not { } subjectValue
        )
        {
            return false;
        }

        List<Claim> claims = [new Claim(SubjectClaimType, subjectValue)];

        if (
            document.TryGetProperty(NameClaimType, out var name)
            && name.ValueKind == JsonValueKind.String
            && name.GetString() is { } nameValue
        )
        {
            claims.Add(new Claim(NameClaimType, nameValue));
        }

        if (
            document.TryGetProperty("claims", out var claimsElement)
            && claimsElement.ValueKind == JsonValueKind.Object
        )
        {
            foreach (var property in claimsElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var element in property.Value.EnumerateArray())
                    {
                        claims.Add(new Claim(property.Name, ValueOf(element)));
                    }
                }
                else
                {
                    claims.Add(new Claim(property.Name, ValueOf(property.Value)));
                }
            }
        }

        AddString(document, "logoutUrl", BffClientClaimTypes.LogoutUrl, claims);
        AddString(document, "logoutSid", BffClientClaimTypes.LogoutSid, claims);

        var identity = new ClaimsIdentity(
            claims,
            BffBlazorClientDefaults.AuthenticationType,
            NameClaimType,
            RoleClaimType
        );

        user = new BffClientUser(new ClaimsPrincipal(identity), ReadIdleExpiresAt(document));
        return true;
    }

    private static string ValueOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.String
            ? element.GetString() ?? string.Empty
            : element.GetRawText();

    private static void AddString(
        JsonElement document,
        string propertyName,
        string claimType,
        List<Claim> claims
    )
    {
        if (
            document.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            && property.GetString() is { } value
        )
        {
            claims.Add(new Claim(claimType, value));
        }
    }

    private static DateTimeOffset? ReadIdleExpiresAt(JsonElement document)
    {
        if (
            document.TryGetProperty("session", out var session)
            && session.ValueKind == JsonValueKind.Object
            && session.TryGetProperty("idleExpiresAt", out var idleExpiresAt)
            && idleExpiresAt.ValueKind == JsonValueKind.Number
            && idleExpiresAt.TryGetInt64(out var seconds)
            && DateCoercion.TryFromNumericDate(seconds, out var instant)
        )
        {
            return instant;
        }

        return null;
    }
}
