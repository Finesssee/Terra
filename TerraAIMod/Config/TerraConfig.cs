using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TerraAIMod.Config
{
    /// <summary>
    /// Configuration settings for Terra AI Mod.
    /// Contains API keys, model settings, and behavior options.
    /// </summary>
    public class TerraConfig : ModConfig
    {
        /// <summary>
        /// ConfigScope is ClientSide to keep API keys private per-player.
        /// </summary>
        public override ConfigScope Mode => ConfigScope.ClientSide;

        // ==================== AI Provider Settings ====================

        [Header("AIProviderSettings")]

        [Label("AI Provider")]
        [Tooltip("Select which AI provider to use: 'groq', 'openai', or 'gemini'")]
        [DefaultValue("groq")]
        [OptionStrings(new string[] { "groq", "openai", "gemini" })]
        public string AIProvider { get; set; } = "groq";

        [Label("OpenAI API Key")]
        [Tooltip("Your OpenAI API key for GPT models. Keep this secret!")]
        [DefaultValue("")]
        public string OpenAIApiKey { get; set; } = "";

        [Label("Groq API Key")]
        [Tooltip("Your Groq API key for fast inference. Keep this secret!")]
        [DefaultValue("")]
        public string GroqApiKey { get; set; } = "";

        [Label("Gemini API Key")]
        [Tooltip("Your Google Gemini API key. Keep this secret!")]
        [DefaultValue("")]
        public string GeminiApiKey { get; set; } = "";

        // ==================== Model Settings ====================

        [Header("ModelSettings")]

        [Label("OpenAI Model")]
        [Tooltip("The OpenAI model to use (e.g., 'gpt-4-turbo-preview', 'gpt-4', 'gpt-3.5-turbo')")]
        [DefaultValue("gpt-4-turbo-preview")]
        public string OpenAIModel { get; set; } = "gpt-4-turbo-preview";

        [Label("Max Tokens")]
        [Tooltip("Maximum number of tokens for AI responses (100-65536)")]
        [Range(100, 65536)]
        [DefaultValue(8000)]
        public int MaxTokens { get; set; } = 8000;

        [Label("Temperature")]
        [Tooltip("AI creativity/randomness level (0 = deterministic, 2 = very creative)")]
        [Range(0f, 2f)]
        [DefaultValue(0.7f)]
        public float Temperature { get; set; } = 0.7f;

        // ==================== Behavior Settings ====================

        [Header("BehaviorSettings")]

        [Label("Action Tick Delay")]
        [Tooltip("Number of game ticks between Terra AI actions (1-120). Lower = more frequent actions.")]
        [Range(1, 120)]
        [DefaultValue(30)]
        public int ActionTickDelay { get; set; } = 30;

        [Label("Enable Chat Responses")]
        [Tooltip("Whether Terra AI should respond to chat messages")]
        [DefaultValue(true)]
        public bool EnableChatResponses { get; set; } = true;

        [Label("Max Active Terras")]
        [Tooltip("Maximum number of Terra AI instances that can be active at once (1-20)")]
        [Range(1, 20)]
        [DefaultValue(5)]
        public int MaxActiveTerras { get; set; } = 5;
    }
}
