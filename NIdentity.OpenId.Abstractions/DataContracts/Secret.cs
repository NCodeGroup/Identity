namespace NIdentity.OpenId.DataContracts
{
    /// <summary>
    /// Contains the configuration for a cryptographic secret.
    /// </summary>
    public class Secret : ISupportId
    {
        /// <summary>
        /// Gets or sets the surrogate key for this entity.
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Gets or sets the natural key for this entity.
        /// </summary>
        public string KeyId { get; set; } = null!;

        /// <summary>
        /// Gets or sets the value of <see cref="KeyId"/> in uppercase so that lookups can be sargable for DBMS
        /// engines that don't support case-insensitive indices.
        /// </summary>
        public string NormalizedKeyId { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value that specifies how <see cref="Value"/> is encoded to/from a string. Examples values are:
        /// <list type="bullet">
        ///     <item>
        ///         <term>base64</term>
        ///     </item>
        ///     <item>
        ///         <term>pem</term>
        ///     </item>
        /// </list>
        /// </summary>
        public string Encoding { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value that specifies the cryptographic algorithm used by the secret. Examples values are:
        /// <list type="bullet">
        ///     <item>
        ///         <term>aes</term>
        ///     </item>
        ///     <item>
        ///         <term>rsa</term>
        ///     </item>
        ///     <item>
        ///         <term>dsa</term>
        ///     </item>
        ///     <item>
        ///         <term>ecdsa</term>
        ///     </item>
        ///     <item>
        ///         <term>ecdh</term>
        ///     </item>
        /// </list>
        /// </summary>
        public string Algorithm { get; set; } = null!;

        /// <summary>
        /// Gets or sets a value that specifies the type of secret. Examples values are:
        /// <list type="bullet">
        ///     <item>
        ///         <term>shared_secret</term>
        ///     </item>
        ///     <item>
        ///         <term>symmetric_key</term>
        ///     </item>
        ///     <item>
        ///         <term>asymmetric_key</term>
        ///     </item>
        ///     <item>
        ///         <term>certificate</term>
        ///     </item>
        /// </list>
        /// </summary>
        public string Type { get; set; } = null!;

        /// <summary>
        /// Gets or sets the encoded value of the secret.
        /// </summary>
        public string Value { get; set; } = null!;
    }
}
