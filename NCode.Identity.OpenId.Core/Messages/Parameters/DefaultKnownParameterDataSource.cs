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

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides the default implementation for a data source collection of <see cref="KnownParameter"/> instances supported by this library.
/// </summary>
public class DefaultKnownParameterDataSource : ICollectionDataSource<KnownParameter>
{
    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken.Singleton;

    /// <inheritdoc />
    public IEnumerable<KnownParameter> Collection
    {
        get
        {
            var type = typeof(KnownParameters);

            var fromFields = type
                .GetFields()
                .Where(x => x.FieldType == typeof(KnownParameter))
                .Select(x => x.GetValue(null))
                .OfType<KnownParameter>();

            var fromProperties = type
                .GetProperties()
                .Where(x => x.PropertyType == typeof(KnownParameter) && x.CanRead)
                .Select(x => x.GetValue(null))
                .OfType<KnownParameter>();

            return fromFields.Concat(fromProperties);
        }
    }
}
