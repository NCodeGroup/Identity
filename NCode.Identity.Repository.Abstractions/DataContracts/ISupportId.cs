using System;

namespace NCode.Identity.Repository.DataContracts
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
