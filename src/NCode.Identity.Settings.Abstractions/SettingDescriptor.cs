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

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Contains information about a configurable setting.
/// </summary>
[PublicAPI]
public abstract class SettingDescriptor
{
    /// <summary>
    /// Gets the name of the setting.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the names of the scopes the setting applies to: the registered <see cref="SettingScopeDescriptor"/> scopes
    /// whose effective settings the server reads the setting from, whether to enforce it or to advertise it. A settings
    /// document may carry the setting only at a scope that applies it or that cascades into one that does
    /// (<see cref="ISettingScopeCollection.AppliesWithin(SettingDescriptor, string)"/>); anywhere else the setting would
    /// be accepted without being honored, so every entry point that accepts a settings document rejects it there. A
    /// setting that names no registered scope applies nowhere.
    /// </summary>
    // See ADR-0149.
    public required ImmutableArray<string> AppliesTo { get; init; }

    /// <summary>
    /// Gets the type of the setting's value.
    /// </summary>
    public abstract Type ValueType { get; }

    /// <summary>
    /// Gets a value indicating whether the setting is discoverable.
    /// </summary>
    public bool IsDiscoverable { get; init; }

    /// <summary>
    /// Gets a value indicating whether the default value is set.
    /// </summary>
    public abstract bool HasDefault { get; }

    /// <summary>
    /// Gets a value indicating whether the setting merges with a lattice operation of <see cref="SettingMerge"/> — a
    /// meet or a join, which is commutative, associative, and idempotent — rather than a projection
    /// (<see cref="SettingMerge.Keep{T}"/>, <see cref="SettingMerge.Replace{T}"/>) or another function. A value
    /// contributed at any scope through such a merge bounds the value of every scope below it, and values contributed
    /// together reach the same bound in whatever order they are merged.
    /// </summary>
    public abstract bool HasLatticeMerge { get; }

    /// <summary>
    /// Gets a value indicating whether the setting merges with a meet of <see cref="SettingMerge"/>
    /// (<see cref="SettingMerge.Min{T}"/>, <see cref="SettingMerge.And"/>, <see cref="SettingMerge.Intersect{T}"/>),
    /// whose result is never above either value. A value contributed at any scope is a ceiling over every scope below
    /// it, and a value a child sets survives only where no ceiling above it is lower. Every setting with a meet merge
    /// also has a lattice merge (<see cref="HasLatticeMerge"/>).
    /// </summary>
    public abstract bool HasMeetMerge { get; }

    /// <summary>
    /// Factory method used to create a new <see cref="Setting"/> instance with the specified <paramref name="value"/>.
    /// The <paramref name="value"/> must be castable to <see cref="ValueType"/>.
    /// </summary>
    /// <param name="value">The value for the setting.</param>
    /// <returns>The newly created <see cref="Setting"/> instance.</returns>
    public abstract Setting Create(object value);

    /// <summary>
    /// Factory method used to create a new <see cref="Setting"/> instance with the default value if supported,
    /// otherwise throws an <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <returns>The newly created <see cref="Setting"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a default value is not supported.</exception>
    public abstract Setting CreateDefault();

    /// <summary>
    /// Used to merge two <see cref="Setting"/> instances into a new <see cref="Setting"/> instance.
    /// </summary>
    /// <param name="current">The current <see cref="Setting"/> instance to merge.</param>
    /// <param name="other">The other <see cref="Setting"/> instance to merge.</param>
    /// <returns>The <see cref="Setting"/> instance from the result of the merge.</returns>
    public abstract Setting Merge(Setting current, Setting other);

    /// <summary>
    /// Used to format the setting's value to be returned in the discovery document.
    /// </summary>
    /// <param name="setting">The <see cref="Setting"/> to format.</param>
    /// <returns>The setting's formatted value.</returns>
    public abstract object Format(Setting setting);

    /// <summary>
    /// Determines whether a value is in the setting's domain: beyond conforming to <see cref="ValueType"/>, it satisfies
    /// the rule the descriptor declares. A value outside the domain is one the server would accept without honoring, so
    /// every entry point that accepts a setting — configuration, management writes, and stored documents when they are
    /// read — rejects it. The <paramref name="value"/> must be castable to <see cref="ValueType"/>.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="failure">When this method returns <see langword="false"/>, a phrase that states why the value is
    /// rejected, written to follow the name of the setting; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is in the setting's domain; otherwise, <see langword="false"/>.</returns>
    public abstract bool TryValidate(object value, [NotNullWhen(false)] out string? failure);
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
    public override bool HasDefault => _hasDefault;

    /// <inheritdoc />
    public override bool HasLatticeMerge => SettingMerge.IsLatticeOperation(OnMerge);

    /// <inheritdoc />
    public override bool HasMeetMerge => SettingMerge.IsMeetOperation(OnMerge);

    private readonly TValue? _defaultValue;
    private readonly bool _hasDefault;

    /// <summary>
    /// Gets the default value for the setting, or <c>null</c>/<c>default</c> when no default was configured. Use
    /// <see cref="HasDefault"/> to distinguish an unconfigured default from a configured default equal to
    /// <c>default(TValue)</c> — a value-type <c>TValue</c> (e.g. <see cref="System.TimeSpan"/>) cannot carry a null
    /// sentinel here, so the presence of a default is tracked explicitly rather than inferred from this value.
    /// </summary>
    public TValue? DefaultOrNull
    {
        get => _defaultValue;
        init
        {
            _defaultValue = value;
            _hasDefault = true;
        }
    }

