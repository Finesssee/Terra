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
    /// Google Gemini API client implementation.
    /// Uses Gemini 2.5 Flash model for fast, capable responses.
    /// </summary>
    public class GeminiClient : ILLMClient
    {
        private const string API_URL = "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent";

        private static readonly HttpClient _httpClient;

        public string ProviderName => "Gemini";

        static GeminiClient()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(60)
            };
        }

        public async Task<string> SendRequestAsync(string systemPrompt, string userPrompt)
        {
            var config = ModContent.GetInstance<TerraConfig>();
            if (config == null)
            {
                throw new InvalidOperationException("TerraConfig not available");
            }

            if (string.IsNullOrEmpty(config.GeminiApiKey))
            {
                throw new InvalidOperationException("Gemini API key is not configured");
            }

            // Gemini uses a different request format with contents array
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = $"{systemPrompt}\n\n{userPrompt}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = config.Temperature,
                    maxOutputTokens = config.MaxTokens
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);

            // API key is passed as a query parameter for Gemini
            var urlWithKey = $"{API_URL}?key={config.GeminiApiKey}";

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, urlWithKey);
                request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Gemini API request failed with status {response.StatusCode}: {errorContent}");
                }

                var responseJson = await response.Content.ReadAsStringAsync();
                return ParseResponse(responseJson);
            }
            catch (TaskCanceledException ex) when (ex.CancellationToken == default)
            {
                throw new TimeoutException("Gemini API request timed out");
            }
            catch (HttpRequestException)
            {
                throw;
            }
            catch (Exception ex)
            {
                TerraAIMod.Instance?.Logger.Error($"Gemini request error: {ex.Message}");
                throw;
            }
        }

        private static string ParseResponse(string responseJson)
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            // Gemini response format: candidates[0].content.parts[0].text
            if (root.TryGetProperty("candidates", out var candidates) &&
                candidates.GetArrayLength() > 0)
            {
                var firstCandidate = candidates[0];
                if (firstCandidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.GetArrayLength() > 0)
                {
                    var firstPart = parts[0];
                    if (firstPart.TryGetProperty("text", out var text))
                    {
                        return text.GetString() ?? string.Empty;
                    }
                }
            }

            throw new InvalidOperationException("Failed to parse Gemini response: unexpected format");
        }
    }
}
