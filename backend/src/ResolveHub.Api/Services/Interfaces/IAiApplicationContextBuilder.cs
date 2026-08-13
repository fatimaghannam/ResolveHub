namespace ResolveHub.Api.Services.Interfaces;

public interface IAiApplicationContextBuilder
{
    Task<string> BuildAsync(string role, string? pageContext, string? currentQuestion, CancellationToken token);
}
