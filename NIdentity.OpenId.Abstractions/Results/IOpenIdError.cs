using NIdentity.OpenId.Messages;

namespace NIdentity.OpenId.Results;

/// <summary>
/// Contains the details for an <c>OAuth</c> or <c>OpenID Connect</c> error.
/// </summary>
public interface IOpenIdError : IOpenIdMessage
{
    /// <summary>
    /// Gets or sets the HTTP status code to be used when returning a response.
    /// </summary>
    int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="Exception"/> that triggered the <c>OAuth</c> or <c>OpenID Connect</c> error.
    /// May be <c>null</c> when protocol violations occur such as validations or other non-transient errors.
    /// </summary>
    Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the <c>error</c> parameter.
    /// </summary>
    string Code { get; set; }

    /// <summary>
    /// Gets or sets the <c>error_description</c> parameter.
    /// </summary>
    string? Description { get; set; }

    /// <summary>
    /// Gets or sets the <c>error_uri</c> parameter.
    /// </summary>
    Uri? Uri { get; set; }

    /// <summary>
    /// Gets or sets the <c>state</c> parameter.
    /// </summary>
    string? State { get; set; }
}
