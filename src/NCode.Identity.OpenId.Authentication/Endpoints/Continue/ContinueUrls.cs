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

using Microsoft.AspNetCore.WebUtilities;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides the reading of the continue URLs that park an operation until the user-agent returns.
/// </summary>
internal static class ContinueUrls
{
    /// <summary>
    /// Gets the opaque <c>state</c> key that a continue URL carries, or <see langword="null"/> when the URL has no
    /// query or its query has no <c>state</c>.
    /// </summary>
    /// <param name="continueUrl">The absolute or local continue URL.</param>
    /// <returns>The <c>state</c> key, or <see langword="null"/> when there is none.</returns>
    // The parked operation is addressed by the continue endpoint's opaque 'state' key, carried inside the continue URL;
    // the framework query parser decodes it.
    public static string? GetStateOrDefault(string continueUrl)
    {
        var separatorIndex = continueUrl.IndexOf('?', StringComparison.Ordinal);
        if (separatorIndex < 0)
            return null;

        var query = QueryHelpers.ParseQuery(continueUrl[separatorIndex..]);
        return query.TryGetValue(OpenIdConstants.Parameters.State, out var values)
            ? values.ToString()
            : null;
    }
}
