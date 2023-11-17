namespace NIdentity.OpenId.Settings;

/// <summary>
/// Provides the base class for a configurable setting.
/// </summary>
public abstract class Setting
{
    /// <summary>
    /// Gets the <see cref="SettingDescriptor"/> that describes this setting.
    /// </summary>
    public SettingDescriptor Descriptor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Setting"/> class.
    /// </summary>
    /// <param name="descriptor">The <see cref="SettingDescriptor"/> for the setting.</param>
    protected Setting(SettingDescriptor descriptor)
    {
        Descriptor = descriptor;
    }

    /// <summary>
    /// Gets the boxed value of this setting.
    /// </summary>
    public abstract object GetValue();
}

/// <summary>
/// Provides a default implementation for the <see cref="Setting"/> abstraction with a strongly typed value.
/// </summary>
/// <typeparam name="TValue">The type of the setting's value.</typeparam>
public class Setting<TValue> : Setting
    where TValue : notnull
{
    /// <summary>
    /// Gets the <see cref="SettingDescriptor{TValue}"/> that describes this setting.
    /// </summary>
    public new SettingDescriptor<TValue> Descriptor { get; }

    /// <summary>
    /// Gets the type-safe value of this setting.
    /// </summary>
    public TValue Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Setting{TValue}"/> class.
    /// </summary>
    /// <param name="descriptor">The <see cref="SettingDescriptor{TValue}"/> for the setting.</param>
    /// <param name="value">The type-safe value for the setting.</param>
    public Setting(SettingDescriptor<TValue> descriptor, TValue value)
        : base(descriptor)
    {
        Descriptor = descriptor;
        Value = value;
    }

    /// <inheritdoc />
    public override object GetValue() => Value;
}
