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
/// A complimentary enumeration to <see cref="SerializationFormat"/> that allows for multiple serialization formats to be specified.
/// </summary>
[Flags]
[PublicAPI]
public enum SerializationFormats
{
    /// <summary>
    /// Indicates that no serialization formats are specified.
    /// </summary>
    None = 0,

    /// <summary>
    /// Indicates that the <see cref="SerializationFormat.Json"/> serialization format is specified.
    /// </summary>
    Json = 1,

    /// <summary>
    /// Indicates that the <see cref="SerializationFormat.OpenId"/> serialization format is specified.
    /// </summary>
    OpenId = 2,
}
