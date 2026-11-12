// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Text;
using Microsoft.AspNetCore.Http;
using NCode.Buffers;
using NCode.Identity.AspNetCore;
using Nerdbank.Streams;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// An <see cref="IResult"/> that writes a server-rendered page, encoded as UTF-8, straight to the response body pipe
/// and returns its pooled buffer.
/// </summary>
/// <param name="html">The rendered page, which the result disposes once it is written.</param>
/// <param name="policy">The security policy that the page is served under.</param>
internal sealed class InteractiveHtmlResult(Sequence<char> html, HtmlPageSecurityPolicy policy)
    : IResult,
        IContentTypeHttpResult
{
    /// <summary>
    /// The content type of every server-rendered page.
    /// </summary>
    public const string HtmlContentType = "text/html; charset=utf-8";

    private Sequence<char> Html { get; } = html;
    private HtmlPageSecurityPolicy Policy { get; } = policy;

    /// <inheritdoc />
    public string ContentType => HtmlContentType;

    /// <summary>
    /// Creates a pooled buffer to render a page into.
    /// </summary>
    public static Sequence<char> CreateBuffer() =>
        BufferFactory.CreatePooledBufferWriter<char>(isSensitive: false);

    /// <summary>
    /// Writes <paramref name="html"/> to <paramref name="httpResponse"/> as a UTF-8 <c>text/html</c> body under the
    /// security headers of <paramref name="policy"/>.
    /// </summary>
    public static async ValueTask WriteAsync(
        HttpResponse httpResponse,
        ReadOnlySequence<char> html,
        HtmlPageSecurityPolicy policy,
        CancellationToken cancellationToken
    )
    {
        httpResponse.SetHtmlPageSecurity(policy);
        httpResponse.ContentType = HtmlContentType;
        SecureEncoding.UTF8.GetBytes(html, httpResponse.BodyWriter);
        await httpResponse.BodyWriter.FlushAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        using (Html)
        {
            await WriteAsync(
                httpContext.Response,
                Html.AsReadOnlySequence,
                Policy,
                httpContext.RequestAborted
            );
        }
    }
}
