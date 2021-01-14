using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NCode.Identity.Validation
{
    /// <summary>
    /// Represents the response payload when an error occurs.
    /// </summary>
    public interface IErrorDetails
    {
        public int? StatusCode { get; set; }

        /// <summary>
        /// Gets the <c>ErrorCode</c> property.
        /// </summary>
        string ErrorCode { get; set; }

        /// <summary>
        /// Gets or sets the <c>ErrorDescription</c> property.
        /// </summary>
        string ErrorDescription { get; set; }

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
        [JsonExtensionData]
        public Dictionary<string, object> ExtensionData { get; set; } = new();
    }
}
