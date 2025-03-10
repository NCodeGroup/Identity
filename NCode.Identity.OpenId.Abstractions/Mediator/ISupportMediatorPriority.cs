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

namespace NCode.Identity.OpenId.Mediator;

/// <summary>
/// Provides the ability to prioritize the order in which collections of similar items are processed.
/// </summary>
public interface ISupportMediatorPriority
{
    /// <summary>
    /// Gets the numeric priority that determines the order in which collections of similar items are processed.
    /// Higher values are processed first and lower values are processed last. The default value is zero.
    /// </summary>
    int MediatorPriority { get; }
}
