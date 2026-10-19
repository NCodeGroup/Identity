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

using Microsoft.Extensions.Options;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Reads the single-page bundle's <c>index.html</c> so the login, consent, and error pages can inject their
/// server-rendered form into the bundle's <c>#root</c> mount point (progressive enhancement). The index is read on
/// each request rather than cached so a locally rebuilt bundle is picked up without a restart; the no-JavaScript forms
/// render standalone when no bundle is configured or available.
/// </summary>
internal sealed class InteractiveBundleReader(
    IOptions<InteractiveAuthenticationOptions> optionsAccessor
)
{
    private InteractiveAuthenticationOptions Options { get; } = optionsAccessor.Value;

    /// <summary>
    /// Reads the single-page bundle's <c>index.html</c>, or returns <see langword="null"/> when no bundle is
    /// configured or available.
    /// </summary>
    public async ValueTask<string?> TryReadIndexHtmlAsync(CancellationToken cancellationToken)
    {
        var bundleRootPath = Options.BundleRootPath;
        if (string.IsNullOrEmpty(bundleRootPath))
        {
            return null;
        }

        var indexPath = Path.Combine(bundleRootPath, "index.html");
        if (!File.Exists(indexPath))
        {
            return null;
        }

        return await File.ReadAllTextAsync(indexPath, cancellationToken);
    }
}
