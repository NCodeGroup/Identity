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

using System.Collections.Frozen;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NCode.Identity.OpenId;

namespace NCode.Identity.Server.OpenApi;

/// <summary>
/// Declares the <c>Bearer</c> (JWT) security scheme so a renderer such as Scalar presents an authentication affordance,
/// and attaches it as a security requirement to the management endpoints (those tagged with a management tag). The
/// <c>OAuth</c> / <c>OpenID Connect</c> protocol endpoints are left open, since they are how a caller obtains a token.
/// </summary>
internal sealed class SecuritySchemeDocumentTransformer : IOpenApiDocumentTransformer
{
    /// <summary>
    /// The name of the bearer security scheme declared in the OpenAPI document.
    /// </summary>
    public const string BearerSchemeName = "Bearer";

    private static readonly FrozenSet<string> ManagementTagNames = new[]
    {
        OpenIdConstants.EndpointTags.Clients,
        OpenIdConstants.EndpointTags.Grants,
        OpenIdConstants.EndpointTags.ClientGrants,
        OpenIdConstants.EndpointTags.ResourceServers,
        OpenIdConstants.EndpointTags.Servers,
        OpenIdConstants.EndpointTags.Tenants,
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var bearerScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Description =
                "A self-issued JWT access token for the 'urn:ncode:management' audience, obtained from the "
                + "token endpoint (for example, the client_credentials grant).",
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(
            StringComparer.Ordinal
        );
        document.Components.SecuritySchemes[BearerSchemeName] = bearerScheme;

        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = new List<string>(),
        };

        foreach (var pathItem in document.Paths.Values)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            foreach (var operation in pathItem.Operations.Values)
            {
                var isManagement =
                    operation.Tags?.Any(tag =>
                        tag.Name is { Length: > 0 } name && ManagementTagNames.Contains(name)
                    )
                    ?? false;

                if (!isManagement)
                {
                    continue;
                }

                operation.Security ??= [];
                operation.Security.Add(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
