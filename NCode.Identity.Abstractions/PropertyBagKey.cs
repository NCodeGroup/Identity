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

namespace NCode.Identity;

/// <summary>
/// Base interface for a strongly typed key in a <see cref="IPropertyBag"/>.
/// </summary>
public interface IPropertyBagKey : IEquatable<IPropertyBagKey>
{
    /// <summary>
    /// Gets the <see cref="Type"/> of the value in the property bag.
    /// </summary>
    Type Type { get; }

    /// <summary>
    /// Gets the <see cref="string"/> name of the value in the property bag.
    /// </summary>
    string Name { get; }
}

// TODO: using a struct might be a bad idea, should we always use a class?

/// <summary>
/// Represents a strongly typed key in a <see cref="IPropertyBag"/>.
/// </summary>
/// <typeparam name="T">The type of the value in the property bag.</typeparam>
public readonly struct PropertyBagKey<T> : IPropertyBagKey
{
    /// <inheritdoc/>
    public Type Type => typeof(T);

    /// <inheritdoc/>
    public string Name { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyBagKey{T}"/> struct with the supplied <paramref name="name"/> value.
    /// </summary>
    /// <param name="name">The <see cref="string"/> name of the value in the property bag.</param>
    public PropertyBagKey(string name)
    {
        Name = name;
    }

    /// <inheritdoc/>
    public bool Equals(IPropertyBagKey? other) =>
        other != null && Type == other.Type && Name == other.Name;

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is IPropertyBagKey other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        HashCode.Combine(Type, Name);

    /// <summary>
    /// The equality operator that returns <c>true</c> if the two operands are equal.
    /// </summary>
    public static bool operator ==(PropertyBagKey<T> left, PropertyBagKey<T> right) =>
        left.Equals(right);

    /// <summary>
    /// The inequality operator that returns <c>true</c> if the two operands aren't equal.
    /// </summary>
    public static bool operator !=(PropertyBagKey<T> left, PropertyBagKey<T> right) =>
        !(left == right);
}
