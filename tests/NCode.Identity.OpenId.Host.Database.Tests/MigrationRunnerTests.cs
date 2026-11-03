// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.OpenId.Host.Database.Tests;

public sealed class MigrationRunnerTests
{
    private const string ContextName = "OpenIdDbContext";
    private const string InitialCreate = "20261103144453_InitialCreate";
    private const string UnknownInitialCreate = "20261001000000_InitialCreate";

    #region ThrowIfUnknownMigrationsApplied Tests

    [Fact]
    public void ThrowIfUnknownMigrationsApplied_WhenNoneApplied_DoesNotThrow()
    {
        MigrationRunner.ThrowIfUnknownMigrationsApplied(ContextName, [], [InitialCreate]);
    }

    [Fact]
    public void ThrowIfUnknownMigrationsApplied_WhenAppliedIdIsKnown_DoesNotThrow()
    {
        MigrationRunner.ThrowIfUnknownMigrationsApplied(
            ContextName,
            [InitialCreate],
            [InitialCreate]
        );
    }

    [Fact]
    public void ThrowIfUnknownMigrationsApplied_WhenAppliedIdIsUnknown_ThrowsWithTheReset()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MigrationRunner.ThrowIfUnknownMigrationsApplied(
                ContextName,
                [UnknownInitialCreate],
                [InitialCreate]
            )
        );

        Assert.Equal(
            $"The database's {ContextName} migrations history records {UnknownInitialCreate}, which this host does "
                + $"not hold, so its {ContextName} migrations cannot be applied to it. Each host database context has "
                + "one InitialCreate, which is re-scaffolded with a fresh identifier on each model change. Stop the "
                + "AppHost and reset the local database with build/dev-db-reset.ps1; the next AppHost run creates "
                + "the schema and seeds the development data.",
            exception.Message
        );
    }

    #endregion
}
