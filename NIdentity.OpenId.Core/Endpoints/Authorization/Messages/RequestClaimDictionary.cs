#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

internal class RequestClaimDictionary :
    IDictionary<string, RequestClaim?>,
    IReadOnlyDictionary<string, RequestClaim?>,
    IReadOnlyDictionary<string, IRequestClaim?>
{
    private IDictionary<string, RequestClaim?> Inner { get; }

    public RequestClaimDictionary()
        : this(StringComparer.Ordinal)
    {
        // nothing
    }

    public RequestClaimDictionary(StringComparer comparer)
    {
        Inner = new Dictionary<string, RequestClaim?>(comparer);
    }

    public int Count => Inner.Count;

    public bool ContainsKey(string key)
    {
        return Inner.ContainsKey(key);
    }

    public bool TryGetValue(string key, out RequestClaim? value)
    {
        return Inner.TryGetValue(key, out value);
    }

    public bool TryGetValue(string key, out IRequestClaim? value)
    {
        if (Inner.TryGetValue(key, out var innerValue))
        {
            value = innerValue;
            return true;
        }

        value = default;
        return false;
    }

    #region IEnumerable Members

    IEnumerator IEnumerable.GetEnumerator()
    {
        // since KeyValuePair is a struct, it doesn't support variance
        throw new NotSupportedException();
    }

    IEnumerator<KeyValuePair<string, RequestClaim?>> IEnumerable<KeyValuePair<string, RequestClaim?>>.
        GetEnumerator()
    {
        return Inner.GetEnumerator();
    }

    public IEnumerator<KeyValuePair<string, IRequestClaim?>> GetEnumerator()
    {
        return Inner.Select(kvp => KeyValuePair.Create<string, IRequestClaim?>(kvp.Key, kvp.Value))
            .GetEnumerator();
    }

    #endregion

    #region ICollection Members

    bool ICollection<KeyValuePair<string, RequestClaim?>>.IsReadOnly => Inner.IsReadOnly;

    bool ICollection<KeyValuePair<string, RequestClaim?>>.Contains(KeyValuePair<string, RequestClaim?> item)
    {
        return Inner.Contains(item);
    }

    void ICollection<KeyValuePair<string, RequestClaim?>>.CopyTo(
        KeyValuePair<string, RequestClaim?>[] array,
        int arrayIndex)
    {
        Inner.CopyTo(array, arrayIndex);
    }

    void ICollection<KeyValuePair<string, RequestClaim?>>.Add(KeyValuePair<string, RequestClaim?> item)
    {
        Inner.Add(item);
    }

    bool IDictionary<string, RequestClaim?>.Remove(string key)
    {
        return Inner.Remove(key);
    }

    bool ICollection<KeyValuePair<string, RequestClaim?>>.Remove(KeyValuePair<string, RequestClaim?> item)
    {
        return Inner.Remove(item);
    }

    void ICollection<KeyValuePair<string, RequestClaim?>>.Clear()
    {
        Inner.Clear();
    }

    #endregion

    #region IDictionary Members

    ICollection<string> IDictionary<string, RequestClaim?>.Keys => Inner.Keys;

    ICollection<RequestClaim?> IDictionary<string, RequestClaim?>.Values => Inner.Values;

    RequestClaim? IDictionary<string, RequestClaim?>.this[string key]
    {
        get => Inner[key];
        set => Inner[key] = value;
    }

    void IDictionary<string, RequestClaim?>.Add(string key, RequestClaim? value)
    {
        Inner.Add(key, value);
    }

    #endregion

    #region IReadOnlyDictionary Members

    IEnumerable<string> IReadOnlyDictionary<string, RequestClaim?>.Keys => Inner.Keys;

    IEnumerable<string> IReadOnlyDictionary<string, IRequestClaim?>.Keys => Inner.Keys;

    IEnumerable<RequestClaim?> IReadOnlyDictionary<string, RequestClaim?>.Values => Inner.Values;

    IEnumerable<IRequestClaim?> IReadOnlyDictionary<string, IRequestClaim?>.Values => Inner.Values;

    RequestClaim? IReadOnlyDictionary<string, RequestClaim?>.this[string key] => Inner[key];

    IRequestClaim? IReadOnlyDictionary<string, IRequestClaim?>.this[string key] => Inner[key];

    #endregion
}
