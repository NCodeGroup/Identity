// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Errors;

/// <summary>
/// Provides the ability to associate an <see cref="IOpenIdError"/> with the current instance.
/// </summary>
[PublicAPI]
public interface ISupportOpenIdError
{
    /// <summary>
    /// Gets the <see cref="IOpenIdError"/> that contains detailed error information.
    /// </summary>
    IOpenIdError? Error { get; }
}
