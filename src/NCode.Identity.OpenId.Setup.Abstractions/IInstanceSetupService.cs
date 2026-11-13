// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Creates the root administrator of an instance once and records the instance as initialized. Register a custom
/// implementation to replace or extend the default behavior.
/// </summary>
[PublicAPI]
public interface IInstanceSetupService
{
    /// <summary>
    /// Determines whether the instance is initialized.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the root administrator was created; otherwise, <c>false</c>.</returns>
    ValueTask<bool> IsInitializedAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates the root administrator and marks the instance initialized in one unit of work.
    /// </summary>
    /// <param name="root">The <see cref="SetupRootOptions"/> describing the root administrator.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="InstanceSetupResult"/> carrying the created root administrator, or the error describing the first failed precondition.</returns>
    ValueTask<InstanceSetupResult> CreateRootAsync(
        SetupRootOptions root,
        CancellationToken cancellationToken
    );
}
