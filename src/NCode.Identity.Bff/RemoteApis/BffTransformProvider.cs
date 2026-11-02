// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Frozen;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms.Builder;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Checks the BFF metadata of every YARP route, and gives each route that names a token requirement the transform
/// that strips the browser's credentials and resolves the route's token.
/// </summary>
internal sealed class BffTransformProvider : ITransformProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BffTransformProvider"/> class.
    /// </summary>
    /// <param name="clientFactory">The effective forwarder client factory, which must be the BFF's decorator.</param>
    /// <param name="requirements">The registered token requirements.</param>
    /// <param name="tokens">The accessor of the BFF's tokens.</param>
    /// <param name="options">The BFF options.</param>
    /// <exception cref="InvalidOperationException">The BFF's decorator is not the effective client factory.</exception>
    public BffTransformProvider(
        IForwarderHttpClientFactory clientFactory,
        IEnumerable<IBffTokenRequirement> requirements,
        IBffTokenAccessor tokens,
        IOptions<BffOptions> options
    )
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(tokens);
        ArgumentNullException.ThrowIfNull(options);

        // Without the decorator a route's token is never attached and the BFF's cookies cross the proxy.
        if (clientFactory is not BffForwarderHttpClientFactory)
        {
            throw new InvalidOperationException(
                "AddBffTransforms() must register the last IForwarderHttpClientFactory: call it after ConfigureHttpClient() and after any custom factory is registered."
            );
        }

        RequirementNames = requirements
            .Select(requirement => requirement.Name)
            .ToFrozenSet(StringComparer.Ordinal);
        Tokens = tokens;
        Options = options;
    }

    private FrozenSet<string> RequirementNames { get; }

    private IBffTokenAccessor Tokens { get; }

    private IOptions<BffOptions> Options { get; }

    /// <inheritdoc />
    public void ValidateRoute(TransformRouteValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        BffReverseProxyRoutes.Validate(context.Route, RequirementNames, context.Errors);
    }

    /// <inheritdoc />
    public void ValidateCluster(TransformClusterValidationContext context) =>
        ArgumentNullException.ThrowIfNull(context);

    /// <inheritdoc />
    public void Apply(TransformBuilderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var tokenRequest = BffReverseProxyRoutes.GetTokenRequest(context.Route);
        if (tokenRequest is null)
            return;

        var transform = new BffTokenRequestTransform(
            tokenRequest,
            Tokens,
            Options.Value.Antiforgery.HeaderName
        );
        context.RequestTransforms.Add(transform);
    }
}
