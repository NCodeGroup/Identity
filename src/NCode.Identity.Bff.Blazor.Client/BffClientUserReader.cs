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

    private const string SubjectPropertyName = "sub";
    private const string NamePropertyName = "name";
    private const string ClaimsPropertyName = "claims";
    private const string LogoutUrlPropertyName = "logoutUrl";
    private const string LogoutSidPropertyName = "logoutSid";
    private const string SessionPropertyName = "session";
    private const string IdleExpiresAtPropertyName = "idleExpiresAt";

    /// <summary>
    /// Reads <paramref name="userDocument"/> into a user.
    /// </summary>
    /// <param name="userDocument">The user document.</param>
    /// <param name="user">The user, when the document is an object with a string <c>sub</c>.</param>
    /// <returns>
    /// <see langword="true"/> when the document describes a user; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryRead(
        JsonElement userDocument,
        [NotNullWhen(true)] out BffClientUser? user
    )
    {
        user = null;

        if (userDocument.ValueKind != JsonValueKind.Object)
            return false;

        if (!userDocument.TryGetPropertyValue<string>(SubjectPropertyName, out var subjectValue))
            return false;

        List<Claim> claims = [new Claim(SubjectClaimType, subjectValue)];

        if (userDocument.TryGetPropertyValue<string>(NamePropertyName, out var nameValue))
            claims.Add(new Claim(NameClaimType, nameValue));

        if (
            userDocument.TryGetPropertyValue<JsonElement>(ClaimsPropertyName, out var claimsElement)
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

        AddString(userDocument, LogoutUrlPropertyName, BffClientClaimTypes.LogoutUrl, claims);
        AddString(userDocument, LogoutSidPropertyName, BffClientClaimTypes.LogoutSid, claims);

        var identity = new ClaimsIdentity(
            claims,
            BffBlazorClientDefaults.AuthenticationType,
            NameClaimType,
            RoleClaimType
        );

        user = new BffClientUser(new ClaimsPrincipal(identity), ReadIdleExpiresAt(userDocument));
        return true;
    }

    private static string ValueOf(JsonElement element) =>
        element.TryGetValue<string>(out var value) ? value : element.GetRawText();

    private static void AddString(
        JsonElement userDocument,
        string propertyName,
        string claimType,
        List<Claim> claims
    )
    {
        if (userDocument.TryGetPropertyValue<string>(propertyName, out var value))
            claims.Add(new Claim(claimType, value));
    }

    private static DateTimeOffset? ReadIdleExpiresAt(JsonElement userDocument)
    {
        if (
            userDocument.TryGetPropertyValue<JsonElement>(SessionPropertyName, out var session)
            && session.ValueKind == JsonValueKind.Object
            && session.TryGetPropertyNumericDate(IdleExpiresAtPropertyName, out var instant)
        )
        {
            return instant;
        }

        return null;
    }
}
