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

using IdGen.DependencyInjection;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Options;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.Server;

/*
 *
 * InferEndpointType
 * InferIssuerFromHost
 * ValidateTransportSecurityRequirement
 *
 */

namespace NCode.Identity.OpenId.Playground;

internal class Startup(IConfiguration configuration)
{
    private IConfiguration Configuration { get; } = configuration;

    public void ConfigureServices(IServiceCollection services)
    {
        const int generatorId = 1;
        services.AddIdGen(generatorId);

        services.AddHealthChecks();
        services.AddHttpLogging(options => { options.LoggingFields = HttpLoggingFields.All; });

        services.AddHttpClient();

        var openIdOptionsSectionName = Environment.GetEnvironmentVariable("OpenId_OptionsSectionName");
        if (string.IsNullOrEmpty(openIdOptionsSectionName))
        {
            openIdOptionsSectionName = OpenIdOptions.DefaultSectionName;
        }

        services.Configure<OpenIdOptions>(Configuration.GetSection(openIdOptionsSectionName));
        services.Configure<OpenIdOptions>(options => options.SectionName = openIdOptionsSectionName);

        services.AddIdentityServer(builder =>
        {
            //
            // builder.AddOpenIdAuthentication();
        });

        services.AddEntityFrameworkPersistenceServices<OpenIdDbContext>();

        services.AddDbContextFactory<OpenIdDbContext>(builder =>
        {
            // TODO
            builder.UseInMemoryDatabase("OpenId");
        });

        services.AddControllers();
        services.AddSwaggerGen(c => { c.SwaggerDoc("v1", new OpenApiInfo { Title = "NCode.Identity.OpenId.Playground", Version = "v1" }); });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "NCode.Identity.OpenId.Playground v1"));
        }

        app.UseHttpLogging();
        app.UseHttpsRedirection();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        // app.UseMiddleware<OpenIdMiddleware>();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapIdentityEndpoints();
            // endpoints.MapOpenId().WithHttpLogging(HttpLoggingFields.All);

            endpoints.MapControllers().WithHttpLogging(HttpLoggingFields.All);
            endpoints.MapHealthChecks("/health").WithName("health_endpoint");
        });
    }
}
