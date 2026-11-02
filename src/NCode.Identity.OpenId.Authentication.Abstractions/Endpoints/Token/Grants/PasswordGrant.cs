// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Subject;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Token.Grants;

/// <summary>
/// Represents a Resource Owner Password Credentials Grant.
/// </summary>
[PublicAPI]
public readonly record struct PasswordGrant(SubjectAuthentication SubjectAuthentication);
