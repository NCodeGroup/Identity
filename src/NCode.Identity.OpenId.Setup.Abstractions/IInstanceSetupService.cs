// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Creates the root administrator of an instance once, together with the management console's client when the request
/// carries one, and records the instance as initialized. Register a custom implementation to replace or extend the
/// default behavior.
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
    /// Creates the root administrator and the management console's client, when the request carries one, and marks the
    /// instance initialized in one unit of work.
    /// </summary>
    /// <param name="request">The <see cref="InstanceSetupRequest"/> describing what setup creates.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="InstanceSetupResult"/> carrying the created root administrator, or the error describing the first failed precondition.</returns>
    ValueTask<InstanceSetupResult> SetUpAsync(
        InstanceSetupRequest request,
        CancellationToken cancellationToken
    );
}
