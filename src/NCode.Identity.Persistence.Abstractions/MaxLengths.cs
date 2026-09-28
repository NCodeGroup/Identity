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

using JetBrains.Annotations;

namespace NCode.Identity.Persistence;

/// <summary>
/// Contains constants for the maximum lengths of various fields.
/// </summary>
[PublicAPI]
public static class MaxLengths
{
    /// <summary>
    /// Specifies the maximum length of a resource's type discriminator.
    /// </summary>
    public const int ResourceType = 100;

    /// <summary>
    /// Specifies the maximum length of a resource's identifier.
    /// </summary>
    public const int ResourceId = 300;

    /// <summary>
    /// Specifies the maximum length of a concurrency token.
    /// </summary>
    public const int ConcurrencyToken = 50;
}
