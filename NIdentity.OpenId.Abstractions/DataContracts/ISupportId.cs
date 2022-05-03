using System;

namespace NIdentity.OpenId.DataContracts;

/// <summary>
/// Provides the ability to return the surrogate key for an entity where the key type is <see cref="long"/>.
/// </summary>
public interface ISupportId : ISupportId<long>
{
    // nothing
}

/// <summary>
/// Provides the ability to return the surrogate key for an entity.
/// </summary>
/// <typeparam name="TKey">The type of the surrogate key.</typeparam>
public interface ISupportId<out TKey>
    where TKey : IEquatable<TKey>
{
    /// <summary>
    /// Gets the surrogate key.
    /// </summary>
    TKey Id { get; }
}