namespace NIdentity.OpenId.Messages.Authorization;

internal class AuthorizationRequestObject : BaseAuthorizationRequestMessage<AuthorizationRequestObject>, IAuthorizationRequestObject
{
    public RequestObjectSource Source { get; set; }
}