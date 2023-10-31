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

// example:
// response_types_supported
// REQUIRED. JSON array containing a list of the OAuth 2.0 response_type values that this OP supports.
// Dynamic OpenID Providers MUST support the code, id_token, and the token id_token Response Type values.

public static class KnownSettings
{
    public static SettingDescriptor<IReadOnlyCollection<ResponseTypes>> ResponseTypesSupported { get; } = new()
    {
        SettingName = "response_types_supported",
        DefaultMergeBehavior = SettingMergeBehaviors.List.Intersect
    };
}
