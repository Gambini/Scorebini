namespace ScorebiniTwitchApi
{
    public record class AuthorizeRedirectResponse(
        ResponseCommonMetadata Meta,
        string RedirectUri
        );
}
