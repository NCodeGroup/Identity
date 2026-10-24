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

using System.Collections.Frozen;
using System.Collections.Immutable;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Provides the default implementation of <see cref="IRateLimitDescriptorCatalog"/> over the registered
/// <see cref="RateLimitDescriptor"/> data sources.
/// </summary>
/// <remarks>
/// <para>
/// The descriptors are static, so the constructor reads each data source once and does not watch its change token.
/// </para>
/// <para>
/// The constructor throws an <see cref="InvalidOperationException"/> that names the descriptor when the registered
/// descriptors are inconsistent: a name or a stage is empty or contains whitespace, two descriptors share a name, a
/// window is not positive, a selector is missing, a limit or overage key does not name a registered <c>int</c>
/// setting, a sliding window has an overage, an overage meter has no overage setting, or a usage meter is empty or
/// longer than <see cref="OpenIdMaxLengths.UsageMeter"/> characters.
/// </para>
/// </remarks>
// See ADR-0246.
internal sealed class DefaultRateLimitDescriptorCatalog : IRateLimitDescriptorCatalog
{
    private FrozenDictionary<
        string,
        ImmutableArray<RateLimitDescriptor>
    > DescriptorsByStage { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultRateLimitDescriptorCatalog"/> class.
    /// </summary>
    /// <param name="dataSources">The data sources that contribute the rate-limit descriptors.</param>
    /// <param name="settingDescriptorCollectionProvider">The provider of the registered setting descriptors.</param>
    public DefaultRateLimitDescriptorCatalog(
        IEnumerable<ICollectionDataSource<RateLimitDescriptor>> dataSources,
        ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
    )
    {
        var settingDescriptors = settingDescriptorCollectionProvider.Collection;
        var names = new HashSet<string>(StringComparer.Ordinal);
        var descriptorsByStage = new Dictionary<string, List<RateLimitDescriptor>>(
            StringComparer.Ordinal
        );

        foreach (var dataSource in dataSources)
        {
            foreach (var descriptor in dataSource.Collection)
            {
                Validate(descriptor, names, settingDescriptors);

                if (!descriptorsByStage.TryGetValue(descriptor.Stage, out var stageDescriptors))
                {
                    stageDescriptors = [];
                    descriptorsByStage.Add(descriptor.Stage, stageDescriptors);
                }

                stageDescriptors.Add(descriptor);
            }
        }

        DescriptorsByStage = descriptorsByStage.ToFrozenDictionary(
            static pair => pair.Key,
            static pair =>
                pair.Value.OrderBy(static descriptor => descriptor.Order)
                    .ThenBy(static descriptor => descriptor.Name, StringComparer.Ordinal)
                    .ToImmutableArray(),
            StringComparer.Ordinal
        );
    }

    /// <inheritdoc />
    public ImmutableArray<RateLimitDescriptor> GetStage(string stage) =>
        DescriptorsByStage.TryGetValue(stage, out var descriptors) ? descriptors : [];

    private static void Validate(
        RateLimitDescriptor descriptor,
        HashSet<string> names,
        ISettingDescriptorCollection settingDescriptors
    )
    {
        var name = descriptor.Name;
        if (string.IsNullOrEmpty(name) || name.Any(char.IsWhiteSpace))
            throw new InvalidOperationException(
                "A rate-limit descriptor must have a name that is not empty and contains no whitespace."
            );

        var stage = descriptor.Stage;
        if (string.IsNullOrEmpty(stage) || stage.Any(char.IsWhiteSpace))
            throw new InvalidOperationException(
                $"The rate limit '{name}' must have a stage that is not empty and contains no whitespace."
            );

        if (!names.Add(name))
            throw new InvalidOperationException($"More than one rate limit is named '{name}'.");

        if (descriptor.Window <= TimeSpan.Zero)
            throw new InvalidOperationException(
                $"The rate limit '{name}' must have a window longer than zero."
            );

        if (descriptor.PartitionValueSelector is null)
            throw new InvalidOperationException(
                $"The rate limit '{name}' must have a partition value selector."
            );

        if (!settingDescriptors.TryGet(descriptor.LimitKey, out _))
            throw new InvalidOperationException(
                $"The rate limit '{name}' names the limit setting '{descriptor.LimitKey.SettingName}', which is not a registered int setting."
            );

        if (
            descriptor.OverageKey is { } overageKey
            && !settingDescriptors.TryGet(overageKey, out _)
        )
            throw new InvalidOperationException(
                $"The rate limit '{name}' names the overage setting '{overageKey.SettingName}', which is not a registered int setting."
            );

        if (descriptor.IsSliding && descriptor.OverageKey is not null)
            throw new InvalidOperationException(
                $"The rate limit '{name}' has a sliding window, so it cannot have an overage."
            );

        if (descriptor.OverageUsageMeter is not null && descriptor.OverageKey is null)
            throw new InvalidOperationException(
                $"The rate limit '{name}' has an overage usage meter but no overage setting."
            );

        ValidateUsageMeter(name, nameof(RateLimitDescriptor.UsageMeter), descriptor.UsageMeter);
        ValidateUsageMeter(
            name,
            nameof(RateLimitDescriptor.OverageUsageMeter),
            descriptor.OverageUsageMeter
        );
    }

    private static void ValidateUsageMeter(string name, string property, string? meter)
    {
        if (meter is null)
            return;

        if (meter.Length is 0 or > OpenIdMaxLengths.UsageMeter)
            throw new InvalidOperationException(
                $"The {property} of the rate limit '{name}' must have between 1 and {OpenIdMaxLengths.UsageMeter} characters."
            );
    }
}
