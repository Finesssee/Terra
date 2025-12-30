using System.Threading.Tasks;

namespace TerraAIMod.AI
{
    /// <summary>
    /// Interface for LLM (Large Language Model) clients.
    /// Provides a common abstraction for different AI providers.
    /// </summary>
    public interface ILLMClient
    {
        /// <summary>
        /// Sends a request to the LLM with the given prompts.
        /// </summary>
        /// <param name="systemPrompt">The system prompt that sets the AI's behavior and context.</param>
        /// <param name="userPrompt">The user's input message.</param>
        /// <returns>The AI's response text.</returns>
        Task<string> SendRequestAsync(string systemPrompt, string userPrompt);

        /// <summary>
        /// Gets the name of the AI provider (e.g., "OpenAI", "Groq", "Gemini").
        /// </summary>
        string ProviderName { get; }
    }
}
