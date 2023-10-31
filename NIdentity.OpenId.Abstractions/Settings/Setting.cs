using System.Text.Json;

namespace NIdentity.OpenId.Settings;

public abstract class Setting
{
    public abstract SettingDescriptor Descriptor { get; }

    public abstract void Write(Utf8JsonWriter writer, JsonSerializerOptions options);

    public abstract Setting Merge(Setting other, SettingMergeOptions options = default);
}

public abstract class Setting<TValue> : Setting
{
    public abstract TValue Value { get; }

    public abstract Setting<TValue> Merge(Setting<TValue> other, SettingMergeOptions options = default);

    public override Setting Merge(Setting other, SettingMergeOptions options = default)
    {
        if (other is not Setting<TValue> otherSetting)
            throw new ArgumentException("Incompatible ValueType.", nameof(other));

        return Merge(otherSetting, options);
    }
}
