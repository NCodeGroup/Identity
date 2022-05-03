namespace NIdentity.OpenId.DataContracts;

/// <summary>
/// Provides the ability to check for optimistic concurrency violations by using a random value that compared to
/// the existing value in a database. The random value is automatically generated every time a row is inserted or
/// updated in the database.
/// </summary>
public interface ISupportConcurrencyToken
{
    /// <summary>
    /// Gets or sets a random value that is used to check for optimistic concurrency violations.
    /// </summary>
    string ConcurrencyToken { get; set; }
}