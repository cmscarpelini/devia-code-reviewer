namespace DevIa.Infrastructure.Ai;

/// <summary>LLM provider settings (bound from the "Llm" config section). Provider-agnostic.</summary>
public sealed class LlmOptions
{
    /// <summary>OpenAI | AzureOpenAI | GitHubModels | Ollama.</summary>
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "gpt-4o-mini";
    public string ApiKey { get; set; } = "";
    public string? Endpoint { get; set; }
}
