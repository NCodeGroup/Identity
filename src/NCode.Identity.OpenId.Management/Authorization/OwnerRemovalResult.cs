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

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Describes the outcome of removing an owner from a resource.
/// </summary>
[PublicAPI]
public enum OwnerRemovalResult
{
    /// <summary>
    /// The owner assignment was removed.
    /// </summary>
    Removed = 0,

    /// <summary>
    /// The principal was not an owner of the resource; nothing was removed.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// The removal was refused because it would leave the resource with no owner.
    /// </summary>
    LastOwnerForbidden = 2,
}
