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

using NCode.Collections.Providers;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides a default implementation for the <see cref="IAlgorithmProvider"/> interface.
/// </summary>
public class DefaultAlgorithmProvider : CollectionProvider<Algorithm, IAlgorithmCollection>, IAlgorithmProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAlgorithmProvider"/> class with the specified collection of <see cref="ICollectionDataSource{Algorithm}"/> instances.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ICollectionDataSource{Algorithm}"/> instances to aggregate.</param>
    public DefaultAlgorithmProvider(IEnumerable<ICollectionDataSource<Algorithm>> dataSources)
        : base(dataSources)
    {
        // nothing
    }

    /// <inheritdoc />
    protected override IAlgorithmCollection CreateCollection(IEnumerable<Algorithm> items)
    {
        return new AlgorithmCollection(items);
    }
}
