using JetBrains.Annotations;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides the base class for a configurable setting.
/// </summary>
/// <param name="descriptor">The <see cref="SettingDescriptor"/> for the setting.</param>
[PublicAPI]
public abstract class Setting(SettingDescriptor descriptor)
{
    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> that describes this setting.
    /// </summary>
    public SettingDescriptor Descriptor { get; } = descriptor;

    /// <summary>
    /// Gets the boxed value of this setting.
    /// </summary>
    public abstract object GetValue();
}

/// <summary>
/// Provides a default implementation for the <see cref="Setting"/> abstraction with a strongly typed value.
/// </summary>
/// <typeparam name="TValue">The type of the setting's value.</typeparam>
/// <param name="descriptor">The <see cref="SettingDescriptor{TValue}"/> for the setting.</param>
/// <param name="value">The type-safe value for the setting.</param>
[PublicAPI]
public class Setting<TValue>(SettingDescriptor<TValue> descriptor, TValue value)
    : Setting(descriptor)
    where TValue : notnull
{
    /// <summary>
    /// Gets the <see cref="SettingDescriptor{TValue}"/> that describes this setting.
    /// </summary>
    public new SettingDescriptor<TValue> Descriptor { get; } = descriptor;

    /// <summary>
    /// Gets the type-safe value of this setting.
    /// </summary>
    public TValue Value { get; } = value;

    /// <inheritdoc />
    public override object GetValue() => Value;
}
