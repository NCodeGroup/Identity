using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NIdentity.OpenId.Validation
{
    /// <summary>
    /// Represents the response payload when an error occurs.
    /// </summary>
    public interface IErrorDetails
    {
        public int? StatusCode { get; set; }

        /// <summary>
        /// Gets the <c>error</c> parameter.
        /// </summary>
        string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the <c>error_description</c> parameter.
        /// </summary>
        string ErrorDescription { get; set; }

        /// <summary>
        /// Gets or sets the <c>error_uri</c> parameter.
        /// </summary>
        string ErrorUri { get; set; }

        /// <summary>
        /// Gets or sets a dictionary that is used to handle JSON serialization (or
        /// deserialization) of additional properties that aren't declared.
        /// </summary>
        Dictionary<string, object> ExtensionData { get; set; }
    }

    /// <summary>
    /// Represents the response payload when an error occurs.
    /// </summary>
    public class ErrorDetails : IErrorDetails
    {
        /// <inheritdoc />
        [JsonIgnore]
        public int? StatusCode { get; set; }

        /// <inheritdoc />
        [JsonPropertyName("error")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ErrorCode { get; set; }

        /// <inheritdoc />
        [JsonPropertyName("error_description")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ErrorDescription { get; set; }

        /// <inheritdoc />
        [JsonPropertyName("error_uri")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string ErrorUri { get; set; }

        /// <inheritdoc />
        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; } = new();
    }
}
