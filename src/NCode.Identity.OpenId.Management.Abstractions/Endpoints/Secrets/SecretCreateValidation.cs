// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Management.Contracts.Secrets;
using NCode.Identity.Secrets.KeyMaterial;

namespace NCode.Identity.OpenId.Management.Endpoints.Secrets;

/// <summary>
/// The outcome of validating a <see cref="CreateSecretRequest"/>: either the parameters the key is generated with,
/// resolved from the request against its algorithm, or the error that describes why the request is not coherent.
/// </summary>
[PublicAPI]
public sealed class SecretCreateValidation
{
    private SecretCreateValidation(
        SecretKeyGenerationParameters? generationParameters,
        ManagementError? error
    )
    {
        GenerationParameters = generationParameters;
        Error = error;
    }

    /// <summary>
    /// Creates the outcome of a coherent request.
    /// </summary>
    /// <param name="generationParameters">The parameters the key is generated with.</param>
    /// <returns>The <see cref="SecretCreateValidation"/>.</returns>
    public static SecretCreateValidation Valid(SecretKeyGenerationParameters generationParameters)
    {
        ArgumentNullException.ThrowIfNull(generationParameters);
        return new SecretCreateValidation(generationParameters, error: null);
    }

    /// <summary>
    /// Creates the outcome of a request that is not coherent.
    /// </summary>
    /// <param name="error">The error that describes the first failed check.</param>
    /// <returns>The <see cref="SecretCreateValidation"/>.</returns>
    public static SecretCreateValidation Invalid(ManagementError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new SecretCreateValidation(generationParameters: null, error);
    }

    /// <summary>
    /// Gets a value indicating whether the request is coherent.
    /// </summary>
    [MemberNotNullWhen(true, nameof(GenerationParameters))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsValid => Error is null;

    /// <summary>
    /// Gets the parameters the key is generated with: the request's size and curve, or, for a request that names an
    /// algorithm bound to curves and no curve, the first curve the algorithm accepts that has the requested size.
    /// <c>null</c> when the request is not coherent.
    /// </summary>
    public SecretKeyGenerationParameters? GenerationParameters { get; }

    /// <summary>
    /// Gets the error that describes the first failed check, or <c>null</c> when the request is coherent.
    /// </summary>
    public ManagementError? Error { get; }
}
