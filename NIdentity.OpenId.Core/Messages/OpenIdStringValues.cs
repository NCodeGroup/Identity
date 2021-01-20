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
using System.Text;
using System.Threading;
using Microsoft.Extensions.Primitives;

namespace NIdentity.OpenId.Messages
{
    /// <summary>
    /// Represents zero/null, one, or many strings in an efficient way.
    /// </summary>
    public readonly struct OpenIdStringValues :
        IList<StringSegment>,
        IReadOnlyList<StringSegment>,
        IEquatable<OpenIdStringValues>,
        IEquatable<string>,
        IEquatable<IEnumerable<string>>,
        IEquatable<IEnumerable<StringSegment>>
    {
        /// <summary>
        /// A readonly instance of the <see cref="OpenIdStringValues"/> struct whose value is an empty string.
        /// </summary>
        /// <remarks>
        /// In application code, this field is most commonly used to safely represent a <see cref="OpenIdStringValues"/> that has null string values.
        /// </remarks>
        public static readonly OpenIdStringValues Empty = new(string.Empty);

        private const char Separator = ' ';

        private readonly Lazy<StringSegment[]> _lazyStringSegments;
        private readonly Lazy<string?> _lazyFullString;

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdStringValues"/> structure using the specified string.
        /// </summary>
        /// <param name="value">A string value or <c>null</c>.</param>
        /// <param name="tokenize">TODO</param>
        public OpenIdStringValues(string? value, bool tokenize = true)
        {
            _lazyStringSegments = string.IsNullOrEmpty(value)
                ? new Lazy<StringSegment[]>(Array.Empty<StringSegment>, LazyThreadSafetyMode.PublicationOnly)
                : new Lazy<StringSegment[]>(() => TokenizeStringSegments(value, tokenize), LazyThreadSafetyMode.PublicationOnly);

            _lazyFullString = new Lazy<string?>(() => value, LazyThreadSafetyMode.PublicationOnly);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OpenIdStringValues"/> structure using the specified collection of strings.
        /// </summary>
        /// <param name="stringValues">A string collection.</param>
        public OpenIdStringValues(IEnumerable<string> stringValues)
        {
            var lazyStringSegments = new Lazy<StringSegment[]>(() => TokenizeStringSegments(stringValues), LazyThreadSafetyMode.PublicationOnly);
            var lazyFullString = new Lazy<string?>(() => GetFullString(lazyStringSegments), LazyThreadSafetyMode.PublicationOnly);

            _lazyStringSegments = lazyStringSegments;
            _lazyFullString = lazyFullString;
        }

        public OpenIdStringValues(IEnumerable<StringSegment> stringSegments)
        {
            var lazyStringSegments = new Lazy<StringSegment[]>(() => TokenizeStringSegments(stringSegments), LazyThreadSafetyMode.PublicationOnly);
            var lazyFullString = new Lazy<string?>(() => GetFullString(lazyStringSegments), LazyThreadSafetyMode.PublicationOnly);

            _lazyStringSegments = lazyStringSegments;
            _lazyFullString = lazyFullString;
        }

        private static StringSegment[] TokenizeStringSegments(string value, bool tokenize) => tokenize
            ? new StringTokenizer(value, new[] { Separator }).ToArray()
            : new[] { new StringSegment(value) };

        private static StringSegment[] TokenizeStringSegments(IEnumerable<string> stringValues) => stringValues
            .Where(stringValue => !string.IsNullOrEmpty(stringValue))
            .SelectMany(stringValue => new StringTokenizer(stringValue, new[] { Separator }))
            .ToArray();

        private static StringSegment[] TokenizeStringSegments(IEnumerable<StringSegment> stringSegments) => stringSegments
            .Where(stringSegment => !StringSegment.IsNullOrEmpty(stringSegment))
            .SelectMany(stringSegment => new StringTokenizer(stringSegment, new[] { Separator }))
            .ToArray();

        private static string GetFullString(Lazy<StringSegment[]> lazyStringSegments)
        {
            var builder = new StringBuilder();
            var stringSegments = lazyStringSegments.Value;
            foreach (var stringSegment in stringSegments)
            {
                if (builder.Length > 0)
                    builder.Append(Separator);

                builder.Append(stringSegment.Buffer, stringSegment.Offset, stringSegment.Length);
            }
            return builder.ToString();
        }

        /// <summary>
        /// Indicates whether the specified <see cref="OpenIdStringValues"/> contains no string values.
        /// </summary>
        /// <param name="value">The <see cref="OpenIdStringValues"/> to test.</param>
        /// <returns>true if <paramref name="value">value</paramref> contains a single null string or empty array; otherwise, false.</returns>
        public static bool IsNullOrEmpty(OpenIdStringValues value) => value.Count == 0;

        #region IEnumerable Members

        /// <inheritdoc />
        public IEnumerator<StringSegment> GetEnumerator()
        {
            return _lazyStringSegments.Value.AsEnumerable().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        #region ICollection Members

        public int Count => _lazyStringSegments.Value.Length;

        bool ICollection<StringSegment>.IsReadOnly => true;

        void ICollection<StringSegment>.Add(StringSegment item) => throw new NotSupportedException();

        bool ICollection<StringSegment>.Remove(StringSegment item) => throw new NotSupportedException();

        void ICollection<StringSegment>.Clear() => throw new NotSupportedException();

        bool ICollection<StringSegment>.Contains(StringSegment item) => IndexOf(item) >= 0;

        void ICollection<StringSegment>.CopyTo(StringSegment[] array, int arrayIndex) => _lazyStringSegments.Value.CopyTo(array, arrayIndex);

        #endregion

        #region IList Members

        public StringSegment this[int index] => _lazyStringSegments.Value[index];

        StringSegment IList<StringSegment>.this[int index]
        {
            get => this[index];
            set => throw new NotSupportedException();
        }

        private int IndexOf(StringSegment item)
        {
            var list = _lazyStringSegments.Value;
            for (var i = 0; i < list.Length; ++i)
            {
                if (StringSegment.Equals(item, list[i], StringComparison.Ordinal))
                    return i;
            }
            return -1;
        }

        int IList<StringSegment>.IndexOf(StringSegment item) => IndexOf(item);

        void IList<StringSegment>.Insert(int index, StringSegment item) => throw new NotSupportedException();

        void IList<StringSegment>.RemoveAt(int index) => throw new NotSupportedException();

        #endregion

        #region IEquatable Members

        /// <summary>
        /// Determines whether two specified <see cref="OpenIdStringValues"/> objects have the same values in the same order.
        /// </summary>
        /// <param name="left">The first <see cref="OpenIdStringValues"/> to compare.</param>
        /// <param name="right">The second <see cref="OpenIdStringValues"/> to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="left"/> is the same as the value of <paramref name="right"/>; otherwise, <c>false</c>.</returns>
        public static bool Equals(OpenIdStringValues left, OpenIdStringValues right)
        {
            var count = left.Count;

            if (count != right.Count)
                return false;

            for (var i = 0; i < count; ++i)
            {
                if (!StringSegment.Equals(left[i], right[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        public static bool Equals(string left, OpenIdStringValues right) => Equals(new OpenIdStringValues(left), right);

        public static bool Equals(IEnumerable<string> left, OpenIdStringValues right) => Equals(new OpenIdStringValues(left), right);

        public static bool Equals(OpenIdStringValues left, string right) => Equals(left, new OpenIdStringValues(right));

        public static bool Equals(OpenIdStringValues left, IEnumerable<string> right) => Equals(left, new OpenIdStringValues(right));

        /// <summary>
        /// Determines whether two specified <see cref="OpenIdStringValues"/> objects have the same values in the same order.
        /// </summary>
        /// <param name="left">The first <see cref="OpenIdStringValues"/> to compare.</param>
        /// <param name="right">The second <see cref="OpenIdStringValues"/> to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="left"/> is the same as the value of <paramref name="right"/>; otherwise, <c>false</c>.</returns>
        public static bool operator ==(OpenIdStringValues left, OpenIdStringValues right) => Equals(left, right);

        /// <summary>
        /// Determines whether two specified <see cref="OpenIdStringValues"/> objects have different values.
        /// </summary>
        /// <param name="left">The first <see cref="OpenIdStringValues"/> to compare.</param>
        /// <param name="right">The second <see cref="OpenIdStringValues"/> to compare.</param>
        /// <returns><c>true</c> if the value of <paramref name="left"/> is different to the value of <paramref name="right"/>; otherwise, <c>false</c>.</returns>
        public static bool operator !=(OpenIdStringValues left, OpenIdStringValues right) => !Equals(left, right);

        public static bool operator ==(string left, OpenIdStringValues right) => Equals(new OpenIdStringValues(left), right);

        public static bool operator ==(IEnumerable<string> left, OpenIdStringValues right) => Equals(new OpenIdStringValues(left), right);

        public static bool operator !=(string left, OpenIdStringValues right) => !Equals(new OpenIdStringValues(left), right);

        public static bool operator !=(IEnumerable<string> left, OpenIdStringValues right) => !Equals(new OpenIdStringValues(left), right);

        public static bool operator ==(OpenIdStringValues left, string right) => Equals(left, new OpenIdStringValues(right));

        public static bool operator ==(OpenIdStringValues left, IEnumerable<string> right) => Equals(left, new OpenIdStringValues(right));

        public static bool operator !=(OpenIdStringValues left, string right) => !Equals(left, new OpenIdStringValues(right));

        public static bool operator !=(OpenIdStringValues left, IEnumerable<string> right) => !Equals(left, new OpenIdStringValues(right));

        /// <inheritdoc />
        public bool Equals(OpenIdStringValues other) => Equals(this, other);

        /// <inheritdoc />
        public bool Equals(string other) => Equals(this, new OpenIdStringValues(other));

        /// <inheritdoc />
        public bool Equals(IEnumerable<string> other) => Equals(this, new OpenIdStringValues(other));

        /// <inheritdoc />
        public bool Equals(IEnumerable<StringSegment> other) => Equals(this, new OpenIdStringValues(other));

        #endregion

        private string? GetFullString() => _lazyFullString.Value;

        /// <inheritdoc />
        public override string ToString() => GetFullString() ?? string.Empty;

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj switch
            {
                OpenIdStringValues other => Equals(this, other),
                IEnumerable<StringSegment> other => Equals(this, other),
                IEnumerable<string> other => Equals(this, other),
                string other => Equals(this, other),
                null => Equals(this, Empty),
                _ => false
            };
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            if (Count == 0) return 0;
            var hashCode = this[0].GetHashCode();
            for (var i = 1; i < Count; ++i)
            {
                hashCode = CombineHash(hashCode, this[i].GetHashCode());
            }
            return hashCode;
        }

        private static int CombineHash(int h1, int h2)
        {
            var rol5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)rol5 + h1) ^ h2;
        }

        /// <summary>
        /// Defines an implicit conversion of a given string to a <see cref="OpenIdStringValues"/>.
        /// </summary>
        /// <param name="value">A string to implicitly convert.</param>
        public static implicit operator OpenIdStringValues(string value) => new(value);

        /// <summary>
        /// Defines an implicit conversion of a given string array to a <see cref="OpenIdStringValues"/>.
        /// </summary>
        /// <param name="values">A string array to implicitly convert.</param>
        public static implicit operator OpenIdStringValues(string[] values) => new(values);

        /// <summary>
        /// Defines an implicit conversion of a given string values to a <see cref="OpenIdStringValues"/>.
        /// </summary>
        /// <param name="values">A string values to implicitly convert.</param>
        public static implicit operator OpenIdStringValues(StringValues values) => new(values.AsEnumerable());
    }
}
