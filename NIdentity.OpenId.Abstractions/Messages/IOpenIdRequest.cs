using System;
using System.Collections.Generic;
using Microsoft.Extensions.Primitives;

namespace NIdentity.OpenId.Messages
{
    public interface IOpenIdRequest : IOpenIdMessage
    {
        GrantType GrantType { get; }

        ResponseMode DefaultResponseMode { get; }

        IEnumerable<StringSegment> Scope { get; set; }

        ResponseTypes ResponseType { get; set; }

        StringSegment ClientId { get; set; }

        StringSegment RedirectUri { get; set; }

        StringSegment State { get; set; }

        ResponseMode ResponseMode { get; set; }

        StringSegment Nonce { get; set; }

        DisplayType? Display { get; set; }

        PromptTypes? Prompt { get; set; }

        TimeSpan? MaxAge { get; set; }
    }
}
