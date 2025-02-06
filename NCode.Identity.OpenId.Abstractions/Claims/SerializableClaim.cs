#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Security.Claims;

namespace NCode.Identity.OpenId.Claims;

/// <summary>
/// Contains the serialized representation of a <see cref="Claim"/> instance.
/// </summary>
public class SerializableClaim
{
    /// <summary>
    /// Gets or sets the value for the <see cref="Claim.Type"/> property.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="Claim.Value"/> property.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="Claim.ValueType"/> property.
    /// </summary>
    public required string? ValueType { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="Claim.Issuer"/> property.
    /// </summary>
    public required string? Issuer { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="Claim.OriginalIssuer"/> property.
    /// </summary>
    public required string? OriginalIssuer { get; init; }

    /// <summary>
    /// Gets or sets the value for the <see cref="Claim.Properties"/> property.
    /// </summary>
    public required IDictionary<string, string> Properties { get; init; }

    /// <summary>
    /// Gets or sets the unique reference identifier for the <see cref="Claim.Subject"/> property.
    /// </summary>
    public string? SubjectRef { get; init; }
}
