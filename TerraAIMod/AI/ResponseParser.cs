using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using TerraAIMod.Action;

namespace TerraAIMod.AI
{
    /// <summary>
    /// Represents a parsed AI response containing reasoning, plan, and tasks.
    /// </summary>
    public class ParsedResponse
    {
        /// <summary>
        /// The AI's reasoning about how to approach the task.
        /// </summary>
        public string Reasoning { get; set; } = string.Empty;

        /// <summary>
        /// The high-level plan description.
        /// </summary>
        public string Plan { get; set; } = string.Empty;

        /// <summary>
        /// The list of tasks to execute.
        /// </summary>
        public List<Task> Tasks { get; set; } = new List<Task>();

        /// <summary>
        /// Whether the response was successfully parsed.
        /// </summary>
        public bool Success { get; set; } = false;

        /// <summary>
        /// Error message if parsing failed.
        /// </summary>
        public string Error { get; set; } = string.Empty;
    }

    /// <summary>
    /// Parses AI responses into structured task data.
    /// Handles JSON extraction, cleaning, and error correction.
    /// </summary>
    public static class ResponseParser
    {
        /// <summary>
        /// Parses an AI response string into a structured ParsedResponse.
        /// </summary>
        /// <param name="response">The raw AI response text.</param>
        /// <returns>A ParsedResponse containing reasoning, plan, and tasks.</returns>
        public static ParsedResponse ParseAIResponse(string response)
        {
            var result = new ParsedResponse();

            if (string.IsNullOrWhiteSpace(response))
            {
                result.Error = "Empty response received from AI";
                return result;
            }

            try
            {
                // Clean the response (remove markdown code blocks)
                string cleanedResponse = CleanResponse(response);

                // Fix common JSON errors
                cleanedResponse = FixCommonJsonErrors(cleanedResponse);

                // Parse JSON
                using var document = JsonDocument.Parse(cleanedResponse);
                var root = document.RootElement;

                // Extract reasoning
                if (root.TryGetProperty("reasoning", out var reasoning))
                {
                    result.Reasoning = reasoning.GetString() ?? string.Empty;
                }

                // Extract plan
                if (root.TryGetProperty("plan", out var plan))
                {
                    result.Plan = plan.GetString() ?? string.Empty;
                }

                // Extract tasks array
                if (root.TryGetProperty("tasks", out var tasks))
                {
                    result.Tasks = ExtractTasks(tasks);
                }

                result.Success = true;
            }
            catch (JsonException ex)
            {
                result.Error = $"JSON parsing error: {ex.Message}";
                TerraAIMod.Instance?.Logger.Warn($"ResponseParser JSON error: {ex.Message}");

                // Try to extract any useful information from malformed response
                TryExtractFromMalformedResponse(response, result);
            }
            catch (Exception ex)
            {
                result.Error = $"Parsing error: {ex.Message}";
                TerraAIMod.Instance?.Logger.Error($"ResponseParser error: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Cleans the AI response by removing markdown code blocks and extra whitespace.
        /// </summary>
        /// <param name="response">The raw response text.</param>
        /// <returns>Cleaned response text.</returns>
        private static string CleanResponse(string response)
        {
            string cleaned = response.Trim();

            // Remove markdown code blocks (```json ... ``` or ``` ... ```)
            var codeBlockRegex = new Regex(@"```(?:json)?\s*([\s\S]*?)\s*```", RegexOptions.IgnoreCase);
            var match = codeBlockRegex.Match(cleaned);
            if (match.Success)
            {
                cleaned = match.Groups[1].Value.Trim();
            }

            // Remove any leading/trailing text before/after JSON object
            int jsonStart = cleaned.IndexOf('{');
            int jsonEnd = cleaned.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                cleaned = cleaned.Substring(jsonStart, jsonEnd - jsonStart + 1);
            }

            return cleaned;
        }

        /// <summary>
        /// Fixes common JSON formatting errors that LLMs tend to make.
        /// </summary>
        /// <param name="json">The JSON string to fix.</param>
        /// <returns>Fixed JSON string.</returns>
        private static string FixCommonJsonErrors(string json)
        {
            // Fix missing commas between array elements (e.g., "} {" -> "}, {")
            json = Regex.Replace(json, @"}\s*{", "}, {");

            // Fix missing commas after string values followed by property names
            json = Regex.Replace(json, @"""(\s*)\n(\s*)""", "\",\n\"");

            // Fix trailing commas in arrays (e.g., "[item,]" -> "[item]")
            json = Regex.Replace(json, @",(\s*)\]", "$1]");

            // Fix trailing commas in objects (e.g., "{prop: val,}" -> "{prop: val}")
            json = Regex.Replace(json, @",(\s*)}", "$1}");

            // Fix single quotes to double quotes
            // Only fix quotes around property names and string values, not within strings
            json = FixSingleQuotes(json);

            return json;
        }

        /// <summary>
        /// Converts single quotes to double quotes in JSON while preserving quotes within strings.
        /// </summary>
        private static string FixSingleQuotes(string json)
        {
            var result = new System.Text.StringBuilder();
            bool inDoubleQuoteString = false;
            bool inSingleQuoteString = false;
            char prevChar = '\0';

            foreach (char c in json)
            {
                if (c == '"' && !inSingleQuoteString && prevChar != '\\')
                {
                    inDoubleQuoteString = !inDoubleQuoteString;
                    result.Append(c);
                }
                else if (c == '\'' && !inDoubleQuoteString && prevChar != '\\')
                {
                    inSingleQuoteString = !inSingleQuoteString;
                    result.Append('"'); // Convert single quote to double quote
                }
                else
                {
                    result.Append(c);
                }

                prevChar = c;
            }

            return result.ToString();
        }

        /// <summary>
        /// Extracts tasks from a JsonElement array.
        /// </summary>
        /// <param name="tasksElement">The JSON element containing the tasks array.</param>
        /// <returns>A list of Task objects.</returns>
        private static List<Task> ExtractTasks(JsonElement tasksElement)
        {
            var tasks = new List<Task>();

            if (tasksElement.ValueKind != JsonValueKind.Array)
            {
                return tasks;
            }

            foreach (var taskElement in tasksElement.EnumerateArray())
            {
                try
                {
                    var task = ParseTask(taskElement);
                    if (task != null)
                    {
                        tasks.Add(task);
                    }
                }
                catch (Exception ex)
                {
                    TerraAIMod.Instance?.Logger.Warn($"Failed to parse task: {ex.Message}");
                }
            }

            return tasks;
        }

        /// <summary>
        /// Parses a single task from a JsonElement.
        /// </summary>
        /// <param name="taskElement">The JSON element representing a task.</param>
        /// <returns>A Task object, or null if parsing fails.</returns>
        private static Task ParseTask(JsonElement taskElement)
        {
            if (taskElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            // Extract action name
            string action = null;
            if (taskElement.TryGetProperty("action", out var actionElement))
            {
                action = actionElement.GetString();
            }

            if (string.IsNullOrEmpty(action))
            {
                return null;
            }

            // Normalize the action name
            action = NormalizeActionName(action);

            // Extract parameters
            var parameters = new Dictionary<string, object>();

            if (taskElement.TryGetProperty("parameters", out var paramsElement) &&
                paramsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in paramsElement.EnumerateObject())
                {
                    // Normalize parameter names to lowercase
                    string normalizedKey = prop.Name.ToLowerInvariant();
                    parameters[normalizedKey] = ConvertJsonValue(prop.Value);
                }
            }

            // Also check for direct properties that might be parameters
            foreach (var prop in taskElement.EnumerateObject())
            {
                if (prop.Name != "action" && prop.Name != "parameters")
                {
                    string normalizedKey = prop.Name.ToLowerInvariant();
                    parameters[normalizedKey] = ConvertJsonValue(prop.Value);
                }
            }

            // Normalize parameter values for certain keys
            NormalizeParameters(action, parameters);

            return new Task(action, parameters);
        }

        /// <summary>
        /// Normalizes action names to match the expected format in ActionExecutor.
        /// Handles common variations and aliases.
        /// </summary>
        /// <param name="action">The raw action name from AI response.</param>
        /// <returns>The normalized action name.</returns>
        private static string NormalizeActionName(string action)
        {
            if (string.IsNullOrEmpty(action))
                return action;

            string lower = action.ToLowerInvariant().Trim();

            // Map common variations to expected action names
            return lower switch
            {
                // Mining/Digging
                "dig" or "digdown" or "dig_down" or "excavate" => "dig",
                "mine" or "mining" or "mine_ore" or "mineore" => "mine",

                // Building
                "build" or "building" or "construct" or "create" => "build",
                "place" or "placeblock" or "place_block" or "placetile" or "place_tile" => "place",

                // Combat
                "attack" or "fight" or "combat" or "kill" or "damage" => "attack",
                "boss" or "bossfight" or "boss_fight" or "fightboss" or "fight_boss" => "boss",

                // Movement
                "follow" or "following" or "followplayer" or "follow_player" => "follow",
                "pathfind" or "goto" or "go_to" or "moveto" or "move_to" or "navigate" => "pathfind",
                "explore" or "exploration" or "scout" => "explore",

                // NPC Housing
                "npchousing" or "npc_housing" or "housing" or "buildhouse" or "build_house" => "npcHousing",

                // Communication
                "say" or "speak" or "talk" or "respond" or "reply" or "chat" or "message" => "say",

                _ => lower
            };
        }

        /// <summary>
        /// Normalizes parameter values based on the action type.
        /// Ensures consistent formatting for parameter values.
        /// </summary>
        /// <param name="action">The normalized action name.</param>
        /// <param name="parameters">The parameters dictionary to normalize.</param>
        private static void NormalizeParameters(string action, Dictionary<string, object> parameters)
        {
            // Normalize direction parameters
            if (parameters.TryGetValue("direction", out var direction) && direction is string dirStr)
            {
                parameters["direction"] = dirStr.ToLowerInvariant().Trim() switch
                {
                    "up" or "upward" or "upwards" => "up",
                    "down" or "downward" or "downwards" => "down",
                    "left" or "west" => "left",
                    "right" or "east" => "right",
                    _ => dirStr.ToLowerInvariant()
                };
            }

            // Normalize target parameters for follow action
            if (action == "follow" && parameters.TryGetValue("player", out var player) && player is string playerStr)
            {
                // Also store as "target" for compatibility
                parameters["target"] = playerStr.ToLowerInvariant().Trim() switch
                {
                    "nearest" or "closest" or "near" => "nearest",
                    _ => playerStr
                };
            }

            // Normalize target parameters for attack action
            if (action == "attack" && parameters.TryGetValue("target", out var target) && target is string targetStr)
            {
                parameters["target"] = targetStr.ToLowerInvariant().Trim() switch
                {
                    "nearest" or "closest" or "near" => "nearest",
                    "strongest" or "biggest" or "powerful" => "strongest",
                    _ => targetStr
                };
            }

            // Normalize structure types for build action
            if (action == "build" && parameters.TryGetValue("structure", out var structure) && structure is string structStr)
            {
                parameters["structure"] = structStr.ToLowerInvariant().Trim() switch
                {
                    "house" or "home" or "npchouse" or "npc_house" => "house",
                    "tower" or "watchtower" or "guard_tower" => "tower",
                    "arena" or "bossfight" or "boss_arena" or "fighting_arena" => "arena",
                    "hellevator" or "hell_evator" or "hellshaft" or "hell_shaft" => "hellevator",
                    "bridge" or "walkway" or "path" => "bridge",
                    "wall" or "barrier" or "defense" => "wall",
                    "platform" or "platforms" or "scaffold" => "platform",
                    _ => structStr.ToLowerInvariant()
                };
            }

            // Normalize boss names
            if (action == "boss" && parameters.TryGetValue("boss", out var boss) && boss is string bossStr)
            {
                parameters["boss"] = NormalizeBossName(bossStr);
            }

            // Normalize biome names for explore action
            if (action == "explore" && parameters.TryGetValue("biome", out var biome) && biome is string biomeStr)
            {
                parameters["biome"] = biomeStr.ToLowerInvariant().Trim() switch
                {
                    "forest" or "surface" or "purity" => "forest",
                    "desert" or "sand" or "sandy" => "desert",
                    "snow" or "ice" or "tundra" or "frozen" => "snow",
                    "jungle" or "rainforest" => "jungle",
                    "corruption" or "corrupt" or "ebonstone" => "corruption",
                    "crimson" or "crimsone" or "bloody" => "crimson",
                    "hallow" or "hallowed" or "holy" => "hallow",
                    "ocean" or "beach" or "sea" => "ocean",
                    "underground" or "below" or "under" => "underground",
                    "cavern" or "caves" or "deep" => "cavern",
                    "underworld" or "hell" or "lava" => "underworld",
                    _ => biomeStr.ToLowerInvariant()
                };
            }

            // Normalize ore/target names for mine action
            if (action == "mine" && parameters.TryGetValue("target", out var mineTarget) && mineTarget is string mineStr)
            {
                parameters["target"] = NormalizeOreName(mineStr);
            }
        }

        /// <summary>
        /// Normalizes boss names to their expected format.
        /// </summary>
        private static string NormalizeBossName(string bossName)
        {
            string lower = bossName.ToLowerInvariant().Trim();

            return lower switch
            {
                "eye" or "eyeofcthulhu" or "eye_of_cthulhu" or "eoc" => "EyeOfCthulhu",
                "kingslime" or "king_slime" or "king slime" or "slimeking" => "KingSlime",
                "eater" or "eaterofworlds" or "eater_of_worlds" or "eow" => "EaterOfWorlds",
                "brain" or "brainofcthulhu" or "brain_of_cthulhu" or "boc" => "BrainOfCthulhu",
                "queenbee" or "queen_bee" or "queen bee" or "bee" => "QueenBee",
                "skeletron" or "skeleton" => "Skeletron",
                "deerclops" or "deer" => "Deerclops",
                "wall" or "wallofflesh" or "wall_of_flesh" or "wof" => "WallOfFlesh",
                "twins" or "thetwins" or "the_twins" => "TheTwins",
                "destroyer" or "thedestroyer" or "the_destroyer" => "TheDestroyer",
                "prime" or "skeletronprime" or "skeletron_prime" => "SkeletronPrime",
                "plantera" or "plant" => "Plantera",
                "golem" or "lihzahrd" => "Golem",
                "fishron" or "dukefishron" or "duke_fishron" or "duke" => "DukeFishron",
                "empress" or "empressoflight" or "empress_of_light" or "eol" => "EmpressOfLight",
                "cultist" or "lunaticcultist" or "lunatic_cultist" => "Cultist",
                "moonlord" or "moon_lord" or "moon lord" or "ml" => "MoonLord",
                "queenslime" or "queen_slime" or "queen slime" => "QueenSlime",
                _ => bossName
            };
        }

        /// <summary>
        /// Normalizes ore/mining target names.
        /// </summary>
        private static string NormalizeOreName(string oreName)
        {
            string lower = oreName.ToLowerInvariant().Trim();

            return lower switch
            {
                "copper" or "copper_ore" or "copper ore" => "copper",
                "tin" or "tin_ore" or "tin ore" => "tin",
                "iron" or "iron_ore" or "iron ore" => "iron",
                "lead" or "lead_ore" or "lead ore" => "lead",
                "silver" or "silver_ore" or "silver ore" => "silver",
                "tungsten" or "tungsten_ore" or "tungsten ore" => "tungsten",
                "gold" or "gold_ore" or "gold ore" => "gold",
                "platinum" or "platinum_ore" or "platinum ore" => "platinum",
                "demonite" or "demonite_ore" or "demonite ore" or "demon" => "demonite",
                "crimtane" or "crimtane_ore" or "crimtane ore" => "crimtane",
                "hellstone" or "hellstone_ore" or "hell" => "hellstone",
                "cobalt" or "cobalt_ore" or "cobalt ore" => "cobalt",
                "palladium" or "palladium_ore" or "palladium ore" => "palladium",
                "mythril" or "mythril_ore" or "mythril ore" => "mythril",
                "orichalcum" or "orichalcum_ore" or "orichalcum ore" => "orichalcum",
                "adamantite" or "adamantite_ore" or "adamantite ore" => "adamantite",
                "titanium" or "titanium_ore" or "titanium ore" => "titanium",
                "chlorophyte" or "chlorophyte_ore" or "chlorophyte ore" or "chloro" => "chlorophyte",
                "luminite" or "luminite_ore" or "luminite ore" or "lunar" => "luminite",
                _ => lower
            };
        }

        /// <summary>
        /// Converts a JsonElement to an appropriate .NET object.
        /// </summary>
        private static object ConvertJsonValue(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    if (element.TryGetInt32(out int intVal))
                        return intVal;
                    if (element.TryGetInt64(out long longVal))
                        return longVal;
                    return element.GetDouble();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Array:
                    var list = new List<object>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertJsonValue(item));
                    }
                    return list;
                case JsonValueKind.Object:
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        dict[prop.Name] = ConvertJsonValue(prop.Value);
                    }
                    return dict;
                case JsonValueKind.Null:
                default:
                    return null;
            }
        }

        /// <summary>
        /// Attempts to extract useful information from a malformed response.
        /// </summary>
        private static void TryExtractFromMalformedResponse(string response, ParsedResponse result)
        {
            // Try to find any action-like patterns in the response
            var actionRegex = new Regex(@"""action""\s*:\s*""(\w+)""", RegexOptions.IgnoreCase);
            var matches = actionRegex.Matches(response);

            if (matches.Count > 0)
            {
                result.Plan = "Extracted from malformed response";

                foreach (Match match in matches)
                {
                    string action = match.Groups[1].Value;
                    action = NormalizeActionName(action);

                    // Try to extract parameters for this action
                    var parameters = TryExtractParametersFromMalformed(response, match.Index);
                    result.Tasks.Add(new Task(action, parameters));
                }

                if (result.Tasks.Count > 0)
                {
                    result.Success = true;
                    result.Error = "Partial parse - some data may be missing";
                }
            }
            else
            {
                // Try keyword-based extraction as last resort
                TryExtractFromKeywords(response, result);
            }
        }

        /// <summary>
        /// Attempts to extract parameters from a malformed response around an action match.
        /// </summary>
        private static Dictionary<string, object> TryExtractParametersFromMalformed(string response, int actionIndex)
        {
            var parameters = new Dictionary<string, object>();

            // Look for common parameter patterns near the action
            // Find the containing object by looking for the enclosing braces
            int objectStart = response.LastIndexOf('{', actionIndex);
            int objectEnd = response.IndexOf('}', actionIndex);

            if (objectStart >= 0 && objectEnd > objectStart)
            {
                string objectContent = response.Substring(objectStart, objectEnd - objectStart + 1);

                // Extract direction
                var directionMatch = Regex.Match(objectContent, @"""direction""\s*:\s*""(\w+)""", RegexOptions.IgnoreCase);
                if (directionMatch.Success)
                {
                    parameters["direction"] = directionMatch.Groups[1].Value.ToLowerInvariant();
                }

                // Extract target
                var targetMatch = Regex.Match(objectContent, @"""target""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (targetMatch.Success)
                {
                    parameters["target"] = targetMatch.Groups[1].Value;
                }

                // Extract structure
                var structureMatch = Regex.Match(objectContent, @"""structure""\s*:\s*""(\w+)""", RegexOptions.IgnoreCase);
                if (structureMatch.Success)
                {
                    parameters["structure"] = structureMatch.Groups[1].Value.ToLowerInvariant();
                }

                // Extract boss
                var bossMatch = Regex.Match(objectContent, @"""boss""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (bossMatch.Success)
                {
                    parameters["boss"] = NormalizeBossName(bossMatch.Groups[1].Value);
                }

                // Extract numeric values (depth, amount, width, height, distance)
                foreach (var numParam in new[] { "depth", "amount", "width", "height", "distance", "x", "y" })
                {
                    var numMatch = Regex.Match(objectContent, $@"""{numParam}""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
                    if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out int value))
                    {
                        parameters[numParam] = value;
                    }
                }

                // Extract player
                var playerMatch = Regex.Match(objectContent, @"""player""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (playerMatch.Success)
                {
                    parameters["player"] = playerMatch.Groups[1].Value;
                }

                // Extract biome
                var biomeMatch = Regex.Match(objectContent, @"""biome""\s*:\s*""(\w+)""", RegexOptions.IgnoreCase);
                if (biomeMatch.Success)
                {
                    parameters["biome"] = biomeMatch.Groups[1].Value.ToLowerInvariant();
                }

                // Extract material
                var materialMatch = Regex.Match(objectContent, @"""material""\s*:\s*""(\w+)""", RegexOptions.IgnoreCase);
                if (materialMatch.Success)
                {
                    parameters["material"] = materialMatch.Groups[1].Value.ToLowerInvariant();
                }

                // Extract tile
                var tileMatch = Regex.Match(objectContent, @"""tile""\s*:\s*""(\w+)""", RegexOptions.IgnoreCase);
                if (tileMatch.Success)
                {
                    parameters["tile"] = tileMatch.Groups[1].Value.ToLowerInvariant();
                }

                // Extract npc
                var npcMatch = Regex.Match(objectContent, @"""npc""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (npcMatch.Success)
                {
                    parameters["npc"] = npcMatch.Groups[1].Value;
                }

                // Extract message (for say action)
                var messageMatch = Regex.Match(objectContent, @"""message""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (messageMatch.Success)
                {
                    parameters["message"] = messageMatch.Groups[1].Value;
                }
            }

            return parameters;
        }

        /// <summary>
        /// Last resort extraction using keyword matching in plain text responses.
        /// </summary>
        private static void TryExtractFromKeywords(string response, ParsedResponse result)
        {
            string lower = response.ToLowerInvariant();

            // Check for common command keywords
            if (lower.Contains("follow") && (lower.Contains("player") || lower.Contains("me")))
            {
                result.Tasks.Add(new Task("follow", new Dictionary<string, object>
                {
                    { "target", "nearest" }
                }));
                result.Plan = "Following player (extracted from text)";
                result.Success = true;
            }
            else if (lower.Contains("mine") || lower.Contains("dig"))
            {
                var parameters = new Dictionary<string, object>();
                if (lower.Contains("iron")) parameters["target"] = "iron";
                else if (lower.Contains("copper")) parameters["target"] = "copper";
                else if (lower.Contains("gold")) parameters["target"] = "gold";
                else if (lower.Contains("silver")) parameters["target"] = "silver";

                if (lower.Contains("down")) parameters["direction"] = "down";
                else if (lower.Contains("left")) parameters["direction"] = "left";
                else if (lower.Contains("right")) parameters["direction"] = "right";

                result.Tasks.Add(new Task(lower.Contains("mine") ? "mine" : "dig", parameters));
                result.Plan = "Mining/digging (extracted from text)";
                result.Success = true;
            }
            else if (lower.Contains("build") || lower.Contains("construct"))
            {
                var parameters = new Dictionary<string, object>();
                if (lower.Contains("house")) parameters["structure"] = "house";
                else if (lower.Contains("tower")) parameters["structure"] = "tower";
                else if (lower.Contains("arena")) parameters["structure"] = "arena";
                else if (lower.Contains("hellevator")) parameters["structure"] = "hellevator";
                else parameters["structure"] = "house"; // Default

                result.Tasks.Add(new Task("build", parameters));
                result.Plan = "Building structure (extracted from text)";
                result.Success = true;
            }
            else if (lower.Contains("attack") || lower.Contains("fight") || lower.Contains("kill"))
            {
                result.Tasks.Add(new Task("attack", new Dictionary<string, object>
                {
                    { "target", "nearest" }
                }));
                result.Plan = "Attacking enemies (extracted from text)";
                result.Success = true;
            }
            else if (lower.Contains("say") || lower.Contains("speak") || lower.Contains("tell"))
            {
                // For say action from keywords, we can't extract a specific message
                // so we use the whole response as a fallback message
                result.Tasks.Add(new Task("say", new Dictionary<string, object>
                {
                    { "message", "I understood you wanted me to say something, but I couldn't parse the message." }
                }));
                result.Plan = "Communication (extracted from text)";
                result.Success = true;
            }

            if (result.Success)
            {
                result.Error = "Extracted from plain text - may be incomplete";
            }
        }
    }
}
