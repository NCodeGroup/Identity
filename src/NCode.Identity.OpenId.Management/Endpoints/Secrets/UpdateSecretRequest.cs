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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// Represents the request body to update the metadata of an existing server secret. Key material is immutable
/// once generated (rotation is create-new + delete-old). See ADR-0011.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class UpdateSecretRequest
{
    /// <summary>
    /// Gets the intended use for the secret, or <c>null</c> for any compatible use.
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the intended algorithm for the secret, or <c>null</c> for any compatible algorithm.
    /// </summary>
    public string? Algorithm { get; init; }

    /// <summary>
    /// Gets the <see cref="DateTimeOffset"/> when the secret expires and is no longer valid.
    /// </summary>
    public required DateTimeOffset ExpiresWhen { get; init; }
}
