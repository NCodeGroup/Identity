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

namespace NCode.Identity.Jwt;

public class PropertyBag : IDictionary<IPropertyBagKey, object?>, IReadOnlyDictionary<IPropertyBagKey, object?>
{
    private IDictionary<IPropertyBagKey, object?>? ItemsOrNull { get; set; }

    private IDictionary<IPropertyBagKey, object?> Items => ItemsOrNull ??= new Dictionary<IPropertyBagKey, object?>();

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBag"/> class.
    /// </summary>
    public PropertyBag()
    {
        // nothing
    }

    /// <summary>
    /// Returns a new instance of the <see cref="PropertyBag"/> class that is a shallow copy of the current instance.
    /// </summary>
    public PropertyBag Clone()
    {
        var newBag = new PropertyBag();

        var items = ItemsOrNull;
        if (items is { Count: > 0 })
        {
            newBag.ItemsOrNull = new Dictionary<IPropertyBagKey, object?>(items);
        }

        return newBag;
    }

    /// <summary>
    /// Sets the strongly typed value for the specified <paramref name="key"/> in the property bag.
    /// </summary>
    /// <param name="key">The key of the strongly typed value to set in the property bag.</param>
    /// <param name="value">The strongly typed value to set in the property bag.</param>
    /// <typeparam name="T">The type of the value to set in the property bag.</typeparam>
    public void Set<T>(PropertyBagKey<T> key, T value)
    {
        Items[key] = value;
    }

    /// <summary>
    /// Gets the strongly typed value associated with the specified key from the property bag.
    /// </summary>
    /// <param name="key">The key of the strongly typed value to get from the property bag.</param>
    /// <param name="value">When this method returns, contains the strongly typed value associated with the specified key,
    /// it the key is found; otherwise, the default value for the type of the <paramref name="value"/> parameter.
    /// This parameter is passed uninitialized.</param>
    /// <typeparam name="T">The type of the value to get from the property bag.</typeparam>
    /// <returns><c>true</c> if the <see cref="PropertyBag"/> contains an element with the specified key; otherwise,
    /// <c>false</c>.</returns>
    public bool TryGetValue<T>(PropertyBagKey<T> key, [MaybeNullWhen(false)] out T value)
    {
        if ((ItemsOrNull?.TryGetValue(key, out var baseValue) ?? false) && baseValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    private bool TryGetValue(IPropertyBagKey key, out object? value)
    {
        if (ItemsOrNull?.TryGetValue(key, out var baseValue) ?? false)
        {
            value = baseValue;
            return true;
        }

        value = default;
        return false;
    }

    private object? GetValue(IPropertyBagKey key)
    {
        if (!TryGetValue(key, out var value))
            throw new KeyNotFoundException();

        return value;
    }

    //

    IEnumerator IEnumerable.GetEnumerator() =>
        (ItemsOrNull ?? Enumerable.Empty<KeyValuePair<IPropertyBagKey, object?>>()).GetEnumerator();

    IEnumerator<KeyValuePair<IPropertyBagKey, object?>> IEnumerable<KeyValuePair<IPropertyBagKey, object?>>.GetEnumerator() =>
        (ItemsOrNull ?? Enumerable.Empty<KeyValuePair<IPropertyBagKey, object?>>()).GetEnumerator();

    //

    bool ICollection<KeyValuePair<IPropertyBagKey, object?>>.IsReadOnly =>
        ItemsOrNull?.IsReadOnly ?? false;

    int ICollection<KeyValuePair<IPropertyBagKey, object?>>.Count =>
        ItemsOrNull?.Count ?? 0;

    bool ICollection<KeyValuePair<IPropertyBagKey, object?>>.Contains(KeyValuePair<IPropertyBagKey, object?> item) =>
        ItemsOrNull?.Contains(item) ?? false;

    void ICollection<KeyValuePair<IPropertyBagKey, object?>>.CopyTo(KeyValuePair<IPropertyBagKey, object?>[] array, int arrayIndex) =>
        ItemsOrNull?.CopyTo(array, arrayIndex);

    void ICollection<KeyValuePair<IPropertyBagKey, object?>>.Clear() =>
        ItemsOrNull = null;

    void ICollection<KeyValuePair<IPropertyBagKey, object?>>.Add(KeyValuePair<IPropertyBagKey, object?> item) =>
        Items.Add(item);

    bool ICollection<KeyValuePair<IPropertyBagKey, object?>>.Remove(KeyValuePair<IPropertyBagKey, object?> item) =>
        ItemsOrNull?.Remove(item) ?? false;

    //

    ICollection<IPropertyBagKey> IDictionary<IPropertyBagKey, object?>.Keys =>
        ItemsOrNull?.Keys ?? Array.Empty<IPropertyBagKey>();

    ICollection<object?> IDictionary<IPropertyBagKey, object?>.Values =>
        ItemsOrNull?.Values ?? Array.Empty<object?>();

    object? IDictionary<IPropertyBagKey, object?>.this[IPropertyBagKey key]
    {
        get => GetValue(key);
        set => Items[key] = value;
    }

    bool IDictionary<IPropertyBagKey, object?>.ContainsKey(IPropertyBagKey key) =>
        ItemsOrNull?.ContainsKey(key) ?? false;

    bool IDictionary<IPropertyBagKey, object?>.TryGetValue(IPropertyBagKey key, out object? value) =>
        TryGetValue(key, out value);

    void IDictionary<IPropertyBagKey, object?>.Add(IPropertyBagKey key, object? value) =>
        Items.Add(key, value);

    bool IDictionary<IPropertyBagKey, object?>.Remove(IPropertyBagKey key) =>
        ItemsOrNull?.Remove(key) ?? false;

    //

    int IReadOnlyCollection<KeyValuePair<IPropertyBagKey, object?>>.Count =>
        ItemsOrNull?.Count ?? 0;

    IEnumerable<IPropertyBagKey> IReadOnlyDictionary<IPropertyBagKey, object?>.Keys =>
        ItemsOrNull?.Keys ?? Array.Empty<IPropertyBagKey>();

    IEnumerable<object?> IReadOnlyDictionary<IPropertyBagKey, object?>.Values =>
        ItemsOrNull?.Values ?? Array.Empty<object?>();

    object? IReadOnlyDictionary<IPropertyBagKey, object?>.this[IPropertyBagKey key] =>
        GetValue(key);

    bool IReadOnlyDictionary<IPropertyBagKey, object?>.ContainsKey(IPropertyBagKey key) =>
        ItemsOrNull?.ContainsKey(key) ?? false;

    bool IReadOnlyDictionary<IPropertyBagKey, object?>.TryGetValue(IPropertyBagKey key, out object? value) =>
        TryGetValue(key, out value);
}
