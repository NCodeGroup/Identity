// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json.Serialization;
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a collection of <see cref="JsonConverter"/> instances for OpenID-related types.
/// </summary>
public interface IOpenIdJsonConverterProvider
{
    /// <summary>
    /// Gets a collection of <see cref="JsonConverter"/> instances for the specified OpenID environment.
    /// </summary>
    /// <param name="openIdEnvironment">The OpenID environment for which to retrieve the converters.</param>
    /// <returns>The collection of <see cref="JsonConverter"/> instances.</returns>
    IEnumerable<JsonConverter> GetJsonConverters(OpenIdEnvironment openIdEnvironment);
}
