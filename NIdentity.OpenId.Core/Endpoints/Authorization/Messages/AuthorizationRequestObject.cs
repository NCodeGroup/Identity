namespace NIdentity.OpenId.Endpoints.Authorization.Messages;

internal class AuthorizationRequestObject : BaseAuthorizationRequestMessage<AuthorizationRequestObject>, IAuthorizationRequestObject
{
    public AuthorizationSourceType AuthorizationSourceType => AuthorizationSourceType.Jar;

    public RequestObjectSource RequestObjectSource { get; set; }
}
