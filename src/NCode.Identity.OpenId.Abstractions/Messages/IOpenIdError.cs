// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Results;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Contains the details for an <c>OAuth</c> or <c>OpenID Connect</c> error response.
/// </summary>
[PublicAPI]
public interface IOpenIdError : IOpenIdMessageResponse, ISupportStatusCode, ISupportResponseHeaders
{
    /// <summary>
    /// Gets or sets the <see cref="Exception"/> that triggered the <c>OAuth</c> or <c>OpenID Connect</c> error.
    /// May be <c>null</c> when protocol violations occur such as validations or other non-transient errors.
    /// </summary>
    Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the <c>error</c> parameter.
    /// </summary>
    string Code { get; set; }

    /// <summary>
    /// Gets or sets the <c>error_description</c> parameter.
    /// </summary>
    string? Description { get; set; }

    /// <summary>
    /// Gets or sets the <c>error_uri</c> parameter.
    /// </summary>
    Uri? Uri { get; set; }

    /// <summary>
    /// Gets or sets the <c>state</c> parameter.
    /// </summary>
    string? State { get; set; }
}
