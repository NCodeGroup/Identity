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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;
using NCode.PropertyBag;

namespace NCode.Identity.Jwt.Validation;

/// <summary>
/// Carries the state shared by all <see cref="IJwtValidationHandler"/> instances while a decoded JSON Web Token (JWT)
/// is evaluated against its <see cref="IJwtValidationRequirement"/> set. Modeled on
/// <c>Microsoft.AspNetCore.Authorization.AuthorizationHandlerContext</c>: handlers vote on each requirement by calling
/// <see cref="Succeed"/> (the requirement is satisfied) or <see cref="Fail(string, string)"/> (a hard veto). The token is valid only
/// when every requirement has succeeded and nothing has failed.
/// </summary>
[PublicAPI]
public sealed class JwtValidationContext
{
    private readonly HashSet<IJwtValidationRequirement> _pendingRequirements;
    private readonly List<string> _failureReasons = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtValidationContext"/> class.
    /// </summary>
    /// <param name="secretKey">The <see cref="SecretKey"/> that was used to decode the JSON Web Token (JWT).</param>
    /// <param name="decodedJwt">The <see cref="DecodedJwt"/> that contains the decoded header and payload.</param>
    /// <param name="requirements">The collection of <see cref="IJwtValidationRequirement"/> instances to evaluate.</param>
    /// <param name="propertyBag">The <see cref="IPropertyBag"/> for custom state information.</param>
    /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve services.</param>
    /// <param name="timeProvider">The <see cref="TimeProvider"/> used to get the current time.</param>
    public JwtValidationContext(
        SecretKey secretKey,
        DecodedJwt decodedJwt,
        IReadOnlyCollection<IJwtValidationRequirement> requirements,
        IPropertyBag propertyBag,
        IServiceProvider serviceProvider,
        TimeProvider timeProvider
    )
    {
        SecretKey = secretKey;
        DecodedJwt = decodedJwt;
        Requirements = requirements;
        PropertyBag = propertyBag;
        ServiceProvider = serviceProvider;
        TimeProvider = timeProvider;
        _pendingRequirements = [.. requirements];
    }

    /// <summary>
    /// Gets the <see cref="SecretKey"/> that was used to decode the JSON Web Token (JWT).
    /// </summary>
    public SecretKey SecretKey { get; }

    /// <summary>
    /// Gets a <see cref="DecodedJwt"/> that contains the decoded JSON Web Token (JWT) header and payload.
    /// </summary>
    public DecodedJwt DecodedJwt { get; }

    /// <summary>
    /// Gets the collection of <see cref="IJwtValidationRequirement"/> instances being evaluated.
    /// </summary>
    public IReadOnlyCollection<IJwtValidationRequirement> Requirements { get; }

    /// <summary>
    /// Gets an <see cref="IPropertyBag"/> that can provide additional user-defined information about the current operation.
    /// </summary>
    public IPropertyBag PropertyBag { get; }

    /// <summary>
    /// Gets an <see cref="IServiceProvider"/> that can be used to resolve services.
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    /// <summary>
    /// Gets a <see cref="TimeProvider"/> that can be used to get the current time.
    /// </summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>
    /// Gets the machine-readable code of the first failure recorded with <see cref="Fail(string, string)"/>, or
    /// <c>null</c> when nothing has failed.
    /// </summary>
    public string? FailureCode { get; private set; }

    /// <summary>
    /// Gets a value indicating whether a handler called <see cref="Fail(string, string)"/> (a hard veto).
    /// </summary>
    [MemberNotNullWhen(true, nameof(FailureCode))]
    public bool HasFailed => FailureCode is not null;

    /// <summary>
    /// Gets the reasons supplied to <see cref="Fail(string, string)"/>, if any.
    /// </summary>
    public IReadOnlyList<string> FailureReasons => _failureReasons;

    /// <summary>
    /// Gets the requirements that no handler has marked as succeeded yet.
    /// </summary>
    public IReadOnlyCollection<IJwtValidationRequirement> PendingRequirements =>
        _pendingRequirements;

    /// <summary>
    /// Gets a value indicating whether every requirement has succeeded and nothing has failed.
    /// </summary>
    public bool HasSucceeded => !HasFailed && _pendingRequirements.Count == 0;

    /// <summary>
    /// Marks the specified <paramref name="requirement"/> as satisfied.
    /// </summary>
    /// <param name="requirement">The <see cref="IJwtValidationRequirement"/> that has been satisfied.</param>
    public void Succeed(IJwtValidationRequirement requirement) =>
        _pendingRequirements.Remove(requirement);

    /// <summary>
    /// Records a hard validation failure with the machine-readable code
    /// <see cref="JwtValidationFailureCodes.InvalidToken"/>. Once called, <see cref="HasSucceeded"/> can never
    /// become <c>true</c>, even if every requirement is subsequently satisfied.
    /// </summary>
    /// <param name="reason">A human-readable description of why validation failed.</param>
    public void Fail(string reason) => Fail(JwtValidationFailureCodes.InvalidToken, reason);

    /// <summary>
    /// Records a hard validation failure. Once called, <see cref="HasSucceeded"/> can never become <c>true</c>,
    /// even if every requirement is subsequently satisfied. The first code recorded is kept in
    /// <see cref="FailureCode"/>, and every <paramref name="reason"/> is kept in <see cref="FailureReasons"/>.
    /// </summary>
    /// <param name="failureCode">
    /// A machine-readable code that names why validation failed, typically one of the
    /// <see cref="JwtValidationFailureCodes"/>.
    /// </param>
    /// <param name="reason">A human-readable description of why validation failed.</param>
    /// <exception cref="ArgumentException"><paramref name="failureCode"/> is <c>null</c> or empty.</exception>
    public void Fail(string failureCode, string reason)
    {
        ArgumentException.ThrowIfNullOrEmpty(failureCode);

        FailureCode ??= failureCode;
        _failureReasons.Add(reason);
    }
}
