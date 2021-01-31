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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NIdentity.OpenId.Messages.Authorization
{
    internal class JsonRequestClaimDictionary :
        IDictionary<string, JsonRequestClaim?>,
        IReadOnlyDictionary<string, JsonRequestClaim?>,
        IReadOnlyDictionary<string, IRequestClaim?>
    {
        private readonly IDictionary<string, JsonRequestClaim?> _inner;

        public JsonRequestClaimDictionary() : this(StringComparer.Ordinal)
        {
            // nothing
        }

        public JsonRequestClaimDictionary(StringComparer comparer)
        {
            _inner = new Dictionary<string, JsonRequestClaim?>(comparer);
        }

        public int Count => _inner.Count;

        public bool ContainsKey(string key)
        {
            return _inner.ContainsKey(key);
        }

        public bool TryGetValue(string key, out JsonRequestClaim? value)
        {
            return _inner.TryGetValue(key, out value);
        }

        public bool TryGetValue(string key, out IRequestClaim? value)
        {
            if (_inner.TryGetValue(key, out var innerValue))
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

        IEnumerator<KeyValuePair<string, JsonRequestClaim?>> IEnumerable<KeyValuePair<string, JsonRequestClaim?>>.GetEnumerator()
        {
            return _inner.GetEnumerator();
        }

        public IEnumerator<KeyValuePair<string, IRequestClaim?>> GetEnumerator()
        {
            return _inner.Select(kvp => KeyValuePair.Create<string, IRequestClaim?>(kvp.Key, kvp.Value)).GetEnumerator();
        }

        #endregion

        #region ICollection Members

        bool ICollection<KeyValuePair<string, JsonRequestClaim?>>.IsReadOnly => _inner.IsReadOnly;

        bool ICollection<KeyValuePair<string, JsonRequestClaim?>>.Contains(KeyValuePair<string, JsonRequestClaim?> item)
        {
            return _inner.Contains(item);
        }

        void ICollection<KeyValuePair<string, JsonRequestClaim?>>.CopyTo(KeyValuePair<string, JsonRequestClaim?>[] array, int arrayIndex)
        {
            _inner.CopyTo(array, arrayIndex);
        }

        void ICollection<KeyValuePair<string, JsonRequestClaim?>>.Add(KeyValuePair<string, JsonRequestClaim?> item)
        {
            _inner.Add(item);
        }

        bool IDictionary<string, JsonRequestClaim?>.Remove(string key)
        {
            return _inner.Remove(key);
        }

        bool ICollection<KeyValuePair<string, JsonRequestClaim?>>.Remove(KeyValuePair<string, JsonRequestClaim?> item)
        {
            return _inner.Remove(item);
        }

        void ICollection<KeyValuePair<string, JsonRequestClaim?>>.Clear()
        {
            _inner.Clear();
        }

        #endregion

        #region IDictionary Members

        ICollection<string> IDictionary<string, JsonRequestClaim?>.Keys => _inner.Keys;

        ICollection<JsonRequestClaim?> IDictionary<string, JsonRequestClaim?>.Values => _inner.Values;

        JsonRequestClaim? IDictionary<string, JsonRequestClaim?>.this[string key]
        {
            get => _inner[key];
            set => _inner[key] = value;
        }

        void IDictionary<string, JsonRequestClaim?>.Add(string key, JsonRequestClaim? value)
        {
            _inner.Add(key, value);
        }

        #endregion

        #region IReadOnlyDictionary Members

        IEnumerable<string> IReadOnlyDictionary<string, JsonRequestClaim?>.Keys => _inner.Keys;

        IEnumerable<string> IReadOnlyDictionary<string, IRequestClaim?>.Keys => _inner.Keys;

        IEnumerable<JsonRequestClaim?> IReadOnlyDictionary<string, JsonRequestClaim?>.Values => _inner.Values;

        IEnumerable<IRequestClaim?> IReadOnlyDictionary<string, IRequestClaim?>.Values => _inner.Values;

        JsonRequestClaim? IReadOnlyDictionary<string, JsonRequestClaim?>.this[string key] => _inner[key];

        IRequestClaim? IReadOnlyDictionary<string, IRequestClaim?>.this[string key] => _inner[key];

        #endregion

    }
}
