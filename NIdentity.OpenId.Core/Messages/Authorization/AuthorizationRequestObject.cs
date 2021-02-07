using System;
using System.Collections.Generic;
using NIdentity.OpenId.Messages.Parameters;

namespace NIdentity.OpenId.Messages.Authorization
{
    internal class AuthorizationRequestObject : BaseAuthorizationRequestMessage, IAuthorizationRequestObject
    {
        public RequestObjectSource Source { get; set; }
    }
}
