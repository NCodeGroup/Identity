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

using IdGen;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.EntityFramework.Accounts;
using NCode.Identity.OpenId.Persistence.EntityFramework.Core;
using NCode.Identity.OpenId.Playground.MigrationService;

// WORKER: a one-shot Aspire resource that applies the Playground Postgres migrations, then stops. It registers only
// what constructing the contexts needs (the OpenID persistence slice + its id generator + model contributors); it does
// not boot the web host. See ADR-0073.
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IIdGenerator<long>>(new IdGenerator(0));
builder
    .Services.AddEntityFrameworkCorePersistence<OpenIdDbContext>()
    .AddLocalAccountEntityFramework();

builder.Services.AddHostedService<MigrationWorker>();

builder.Build().Run();
