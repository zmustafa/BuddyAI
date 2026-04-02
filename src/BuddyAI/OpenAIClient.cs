using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BuddyAI;

public sealed class OpenAIClient : IDisposable
{
    private const string OpenAiChatCompletionsUrl =
        "https://dx2openai.openai.azure.com/openai/v1/chat/completions";

    private const string OpenAiResponsesUrl =
        "https://dx2openai.cognitiveservices.azure.com/openai/responses?api-version=2025-04-01-preview";

    private const string OllamaChat =
        "http://localhost:11434/api/chat";

    private readonly HttpClient _openAiClient;
    private readonly HttpClient _ollamaClient;

    public bool EnableResponsesApi { get; set; } = true;

    public OpenAIClient(string openAiApiKey, string? ollamaApiKey = null)
    {
        if (string.IsNullOrWhiteSpace(openAiApiKey))
            throw new ArgumentException("OpenAI API key is empty.", nameof(openAiApiKey));

        _openAiClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(600)
        };

        _ollamaClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(600)
        };

        _openAiClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", openAiApiKey);

        if (!string.IsNullOrWhiteSpace(ollamaApiKey))
        {
            _ollamaClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", ollamaApiKey);
        }
    }

    public sealed class AiResponse
    {
        public string Text { get; init; } = "";
        public string? ResponseId { get; init; }
        public string? Provider { get; init; }
        public string? Model { get; init; }
    }

    public async Task<string> SendAsync(
        string model,
        double temperature,
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var result = await SendWithStateAsync(
            model: model,
            temperature: temperature,
            prompt: prompt,
            systemPrompt: null,
            previousResponseId: null,
            cancellationToken: cancellationToken);

        return result.Text;
    }

    public async Task<string> SendAsync(
        string model,
        double temperature,
        string prompt,
        string? systemPrompt,
        CancellationToken cancellationToken = default)
    {
        var result = await SendWithStateAsync(
            model: model,
            temperature: temperature,
            prompt: prompt,
            systemPrompt: systemPrompt,
            previousResponseId: null,
            cancellationToken: cancellationToken);

        return result.Text;
    }

    public async Task<AiResponse> SendWithStateAsync(
        string model,
        double temperature,
        string prompt,
        string? systemPrompt = null,
        string? previousResponseId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model is required.", nameof(model));

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt is required.", nameof(prompt));

        temperature = NormalizeTemperature(temperature);

        Debug.WriteLine("using model: " + model);

        if (IsOllamaModel(model))
        {
            var text = await SendViaOllamaChatAsync(model, temperature, prompt, systemPrompt, cancellationToken);
            return new AiResponse
            {
                Text = text,
                ResponseId = null,
                Provider = "Ollama",
                Model = model
            };
        }

        if (EnableResponsesApi && ShouldPreferResponsesApi(model))
        {
            try
            {
                return await SendViaResponsesStatefulAsync(
                    model,
                    temperature,
                    prompt,
                    systemPrompt,
                    previousResponseId,
                    cancellationToken);
            }
            catch (OpenAiHttpException ex) when (ShouldTryChatFallback(model, ex.StatusCode))
            {
                var text = await SendViaChatCompletionsAsync(model, temperature, prompt, systemPrompt, cancellationToken);
                return new AiResponse
                {
                    Text = text,
                    ResponseId = null,
                    Provider = "OpenAI",
                    Model = model
                };
            }
        }

        try
        {
            var text = await SendViaChatCompletionsAsync(model, temperature, prompt, systemPrompt, cancellationToken);
            return new AiResponse
            {
                Text = text,
                ResponseId = null,
                Provider = "OpenAI",
                Model = model
            };
        }
        catch (OpenAiHttpException ex) when (EnableResponsesApi && ShouldTryResponsesFallback(model, ex.StatusCode))
        {
            return await SendViaResponsesStatefulAsync(
                model,
                temperature,
                prompt,
                systemPrompt,
                previousResponseId,
                cancellationToken);
        }
    }
    public sealed class AIResponse
    {
        public string Text { get; init; } = string.Empty;
        public string PreviousResponseId { get; init; } = string.Empty;
    }
    public async Task<AIResponse> SendWithImageAsync(
        string model,
        double temperature,
        string prompt,
        string systemPrompt,string previousMesssageId,
        byte[] imageBytes,
        string imageMimeType,
        CancellationToken cancellationToken = default)
    {
        var result = await SendWithImageAndStateAsync(
            model: model,
            temperature: temperature,
            prompt: prompt,
            systemPrompt: systemPrompt,
            imageBytes: imageBytes,
            imageMimeType: imageMimeType,
            previousResponseId: previousMesssageId,
            cancellationToken: cancellationToken);
        return new AIResponse
        {
            Text = result.Text,
            PreviousResponseId = result.ResponseId
        };
    }

    public async Task<AiResponse> SendWithImageAndStateAsync(
        string model,
        double temperature,
        string prompt,
        string? systemPrompt,
        byte[] imageBytes,
        string imageMimeType,
        string? previousResponseId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model is required.", nameof(model));

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt is required.", nameof(prompt));

        temperature = NormalizeTemperature(temperature);

        if (imageBytes == null || imageBytes.Length == 0)
        {
            //throw new ArgumentException("Image bytes are required.", nameof(imageBytes));
            imageBytes = Array.Empty<byte>();
        }

        //if (string.IsNullOrWhiteSpace(imageMimeType))
        //    throw new ArgumentException("Image MIME type is required.", nameof(imageMimeType));

        Debug.WriteLine("using model with image: " + model);

        if (IsOllamaModel(model))
            throw new NotSupportedException("Image upload is not implemented in this client for Ollama.");

        if (!EnableResponsesApi)
            throw new NotSupportedException("Image input requires Responses API. EnableResponsesApi must be true.");

        return await SendViaResponsesWithImageStatefulAsync(
            model,
            temperature,
            prompt,
            systemPrompt,
            imageBytes,
            imageMimeType,
            previousResponseId,
            cancellationToken);
    }

    private static bool IsOpenAiModel(string model)
    {
        var m = model.Trim().ToLowerInvariant();

        return m.StartsWith("gpt-") ||
               m.StartsWith("o1") ||
               m.StartsWith("o3") ||
               m.StartsWith("o4");
    }

    private static bool IsOllamaModel(string model)
    {
        return !IsOpenAiModel(model);
    }

    private static bool ShouldPreferResponsesApi(string model)
    {
        var m = model.Trim().ToLowerInvariant();

        if (m.StartsWith("gpt-5")) return true;
        if (m.StartsWith("o1")) return true;
        if (m.StartsWith("o3")) return true;
        if (m.StartsWith("o4")) return true;
        if (m.Contains("codex")) return true;

        return false;
    }

    private static bool ShouldTryChatFallback(string model, HttpStatusCode statusCode)
    {
        if (statusCode != HttpStatusCode.BadRequest &&
            statusCode != HttpStatusCode.NotFound)
        {
            return false;
        }

        var m = model.Trim().ToLowerInvariant();

        return m.StartsWith("gpt-4.1") ||
               m.StartsWith("gpt-4o") ||
               m.Contains("mini") ||
               m.Contains("nano");
    }

    private static bool ShouldTryResponsesFallback(string model, HttpStatusCode statusCode)
    {
        if (statusCode != HttpStatusCode.BadRequest &&
            statusCode != HttpStatusCode.NotFound)
        {
            return false;
        }

        var m = model.Trim().ToLowerInvariant();

        return m.StartsWith("gpt-5") ||
               m.StartsWith("o1") ||
               m.StartsWith("o3") ||
               m.StartsWith("o4") ||
               m.Contains("codex");
    }

    private async Task<AiResponse> SendViaResponsesStatefulAsync(
        string model,
        double temperature,
        string prompt,
        string? systemPrompt,
        string? previousResponseId,
        CancellationToken cancellationToken)
    {
        var inputMessages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt) &&
            string.IsNullOrWhiteSpace(previousResponseId))
        {
            inputMessages.Add(new
            {
                role = "system",
                content = new object[]
                {
                    new
                    {
                        type = "input_text",
                        text = systemPrompt
                    }
                }
            });
        }

        inputMessages.Add(new
        {
            role = "user",
            content = new object[]
            {
                new
                {
                    type = "input_text",
                    text = prompt
                }
            }
        });

        var request = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["input"] = inputMessages,
            ["temperature"] = temperature,
            ["max_output_tokens"] = 10000
        };

        if (!string.IsNullOrWhiteSpace(previousResponseId))
            request["previous_response_id"] = previousResponseId;

        var body = await PostJsonAsync(
            _openAiClient,
            OpenAiResponsesUrl,
            request,
            cancellationToken,
            providerName: "OpenAI");

        return ExtractResponsesResult(body, model, "OpenAI");
    }

    private async Task<AiResponse> SendViaResponsesWithImageStatefulAsync(
        string model,
        double temperature,
        string prompt,
        string? systemPrompt,
        byte[] imageBytes,
        string imageMimeType,
        string? previousResponseId,
        CancellationToken cancellationToken)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        var dataUrl = $"data:{imageMimeType};base64,{base64}";
        bool emptyImage = false;
        if (imageBytes.Length == 0)
        {
            emptyImage = true;
        }

        var inputMessages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt) &&
            string.IsNullOrWhiteSpace(previousResponseId))
        {
            inputMessages.Add(new
            {
                role = "system",
                content = new object[]
                {
                    new
                    {
                        type = "input_text",
                        text = systemPrompt
                    }
                }
            });
        }

        if (!emptyImage)
        {
            inputMessages.Add(new
            {
                role = "user",
                content = new object[]
                {
                    new
                    {
                        type = "input_text",
                        text = prompt
                    },
                    new
                    {
                        type = "input_image",
                        image_url = dataUrl,
                        detail = "auto"
                    }
                }
            });
        }
        else
        {
            inputMessages.Add(new
            {
                role = "user",
                content = new object[]
                {
                    new
                    {
                        type = "input_text",
                        text = prompt
                    }
                }
            });
        }
       

        var request = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["input"] = inputMessages, 
            ["max_output_tokens"] = 10000
        };

        if (!model.ToLower().Contains("nano"))
        {
            request.Add("temperature", temperature);
        }

        if (!string.IsNullOrWhiteSpace(previousResponseId))
            request["previous_response_id"] = previousResponseId;

        var body = await PostJsonAsync(
            _openAiClient,
            OpenAiResponsesUrl,
            request,
            cancellationToken,
            providerName: "OpenAI");

        return ExtractResponsesResult(body, model, "OpenAI");
    }

    private async Task<string> SendViaChatCompletionsAsync(
        string model,
        double temperature,
        string prompt,
        string? systemPrompt,
        CancellationToken cancellationToken)
    {
        var useDeveloperRole = UsesDeveloperRole(model);
        var effectiveSystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? "You are a precise code generation assistant. Return exactly the requested output."
            : systemPrompt;

        var request = new
        {
            model,
            temperature,
            messages = new object[]
            {
                new
                {
                    role = useDeveloperRole ? "developer" : "system",
                    content = effectiveSystemPrompt
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            }
        };

        var body = await PostJsonAsync(
            _openAiClient,
            OpenAiChatCompletionsUrl,
            request,
            cancellationToken,
            providerName: "OpenAI");

        return ExtractChatCompletionsText(body);
    }

    private async Task<string> SendViaOllamaChatAsync(
        string model,
        double temperature,
        string prompt,
        string? systemPrompt,
        CancellationToken cancellationToken)
    {
        var effectiveSystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? "You are a precise code generation assistant. Return exactly the requested output."
            : systemPrompt;

        var request = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["stream"] = false,
            ["messages"] = new object[]
            {
                new
                {
                    role = "system",
                    content = effectiveSystemPrompt
                },
                new
                {
                    role = "user",
                    content = prompt
                }
            },
            ["temperature"] = temperature,
            ["options"] = new Dictionary<string, object?>
            {
                ["think"] = "false",
                ["thinking"] = false,
                ["top_p"] = 0.95,
                ["top_k"] = 40,
                ["repetition_penalty"] = 1
            },
            ["thinking"] = new Dictionary<string, object?>
            {
                ["enabled"] = false
            },
            ["add_generation_prompt"] = false
        };

        var body = await PostJsonAsync(
            _ollamaClient,
            OllamaChat,
            request,
            cancellationToken,
            providerName: "Ollama");

        return ExtractOllamaChatText(body);
    }

    private static double NormalizeTemperature(double temperature)
    {
        if (double.IsNaN(temperature) || double.IsInfinity(temperature))
            return 1d;

        return Math.Max(0.1d, Math.Min(1d, temperature));
    }

    private static bool UsesDeveloperRole(string model)
    {
        var m = model.Trim().ToLowerInvariant();

        return m.StartsWith("o1") ||
               m.StartsWith("o3") ||
               m.StartsWith("o4") ||
               m.StartsWith("gpt-5") ||
               m.Contains("codex");
    }

    private async Task<string> PostJsonAsync(
        HttpClient client,
        string url,
        object request,
        CancellationToken cancellationToken,
        string providerName)
    {
        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        //Debug.WriteLine("url " + url);
        //Debug.WriteLine("payload " + json);

        using var resp = await client.PostAsync(url, content, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            throw new OpenAiHttpException(
                $"{providerName} API error {(int)resp.StatusCode} {resp.ReasonPhrase}\n{body}",
                resp.StatusCode);
        }

        return body;
    }

    private static AiResponse ExtractResponsesResult(string body, string model, string provider)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        string? responseId = null;
        if (root.TryGetProperty("id", out var idElement) &&
            idElement.ValueKind == JsonValueKind.String)
        {
            responseId = idElement.GetString();
        }

        var text = ExtractResponsesText(body);

        return new AiResponse
        {
            Text = text,
            ResponseId = responseId,
            Provider = provider,
            Model = model
        };
    }

    private static string ExtractResponsesText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("output_text", out var outputTextElement) &&
            outputTextElement.ValueKind == JsonValueKind.String)
        {
            var direct = outputTextElement.GetString();
            if (!string.IsNullOrWhiteSpace(direct))
                return direct!;
        }

        if (root.TryGetProperty("output", out var outputArray) &&
            outputArray.ValueKind == JsonValueKind.Array)
        {
            var sb = new StringBuilder();

            foreach (var outputItem in outputArray.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("content", out var contentArray) ||
                    contentArray.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out var textElement) &&
                        textElement.ValueKind == JsonValueKind.String)
                    {
                        sb.Append(textElement.GetString());
                        continue;
                    }

                    if (contentItem.TryGetProperty("type", out var typeElement) &&
                        typeElement.ValueKind == JsonValueKind.String &&
                        string.Equals(typeElement.GetString(), "output_text", StringComparison.OrdinalIgnoreCase) &&
                        contentItem.TryGetProperty("text", out var typedTextElement) &&
                        typedTextElement.ValueKind == JsonValueKind.String)
                    {
                        sb.Append(typedTextElement.GetString());
                    }
                }
            }

            var combined = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(combined))
                return combined;
        }

        throw new Exception("Could not parse text from Responses API response.");
    }

    private static string ExtractChatCompletionsText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new Exception("Chat Completions response did not contain choices.");
        }

        var first = choices[0];

        if (!first.TryGetProperty("message", out var message))
            throw new Exception("Chat Completions response did not contain message.");

        if (message.TryGetProperty("content", out var content))
        {
            if (content.ValueKind == JsonValueKind.String)
            {
                var text = content.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text!;
            }

            if (content.ValueKind == JsonValueKind.Array)
            {
                var sb = new StringBuilder();

                foreach (var part in content.EnumerateArray())
                {
                    if (part.TryGetProperty("text", out var textElement) &&
                        textElement.ValueKind == JsonValueKind.String)
                    {
                        sb.Append(textElement.GetString());
                    }
                }

                var combined = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(combined))
                    return combined;
            }
        }

        throw new Exception("Could not parse text from Chat Completions response.");
    }

    private static string ExtractOllamaChatText(string body)
    {
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        if (root.TryGetProperty("message", out var message) &&
            message.ValueKind == JsonValueKind.Object &&
            message.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.String)
        {
            var text = content.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                return text!;
        }

        if (root.TryGetProperty("response", out var response) &&
            response.ValueKind == JsonValueKind.String)
        {
            var text = response.GetString();
            if (!string.IsNullOrWhiteSpace(text))
                return text!;
        }

        throw new Exception("Could not parse text from Ollama chat response.");
    }

    public void Dispose()
    {
        _openAiClient.Dispose();
        _ollamaClient.Dispose();
    }

    private sealed class OpenAiHttpException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public OpenAiHttpException(string message, HttpStatusCode statusCode)
            : base(message)
        {
            StatusCode = statusCode;
        }
    }
}