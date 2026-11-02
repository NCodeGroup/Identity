// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Results;

/// <summary>
/// Provides the ability to associate an HTTP Status Code with the current instance.
/// </summary>
[PublicAPI]
public interface ISupportStatusCode
{
    /// <summary>
    /// Gets or sets the HTTP status code to be used when returning a response.
    /// </summary>
    int? StatusCode { get; set; }
}
