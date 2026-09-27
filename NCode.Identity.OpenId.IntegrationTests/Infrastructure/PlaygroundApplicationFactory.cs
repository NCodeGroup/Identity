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

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Playground;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that boots the <c>Playground</c> host in-process
/// through the full ASP.NET Core HTTP pipeline (via <see cref="TestServer"/>), providing an
/// <see cref="System.Net.Http.HttpClient"/> for over-the-wire integration tests.
/// </summary>
/// <remarks>
/// Each factory instance uses its own uniquely-named in-memory database so tests are fully isolated
/// (EF Core caches its internal service provider by option configuration, so a shared database name
/// would cause parallel test hosts to collide). At present the tenant has no signing keys; once an
/// ephemeral development key composition exists, this factory (or a derived one) can apply it so that
/// token-signing and a non-empty JWKS can be asserted.
/// </remarks>
public class PlaygroundApplicationFactory : WebApplicationFactory<PlaygroundApiMarker>
{
    private string DatabaseName { get; } = $"it-{Guid.NewGuid():N}";

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Resolve the content root to the Playground project so its appsettings.json is loaded.
        builder.UseSolutionRelativeContentRoot("NCode.Identity.OpenId.Playground");
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Development);

        builder.ConfigureTestServices(services =>
        {
            // Replace the Playground's shared in-memory database with a per-factory isolated one.
            services.RemoveAll<DbContextOptions<OpenIdDbContext>>();
            services.RemoveAll<IDbContextFactory<OpenIdDbContext>>();

            var databaseRoot = new InMemoryDatabaseRoot();
            services.AddDbContextFactory<OpenIdDbContext>(options =>
                options.UseInMemoryDatabase(DatabaseName, databaseRoot));
        });
    }
}




