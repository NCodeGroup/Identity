using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using NCode.Identity.Repository.DataContracts;

namespace NCode.Identity.Flows.Authorization
{
    public class AuthorizationContext
    {
        public HttpContext HttpContext { get; set; }

        public AuthorizationRequest Request { get; set; }

        public Client Client { get; set; }

        public IDictionary<object, object> Items { get; set; }
    }
}
