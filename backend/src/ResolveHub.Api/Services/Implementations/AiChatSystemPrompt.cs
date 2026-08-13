using ResolveHub.Api.Constants;

namespace ResolveHub.Api.Services.Implementations;

internal static class AiChatSystemPrompt
{
    public static string Build(string? question) =>
        ResolveHubAssistantKnowledge.BuildSystemPrompt(question);
}
