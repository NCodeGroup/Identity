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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// Writes an <see cref="IOpenIdResponse"/> to the HTTP response in a specific representation (for example HTML).
/// Formatters are registered as a strategy collection; <c>OpenIdResult&lt;T&gt;</c> performs server-driven content
/// negotiation over the registered formatters — selecting one whose <see cref="MediaTypes"/> an interactive client
/// explicitly requests via <c>Accept</c> and which <see cref="CanFormat"/> the response — and falls back to the
/// default JSON representation otherwise. The seam is open on both axes: a formatter decides which response types it
/// handles and which media types it emits, so a new representation (HTML error page, JARM form-post, …) is an additive
/// registration rather than a change to the result type.
/// </summary>
/// <remarks>
/// A formatter is <c>IResultExecutor&lt;T&gt;</c>-shaped: it manipulates the <see cref="HttpResponse"/>
/// directly (status code, content type, body). The response-level envelope (any
/// <c>ISupportResponseHeaders</c>) is applied by the caller before the formatter runs; the formatter owns the
/// content type and body for its representation. Register implementations as singletons.
/// </remarks>
[PublicAPI]
public interface IOpenIdResponseFormatter
{
    /// <summary>
    /// Gets the media types this formatter emits, in the formatter's own preference order (highest first). A formatter
    /// is selected only when an interactive client <em>explicitly</em> lists one of these media types in its
    /// <c>Accept</c> header; a wildcard such as <c>*/*</c> does not select a formatter, so non-browser callers keep the
    /// default JSON body.
    /// </summary>
    IReadOnlyList<string> MediaTypes { get; }

    /// <summary>
    /// Determines whether this formatter can write the specified response (for example, an HTML error formatter
    /// handles only an <see cref="IOpenIdError"/>).
    /// </summary>
    /// <param name="response">The <see cref="IOpenIdResponse"/> to be written.</param>
    /// <returns><see langword="true"/> when this formatter can write the response; otherwise, <see langword="false"/>.</returns>
    bool CanFormat(IOpenIdResponse response);

    /// <summary>
    /// Writes the response to the HTTP response body in this formatter's representation.
    /// </summary>
    /// <param name="context">The <see cref="OpenIdResponseFormatterContext"/> describing the negotiated write.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask WriteAsync(
        OpenIdResponseFormatterContext context,
        CancellationToken cancellationToken
    );
}
