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

            // Extract parameters
            var parameters = new Dictionary<string, object>();

            if (taskElement.TryGetProperty("parameters", out var paramsElement) &&
                paramsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in paramsElement.EnumerateObject())
                {
                    parameters[prop.Name] = ConvertJsonValue(prop.Value);
                }
            }

            // Also check for direct properties that might be parameters
            foreach (var prop in taskElement.EnumerateObject())
            {
                if (prop.Name != "action" && prop.Name != "parameters")
                {
                    parameters[prop.Name] = ConvertJsonValue(prop.Value);
                }
            }

            return new Task(action, parameters);
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
                    result.Tasks.Add(new Task(action));
                }

                if (result.Tasks.Count > 0)
                {
                    result.Success = true;
                    result.Error = "Partial parse - some data may be missing";
                }
            }
        }
    }
}
