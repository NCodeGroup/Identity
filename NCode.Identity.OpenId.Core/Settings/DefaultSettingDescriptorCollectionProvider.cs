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
using NCode.Collections.Providers;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides a default implementation of the <see cref="ISettingDescriptorCollectionProvider"/> abstraction.
/// </summary>
public class DefaultSettingDescriptorCollectionProvider(
    ICollectionProviderFactory collectionProviderFactory,
    IEnumerable<ICollectionDataSource<SettingDescriptor>> dataSources
) : ISettingDescriptorCollectionProvider
{
    private ICollectionProvider<SettingDescriptor, ISettingDescriptorCollection> Inner { get; } =
        collectionProviderFactory.Create(
            items => new SettingDescriptorCollection(items),
            dataSources,
            owns: false
        );

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return Inner.DisposeAsync();
    }

    /// <inheritdoc />
    public ISettingDescriptorCollection Collection => Inner.Collection;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => Inner.GetChangeToken();
}
