// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
