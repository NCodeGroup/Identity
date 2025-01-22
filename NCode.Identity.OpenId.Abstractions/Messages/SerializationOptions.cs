#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Specifies the options for when serializing an <see cref="IOpenIdMessage"/> instance.
/// </summary>
[Flags]
[PublicAPI]
public enum SerializationOptions
{
    /// <summary>
    /// Indicates that no special options are applied when serializing the <see cref="IOpenIdMessage"/> instance.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates that the <see cref="IOpenIdMessage"/> instance is being serialized as an HTTP response
    /// where the <c>$type</c> and <c>$properties</c> metadata properties should be ignored.
    /// </summary>
    HttpResult = IgnoreWritingType | IgnoreWritingProperties,

    /// <summary>
    /// Indicates that the <c>$type</c> metadata property should be ignored when serializing the <see cref="IOpenIdMessage"/> instance.
    /// </summary>
    IgnoreWritingType = 1,

    /// <summary>
    /// Indicates that the <c>$properties</c> metadata property should be ignored when serializing the <see cref="IOpenIdMessage"/> instance.
    /// </summary>
    IgnoreWritingProperties = 2,
}
