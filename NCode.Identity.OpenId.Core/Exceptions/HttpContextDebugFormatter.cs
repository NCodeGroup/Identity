#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.CodeDom.Compiler;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Exceptions;

// ReSharper disable All
#pragma warning disable CS1574 // XML comment has cref attribute that could not be resolved

/// <summary>
/// Copied from <see cref="Microsoft.AspNetCore.Shared.HttpContextDebugFormatter"/>.
/// Credits to the Microsoft team.
/// </summary>
[GeneratedCode("Microsoft.AspNetCore.Shared", null)]
internal static class HttpContextDebugFormatter
{
    public static string RequestToString(HttpRequest request)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(request.Method))
        {
            sb.Append(request.Method);
            sb.Append(' ');
        }

        GetRequestUrl(sb, request, includeQueryString: true);
        if (!string.IsNullOrEmpty(request.Protocol))
        {
            sb.Append(' ');
            sb.Append(request.Protocol);
        }

        if (!string.IsNullOrEmpty(request.ContentType))
        {
            sb.Append(' ');
            sb.Append(request.ContentType);
        }

        return sb.ToString();
    }

    private static void GetRequestUrl(StringBuilder sb, HttpRequest request, bool includeQueryString)
    {
        // The URL might be missing because the context was manually created in a test, e.g. new DefaultHttpContext()
        if (string.IsNullOrEmpty(request.Scheme) &&
            !request.Host.HasValue &&
            !request.PathBase.HasValue &&
            !request.Path.HasValue &&
            !request.QueryString.HasValue)
        {
            sb.Append("(unspecified)");
            return;
        }

        // If some parts of the URL are provided then default the significant parts to avoid a werid output.
        var scheme = request.Scheme;
        if (string.IsNullOrEmpty(scheme))
        {
            scheme = "(unspecified)";
        }

        var host = request.Host.Value;
        if (string.IsNullOrEmpty(host))
        {
            host = "(unspecified)";
        }

        sb.Append(CultureInfo.InvariantCulture, $"{scheme}://{host}{request.PathBase.Value}{request.Path.Value}{(includeQueryString ? request.QueryString.Value : string.Empty)}");
    }
}
