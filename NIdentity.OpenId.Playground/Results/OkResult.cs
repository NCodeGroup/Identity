using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Playground.Results;
// https://github.com/dotnet/aspnetcore/blob/master/src/Mvc/Mvc.Core/src/OkResult.cs

/// <summary>
/// An <see cref="StatusCodeResult"/> that when executed will produce an empty
/// <see cref="Microsoft.AspNetCore.Http.StatusCodes.Status200OK"/> response.
/// </summary>
public class OkResult : StatusCodeResult
{
    private const int DefaultStatusCode = StatusCodes.Status200OK;

    /// <summary>
    /// Initializes a new instance of the <see cref="OkResult"/> class.
    /// </summary>
    public OkResult()
        : base(DefaultStatusCode)
    {
        // nothing
    }
}