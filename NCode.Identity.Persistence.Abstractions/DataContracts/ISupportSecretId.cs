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

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace NCode.Identity.Persistence.DataContracts;

/// <summary>
/// Indicates that an instance supports the <see cref="SecretId"/> property.
/// </summary>
[PublicAPI]
public interface ISupportSecretId
{
    /// <summary>
    /// Gets the natural identifier of the OpenId Secret that this instance belongs to.
    /// Also known as <c>kid</c> or <c>Key ID</c>.
    /// </summary>
    [MaxLength(MaxLengths.ResourceId)]
    string SecretId { get; }
}
