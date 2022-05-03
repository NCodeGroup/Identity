using Microsoft.Extensions.Primitives;

namespace NIdentity.OpenId.Messages;

/// <summary>
/// Represents the base interface for <c>OAuth</c> or <c>OpenId Connect</c> request messages
/// </summary>
public interface IOpenIdMessage : IReadOnlyDictionary<string, StringValues>
{
    /// <summary>
    /// Gets the <see cref="IOpenIdMessageContext"/> for the current instance.
    /// </summary>
    IOpenIdMessageContext Context { get; }
}