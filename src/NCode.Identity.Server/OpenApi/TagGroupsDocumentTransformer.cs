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
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NCode.Identity.OpenId;

namespace NCode.Identity.Server.OpenApi;

/// <summary>
/// Emits the <c>x-tagGroups</c> OpenAPI extension so compatible renderers show collapsible root sections: all
/// <c>OAuth</c> / <c>OpenID Connect</c> endpoints under one root, all known management APIs (each keeping its own
/// sub-tag) under another, and any remaining tags under an <c>Other</c> root.
/// </summary>
internal sealed class TagGroupsDocumentTransformer : IOpenApiDocumentTransformer
{
    private const string TagGroupsExtensionName = "x-tagGroups";
    private const string OAuthGroupName = "OAuth & OpenID Connect";
    private const string ManagementGroupName = "Management";
    private const string OtherGroupName = "Other";

    private static readonly FrozenSet<string> ManagementTagNames = new[]
    {
        OpenIdConstants.EndpointTags.Clients,
        OpenIdConstants.EndpointTags.Grants,
        OpenIdConstants.EndpointTags.ClientGrants,
        OpenIdConstants.EndpointTags.ResourceServers,
        OpenIdConstants.EndpointTags.Servers,
        OpenIdConstants.EndpointTags.Tenants,
        OpenIdConstants.EndpointTags.LocalAccounts,
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        var tagNames =
            document
                .Tags?.Select(tag => tag.Name)
                .OfType<string>()
                .Where(name => name.Length > 0)
                .ToList()
            ?? [];

        // The single OIDC tag forms the OAuth root; known management tags form the Management root; the rest fall
        // into the Other root.
        var oauthTags = tagNames
            .Where(name => name == OpenIdConstants.EndpointTags.OpenId)
            .ToList();

        var managementTags = tagNames
            .Where(name => ManagementTagNames.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var otherTags = tagNames
            .Where(name =>
                name != OpenIdConstants.EndpointTags.OpenId && !ManagementTagNames.Contains(name)
            )
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var tagGroups = new JsonArray();

        if (oauthTags.Count > 0)
        {
            tagGroups.Add(CreateGroup(OAuthGroupName, oauthTags));
        }

        if (managementTags.Count > 0)
        {
            tagGroups.Add(CreateGroup(ManagementGroupName, managementTags));
        }

        if (otherTags.Count > 0)
        {
            tagGroups.Add(CreateGroup(OtherGroupName, otherTags));
        }

        document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        document.Extensions[TagGroupsExtensionName] = new JsonNodeExtension(tagGroups);

        return Task.CompletedTask;
    }

    private static JsonObject CreateGroup(string name, IEnumerable<string> tagNames)
    {
        var tags = new JsonArray();
        foreach (var tagName in tagNames)
        {
            tags.Add(tagName);
        }

        return new JsonObject { ["name"] = name, ["tags"] = tags };
    }
}
