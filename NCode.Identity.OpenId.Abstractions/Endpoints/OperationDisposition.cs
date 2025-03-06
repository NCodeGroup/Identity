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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Errors;

namespace NCode.Identity.OpenId.Endpoints;

/// <summary>
/// Indicates the disposition of an <c>OAuth</c> or <c>OpenID Connect</c> operation.
/// </summary>
[PublicAPI]
public class OperationDisposition
{
    /// <summary>
    /// Gets or sets an <see cref="IOpenIdError"/> that represents the error that occurred during the operation.
    /// </summary>
    public IOpenIdError? OpenIdError { get; set; }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(OpenIdError))]
    public bool Succeeded => OpenIdError is null;

    /// <summary>
    /// Gets a value indicating whether <see cref="OpenIdError"/> is not <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(OpenIdError))]
    public bool HasOpenIdError => OpenIdError is not null;
}
