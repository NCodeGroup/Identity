// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using Moq.Language.Flow;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Core.Tests;

/// <summary>
/// Provides the shared <see cref="IStoreManager"/> mock setups for the store-backed service tests.
/// </summary>
internal static class StoreManagerMockExtensions
{
    /// <summary>
    /// Sets up <see cref="IStoreManager.SaveChangesAsync"/> with <paramref name="cancellationToken"/> to fail with
    /// <paramref name="exception"/> on its first call and succeed on every later call, verified
    /// <paramref name="times"/>.
    /// </summary>
    public static void SetupSaveChangesFailingOnce(
        this Mock<IStoreManager> mock,
        Exception exception,
        Times times,
        CancellationToken cancellationToken
    ) =>
        ReturnsFailingOnce(mock.Setup(x => x.SaveChangesAsync(cancellationToken)), exception)
            .Verifiable(times);

    /// <summary>
    /// Sets up <see cref="IStoreManager.SaveChangesAsync"/> with any cancellation token to fail with
    /// <paramref name="exception"/> on its first call and succeed on every later call, verified
    /// <paramref name="times"/>.
    /// </summary>
    public static void SetupSaveChangesFailingOnce(
        this Mock<IStoreManager> mock,
        Exception exception,
        Times times
    ) =>
        ReturnsFailingOnce(
                mock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())),
                exception
            )
            .Verifiable(times);

    private static IReturnsResult<IStoreManager> ReturnsFailingOnce(
        ISetup<IStoreManager, ValueTask> setup,
        Exception exception
    )
    {
        var saves = 0;
        return setup.Returns(() =>
            ++saves == 1 ? ValueTask.FromException(exception) : ValueTask.CompletedTask
        );
    }
}
