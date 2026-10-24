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

using System.Collections.Immutable;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Provides the registered <see cref="RateLimitDescriptor"/> instances by the stage that judges them.
/// </summary>
// See ADR-0246.
internal interface IRateLimitDescriptorCatalog
{
    /// <summary>
    /// Gets the descriptors of a stage in the order they are judged: by <see cref="RateLimitDescriptor.Order"/>, then by
    /// <see cref="RateLimitDescriptor.Name"/>.
    /// </summary>
    /// <param name="stage">The name of the stage, such as one of the <see cref="RateLimitStages"/>.</param>
    /// <returns>The descriptors of the stage, which is empty when none registered for it.</returns>
    ImmutableArray<RateLimitDescriptor> GetStage(string stage);
}
