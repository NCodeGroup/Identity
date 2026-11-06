// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Antiforgery;
using NCode.Identity.Bff.Blazor;
using NCode.Identity.Bff.Endpoints;
using NCode.Identity.Bff.EndToEnd.BlazorApp;
using NCode.Identity.Bff.EndToEnd.BlazorApp.Client.Pages;
using NCode.Identity.Bff.EndToEnd.BlazorApp.Components;
using NCode.Identity.Bff.Server;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityBff(builder.Configuration).BffLibraryBuilder.AddBffBlazorLibrary();
builder
    .Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<HostCounters>();

var app = builder.Build();

app.UseRouting();
app.UseBff();
app.Use(
    (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/_blazor"))
        {
            context.RequestServices.GetRequiredService<HostCounters>().CountHubRequest();
        }

        return next(context);
    }
);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapBffEndpoints();
app.MapHub<NotificationsHub>("/hubs/notifications").AsBffHub();
app.MapGet(
        "/api/me",
        (HttpContext context, HostCounters counters) =>
        {
            counters.CountApiCall(context.Request.Headers);
            return Results.Json(new { sub = context.User.FindFirst("sub")?.Value });
        }
    )
    .AsBffApi();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(WasmPage).Assembly)
    .AsBffComponents();

app.Run();
