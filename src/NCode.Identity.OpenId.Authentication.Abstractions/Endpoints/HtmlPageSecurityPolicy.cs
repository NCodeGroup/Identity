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

using System.Collections.Immutable;
using System.Text;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Describes the Content-Security-Policy and framing rules of one HTML page. A page starts from the strict default,
/// which allows nothing, forbids framing, and forbids a base URI and a form submission, and then lists the directives
/// that it needs.
/// </summary>
[PublicAPI]
public sealed class HtmlPageSecurityPolicy
{
    /// <summary>
    /// Gets the directives that the page lists, in the order they are written after <c>default-src 'none'</c>. A
    /// <c>base-uri</c> or <c>form-action</c> directive replaces its strict default. The list must not name
    /// <c>default-src</c> or <c>frame-ancestors</c>, or name a directive twice.
    /// </summary>
    public ImmutableArray<ContentSecurityPolicyDirective> Directives { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the page's forms may submit to any URL, so that the policy omits
    /// <c>form-action</c> unless the page lists it. Browsers check <c>form-action</c> against each redirect that
    /// follows a submission, so a page whose submission can reach a client cannot list the targets.
    /// </summary>
    public bool AllowsAnyFormAction { get; init; }

    /// <summary>
    /// Gets a value indicating whether other origins may frame the page, so that the policy omits
    /// <c>frame-ancestors</c> and the response carries no <c>X-Frame-Options</c> header.
    /// </summary>
    public bool AllowsFraming { get; init; }

    /// <summary>
    /// Gets the sources that may frame the page, which the policy writes as its <c>frame-ancestors</c> directive in
    /// place of <c>'none'</c>. When it lists a source, the response carries no <c>X-Frame-Options</c> header, which
    /// cannot name an origin. It must be empty when <see cref="AllowsFraming"/> is set.
    /// </summary>
    public ImmutableArray<string> FrameAncestors { get; init; } = [];

    /// <summary>
    /// Creates the value of the <c>Content-Security-Policy</c> header: <c>default-src 'none'</c>, the page's
    /// directives in order, then the strict defaults for <c>base-uri</c> and <c>form-action</c> that the page did not
    /// replace or open, and <c>frame-ancestors</c>, which lists the page's frame ancestors or is <c>'none'</c> unless
    /// the page allows framing.
    /// </summary>
    /// <returns>The policy as a header value.</returns>
    /// <exception cref="InvalidOperationException">
    /// The page lists <c>default-src</c> or <c>frame-ancestors</c>, lists a directive name twice, or both allows
    /// framing and lists frame ancestors.
    /// </exception>
    /// <exception cref="ArgumentException">A frame ancestor is not a valid source.</exception>
    public string ToContentSecurityPolicy()
    {
        var directives = Directives.IsDefault ? [] : Directives;
        var frameAncestors = FrameAncestors.IsDefault ? [] : FrameAncestors;

        if (AllowsFraming && !frameAncestors.IsEmpty)
        {
            throw new InvalidOperationException(
                "The policy must not both allow framing and list frame ancestors."
            );
        }

        var listedNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var directive in directives)
        {
            if (
                directive.Name
                is ContentSecurityPolicyDirectives.DefaultSrc
                    or ContentSecurityPolicyDirectives.FrameAncestors
            )
            {
                throw new InvalidOperationException(
                    $"The policy must not list the '{directive.Name}' directive; the page security sets it."
                );
            }

            if (!listedNames.Add(directive.Name))
            {
                throw new InvalidOperationException(
                    $"The policy lists the '{directive.Name}' directive twice."
                );
            }
        }

        var builder = new StringBuilder();
        builder
            .Append(ContentSecurityPolicyDirectives.DefaultSrc)
            .Append(' ')
            .Append(ContentSecurityPolicySources.None);

        foreach (var directive in directives)
        {
            builder.Append("; ").Append(directive.Name);
            if (directive.Sources.IsEmpty)
            {
                builder.Append(' ').Append(ContentSecurityPolicySources.None);
                continue;
            }

            foreach (var source in directive.Sources)
            {
                builder.Append(' ').Append(source);
            }
        }

        if (!listedNames.Contains(ContentSecurityPolicyDirectives.BaseUri))
        {
            AppendNone(builder, ContentSecurityPolicyDirectives.BaseUri);
        }

        if (
            !AllowsAnyFormAction
            && !listedNames.Contains(ContentSecurityPolicyDirectives.FormAction)
        )
        {
            AppendNone(builder, ContentSecurityPolicyDirectives.FormAction);
        }

        if (AllowsFraming)
        {
            return builder.ToString();
        }

        if (frameAncestors.IsEmpty)
        {
            AppendNone(builder, ContentSecurityPolicyDirectives.FrameAncestors);
            return builder.ToString();
        }

        var frameAncestorsDirective = new ContentSecurityPolicyDirective(
            ContentSecurityPolicyDirectives.FrameAncestors,
            frameAncestors
        );

        builder.Append("; ").Append(frameAncestorsDirective.Name);
        foreach (var source in frameAncestorsDirective.Sources)
        {
            builder.Append(' ').Append(source);
        }

        return builder.ToString();
    }

    private static void AppendNone(StringBuilder builder, string directiveName) =>
        builder
            .Append("; ")
            .Append(directiveName)
            .Append(' ')
            .Append(ContentSecurityPolicySources.None);
}