    /// <summary>
    /// Gets the default value for the setting or throws an <see cref="InvalidOperationException"/> if the default value is not set.
    /// </summary>
    public TValue Default
    {
        get =>
            _hasDefault && _defaultValue is { } value
                ? value
                : throw new InvalidOperationException();
        init
        {
            _defaultValue = value;
            _hasDefault = true;
        }
    }

    /// <summary>
    /// Gets the factory method used to create a new <see cref="Setting{TValue}"/> instance with the specified <paramref name="value"/>.
    /// The default implementation calls the <see cref="Setting{TValue}"/> constructor.
    /// </summary>
    public Func<SettingDescriptor<TValue>, TValue, Setting<TValue>> OnCreate { get; init; } =
        (descriptor, value) => new Setting<TValue>(descriptor, value);

    /// <summary>
    /// Gets the function that is used to merge two <see cref="Setting{TValue}"/> instances into a new <see cref="Setting{TValue}"/> instance.
    /// The default implementation always returns the other value.
    /// </summary>
    public Func<TValue, TValue, TValue> OnMerge { get; init; } = (_, other) => other;

    /// <summary>
    /// Gets the function that is used to format the setting's value to be returned in the discovery document.
    /// The default implementation returns the value as-is.
    /// </summary>
    public Func<Setting<TValue>, object> OnFormat { get; init; } = setting => setting.Value;

    /// <summary>
    /// Gets the rule the setting's value must satisfy beyond its type — the setting's domain, such as an allowed set, a
    /// range, or a predicate over registered strategies. The default accepts every value of the type.
    /// </summary>
    /// <remarks>
    /// The rule guards the entry points where a value is supplied from outside the code: configuration, management
    /// writes, and stored documents when they are read. A value set in code (a default, a security profile's ceiling)
    /// is not checked when it is set, so the <see cref="Default"/> must itself satisfy the rule.
    /// </remarks>
    public SettingRule<TValue> Rule { get; init; } = SettingRules.Any<TValue>();

    /// <inheritdoc />
    public override Setting Create(object value) => Create((TValue)value);

    /// <inheritdoc />
    public override Setting CreateDefault() =>
        HasDefault ? Create(Default) : throw new InvalidOperationException();

    /// <summary>
    /// Factory method used to create a new <see cref="Setting{TValue}"/> instance with the specified <paramref name="value"/>.
    /// The default implementation calls the <see cref="OnCreate"/> function.
    /// </summary>
    /// <param name="value">The value for the setting.</param>
    /// <returns>The newly created <see cref="Setting{TValue}"/> instance.</returns>
    public virtual Setting<TValue> Create(TValue value) => OnCreate(this, value);

    /// <inheritdoc />
    public override Setting Merge(Setting current, Setting other) =>
        Merge((Setting<TValue>)current, (Setting<TValue>)other);

    /// <summary>
    /// Used to merge two <see cref="Setting{TValue}"/> instances into a new <see cref="Setting{TValue}"/> instance.
    /// The default implementation calls the <see cref="OnMerge"/> function.
    /// </summary>
    /// <param name="current">The current <see cref="Setting{TValue}"/> instance to merge.</param>
    /// <param name="other">The other <see cref="Setting{TValue}"/> instance to merge.</param>
    /// <returns>The <see cref="Setting{TValue}"/> instance from the result of the merge.</returns>
    public virtual Setting<TValue> Merge(Setting<TValue> current, Setting<TValue> other) =>
        OnCreate(this, OnMerge(current.Value, other.Value));

    /// <inheritdoc />
    public override object Format(Setting setting) => Format((Setting<TValue>)setting);

    /// <summary>
    /// Used to format the setting's value to be returned in the discovery document.
    /// The default implementation calls the <see cref="OnFormat"/> function.
    /// </summary>
    /// <param name="setting">The <see cref="Setting"/> to format.</param>
    /// <returns>The setting's formatted value.</returns>
    public virtual object Format(Setting<TValue> setting) => OnFormat(setting);

    /// <inheritdoc />
    public override bool TryValidate(object value, [NotNullWhen(false)] out string? failure) =>
        TryValidate((TValue)value, out failure);

    /// <summary>
    /// Determines whether a value is in the setting's domain.
    /// The default implementation evaluates the <see cref="Rule"/>.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <param name="failure">When this method returns <see langword="false"/>, a phrase that states why the value is
    /// rejected, written to follow the name of the setting; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the value is in the setting's domain; otherwise, <see langword="false"/>.</returns>
    public virtual bool TryValidate(TValue value, [NotNullWhen(false)] out string? failure) =>
        Rule.TryValidate(value, out failure);

    /// <summary>
    /// Operator overload to convert a <see cref="SettingDescriptor{TValue}"/> instance to a <see cref="SettingKey{TValue}"/> instance.
    /// </summary>
    /// <param name="descriptor">The <see cref="SettingDescriptor{TValue}"/> instance.</param>
    /// <returns>The <see cref="SettingKey{TValue}"/> instance.</returns>
    public static implicit operator SettingKey<TValue>(SettingDescriptor<TValue> descriptor) =>
        new() { SettingName = descriptor.Name };
}
