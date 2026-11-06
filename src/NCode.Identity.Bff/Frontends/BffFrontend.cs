// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.Hardening;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client;

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// Describes one browser frontend at runtime: its name, options, client registration, cookie names and selection rule.
/// </summary>
internal sealed class BffFrontend
{
    /// <summary>
    /// Creates what a frontend's selection rule is given from the frontend's options.
    /// </summary>
    /// <param name="options">The options of the frontend.</param>
    /// <returns>The selection, or <see langword="null"/> when the frontend names no rule.</returns>
    internal static BffFrontendSelection? CreateSelection(BffFrontendOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Selection?.Rule))
            return null;

        List<string> paths =
        [
            options.PathPrefix,
            .. options.RemoteApis.Values.OfType<BffRemoteApiOptions>().Select(x => x.Path),
        ];
        return new BffFrontendSelection(options.Selection.Values, options.PublicOrigin, paths);
    }

    private static ClientRegistration CreateRegistration(BffFrontendOptions options) =>
        new(options.Authority, options.ClientId, options.ClientAuthentication.Method)
        {
            KeyRing = options.ClientAuthentication.KeyRing,
            DPoPKeyRing = options.DPoP.KeyRing,
            DPoPBoundAccessTokens = options.DPoP.Mode == BffDPoPModes.Required,
            BackchannelLogoutSessionRequired = options.BackchannelLogoutSessionRequired,
            IdTokenSigningAlgorithms = options.IdToken.SigningAlgorithms,
            IdTokenKeyManagementAlgorithms = options.IdToken.KeyManagementAlgorithms,
            IdTokenContentEncryptionAlgorithms = options.IdToken.ContentEncryptionAlgorithms,
            UserInfoSigningAlgorithms = options.UserInfo.SigningAlgorithms,
            UserInfoKeyManagementAlgorithms = options.UserInfo.KeyManagementAlgorithms,
            UserInfoContentEncryptionAlgorithms = options.UserInfo.ContentEncryptionAlgorithms,
        };

    /// <summary>
    /// Initializes a new instance of the <see cref="BffFrontend"/> class.
    /// </summary>
    /// <param name="name">The name of the frontend.</param>
    /// <param name="options">The options of the frontend.</param>
    /// <param name="selector">The selection rule of the frontend, or <see langword="null"/> when the frontend has none.</param>
    /// <exception cref="ArgumentException">
    /// The frontend names a selection rule without a selector, a selector is given without a selection rule, or the
    /// selector is not the rule that the frontend names.
    /// </exception>
    public BffFrontend(string name, BffFrontendOptions options, IBffFrontendSelector? selector)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(options);

        var selection = CreateSelection(options);
        if (selection is not null && selector is null)
            throw new ArgumentException(
                $"The frontend '{name}' names a selection rule but no selector was given.",
                nameof(selector)
            );
        if (selection is null && selector is not null)
            throw new ArgumentException(
                $"The frontend '{name}' has no selection rule but a selector was given.",
                nameof(selector)
            );
        if (
            selector is not null
            && !string.Equals(selector.Name, options.Selection.Rule, StringComparison.Ordinal)
        )
            throw new ArgumentException(
                $"The frontend '{name}' names the selection rule '{options.Selection.Rule}' but the selector is '{selector.Name}'.",
                nameof(selector)
            );

        Name = name;
        Options = options;
        Selection = selection;
        Selector = selector;
        Registration = CreateRegistration(options);
        SessionCookieName = $"{BffDefaults.SessionCookieName}.{name}";
        LoginCookieName = $"{BffDefaults.LoginCookieName}.{name}";
        SecurityHeaders = BffSecurityHeaders.Create(options.SecurityHeaders);
    }

    /// <summary>
    /// Gets the name of the frontend.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the options of the frontend.
    /// </summary>
    public BffFrontendOptions Options { get; }

    /// <summary>
    /// Gets the client registration that describes the frontend as a client of the OpenID Provider, built once from the
    /// options.
    /// </summary>
    public ClientRegistration Registration { get; }

    /// <summary>
    /// Gets the name of the session cookie of the frontend.
    /// </summary>
    public string SessionCookieName { get; }

    /// <summary>
    /// Gets the name of the login transaction cookie of the frontend.
    /// </summary>
    public string LoginCookieName { get; }

    /// <summary>
    /// Gets the security headers of the frontend, computed once from its options.
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> SecurityHeaders { get; }

    /// <summary>
    /// Gets what the selection rule is given, or <see langword="null"/> when the frontend names no rule.
    /// </summary>
    public BffFrontendSelection? Selection { get; }

    /// <summary>
    /// Gets the selection rule of the frontend, or <see langword="null"/> when the frontend names no rule.
    /// </summary>
    public IBffFrontendSelector? Selector { get; }

    /// <summary>
    /// Gets a value indicating whether the frontend serves every request that no other frontend selects.
    /// </summary>
    public bool IsFallback => Selection is null;

    /// <summary>
    /// Determines whether a request belongs to the frontend by its selection rule.
    /// </summary>
    /// <param name="request">The current HTTP request.</param>
    /// <returns>
    /// <see langword="true"/> when the frontend names a rule and the rule selects the request; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public bool IsMatch(HttpRequest request) =>
        Selector is not null && Selection is not null && Selector.IsMatch(request, Selection);
}
