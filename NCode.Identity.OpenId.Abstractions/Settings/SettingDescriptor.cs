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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Contains information about a configurable setting.
/// </summary>
[PublicAPI]
public abstract class SettingDescriptor
{
    /// <summary>
    /// Gets or sets the name of the setting.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the type of the setting's value.
    /// </summary>
    public abstract Type ValueType { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the setting is discoverable.
    /// </summary>
    public bool IsDiscoverable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the default value is set.
    /// </summary>
    [MemberNotNullWhen(true, nameof(BoxedDefaultOrNull))]
    public abstract bool HasDefault { get; }

    /// <summary>
    /// Gets the boxed default value for the setting if set, otherwise returns <see langword="null"/>.
    /// </summary>
    public abstract object? BoxedDefaultOrNull { get; }

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

    // TODO: is there a better name for the following?

    /// <summary>
    /// Used to format the setting's value to be returned in the discovery document.
    /// </summary>
    /// <param name="setting">The <see cref="Setting"/> to format.</param>
    /// <returns>The setting's formatted value.</returns>
    public abstract object Format(Setting setting);
}

/// <summary>
/// Contains information about a configurable setting.
/// </summary>
/// <typeparam name="TValue">The type of setting's value.</typeparam>
[PublicAPI]
public class SettingDescriptor<TValue> : SettingDescriptor
    where TValue : notnull
{
    /// <summary>
    /// Gets a <see cref="SettingKey{TValue}"/> instance for the current descriptor.
    /// </summary>
    public SettingKey<TValue> Key => this;

    /// <inheritdoc />
    public override Type ValueType => typeof(TValue);

    /// <inheritdoc />
    [MemberNotNullWhen(true, nameof(DefaultOrNull))]
    [MemberNotNullWhen(true, nameof(BoxedDefaultOrNull))]
    public override bool HasDefault => DefaultOrNull is not null;

    /// <inheritdoc />
    public override object? BoxedDefaultOrNull => DefaultOrNull;

    /// <summary>
    /// Gets or sets the default value for the setting.
    /// </summary>
    public TValue? DefaultOrNull { get; init; }

    /// <summary>
    /// Gets the default value for the setting or throws an <see cref="InvalidOperationException"/> if the default value is not set.
    /// </summary>
    public TValue Default
    {
        get => DefaultOrNull ?? throw new InvalidOperationException();
        init => DefaultOrNull = value;
    }

    /// <summary>
    /// Gets or sets the factory method used to create a new <see cref="Setting{TValue}"/> instance with the specified <paramref name="value"/>.
    /// The default implementation calls the <see cref="Setting{TValue}"/> constructor.
    /// </summary>
    public Func<SettingDescriptor<TValue>, TValue, Setting<TValue>> OnCreate { get; init; }
        = (descriptor, value) => new Setting<TValue>(descriptor, value);

    /// <summary>
    /// Gets or sets the function that is used to merge two <see cref="Setting{TValue}"/> instances into a new <see cref="Setting{TValue}"/> instance.
    /// The default implementation always returns the other value.
    /// </summary>
    public Func<TValue, TValue, TValue> OnMerge { get; init; }
        = (_, other) => other;

    /// <summary>
    /// Gets or sets the function that is used to format the setting's value to be returned in the discovery document.
    /// The default implementation returns the value as-is.
    /// </summary>
    public Func<Setting<TValue>, object> OnFormat { get; init; }
        = setting => setting.Value;

    /// <inheritdoc />
    public override Setting Create(object value)
        => Create((TValue)value);

    /// <summary>
    /// Factory method used to create a new <see cref="Setting{TValue}"/> instance with the specified <paramref name="value"/>.
    /// The default implementation calls the <see cref="OnCreate"/> function.
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
    /// The default implementation calls the <see cref="OnMerge"/> function.
    /// </summary>
    /// <param name="current">The current <see cref="Setting{TValue}"/> instance to merge.</param>
    /// <param name="other">The other <see cref="Setting{TValue}"/> instance to merge.</param>
    /// <returns>The <see cref="Setting{TValue}"/> instance from the result of the merge.</returns>
    public virtual Setting<TValue> Merge(Setting<TValue> current, Setting<TValue> other)
        => OnCreate(this, OnMerge(current.Value, other.Value));

    /// <inheritdoc />
    public override object Format(Setting setting)
        => Format((Setting<TValue>)setting);

    /// <summary>
    /// Used to format the setting's value to be returned in the discovery document.
    /// The default implementation calls the <see cref="OnFormat"/> function.
    /// </summary>
    /// <param name="setting">The <see cref="Setting"/> to format.</param>
    /// <returns>The setting's formatted value.</returns>
    public virtual object Format(Setting<TValue> setting)
        => OnFormat(setting);

    /// <summary>
    /// Operator overload to convert a <see cref="SettingDescriptor{TValue}"/> instance to a <see cref="SettingKey{TValue}"/> instance.
    /// </summary>
    /// <param name="descriptor">The <see cref="SettingDescriptor{TValue}"/> instance.</param>
    /// <returns>The <see cref="SettingKey{TValue}"/> instance.</returns>
    public static implicit operator SettingKey<TValue>(SettingDescriptor<TValue> descriptor) => new()
    {
        SettingName = descriptor.Name
    };
}
