// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Messages;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// The default <see cref="IOpenIdResponseFormatter"/> that writes any <see cref="IOpenIdResponse"/> as an
/// <c>application/json</c> body — the OAuth/OpenID Connect wire default. It can format every response, so it is the
/// fallback the negotiation resolves to when no interactive client explicitly requests an alternate representation.
/// </summary>
internal sealed class DefaultJsonOpenIdResponseFormatter : IOpenIdResponseFormatter
{
    /// <inheritdoc />
    public IReadOnlyList<string> MediaTypes { get; } = [OpenIdProtocol.MediaTypes.Json];

    /// <inheritdoc />
    public bool CanFormat(IOpenIdResponse response) => true;

    /// <inheritdoc />
    public async ValueTask WriteAsync(
        OpenIdResponseFormatterContext context,
        CancellationToken cancellationToken
    )
    {
        var response = context.Response;
        var jsonSerializerOptions = response.OpenIdEnvironment.JsonSerializerOptions;

        var jsonResult = HttpResults.Json(
            response,
            jsonSerializerOptions.GetTypeInfo(response.GetType()),
            statusCode: context.StatusCode
        );

        await jsonResult.ExecuteAsync(context.HttpContext);
    }
}
