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

using Microsoft.Extensions.Primitives;
using NCode.Jose.Internal;

namespace NCode.Jose.Algorithms.DataSources;

/// <summary>
/// Provides a base implementation for <see cref="IAlgorithmDataSource"/> that returns a static collection of
/// <see cref="IAlgorithm"/> instances.
/// </summary>
public abstract class StaticAlgorithmDataSource : IAlgorithmDataSource
{
    /// <inheritdoc />
    public abstract IEnumerable<IAlgorithm> Algorithms { get; }

    /// <inheritdoc />
    public virtual IChangeToken GetChangeToken() => NullChangeToken.Singleton;
}
