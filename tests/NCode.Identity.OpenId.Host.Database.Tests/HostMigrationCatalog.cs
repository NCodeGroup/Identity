// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace NCode.Identity.OpenId.Host.Database.Tests;

/// <summary>
/// Reads the host's migrations from the database project, each by its name relative to the migrations namespace, such
/// as <c>OpenId.InitialCreate</c>.
/// </summary>
internal static class HostMigrationCatalog
{
    public const string MigrationsNamespace = "NCode.Identity.OpenId.Host.Database.Migrations";

    public static IEnumerable<Type> GetTypes() =>
        typeof(HostDatabase)
            .Assembly.GetTypes()
            .Where(type =>
                typeof(Migration).IsAssignableFrom(type)
                && type is { IsAbstract: false }
                && type.GetCustomAttribute<MigrationAttribute>() is not null
            )
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

    public static TheoryData<string> GetNames()
    {
        var data = new TheoryData<string>();
        foreach (var type in GetTypes())
            data.Add(type.FullName![(MigrationsNamespace.Length + 1)..]);
        return data;
    }

    public static Migration Create(string migrationName)
    {
        var type = typeof(HostDatabase).Assembly.GetType(
            $"{MigrationsNamespace}.{migrationName}",
            throwOnError: true
        )!;
        var migration = (Migration)Activator.CreateInstance(type)!;
        migration.ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
        return migration;
    }
}
