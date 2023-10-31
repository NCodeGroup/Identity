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

namespace NIdentity.OpenId;

/// <summary>
/// Provides an ordered collection that contains no duplicate items and has O(1) access.
/// </summary>
/// <typeparam name="T">The type of the items in the collection.</typeparam>
public class UniqueCollection<T> : ICollection<T>, IReadOnlyCollection<T>
    where T : notnull
{
    private Dictionary<T, LinkedListNode<T>> Dictionary { get; }
    private LinkedList<T> LinkedList { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueCollection{T}"/> class.
    /// </summary>
    public UniqueCollection()
        : this(EqualityComparer<T>.Default)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueCollection{T}"/> class.
    /// </summary>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing items in the collection.</param>
    public UniqueCollection(IEqualityComparer<T> comparer)
    {
        Dictionary = new Dictionary<T, LinkedListNode<T>>(comparer);
        LinkedList = new LinkedList<T>();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueCollection{T}"/> class.
    /// </summary>
    /// <param name="collection">The collection whose items are copied to the new collection.</param>
    public UniqueCollection(IEnumerable<T> collection)
        : this(collection, EqualityComparer<T>.Default)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UniqueCollection{T}"/> class.
    /// </summary>
    /// <param name="collection">The collection whose items are copied to the new collection.</param>
    /// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing items in the collection.</param>
    public UniqueCollection(IEnumerable<T> collection, IEqualityComparer<T> comparer)
        : this(comparer)
    {
        if (collection.TryGetNonEnumeratedCount(out var count))
        {
            if (count == 0) return;
            Dictionary.EnsureCapacity(count);
        }

        foreach (var item in collection)
        {
            CoreAppend(item);
        }
    }

    /// <inheritdoc />
    public int Count => Dictionary.Count;

    int IReadOnlyCollection<T>.Count => Count;

    bool ICollection<T>.IsReadOnly => ((IDictionary)Dictionary).IsReadOnly;

    /// <inheritdoc />
    public bool Contains(T item) => Dictionary.ContainsKey(item);

    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>
    /// Adds an item to the current collection and returns a value to indicate if the item was successfully added.
    /// </summary>
    /// <param name="item">The item to add to the collection.</param>
    /// <returns><c>true</c> if the item is added to the collection; <c>false</c> if the item is already in the collection.</returns>
    public bool Add(T item)
    {
        if (Dictionary.ContainsKey(item))
            return false;

        CoreAppend(item);
        return true;
    }

    private void CoreAppend(T item)
    {
        var node = LinkedList.AddLast(item);
        Dictionary.Add(item, node);
    }

    /// <inheritdoc />
    public bool Remove(T item)
    {
        if (!Dictionary.Remove(item, out var node))
            return false;

        LinkedList.Remove(node);
        return true;
    }

    /// <inheritdoc />
    public void Clear()
    {
        LinkedList.Clear();
        Dictionary.Clear();
    }

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => LinkedList.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public IEnumerator<T> GetEnumerator() => LinkedList.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
