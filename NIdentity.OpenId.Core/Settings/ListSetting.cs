using System.Text.Json;

namespace NIdentity.OpenId.Settings;

/// <summary>
/// Provides an implementation of the <see cref="Setting{TValue}"/> abstraction for a collection of <typeparamref name="TItem"/> values.
/// </summary>
/// <typeparam name="TItem">The type of value in the collection.</typeparam>
public class ListSetting<TItem> : Setting<IReadOnlyCollection<TItem>>
    where TItem : notnull
{
    /// <inheritdoc />
    public override SettingDescriptor Descriptor { get; }

    /// <inheritdoc />
    public override IReadOnlyCollection<TItem> Value { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ListSetting{TItem}"/> class.
    /// </summary>
    /// <param name="descriptor">The <see cref="SettingDescriptor"/> instance.</param>
    /// <param name="collection">The value for this setting.</param>
    public ListSetting(SettingDescriptor descriptor, IEnumerable<TItem> collection)
    {
        if (descriptor.ValueType != typeof(IReadOnlyCollection<TItem>))
            throw new ArgumentException("Invalid ValueType.", nameof(descriptor));

        Descriptor = descriptor;
        Value = collection.ToList();
    }

    /// <inheritdoc />
    public override Setting<IReadOnlyCollection<TItem>> Merge(Setting<IReadOnlyCollection<TItem>> other, SettingMergeOptions options = default)
    {
        var behavior = options.Behavior ?? Descriptor.DefaultMergeBehavior;
        var newCollection = behavior switch
        {
            SettingMergeBehaviors.List.Intersect => Value.Intersect(other.Value),
            SettingMergeBehaviors.List.Union => Value.Union(other.Value),
            SettingMergeBehaviors.List.Except => Value.Except(other.Value),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        return new ListSetting<TItem>(Descriptor, newCollection);
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, JsonSerializerOptions options)
    {
        writer.WriteStartArray(Descriptor.SettingName);

        foreach (var item in Value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }
}
