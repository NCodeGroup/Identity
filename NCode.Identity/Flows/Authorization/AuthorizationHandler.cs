using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Contracts;
using NCode.Identity.Results;
using static NCode.Identity.Results.HttpResultFactory;

// https://tools.ietf.org/html/rfc6749 (The OAuth 2.0 Authorization Framework)
// https://openid.net/specs/openid-connect-core-1_0.html
// https://identityserver4.readthedocs.io/en/latest/endpoints/authorize.html
// https://auth0.com/docs/flows

namespace NCode.Identity.Flows.Authorization
{
    public enum ResponseMode
    {
        Unknown = 0,
        Query,
        Fragment,
        FormPost
    }

    public enum DisplayType
    {
        Unknown = 0,
        Page,
        Popup,
        Touch,
        Wap
    }

    [Flags]
    public enum PromptTypes
    {
        Unknown = 0,
        None = 1,
        Login = 2,
        Consent = 4,
        SelectAccount = 8
    }

    public enum CodeChallengeMethod
    {
        Unknown = 0,
        Plain,
        S256
    }

    public class AuthorizationHandler : IEndpointHandler
    {
        private readonly IEnumerable<IAuthorizationStageHandler> _stageHandlers;

        public AuthorizationHandler(HttpContext httpContext, IEnumerable<IAuthorizationStageHandler> stageHandlers)
        {
            HttpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));

            _stageHandlers = stageHandlers;
        }

        public HttpContext HttpContext { get; }

        public async ValueTask<IHttpResult> HandleAsync(CancellationToken cancellationToken)
        {
            var context = new AuthorizationContext
            {
                HttpContext = HttpContext,
                Request = new AuthorizationRequest(),
                Items = new Dictionary<object, object>()
            };

            var stageGroups = _stageHandlers
                .GroupBy(handler => handler.Stage)
                .OrderBy(grouping => (int)grouping.Key);

            foreach (var stageGroup in stageGroups)
            {
                var stage = stageGroup.Key;
                var handlers = stageGroup.OrderBy(handler => handler.Stage);

                foreach (var handler in handlers)
                {
                    var result = await handler.HandleAsync(context, cancellationToken);
                    if (result.HasError)
                    {
                        /*
						 * state
						 * REQUIRED if a "state" parameter was present in the client
						 * authorization request.  The exact value received from the
						 * client.
						 */
                        const string stateKey = IdentityConstants.Parameters.State;
                        if (context.Request.RawValues.TryGetValue(stateKey, out var stateStringValues))
                        {
                            result.ErrorDetails.ExtensionData[stateKey] = stateStringValues[0];
                        }

                        return BadRequest(result.ErrorDetails);
                    }
                }
            }

            // TODO: validate PKCE

            return Ok();
        }

    }
}
