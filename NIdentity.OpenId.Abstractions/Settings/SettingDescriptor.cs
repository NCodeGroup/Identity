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

public readonly struct SettingDescriptor
{
    public required string SettingName { get; init; }

    public required Type ValueType { get; init; }

    public required string DefaultMergeBehavior { get; init; }

    public bool NonDiscoverable { get; init; }

    public SettingKey Key => this;

    public static implicit operator SettingKey(SettingDescriptor descriptor) => new()
    {
        SettingName = descriptor.SettingName,
        ValueType = descriptor.ValueType
    };
}

public readonly struct SettingDescriptor<TValue>
{
    public required string SettingName { get; init; }

    public Type ValueType => typeof(TValue);

    public required string DefaultMergeBehavior { get; init; }

    public bool NonDiscoverable { get; init; }

    public SettingKey<TValue> Key => this;

    public static implicit operator SettingKey<TValue>(SettingDescriptor<TValue> descriptor) => new()
    {
        SettingName = descriptor.SettingName
    };

    public static implicit operator SettingDescriptor(SettingDescriptor<TValue> descriptor) => new()
    {
        SettingName = descriptor.SettingName,
        ValueType = descriptor.ValueType,
        DefaultMergeBehavior = descriptor.DefaultMergeBehavior,
        NonDiscoverable = descriptor.NonDiscoverable
    };
}
