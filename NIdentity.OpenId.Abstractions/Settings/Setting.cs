namespace NIdentity.OpenId.Settings;

public abstract class Setting
{
    public SettingDescriptor BaseDescriptor { get; }

    protected Setting(SettingDescriptor baseDescriptor)
    {
        BaseDescriptor = baseDescriptor;
    }

    public abstract object GetValue();
}

public class Setting<TValue> : Setting
    where TValue : notnull
{
    public SettingDescriptor<TValue> Descriptor { get; }

    public TValue Value { get; }

    public Setting(SettingDescriptor<TValue> descriptor, TValue value)
        : base(descriptor)
    {
        Descriptor = descriptor;
        Value = value;
    }

    /// <inheritdoc />
    public override object GetValue() => Value;
}
