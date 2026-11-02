// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// One request a client sent through a <see cref="RecordingHttpMessageHandler"/>, with the parts of the response it received.
/// </summary>
/// <param name="Path">The absolute path of the request URI, such as <c>/oauth2/par</c>.</param>
/// <param name="AuthorizationScheme">The scheme of the request's <c>Authorization</c> header, or <see langword="null"/>.</param>
/// <param name="Body">The request's body as text, or <see langword="null"/> when it has none.</param>
/// <param name="StatusCode">The response's status code.</param>
/// <param name="DPoPProof">The request's <c>DPoP</c> header, or <see langword="null"/> when it has none.</param>
/// <param name="DPoPNonce">The response's <c>DPoP-Nonce</c> header, or <see langword="null"/> when it has none.</param>
/// <param name="ResponseBody">The response's body as text, or <see langword="null"/> when it has none.</param>
internal sealed record RecordedHttpExchange(
    string Path,
    string? AuthorizationScheme,
    string? Body,
    HttpStatusCode StatusCode,
    string? DPoPProof = null,
    string? DPoPNonce = null,
    string? ResponseBody = null
);
