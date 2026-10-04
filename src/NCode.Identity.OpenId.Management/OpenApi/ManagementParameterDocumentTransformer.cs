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
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace NCode.Identity.OpenId.Management.OpenApi;

/// <summary>
/// Fills in human-readable descriptions for the parameters shared across the management API — the path parameters
/// (tenant, client, server, and so on), the <c>If-Match</c> concurrency header, and the <c>cursor</c>/<c>limit</c>
/// pagination query parameters — so a renderer such as Scalar shows meaningful parameter help. It also declares any
/// templated path parameter that is bound by a route group rather than a handler argument (the tenant id), which the
/// framework would otherwise omit from the operation.
/// </summary>
internal sealed partial class ManagementParameterDocumentTransformer : IOpenApiDocumentTransformer
{
    // Path parameters; these may be bound by a route group rather than a handler argument, so they are also
    // declared when the framework omits them from the operation.
    private static readonly FrozenDictionary<string, string> PathParameters = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        ["tenantId"] = "The identifier of the tenant that scopes the request.",
        ["clientId"] = "The identifier of the client.",
        ["serverId"] = "The identifier of the server.",
        ["resourceServerId"] = "The identifier of the API (resource server).",
        ["secretId"] = "The identifier of the secret.",
        ["scopeValue"] = "The scope value.",
        ["grantId"] = "The identifier of the grant.",
        ["principalId"] = "The identifier of the principal (an owner of the resource).",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // Header and query parameters; described in place wherever they already appear on an operation.
    private static readonly FrozenDictionary<string, string> HeaderAndQueryParameters =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["If-Match"] =
                "An optional optimistic-concurrency precondition. When supplied, the request succeeds only if the "
                + "target's current concurrency token matches this value; otherwise it fails with 412 Precondition "
                + "Failed.",
            ["cursor"] =
                "The opaque continuation token returned by a previous page, or omitted to fetch the first page.",
            ["limit"] = "The maximum number of items to return on the page.",
        }.ToFrozenDictionary(StringComparer.Ordinal);

    [GeneratedRegex(@"\{(?<name>[A-Za-z][A-Za-z0-9_]*)\}", RegexOptions.Compiled)]
    private static partial Regex PathTokenRegex();

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (document.Paths is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (pathTemplate, pathItem) in document.Paths)
        {
            if (pathItem.Operations is null)
            {
                continue;
            }

            var pathTokens = PathTokenRegex()
                .Matches(pathTemplate)
                .Select(match => match.Groups["name"].Value)
                .Where(PathParameters.ContainsKey)
                .ToList();

            foreach (var operation in pathItem.Operations.Values)
            {
                DescribeExistingParameters(operation);

                foreach (var token in pathTokens)
                {
                    EnsurePathParameter(operation, token, PathParameters[token]);
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void DescribeExistingParameters(OpenApiOperation operation)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>())
        {
            if (parameter.Name is not { Length: > 0 } name)
            {
                continue;
            }

            if (
                parameter.In == ParameterLocation.Path
                && PathParameters.TryGetValue(name, out var pathDescription)
            )
            {
                parameter.Description ??= pathDescription;
            }
            else if (HeaderAndQueryParameters.TryGetValue(name, out var otherDescription))
            {
                parameter.Description ??= otherDescription;
            }
        }
    }

    private static void EnsurePathParameter(
        OpenApiOperation operation,
        string name,
        string description
    )
    {
        operation.Parameters ??= [];

        var exists = operation
            .Parameters.OfType<OpenApiParameter>()
            .Any(parameter =>
                parameter.In == ParameterLocation.Path
                && string.Equals(parameter.Name, name, StringComparison.Ordinal)
            );

        if (exists)
        {
            return;
        }

        // A route-group-bound path parameter (the tenant id) is not a handler argument, so the framework never
        // emits it; declare it here so the operation's path template is fully described.
        operation.Parameters.Add(
            new OpenApiParameter
            {
                Name = name,
                In = ParameterLocation.Path,
                Required = true,
                Description = description,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            }
        );
    }
}
