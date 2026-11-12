// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Buffers;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Navigation;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Jose;

namespace NCode.Identity.Bff.Endpoints;

/// <summary>
/// The default <see cref="IBffUserDocumentFactory"/>, which builds the session's claims, expiry, logout form and the
/// fields of the registered <see cref="IBffUserContributor"/> strategies. The document carries no token.
/// </summary>
internal sealed class DefaultBffUserDocumentFactory(
    BffSessionResolver resolver,
    IEnumerable<IBffUserContributor> contributors,
    BffFrontendCatalog frontends
) : IBffUserDocumentFactory
{
    /// <inheritdoc />
    public async ValueTask<JsonElement?> CreateAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (!BffGuardedEndpoints.IsGuarded(httpContext.GetEndpoint()))
            throw new InvalidOperationException(
                "The user document is available only to an endpoint the BFF guards."
            );

        var session = await resolver.ResolveAsync(httpContext, cancellationToken);
        if (session is null)
            return null;

        var frontend = frontends.GetRequired(httpContext);
        var principal = BffSessionPrincipal.Create(session.Payload.Claims, session.Metadata.Issuer);

        // The contributors run before the document is written, so a contributor that fails leaves no partial document.
        var userContext = new BffUserContext(httpContext, principal, session.Metadata);
        foreach (var contributor in contributors)
        {
            await contributor.ContributeAsync(userContext, cancellationToken);
        }

        var buffer = BufferFactory.CreateArrayBufferWriter();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            writer.WriteString(BffUserDocumentNames.Subject, session.Metadata.Subject);
            var name = principal.FindFirst(JoseClaimNames.Payload.Name)?.Value;
            if (name is not null)
                writer.WriteString(BffUserDocumentNames.Name, name);

            writer.WriteStartObject(BffUserDocumentNames.Claims);
            foreach (var claimType in frontend.Options.UserClaims)
            {
                var values = principal.FindAll(claimType).Select(claim => claim.Value).ToList();
                if (values.Count == 0)
                    continue;

                if (values.Count == 1)
                {
                    writer.WriteString(claimType, values[0]);
                    continue;
                }

                writer.WriteStartArray(claimType);
                foreach (var value in values)
                {
                    writer.WriteStringValue(value);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();

            writer.WriteStartObject(BffUserDocumentNames.Session);
            writer.WriteNumber(
                BffUserDocumentNames.ExpiresAt,
                session.Metadata.AbsoluteExpiresAt.ToUnixTimeSeconds()
            );
            writer.WriteNumber(
                BffUserDocumentNames.IdleExpiresAt,
                session.Metadata.IdleExpiresAt.ToUnixTimeSeconds()
            );
            writer.WriteEndObject();

            var logoutPath = BffNavigation.LogoutUrl(httpContext.Request, frontend.Options);
            writer.WriteString(BffUserDocumentNames.LogoutUrl, logoutPath);
            writer.WriteString(BffUserDocumentNames.LogoutSid, session.Payload.LogoutGuard);

            foreach (var (fieldName, value) in userContext.Fields)
            {
                writer.WritePropertyName(fieldName);
                if (value is null)
                    writer.WriteNullValue();
                else
                    value.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return JsonElement.Parse(buffer.WrittenSpan);
    }
}
