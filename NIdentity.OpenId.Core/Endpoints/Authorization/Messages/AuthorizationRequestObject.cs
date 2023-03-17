namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

internal class AuthorizationRequestObject : BaseAuthorizationRequestMessage<AuthorizationRequestObject>, IAuthorizationRequestObject
{
    public AuthorizationSource AuthorizationSource => AuthorizationSource.Jar;

    public RequestObjectSource RequestObjectSource { get; set; }
}
