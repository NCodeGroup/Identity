// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.Jose.SdJwt.Verification;

/// <summary>
/// The outcome of verifying the Key Binding JWT of a presentation: the verified key binding, or the failure the
/// verifier returns.
/// </summary>
internal readonly struct KeyBindingOutcome
{
    private KeyBindingOutcome(SdJwtKeyBinding? keyBinding, SdJwtVerificationResult? failure)
    {
        KeyBinding = keyBinding;
        Failure = failure;
    }

    /// <summary>
    /// Creates the outcome of a verified key binding.
    /// </summary>
    /// <param name="keyBinding">The verified key binding.</param>
    /// <returns>The outcome.</returns>
    public static KeyBindingOutcome Succeed(SdJwtKeyBinding keyBinding) =>
        new(keyBinding, failure: null);

    /// <summary>
    /// Creates the outcome of a refused key binding.
    /// </summary>
    /// <param name="failureCode">The code of the rule the Key Binding JWT failed.</param>
    /// <param name="failureReason">Why the Key Binding JWT is refused.</param>
    /// <returns>The outcome.</returns>
    public static KeyBindingOutcome Fail(string failureCode, string failureReason) =>
        new(keyBinding: null, SdJwtVerificationResult.Fail(failureCode, failureReason));

    /// <summary>
    /// Gets a value indicating whether the Key Binding JWT verified.
    /// </summary>
    [MemberNotNullWhen(true, nameof(KeyBinding))]
    [MemberNotNullWhen(false, nameof(Failure))]
    public bool IsValid => Failure is null;

    /// <summary>
    /// Gets the verified key binding, or <c>null</c> when the Key Binding JWT is refused.
    /// </summary>
    public SdJwtKeyBinding? KeyBinding { get; }

    /// <summary>
    /// Gets the failure the verifier returns, or <c>null</c> when the Key Binding JWT verified.
    /// </summary>
    public SdJwtVerificationResult? Failure { get; }
}
