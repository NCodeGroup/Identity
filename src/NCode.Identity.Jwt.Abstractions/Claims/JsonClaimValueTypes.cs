// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Claims;

/// <summary>
/// Constants for <see cref="Claim.ValueType"/> that can be used to indicate how <see cref="Claim.Value"/> should be interpreted.
/// </summary>
[PublicAPI]
public static class JsonClaimValueTypes
{
    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is a <c>JSON</c> object.
    /// </summary>
    public const string Json = "JSON";

    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is a <c>JSON</c> array.
    /// </summary>
    public const string JsonArray = "JSON_ARRAY";

    /// <summary>
    /// A value that indicates the <see cref="Claim.Value"/> is <c>null</c>.
    /// </summary>
    public const string JsonNull = "JSON_NULL";
}
