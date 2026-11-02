// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;

namespace NCode.Identity.OpenId.Messages.Parameters;

/// <summary>
/// Provides the default implementation for a data source collection of <see cref="KnownParameter"/> instances supported by this library.
/// </summary>
internal class DefaultCommonParameterDataSource(INullChangeToken nullChangeToken)
    : ICollectionDataSource<KnownParameter>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<KnownParameter> Collection
    {
        get
        {
            var type = typeof(OpenIdCommonParameters);

            var fromFields = type.GetFields()
                .Where(x => typeof(KnownParameter).IsAssignableFrom(x.FieldType))
                .Select(x => x.GetValue(null))
                .OfType<KnownParameter>();

            var fromProperties = type.GetProperties()
                .Where(x => typeof(KnownParameter).IsAssignableFrom(x.PropertyType) && x.CanRead)
                .Select(x => x.GetValue(null))
                .OfType<KnownParameter>();

            return fromFields.Concat(fromProperties);
        }
    }
}
