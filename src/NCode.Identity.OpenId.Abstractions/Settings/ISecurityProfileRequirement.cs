// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Marks a single, self-contained rule imposed by a <see cref="ISecurityProfile"/> — one that cannot be expressed as a
/// floor/ceiling setting and is instead enforced at request time. A requirement carries both its definition and its
/// assertion logic; a profile composes a collection of them.
/// </summary>
/// <remarks>
/// This marker is deliberately open: a concrete requirement implements the generic
/// <see cref="ISecurityProfileRequirement{TContext}"/> for each enforcement <i>phase</i> it participates in (named
/// phase aliases — for example in the authentication slice — specialize it for readability). Each enforcement point
/// calls <see cref="ISecurityProfileCatalog.EvaluateRequirementsAsync{TContext}"/> with its phase context, which runs
/// the selected profiles' requirements assignable to that phase, so a new requirement kind is purely additive: it is a
/// new class a profile includes, and enforcement sites that do not understand its phase simply never see it. This keeps
/// the set of requirement kinds backward- and forward-compatible without changing this type, the catalog, or any
/// existing profile.
/// </remarks>
[PublicAPI]
public interface ISecurityProfileRequirement;

/// <summary>
/// A <see cref="ISecurityProfileRequirement"/> that is evaluated against a <typeparamref name="TContext"/> at the
/// enforcement phase that owns that context. An implementation throws an <c>OpenId</c> exception to reject the request.
/// </summary>
/// <typeparam name="TContext">The phase-specific evaluation context.</typeparam>
[PublicAPI]
public interface ISecurityProfileRequirement<in TContext> : ISecurityProfileRequirement
{
    /// <summary>
    /// Evaluates this requirement against <paramref name="context"/>, throwing to reject the request when the
    /// requirement is not satisfied.
    /// </summary>
    /// <param name="context">The phase-specific evaluation context.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken"/> for the operation.</param>
    ValueTask EvaluateAsync(TContext context, CancellationToken cancellationToken);
}
