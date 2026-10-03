#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using NCode.Identity.OpenId.Persistence.EntityFramework;

namespace NCode.Identity.OpenId.Playground;

internal static class Program
{
    public static void Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        InitializeDatabase(host);
        host.Run();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<Startup>();
            });

    private static void InitializeDatabase(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        var environment = services.GetRequiredService<IHostEnvironment>();
        var context = services.GetRequiredService<OpenIdDbContext>();

        // The in-memory provider has no migrations; just materialize the model from scratch.
        if (!context.Database.IsRelational())
        {
            context.Database.EnsureCreated();
            return;
        }

        // Production applies schema changes deliberately (EF CLI / idempotent SQL script), never on startup. Only
        // Development auto-applies pending migrations so the local developer database evolves without losing data.
        if (!environment.IsDevelopment())
        {
            return;
        }

        // A database with tables but no migrations history was built by EnsureCreated (or by hand) and cannot be
        // migrated in place; fail with guidance instead of a cryptic "table already exists". Never auto-delete.
        var databaseCreator = context.GetService<IRelationalDatabaseCreator>();
        if (
            databaseCreator.Exists()
            && databaseCreator.HasTables()
            && !context.Database.GetAppliedMigrations().Any()
        )
        {
            throw new InvalidOperationException(
                "The developer database is not managed by EF Core migrations (it was likely created by an earlier "
                    + "EnsureCreated build). Delete 'App_Data/openid-dev.db*' and restart to rebuild it from migrations."
            );
        }

        context.Database.Migrate();
    }
}
