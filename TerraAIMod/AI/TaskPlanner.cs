using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TerraAIMod.Config;
using TerraAIMod.NPCs;
using Terraria.ModLoader;

namespace TerraAIMod.AI
{
    /// <summary>
    /// Plans and orchestrates AI-driven tasks for Terra.
    /// Manages LLM client selection and response processing.
    /// </summary>
    public class TaskPlanner
    {
        private readonly ILLMClient _openaiClient;
        private readonly ILLMClient _groqClient;
        private readonly ILLMClient _geminiClient;

        /// <summary>
        /// Creates a new TaskPlanner with all available LLM clients initialized.
        /// </summary>
        public TaskPlanner()
        {
            _openaiClient = new OpenAIClient();
            _groqClient = new GroqClient();
            _geminiClient = new GeminiClient();
        }

        /// <summary>
        /// Plans tasks for a given command using the AI system.
        /// </summary>
        /// <param name="terra">The Terra NPC instance.</param>
        /// <param name="command">The player's command.</param>
        /// <returns>A ParsedResponse containing the planned tasks.</returns>
        public async Task<ParsedResponse> PlanTasksAsync(TerraNPC terra, string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return new ParsedResponse
                {
                    Success = false,
                    Error = "Empty command provided"
                };
            }

            // Build prompts
            string systemPrompt = PromptBuilder.BuildSystemPrompt();
            string userPrompt = PromptBuilder.BuildUserPrompt(terra, command);

            // Get the configured provider
            var config = ModContent.GetInstance<TerraConfig>();
            string provider = config?.AIProvider ?? "groq";

            string aiResponse = null;
            string primaryError = null;

            // Try primary provider
            try
            {
                aiResponse = await GetResponseFromProvider(provider, systemPrompt, userPrompt);
            }
            catch (Exception ex)
            {
                primaryError = ex.Message;
                TerraAIMod.Instance?.Logger.Warn($"Primary provider ({provider}) failed: {ex.Message}");

                // Fallback to Groq if primary wasn't already Groq
                if (provider != "groq")
                {
                    try
                    {
                        TerraAIMod.Instance?.Logger.Info("Attempting fallback to Groq...");
                        aiResponse = await _groqClient.SendRequestAsync(systemPrompt, userPrompt);
                    }
                    catch (Exception fallbackEx)
                    {
                        TerraAIMod.Instance?.Logger.Error($"Fallback to Groq also failed: {fallbackEx.Message}");
                    }
                }
            }

            // If we still don't have a response, return error
            if (string.IsNullOrEmpty(aiResponse))
            {
                return new ParsedResponse
                {
                    Success = false,
                    Error = $"AI request failed: {primaryError ?? "Unknown error"}"
                };
            }

            // Parse the response
            var parsedResponse = ResponseParser.ParseAIResponse(aiResponse);

            // Validate tasks
            if (parsedResponse.Success && parsedResponse.Tasks != null)
            {
                var validTasks = new List<Action.Task>();
                foreach (var task in parsedResponse.Tasks)
                {
                    var (isValid, error) = ValidateTask(task);
                    if (isValid)
                    {
                        validTasks.Add(task);
                    }
                    else
                    {
                        TerraAIMod.Instance?.Logger.Warn($"Invalid task '{task.Action}': {error}");
                    }
                }
                parsedResponse.Tasks = validTasks;

                // If all tasks were invalid, mark as partial success
                if (validTasks.Count == 0 && parsedResponse.Tasks.Count > 0)
                {
                    parsedResponse.Error = "All planned tasks were invalid";
                }
            }

            return parsedResponse;
        }

        /// <summary>
        /// Gets a response from the specified AI provider.
        /// </summary>
        /// <param name="provider">The provider name ("openai", "groq", or "gemini").</param>
        /// <param name="systemPrompt">The system prompt.</param>
        /// <param name="userPrompt">The user prompt.</param>
        /// <returns>The AI response string.</returns>
        private async Task<string> GetResponseFromProvider(string provider, string systemPrompt, string userPrompt)
        {
            ILLMClient client = provider.ToLowerInvariant() switch
            {
                "openai" => _openaiClient,
                "gemini" => _geminiClient,
                _ => _groqClient // Default to Groq
            };

            TerraAIMod.Instance?.Logger.Info($"Sending request to {client.ProviderName}...");
            return await client.SendRequestAsync(systemPrompt, userPrompt);
        }

        /// <summary>
        /// Validates a task based on its action type and required parameters.
        /// </summary>
        /// <param name="task">The task to validate.</param>
        /// <returns>A tuple of (isValid, errorMessage).</returns>
        public static (bool isValid, string error) ValidateTask(Action.Task task)
        {
            if (task == null)
            {
                return (false, "Task is null");
            }

            if (string.IsNullOrWhiteSpace(task.Action))
            {
                return (false, "Task action is empty");
            }

            // Validate based on action type
            switch (task.Action.ToLowerInvariant())
            {
                case "dig":
                    if (!task.HasParameter("direction"))
                    {
                        return (false, "dig action requires 'direction' parameter");
                    }
                    var digDirection = task.GetParameter<string>("direction", "").ToLowerInvariant();
                    if (digDirection != "down" && digDirection != "left" && digDirection != "right" && digDirection != "up")
                    {
                        return (false, "dig direction must be 'down', 'left', 'right', or 'up'");
                    }
                    break;

                case "mine":
                    if (!task.HasParameter("target"))
                    {
                        return (false, "mine action requires 'target' parameter");
                    }
                    break;

                case "place":
                    if (!task.HasParameter("tile"))
                    {
                        return (false, "place action requires 'tile' parameter");
                    }
                    break;

                case "build":
                    if (!task.HasParameter("structure"))
                    {
                        return (false, "build action requires 'structure' parameter");
                    }
                    var structure = task.GetParameter<string>("structure", "").ToLowerInvariant();
                    var validStructures = new HashSet<string> { "house", "tower", "arena", "hellevator", "bridge", "wall", "platform" };
                    if (!validStructures.Contains(structure))
                    {
                        return (false, $"Unknown structure type: {structure}");
                    }
                    break;

                case "attack":
                    // Attack can work with or without parameters (defaults to nearest enemy)
                    break;

                case "follow":
                    // Follow can work with or without parameters (defaults to nearest player)
                    break;

                case "boss":
                    if (!task.HasParameter("boss"))
                    {
                        return (false, "boss action requires 'boss' parameter");
                    }
                    break;

                case "explore":
                    // Explore can work with or without parameters
                    break;

                case "npchousing":
                    if (!task.HasParameter("action"))
                    {
                        return (false, "npcHousing action requires 'action' parameter");
                    }
                    var housingAction = task.GetParameter<string>("action", "").ToLowerInvariant();
                    if (housingAction != "build" && housingAction != "check" && housingAction != "assign")
                    {
                        return (false, "npcHousing action must be 'build', 'check', or 'assign'");
                    }
                    break;

                default:
                    return (false, $"Unknown action type: {task.Action}");
            }

            return (true, null);
        }
    }
}
