// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.OpenApi;
using NCode.Identity.OpenId.Management.Endpoints;

namespace NCode.Identity.OpenId.Management.OpenApi;

/// <summary>
/// Describes the problem responses that every management route shares. A route that documents one of its own error
/// responses keeps that text; the answers that the root management group produces for every route (an unauthenticated
/// caller, a failed authorization, a failed precondition, an unsupported content type, an unreadable stored settings
/// document, an unavailable root key and a request that fails to bind) get one shared sentence, so no sentence is
/// repeated on each route and none can drift from the others.
/// </summary>
// See ADR-1481.
internal class ManagementProblemResponseOperationTransformer : IOpenApiOperationTransformer
{
    /// <summary>
    /// The description of a <c>401</c> response that its route does not describe.
    /// </summary>
    internal const string UnauthorizedDescription =
        "The request carries no valid management access token.";

    /// <summary>
    /// The description of a <c>403</c> response that its route does not describe.
    /// </summary>
    internal const string ForbiddenDescription =
        "The caller holds no role that permits this operation on the resource the route names.";

    /// <summary>
    /// The description of a <c>412</c> response that its route does not describe.
    /// </summary>
    internal const string PreconditionFailedDescription =
        "The If-Match token does not match the resource's current concurrency token, or another request changed the resource after this request read it.";

    /// <summary>
    /// The description of a <c>415</c> response that its route does not describe.
    /// </summary>
    internal const string UnsupportedMediaTypeDescription =
        "The request's content type is not JSON.";

    /// <summary>
    /// The sentence appended to the description of a <c>400</c> response of an operation that binds a request body or
    /// a parameter that can fail to parse.
    /// </summary>
    internal const string BindingFailureDescription =
        "The request body is not valid JSON for this operation, or a parameter is missing or cannot be parsed.";

    /// <summary>
    /// The sentence appended to the description of every <c>409</c> response.
    /// </summary>
    internal const string StoredSettingsConflictDescription =
        "A stored settings document the operation reads cannot be honored; replace it with a settings write.";

    /// <summary>
    /// The sentence appended to the description of every <c>503</c> response.
    /// </summary>
    internal const string RootKeyUnavailableDescription =
        "A root key the operation needs is unavailable; retry after the interval in the Retry-After header.";

    // The description of a response that its route does not describe.
    private static readonly FrozenDictionary<int, string> Defaults = new Dictionary<int, string>
    {
        [StatusCodes.Status401Unauthorized] = UnauthorizedDescription,
        [StatusCodes.Status403Forbidden] = ForbiddenDescription,
        [StatusCodes.Status412PreconditionFailed] = PreconditionFailedDescription,
        [StatusCodes.Status415UnsupportedMediaType] = UnsupportedMediaTypeDescription,
    }.ToFrozenDictionary();

    // The sentence appended to the description of a response, whether or not its route describes it.
    private static readonly FrozenDictionary<int, string> Additions = new Dictionary<int, string>
    {
        [StatusCodes.Status409Conflict] = StoredSettingsConflictDescription,
        [StatusCodes.Status503ServiceUnavailable] = RootKeyUnavailableDescription,
    }.ToFrozenDictionary();

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (operation.Responses is null)
        {
            return Task.CompletedTask;
        }

        foreach (var (key, response) in operation.Responses)
        {
            if (
                !int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
                || !ErrorStatusCodes.Contains(code)
            )
            {
                continue;
            }

            var own = response.Description;
            if (
                string.IsNullOrEmpty(own)
                || string.Equals(own, ReasonPhrases.GetReasonPhrase(code), StringComparison.Ordinal)
            )
            {
                own = null;
            }

            own ??= Defaults.GetValueOrDefault(code);

            var addition =
                code == StatusCodes.Status400BadRequest
                    ? BindsFallibly(context.Description)
                        ? BindingFailureDescription
                        : null
                    : Additions.GetValueOrDefault(code);

            if (own is null && addition is null)
            {
                continue;
            }

            response.Description =
                own is null ? addition
                : addition is null ? own
                : own + " " + addition;
        }

        return Task.CompletedTask;
    }

    private static bool BindsFallibly(ApiDescription description) =>
        description.ParameterDescriptions.Any(parameter =>
        {
            if (parameter.Source == BindingSource.Body)
            {
                return true;
            }

            var isQuery = parameter.Source == BindingSource.Query;
            var isHeader = parameter.Source == BindingSource.Header;
            var isPath = parameter.Source == BindingSource.Path;
            if (!isQuery && !isHeader && !isPath)
            {
                return false;
            }

            var type = parameter.Type;
            var underlyingType = type is null ? null : Nullable.GetUnderlyingType(type) ?? type;
            return underlyingType != typeof(string) || (!isPath && parameter.IsRequired);
        });
}
