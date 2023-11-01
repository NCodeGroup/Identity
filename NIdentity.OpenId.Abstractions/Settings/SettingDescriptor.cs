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

namespace NIdentity.OpenId.Settings;

/// <summary>
/// Contains information about a configurable setting.
/// </summary>
public abstract class SettingDescriptor
{
    /// <summary>
    /// Gets or sets the name of the setting.
    /// </summary>
    public required string SettingName { get; init; }

    /// <summary>
    /// Gets the type of the setting's value.
    /// </summary>
    public abstract Type ValueType { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the setting is discoverable.
    /// </summary>
    public bool Discoverable { get; init; }

    /// <summary>
    /// Factory method used to create a new <see cref="Setting"/> instance with the specified <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value for the setting.</param>
    /// <returns>The newly created <see cref="Setting"/> instance.</returns>
    public abstract Setting Create(object value);

    /// <summary>
    /// Used to merge two <see cref="Setting"/> instances into a new <see cref="Setting"/> instance.
    /// </summary>
    /// <param name="current">The current <see cref="Setting"/> instance to merge.</param>
    /// <param name="other">The other <see cref="Setting"/> instance to merge.</param>
    /// <returns>The <see cref="Setting"/> instance from the result of the merge.</returns>
    public abstract Setting Merge(Setting current, Setting other);
}

/// <summary>
/// Contains information about a configurable setting.
/// </summary>
/// <typeparam name="TValue">The type of setting's value.</typeparam>
public class SettingDescriptor<TValue> : SettingDescriptor
    where TValue : notnull
{
    /// <summary>
    /// Gets a <see cref="SettingKey{TValue}"/> instance for the current descriptor.
    /// </summary>
    public SettingKey<TValue> Key => this;

    /// <inheritdoc />
    public override Type ValueType => typeof(TValue);

    /// <summary>
    /// Gets or sets the factory method used to create a new <see cref="Setting{TValue}"/> instance with the specified <paramref name="value"/>.
    /// </summary>
    public Func<SettingDescriptor<TValue>, TValue, Setting<TValue>> OnCreate { get; init; }
        = (descriptor, value) => new Setting<TValue>(descriptor, value);

    /// <summary>
    /// Gets or sets the function used to merge two <see cref="Setting{TValue}"/> instances into a new <see cref="Setting{TValue}"/> instance.
    /// </summary>
    public required Func<TValue, TValue, TValue> OnMerge { get; init; }

    /// <inheritdoc />
    public override Setting Create(object value)
        => Create((TValue)value);

    /// <summary>
    /// Factory method used to create a new <see cref="Setting{TValue}"/> instance with the specified <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value for the setting.</param>
    /// <returns>The newly created <see cref="Setting{TValue}"/> instance.</returns>
    public virtual Setting<TValue> Create(TValue value)
        => OnCreate(this, value);

    /// <inheritdoc />
    public override Setting Merge(Setting current, Setting other)
        => Merge((Setting<TValue>)current, (Setting<TValue>)other);

    /// <summary>
    /// Used to merge two <see cref="Setting{TValue}"/> instances into a new <see cref="Setting{TValue}"/> instance.
    /// </summary>
    /// <param name="current">The current <see cref="Setting{TValue}"/> instance to merge.</param>
    /// <param name="other">The other <see cref="Setting{TValue}"/> instance to merge.</param>
    /// <returns>The <see cref="Setting{TValue}"/> instance from the result of the merge.</returns>
    public virtual Setting<TValue> Merge(Setting<TValue> current, Setting<TValue> other)
        => OnCreate(this, OnMerge(current.Value, other.Value));

    /// <summary>
    /// Operator overload to convert a <see cref="SettingDescriptor{TValue}"/> instance to a <see cref="SettingKey{TValue}"/> instance.
    /// </summary>
    /// <param name="descriptor">The <see cref="SettingDescriptor{TValue}"/> instance.</param>
    /// <returns>The <see cref="SettingKey{TValue}"/> instance.</returns>
    public static implicit operator SettingKey<TValue>(SettingDescriptor<TValue> descriptor) => new()
    {
        SettingName = descriptor.SettingName
    };
}
