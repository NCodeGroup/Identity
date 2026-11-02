// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using IdGen;
using NCode.Identity.OpenId.Host.MigrationService;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core;

// WORKER: a one-shot Aspire resource that applies the host Postgres migrations, then stops. It registers only
// what constructing the contexts needs (the OpenID persistence slice + its id generator + model contributors); it does
// not boot the web host. See ADR-1073.
var builder = Host.CreateApplicationBuilder(args);

// Logs go to standard output on every platform: the console provider writes them, and the EventSource provider hands
// them to a diagnostics tool that attaches (dotnet-monitor, dotnet-trace). The default builder's Windows Event Log and
// debugger providers are left out. See project-template ADR-0115.
builder.Logging.ClearProviders().AddConsole().AddEventSourceLogger();

builder.Services.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
builder
    .Services.AddEntityFrameworkCorePersistence<OpenIdDbContext>()
    .AddLocalAccountEntityFramework();

builder.Services.AddHostedService<MigrationWorker>();

builder.Build().Run();
