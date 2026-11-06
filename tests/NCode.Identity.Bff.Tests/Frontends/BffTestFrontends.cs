// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
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

    public const string Authority = "https://op.example.com";

    public const string ClientId = "bff";

    public static BffFrontendOptions CreateOptions(Action<BffFrontendOptions>? configure = null)
    {
        var options = new BffFrontendOptions
        {
            Authority = Authority,
            PublicOrigin = "https://app.example.com",
            ClientId = ClientId,
            ReturnUrls = ["/"],
        };
        configure?.Invoke(options);
        return options;
    }

    public static BffOptions CreateBffOptions(Action<BffOptions>? configure = null)
    {
        var options = new BffOptions { Frontends = { [Name] = CreateOptions() } };
        configure?.Invoke(options);
        return options;
    }

    public static BffFrontendCatalog CreateCatalog(Action<BffFrontendOptions>? configure = null) =>
        CreateCatalog(new BffOptions { Frontends = { [Name] = CreateOptions(configure) } });

    public static BffFrontendCatalog CreateCatalog(BffOptions options) =>
        CreateCatalog(new BffTestOptionsSource(options));

    public static BffFrontendCatalog CreateCatalog(
        BffTestOptionsSource source,
        params IBffFrontendSetCheck[] checks
    ) =>
        new(
            source,
            [source],
            [new HostBffFrontendSelector(), new PathBffFrontendSelector()],
            checks,
            NullLogger<BffFrontendCatalog>.Instance
        );

    public static BffFrontend Get(BffFrontendCatalog catalog) => catalog.Frontends.Single();
}
