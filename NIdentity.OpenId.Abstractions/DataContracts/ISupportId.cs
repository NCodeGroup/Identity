using System;

namespace NIdentity.OpenId.DataContracts
{
    public interface ISupportId : ISupportId<long>
    {
        // nothing
    }

    public interface ISupportId<out TKey>
        where TKey : IEquatable<TKey>
    {
        TKey Id { get; }
    }
}
