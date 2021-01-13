namespace NCode.Identity.Repository.DataContracts
{
    public interface ISupportConcurrencyToken
    {
        /// <summary>
        /// A random value that must change whenever an entity is persisted to the store
        /// </summary>
        string ConcurrencyToken { get; set; }
    }
}
