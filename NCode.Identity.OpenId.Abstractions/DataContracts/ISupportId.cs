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

namespace NCode.Identity.OpenId.DataContracts;

/// <summary>
/// Provides the ability to return the surrogate key for an entity where the key type is <see cref="long"/>.
/// </summary>
public interface ISupportId : ISupportId<long>
{
    // nothing
}

/// <summary>
/// Provides the ability to return the surrogate key for an entity.
/// </summary>
/// <typeparam name="TKey">The type of the surrogate key.</typeparam>
public interface ISupportId<out TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Gets the surrogate key.
    /// </summary>
    TKey Id { get; }
}
