#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

namespace NCode.Identity.Endpoints;

/// <summary>
/// Indicates the disposition of an operation.
/// </summary>
[PublicAPI]
public class OperationDisposition<TError>
{
    /// <summary>
    /// Gets or sets the error that occurred during the operation.
    /// </summary>
    public TError? Error { get; set; }

    /// <summary>
    /// Gets a value indicating whether the operation was successful.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public bool Succeeded => Error is null;

    /// <summary>
    /// Gets a value indicating whether <see cref="Error"/> is not <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool HasError => Error is not null;
}
