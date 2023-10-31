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

namespace NIdentity.OpenId.Settings;

public interface ISettingCollection : IReadOnlyCollection<Setting>
{
    bool TryGet(SettingKey key, [MaybeNullWhen(false)] out Setting setting);

    bool TryGet<TValue>(SettingKey<TValue> key, [MaybeNullWhen(false)] out Setting<TValue> setting);

    void Set<TValue>(SettingKey<TValue> key, Setting<TValue> setting);

    bool Remove<TValue>(SettingKey<TValue> key);

    ISettingCollection Merge(ISettingCollection otherCollection, SettingMergeOptions options = default);
}
