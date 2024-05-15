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

namespace NCode.Identity.Persistence.DataContracts;

/// <summary>
/// Provides the ability to return the surrogate identifier for an entity where the identifier type is <see cref="long"/>.
/// </summary>
public interface ISupportId : ISupportId<long>
{
    // nothing
}

/// <summary>
/// Provides the ability to return the surrogate identifier for an entity.
/// </summary>
/// <typeparam name="T">The type of the surrogate identifier.</typeparam>
public interface ISupportId<out T>
    where T : IEquatable<T>
{
    /// <summary>
    /// Gets the surrogate identifier for this entity.
    /// A value of <c>0</c> indicates that this entity has not been persisted to storage yet.
    /// </summary>
    T Id { get; }
}
