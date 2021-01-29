using Microsoft.AspNetCore.Http;

namespace NIdentity.OpenId.Playground.Results
{
    /// <summary>
    /// An <see cref="StatusCodeResult"/> that when executed will produce an empty
    /// <see cref="StatusCodes.Status404NotFound"/> response.
    /// </summary>
    public class NotFoundResult : StatusCodeResult
    {
        private const int DefaultStatusCode = StatusCodes.Status404NotFound;

        /// <summary>
        /// Initializes a new instance of the <see cref="NotFoundResult"/> class.
        /// </summary>
        public NotFoundResult()
            : base(DefaultStatusCode)
        {
            // nothing
        }
    }
}
