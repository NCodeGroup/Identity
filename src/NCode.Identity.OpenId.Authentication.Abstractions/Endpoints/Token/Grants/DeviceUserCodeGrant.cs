#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;

/// <summary>
/// Represents the payload of the pointer grant that maps a human-typable <c>user_code</c> to the <c>device_code</c> of
/// its pending device authorization request (RFC 8628), so the verification endpoint can resolve the request from the
/// <c>user_code</c> alone.
/// </summary>
/// <param name="DeviceCode">The <c>device_code</c> of the device authorization request this <c>user_code</c> maps to.</param>
[PublicAPI]
public readonly record struct DeviceUserCodeGrant(string DeviceCode);
