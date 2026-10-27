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


namespace NCode.Identity.OpenId.Client.Caching;

/// <summary>
/// The timing rules of a <see cref="RefreshingCache{TValue}"/> lookup.
/// </summary>
/// <param name="RefreshInterval">How long a fetched value is fresh.</param>
/// <param name="MinimumRefreshInterval">The minimum time between two fetch attempts for the same key.</param>
/// <param name="StaleLifetime">How long a value may still be served when a refresh fails.</param>
internal readonly record struct RefreshingCachePolicy(
    TimeSpan RefreshInterval,
    TimeSpan MinimumRefreshInterval,
    TimeSpan StaleLifetime
);
