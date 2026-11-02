// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Represents the base interface for <c>OAuth</c> or <c>OpenID Connect</c> response messages.
/// </summary>
[PublicAPI]
public interface IOpenIdResponse : IBaseOpenIdMessage
{
    // nothing
}
