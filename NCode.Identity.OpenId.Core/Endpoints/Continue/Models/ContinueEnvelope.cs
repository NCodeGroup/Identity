#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Text.Json;

namespace NCode.Identity.OpenId.Endpoints.Continue.Models;

/// <summary>
/// Contains the properties that will be persisted in order to continue an operation.
/// </summary>
public readonly struct ContinueEnvelope
{
    /// <summary>
    /// Gets or sets the <see cref="string"/> code that contains the type of operation to continue.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets or sets the <see cref="JsonElement"/> payload that contains any required data to continue the operation.
    /// </summary>
    public required JsonElement PayloadJson { get; init; }
}
