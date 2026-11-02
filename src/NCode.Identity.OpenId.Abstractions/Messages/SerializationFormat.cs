// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Specifies the serialization format to use when serializing a value.
/// </summary>
[PublicAPI]
public enum SerializationFormat
{
    /// <summary>
    /// Indicates that a value should be serialized as JSON.
    /// </summary>
    Json = 0,

    /// <summary>
    /// Indicates that value should be serialized using OpenID standards.
    /// </summary>
    OpenId = 1,
}
