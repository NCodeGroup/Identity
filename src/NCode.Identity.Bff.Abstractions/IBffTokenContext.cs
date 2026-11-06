// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff;

/// <summary>
/// Represents the context in which a token requirement obtains a token: a request to an endpoint the BFF guards, or a
/// connection that such a request opened.
/// </summary>
[PublicAPI]
public interface IBffTokenContext
{
    /// <summary>
    /// Gets the name of the frontend whose client registration obtains the token, or <see langword="null"/> when no
    /// frontend selects the request.
    /// </summary>
    string? Frontend { get; }

    /// <summary>
    /// Gets the request to an endpoint the BFF guards, or <see langword="null"/> for the work a connection does after
    /// the guarded hub request that opened it.
    /// </summary>
    HttpContext? HttpContext { get; }
}
