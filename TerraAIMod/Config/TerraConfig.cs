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

        [DefaultValue("groq")]
        [OptionStrings(new string[] { "groq", "openai", "gemini" })]
        public string AIProvider { get; set; } = "groq";

        [DefaultValue("")]
        public string OpenAIApiKey { get; set; } = "";

        [DefaultValue("")]
        public string GroqApiKey { get; set; } = "";

        [DefaultValue("")]
        public string GeminiApiKey { get; set; } = "";

        // ==================== Model Settings ====================

        [Header("ModelSettings")]

        [DefaultValue("gpt-4-turbo-preview")]
        public string OpenAIModel { get; set; } = "gpt-4-turbo-preview";

        [Range(100, 65536)]
        [DefaultValue(8000)]
        public int MaxTokens { get; set; } = 8000;

        [Range(0f, 2f)]
        [DefaultValue(0.7f)]
        public float Temperature { get; set; } = 0.7f;

        // ==================== Behavior Settings ====================

        [Header("BehaviorSettings")]

        [Range(1, 120)]
        [DefaultValue(30)]
        public int ActionTickDelay { get; set; } = 30;

        [DefaultValue(true)]
        public bool EnableChatResponses { get; set; } = true;

        [Range(1, 20)]
        [DefaultValue(5)]
        public int MaxActiveTerras { get; set; } = 5;
    }
}
