// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Represents a declarative, data-only requirement that a JSON Web Token (JWT) must satisfy to be considered valid.
/// Modeled on <c>Microsoft.AspNetCore.Authorization.IAuthorizationRequirement</c>: a requirement carries no logic;
/// it is interpreted at validation time by one or more registered <see cref="IJwtValidationHandler"/> strategies.
/// </summary>
[PublicAPI]
public interface IJwtValidationRequirement;
