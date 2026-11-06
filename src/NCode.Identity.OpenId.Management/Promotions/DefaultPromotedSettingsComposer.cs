// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Promotions;

/// <summary>
/// Provides a default implementation of <see cref="IPromotedSettingsComposer"/> that decides which settings are
/// environment-bound from the registered <see cref="SettingDescriptor"/> collection.
/// </summary>
// See ADR-1425.
internal class DefaultPromotedSettingsComposer(
    ISettingDescriptorCollectionProvider settingDescriptorCollectionProvider
) : IPromotedSettingsComposer
{
    private ISettingDescriptorCollectionProvider SettingDescriptorCollectionProvider { get; } =
        settingDescriptorCollectionProvider;

    /// <inheritdoc />
    public PromotedSettings Compose(
        JsonElement sourceSettings,
        JsonElement? targetSettings,
        JsonElement? environmentSettings
    )
    {
        var descriptors = SettingDescriptorCollectionProvider.Collection;
        var result = new JsonObject();
        var boundSourceNames = new List<string>();

        foreach (var member in EnumerateMembers(sourceSettings))
        {
            if (IsEnvironmentBound(descriptors, member.Name))
            {
                boundSourceNames.Add(member.Name);
                continue;
            }

            result[member.Name] = JsonNode.Parse(member.Value.GetRawText());
        }

        if (targetSettings.HasValue)
        {
            foreach (var member in EnumerateMembers(targetSettings.Value))
            {
                if (IsEnvironmentBound(descriptors, member.Name))
                {
                    result[member.Name] = JsonNode.Parse(member.Value.GetRawText());
                }
            }
        }

        var refusedSettingNames = new List<string>();
        if (environmentSettings.HasValue)
        {
            foreach (var member in EnumerateMembers(environmentSettings.Value))
            {
                if (IsEnvironmentBound(descriptors, member.Name))
                {
                    result[member.Name] = JsonNode.Parse(member.Value.GetRawText());
                }
                else
                {
                    refusedSettingNames.Add(member.Name);
                }
            }
        }

        var unsetSettingNames = new List<string>();
        foreach (var name in boundSourceNames)
        {
            if (!result.ContainsKey(name))
            {
                unsetSettingNames.Add(name);
            }
        }

        return new PromotedSettings
        {
            Document = JsonSerializer.SerializeToElement(result),
            UnsetSettingNames = unsetSettingNames,
            RefusedSettingNames = refusedSettingNames,
        };
    }

    private static bool IsEnvironmentBound(ISettingDescriptorCollection descriptors, string name) =>
        descriptors.TryGet(name, out var descriptor) && descriptor.IsEnvironmentBound;

    private static List<JsonProperty> EnumerateMembers(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? [.. element.EnumerateObject()] : [];
}
