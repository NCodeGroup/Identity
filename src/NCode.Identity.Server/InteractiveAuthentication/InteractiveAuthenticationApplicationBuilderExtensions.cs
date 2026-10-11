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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;

namespace NCode.Identity.Server.InteractiveAuthentication;

/// <summary>
/// Provides the request-pipeline counterpart to <see cref="InteractiveAuthenticationRegistration"/>: serves the built
/// single-page login/consent bundle's hashed assets under <see cref="InteractiveAuthenticationDefaults.BasePath"/>.
/// </summary>
[PublicAPI]
public static class InteractiveAuthenticationApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        /// <summary>
        /// Serves the interactive surface's static assets (the <c>apps/auth</c> bundle) under
        /// <see cref="InteractiveAuthenticationDefaults.BasePath"/> when a bundle path is configured and present. It is
        /// a no-op when no bundle is configured, so it is safe to leave wired. Call before the endpoint middleware.
        /// </summary>
        /// <returns>The same <see cref="IApplicationBuilder"/> instance for method chaining.</returns>
        public IApplicationBuilder UseInteractiveAuthentication()
        {
            var options =
                app.ApplicationServices.GetRequiredService<InteractiveAuthenticationOptions>();

            var bundleRootPath = options.BundleRootPath;
            if (string.IsNullOrEmpty(bundleRootPath) || !Directory.Exists(bundleRootPath))
            {
                return app;
            }

            return app.UseStaticFiles(
                new StaticFileOptions
                {
                    FileProvider = new PhysicalFileProvider(bundleRootPath),
                    RequestPath = InteractiveAuthenticationDefaults.BasePath,
                }
            );
        }
    }
}
