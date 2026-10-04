#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Represents a validation failure produced by a management operation. It carries the HTTP status code and a
/// fixed, safe detail message that is returned to the caller (never a raw exception message).
/// </summary>
[PublicAPI]
public sealed class ManagementError
{
    /// <summary>
    /// Gets the HTTP status code that describes the failure.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// Gets the fixed, safe detail message describing the failure.
    /// </summary>
    public required string Detail { get; init; }
}
