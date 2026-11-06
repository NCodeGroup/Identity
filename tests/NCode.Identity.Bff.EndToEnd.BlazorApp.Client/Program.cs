// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using NCode.Identity.Bff.Blazor.Client;
using NCode.Identity.Bff.EndToEnd.BlazorApp.Client.Pages;

namespace NCode.Identity.Bff.EndToEnd.BlazorApp.Client;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);

        builder.Services.AddBffBlazorClient();
        builder
            .Services.AddHttpClient(
                WasmPage.ApiClientName,
                client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            )
            .AddBffAntiforgeryHeader();

        await builder.Build().RunAsync();
    }
}
