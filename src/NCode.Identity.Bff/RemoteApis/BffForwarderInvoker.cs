// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// The invoker that <see cref="BffForwarderHttpClientFactory"/> returns: the BFF's handlers in front of an invoker that
/// another factory created, which it keeps so that a later configuration can reuse it.
/// </summary>
/// <param name="handler">The first of the BFF's handlers.</param>
/// <param name="inner">The invoker that the BFF's handlers end in.</param>
internal sealed class BffForwarderInvoker(HttpMessageHandler handler, HttpMessageInvoker inner)
    : HttpMessageInvoker(handler, disposeHandler: true)
{
    /// <summary>
    /// Gets the invoker that the BFF's handlers end in.
    /// </summary>
    public HttpMessageInvoker Inner { get; } = inner;
}
