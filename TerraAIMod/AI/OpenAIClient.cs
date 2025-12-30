using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TerraAIMod.Config;
using Terraria.ModLoader;

namespace TerraAIMod.AI
{
    /// <summary>
    /// OpenAI API client implementation.
    /// Supports GPT models with retry logic for rate limiting and server errors.
    /// </summary>
    public class OpenAIClient : ILLMClient
    {
        private const string API_URL = "https://api.openai.com/v1/chat/completions";
        private const int MAX_RETRIES = 3;

        private static readonly HttpClient _httpClient;

        public string ProviderName => "OpenAI";

        static OpenAIClient()
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

            if (string.IsNullOrEmpty(config.OpenAIApiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured");
            }

            var requestBody = new
            {
                model = config.OpenAIModel,
                temperature = config.Temperature,
                max_tokens = config.MaxTokens,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userPrompt }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            int retryCount = 0;
            int delayMs = 1000;

            while (true)
            {
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Post, API_URL);
                    request.Headers.Add("Authorization", $"Bearer {config.OpenAIApiKey}");
                    request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                    using var response = await _httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseJson = await response.Content.ReadAsStringAsync();
                        return ParseResponse(responseJson);
                    }

                    // Check if we should retry (429 rate limit or 5xx server errors)
                    var statusCode = (int)response.StatusCode;
                    bool shouldRetry = statusCode == 429 || (statusCode >= 500 && statusCode < 600);

                    if (shouldRetry && retryCount < MAX_RETRIES)
                    {
                        retryCount++;
                        TerraAIMod.Instance?.Logger.Warn($"OpenAI request failed with status {statusCode}, retrying in {delayMs}ms (attempt {retryCount}/{MAX_RETRIES})");
                        await Task.Delay(delayMs);
                        delayMs *= 2; // Exponential backoff
                        continue;
                    }

                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"OpenAI API request failed with status {response.StatusCode}: {errorContent}");
                }
                catch (TaskCanceledException ex) when (ex.CancellationToken == default)
                {
                    // Timeout
                    if (retryCount < MAX_RETRIES)
                    {
                        retryCount++;
                        TerraAIMod.Instance?.Logger.Warn($"OpenAI request timed out, retrying in {delayMs}ms (attempt {retryCount}/{MAX_RETRIES})");
                        await Task.Delay(delayMs);
                        delayMs *= 2;
                        continue;
                    }
                    throw new TimeoutException("OpenAI API request timed out after all retries");
                }
                catch (HttpRequestException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    TerraAIMod.Instance?.Logger.Error($"OpenAI request error: {ex.Message}");
                    throw;
                }
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

            throw new InvalidOperationException("Failed to parse OpenAI response: unexpected format");
        }
    }
}
