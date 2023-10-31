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

public abstract class SettingDescriptor
{
    public SettingKey Key => this;

    public required string SettingName { get; init; }

    public abstract Type ValueType { get; }

    public bool Discoverable { get; init; }

    public abstract Setting Create(object value);

    public abstract Setting Merge(Setting current, Setting other);

    public static implicit operator SettingKey(SettingDescriptor descriptor) => new()
    {
        SettingName = descriptor.SettingName,
        ValueType = descriptor.ValueType
    };
}

public class SettingDescriptor<TValue> : SettingDescriptor
    where TValue : notnull
{
    public new SettingKey<TValue> Key => this;

    public override Type ValueType => typeof(TValue);

    public Func<SettingDescriptor<TValue>, TValue, Setting<TValue>> OnCreate { get; init; }
        = (descriptor, value) => new Setting<TValue>(descriptor, value);

    public required Func<TValue, TValue, TValue> OnMerge { get; init; }

    /// <inheritdoc />
    public override Setting Create(object value)
        => Create((TValue)value);

    public virtual Setting<TValue> Create(TValue value)
        => OnCreate(this, value);

    /// <inheritdoc />
    public override Setting Merge(Setting current, Setting other)
        => Merge((Setting<TValue>)current, (Setting<TValue>)other);

    public virtual Setting<TValue> Merge(Setting<TValue> current, Setting<TValue> other)
        => OnCreate(this, OnMerge(current.Value, other.Value));

    public static implicit operator SettingKey<TValue>(SettingDescriptor<TValue> descriptor) => new()
    {
        SettingName = descriptor.SettingName
    };
}
