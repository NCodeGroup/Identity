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

using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NCode.PropertyBag;

/// <summary>
/// Provides a default implementation of the <see cref="IPropertyBag"/> abstraction.
/// </summary>
public class DefaultPropertyBag : IPropertyBag, IReadOnlyDictionary<PropertyBagKey, object?>
{
    private Dictionary<PropertyBagKey, object?>? ItemsOrNull { get; set; }

    private Dictionary<PropertyBagKey, object?> Items => ItemsOrNull ??= new Dictionary<PropertyBagKey, object?>();

    /// <inheritdoc />
    public IPropertyBag Clone()
    {
        var newBag = new DefaultPropertyBag();

        var items = ItemsOrNull;
        if (items is { Count: > 0 })
        {
            newBag.ItemsOrNull = new Dictionary<PropertyBagKey, object?>(items);
        }

        return newBag;
    }

    /// <inheritdoc />
    public IPropertyBag Set<T>(PropertyBagKey<T> key, T value)
    {
        Items[key] = value;
        return this;
    }

    /// <inheritdoc />
    public bool TryGetValue<T>(PropertyBagKey<T> key, [MaybeNullWhen(false)] out T value)
    {
        if (TryGetBase(key, out var baseValue) && baseValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    private bool TryGetBase(PropertyBagKey key, out object? value)
    {
        if (ItemsOrNull?.TryGetValue(key, out var baseValue) ?? false)
        {
            value = baseValue;
            return true;
        }

        value = default;
        return false;
    }

    //

    IEnumerator IEnumerable.GetEnumerator() =>
        (ItemsOrNull ?? Enumerable.Empty<KeyValuePair<PropertyBagKey, object?>>()).GetEnumerator();

    IEnumerator<KeyValuePair<PropertyBagKey, object?>> IEnumerable<KeyValuePair<PropertyBagKey, object?>>.GetEnumerator() =>
        (ItemsOrNull ?? Enumerable.Empty<KeyValuePair<PropertyBagKey, object?>>()).GetEnumerator();

    int IReadOnlyCollection<KeyValuePair<PropertyBagKey, object?>>.Count =>
        ItemsOrNull?.Count ?? 0;

    IEnumerable<PropertyBagKey> IReadOnlyDictionary<PropertyBagKey, object?>.Keys =>
        ItemsOrNull?.Keys ?? Enumerable.Empty<PropertyBagKey>();

    IEnumerable<object?> IReadOnlyDictionary<PropertyBagKey, object?>.Values =>
        ItemsOrNull?.Values ?? Enumerable.Empty<object?>();

    object? IReadOnlyDictionary<PropertyBagKey, object?>.this[PropertyBagKey key] =>
        TryGetBase(key, out var value) ? value : default;

    bool IReadOnlyDictionary<PropertyBagKey, object?>.ContainsKey(PropertyBagKey key) =>
        ItemsOrNull?.ContainsKey(key) ?? false;

    bool IReadOnlyDictionary<PropertyBagKey, object?>.TryGetValue(PropertyBagKey key, out object? value) =>
        TryGetBase(key, out value);
}
