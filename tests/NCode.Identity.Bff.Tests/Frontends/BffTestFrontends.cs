// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Options;

namespace NCode.Identity.Bff.Tests.Frontends;

/// <summary>
/// Builds the frontends and catalogs that tests of the BFF use.
/// </summary>
internal static class BffTestFrontends
{
    public const string Name = "default";

    public const string SessionCookieName = "__Host-Http-bff.default";

    public const string LoginCookieName = "__Host-Http-bff-login.default";

    public static BffFrontendOptions CreateOptions(Action<BffFrontendOptions>? configure = null)
    {
        var options = new BffFrontendOptions
        {
            Authority = "https://op.example.com",
            PublicOrigin = "https://app.example.com",
            ClientId = "bff",
            ReturnUrls = ["/"],
        };
        configure?.Invoke(options);
        return options;
    }

    public static BffFrontendCatalog CreateCatalog(Action<BffFrontendOptions>? configure = null) =>
        CreateCatalog(new BffOptions { Frontends = { [Name] = CreateOptions(configure) } });

    public static BffFrontendCatalog CreateCatalog(BffOptions options) =>
        new(
            Microsoft.Extensions.Options.Options.Create(options),
            [new HostBffFrontendSelector(), new PathBffFrontendSelector()]
        );

    public static BffFrontend Get(BffFrontendCatalog catalog) => catalog.Frontends.Single();
}
