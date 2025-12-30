using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TerraAIMod.Config;
using Terraria.ModLoader;

namespace TerraAIMod.AI
{
    /// <summary>
    /// Groq API client implementation.
    /// Uses the fast Llama 3.1 8B Instant model for quick responses.
    /// </summary>
    public class GroqClient : ILLMClient
    {
        private const string API_URL = "https://api.groq.com/openai/v1/chat/completions";
        private const string MODEL = "llama-3.1-8b-instant";

        private static readonly HttpClient _httpClient;

        public string ProviderName => "Groq";

        static GroqClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task<string> SendRequestAsync(string systemPrompt, string userPrompt)
        {
            var config = ModContent.GetInstance<TerraConfig>();
            if (config == null)
            {
                throw new InvalidOperationException("TerraConfig not available");
            }

            if (string.IsNullOrEmpty(config.GroqApiKey))
            {
                throw new InvalidOperationException("Groq API key is not configured");
            }

            var requestBody = new
            {
                model = MODEL,
                temperature = config.Temperature,
                max_tokens = config.MaxTokens,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, API_URL);
                request.Headers.Add("Authorization", $"Bearer {config.GroqApiKey}");
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Groq API request failed with status {response.StatusCode}: {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                return ParseResponse(responseJson);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken == default)
            {
                throw new TimeoutException("Groq API request timed out");
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                TerraAIMod.Instance?.Logger.Error($"Groq request error: {ex.Message}");
                throw;
            }
        }

        private static string ParseResponse(string responseJson)
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.TryGetProperty("choices", out var choices) &&
                choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) &&
                    message.TryGetProperty("content", out var content))
                {
                    return content.GetString() ?? string.Empty;
                }
            }

            throw new InvalidOperationException("Failed to parse Groq response: unexpected format");
        }
    }
}
