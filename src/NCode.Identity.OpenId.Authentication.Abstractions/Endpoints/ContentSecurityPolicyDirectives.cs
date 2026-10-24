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

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides the names of the Content-Security-Policy directives that the server's pages use. The list is not
/// exhaustive: any well-formed directive name is accepted.
/// </summary>
[PublicAPI]
public static class ContentSecurityPolicyDirectives
{
    /// <summary>
    /// The <c>default-src</c> directive, which is the fallback for every fetch directive.
    /// </summary>
    public const string DefaultSrc = "default-src";

    /// <summary>
    /// The <c>script-src</c> directive, which restricts the scripts a page may run.
    /// </summary>
    public const string ScriptSrc = "script-src";

    /// <summary>
    /// The <c>style-src</c> directive, which restricts the stylesheets and styles a page may apply.
    /// </summary>
    public const string StyleSrc = "style-src";

    /// <summary>
    /// The <c>img-src</c> directive, which restricts the images a page may load.
    /// </summary>
    public const string ImgSrc = "img-src";

    /// <summary>
    /// The <c>connect-src</c> directive, which restricts the URLs a page's script may fetch.
    /// </summary>
    public const string ConnectSrc = "connect-src";

    /// <summary>
    /// The <c>frame-src</c> directive, which restricts the documents a page may embed in frames.
    /// </summary>
    public const string FrameSrc = "frame-src";

    /// <summary>
    /// The <c>base-uri</c> directive, which restricts the URLs a page's <c>base</c> element may use.
    /// </summary>
    public const string BaseUri = "base-uri";

    /// <summary>
    /// The <c>form-action</c> directive, which restricts the URLs a page's forms may submit to.
    /// </summary>
    public const string FormAction = "form-action";

    /// <summary>
    /// The <c>frame-ancestors</c> directive, which restricts the documents that may embed the page in a frame.
    /// </summary>
    public const string FrameAncestors = "frame-ancestors";
}
