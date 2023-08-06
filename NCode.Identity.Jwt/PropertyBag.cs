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

public class PropertyBag : IDictionary<string, object?>, IReadOnlyDictionary<string, object?>
{
    private StringComparer Comparer { get; }

    private IDictionary<string, object?>? ItemsOrNull { get; set; }

    private IDictionary<string, object?> Items => ItemsOrNull ??= new Dictionary<string, object?>(Comparer);

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBag"/> class.
    /// </summary>
    public PropertyBag()
        : this(StringComparer.Ordinal)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBag"/> class with the specified <see cref="StringComparer"/>.
    /// </summary>
    /// <param name="comparer">The <see cref="StringComparer"/> to use for comparing keys.</param>
    public PropertyBag(StringComparer comparer)
    {
        Comparer = comparer;
    }

    /// <summary>
    /// Returns a new instance of the <see cref="PropertyBag"/> class that is a shallow copy of the current instance.
    /// </summary>
    public PropertyBag Clone()
    {
        var newBag = new PropertyBag(Comparer);

        var items = ItemsOrNull;
        if (items is { Count: > 0 })
        {
            newBag.ItemsOrNull = new Dictionary<string, object?>(items, Comparer);
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
        Items[key.Name] = value;
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
        if ((ItemsOrNull?.TryGetValue(key.Name, out var baseValue) ?? false) && baseValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    //

    IEnumerator IEnumerable.GetEnumerator() =>
        (ItemsOrNull ?? Enumerable.Empty<KeyValuePair<string, object?>>()).GetEnumerator();

    IEnumerator<KeyValuePair<string, object?>> IEnumerable<KeyValuePair<string, object?>>.GetEnumerator() =>
        (ItemsOrNull ?? Enumerable.Empty<KeyValuePair<string, object?>>()).GetEnumerator();

    //

    bool ICollection<KeyValuePair<string, object?>>.IsReadOnly =>
        ItemsOrNull?.IsReadOnly ?? false;

    int ICollection<KeyValuePair<string, object?>>.Count =>
        ItemsOrNull?.Count ?? 0;

    bool ICollection<KeyValuePair<string, object?>>.Contains(KeyValuePair<string, object?> item) =>
        ItemsOrNull?.Contains(item) ?? false;

    void ICollection<KeyValuePair<string, object?>>.CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) =>
        ItemsOrNull?.CopyTo(array, arrayIndex);

    void ICollection<KeyValuePair<string, object?>>.Clear()
    {
        var items = ItemsOrNull;
        if (items is { Count: > 0 })
            items.Clear();
    }

    void ICollection<KeyValuePair<string, object?>>.Add(KeyValuePair<string, object?> item) =>
        Items.Add(item);

    bool ICollection<KeyValuePair<string, object?>>.Remove(KeyValuePair<string, object?> item)
    {
        var items = ItemsOrNull;
        return items is { Count: > 0 } && items.Remove(item);
    }

    //

    ICollection<string> IDictionary<string, object?>.Keys =>
        ItemsOrNull?.Keys ?? Array.Empty<string>();

    ICollection<object?> IDictionary<string, object?>.Values =>
        ItemsOrNull?.Values ?? Array.Empty<object?>();

    object? IDictionary<string, object?>.this[string key]
    {
        get
        {
            var items = ItemsOrNull;
            return items is { Count: > 0 } ? items[key] : throw new KeyNotFoundException();
        }
        set => Items[key] = value;
    }

    bool IDictionary<string, object?>.ContainsKey(string key) =>
        ItemsOrNull?.ContainsKey(key) ?? false;

    bool IDictionary<string, object?>.TryGetValue(string key, out object? value)
    {
        var items = ItemsOrNull;
        if (items is { Count: > 0 })
            return items.TryGetValue(key, out value);
        value = default;
        return false;
    }

    void IDictionary<string, object?>.Add(string key, object? value) =>
        Items.Add(key, value);

    bool IDictionary<string, object?>.Remove(string key)
    {
        var items = ItemsOrNull;
        return items is { Count: > 0 } && Items.Remove(key);
    }

    //

    int IReadOnlyCollection<KeyValuePair<string, object?>>.Count =>
        ItemsOrNull?.Count ?? 0;

    IEnumerable<string> IReadOnlyDictionary<string, object?>.Keys =>
        ItemsOrNull?.Keys ?? Array.Empty<string>();

    IEnumerable<object?> IReadOnlyDictionary<string, object?>.Values =>
        ItemsOrNull?.Values ?? Array.Empty<object?>();

    object? IReadOnlyDictionary<string, object?>.this[string key]
    {
        get
        {
            var items = ItemsOrNull;
            return items is { Count: > 0 } ? items[key] : throw new KeyNotFoundException();
        }
    }

    bool IReadOnlyDictionary<string, object?>.ContainsKey(string key) =>
        ItemsOrNull?.ContainsKey(key) ?? false;

    bool IReadOnlyDictionary<string, object?>.TryGetValue(string key, out object? value)
    {
        var items = ItemsOrNull;
        if (items is { Count: > 0 })
            return items.TryGetValue(key, out value);
        value = default;
        return false;
    }
}
