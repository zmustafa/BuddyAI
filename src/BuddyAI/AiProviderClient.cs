using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using BuddyAI.Models;
using BuddyAI.Services;

namespace BuddyAI;

public sealed class AiProviderClient : IDisposable
{
    private readonly HttpClient _httpClient;

    public event Action<string, string>? OnHttpRequestStarted;
    public event Action<string, string>? OnHttpResponseReceived;

    public AiProviderClient()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(600)
        };
    }

    public async Task<List<string>> GetModelsAsync(AiProviderDefinition provider, CancellationToken cancellationToken = default)
    {
        string baseUrl = (provider.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) return new List<string>();

        string url = baseUrl + "/models";
        using HttpRequestMessage message = new(HttpMethod.Get, url);
        await ApplyHeadersAsync(message, provider, cancellationToken);

        try
        {
            using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new List<string>();

            string body = await response.Content.ReadAsStringAsync(cancellationToken);
            List<string> models = new();
            using JsonDocument doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("data", out JsonElement dataElement) && dataElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in dataElement.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out JsonElement idElement) && idElement.ValueKind == JsonValueKind.String)
                    {
                        string? id = idElement.GetString();
                        if (!string.IsNullOrWhiteSpace(id))
                            models.Add(id);
                    }
                }
            }
            return models;
        }
        catch
        {
            return new List<string>();
        }
    }

    public sealed class ConversationContext
    {
        public string PreviousUserPrompt { get; init; } = string.Empty;
        public string PreviousAssistantResponse { get; init; } = string.Empty;
    }

    public sealed class AiResponse
    {
        public string Text { get; init; } = string.Empty;
        public string? ResponseId { get; init; }
        public string? Provider { get; init; }
        public string? Model { get; init; }
    }

    public static bool SupportsStatefulConversation(AiProviderDefinition provider)
    {
        return ResolveEndpointKind(provider) == EndpointKind.Responses;
    }

    public async Task<AiResponse> SendWithImageAndStateAsync(
        AiProviderDefinition provider,
        string model,
        double temperature,
        string prompt,
        string? systemPrompt,
        byte[] imageBytes,
        string imageMimeType,
        string? previousResponseId = null,
        ConversationContext? conversationContext = null,
        CancellationToken cancellationToken = default)
    {
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));

        if (string.IsNullOrWhiteSpace(provider.ApiKey))
            throw new InvalidOperationException($"API key is empty for provider '{provider.Name}'. Open AI Provider Manager and add the key before sending requests.");

        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model is required.", nameof(model));

        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Prompt is required.", nameof(prompt));

        AiProviderModelDefinition selectedModel = AiProviderService.FindModel(provider, model) ?? AiProviderService.CreateModel(provider.ProviderType, model);

        temperature = NormalizeTemperature(temperature);
        imageBytes ??= Array.Empty<byte>();
        imageMimeType = string.IsNullOrWhiteSpace(imageMimeType) ? "image/png" : imageMimeType;

        if (imageBytes.Length > 0 && !selectedModel.SupportsImages)
            throw new InvalidOperationException($"The selected model '{selectedModel.Name}' does not support image input.");

        return ResolveEndpointKind(provider) switch
        {
            EndpointKind.ClaudeMessages => await SendViaClaudeMessagesAsync(provider, selectedModel, temperature, prompt, systemPrompt, imageBytes, imageMimeType, conversationContext, cancellationToken),
            EndpointKind.ClaudeWebChat => await SendViaClaudeWebChatAsync(provider, selectedModel, temperature, prompt, systemPrompt, imageBytes, imageMimeType, conversationContext, cancellationToken),
            EndpointKind.ChatCompletions => await SendViaChatCompletionsAsync(provider, selectedModel, temperature, prompt, systemPrompt, imageBytes, imageMimeType, conversationContext, cancellationToken),
            EndpointKind.CodexResponses => await SendViaCodexResponsesAsync(provider, selectedModel, temperature, prompt, systemPrompt, imageBytes, imageMimeType, previousResponseId, conversationContext, cancellationToken),
            _ => await SendViaResponsesAsync(provider, selectedModel, temperature, prompt, systemPrompt, imageBytes, imageMimeType, previousResponseId, conversationContext, cancellationToken)
        };
    }

    private async Task<AiResponse> SendViaResponsesAsync(
        AiProviderDefinition provider,
        AiProviderModelDefinition model,
        double temperature,
        string prompt,
        string? systemPrompt,
        byte[] imageBytes,
        string imageMimeType,
        string? previousResponseId,
        ConversationContext? conversationContext,
        CancellationToken cancellationToken)
    {
        bool includeImage = model.SupportsImages && imageBytes.Length > 0;
        string dataUrl = includeImage
            ? $"data:{imageMimeType};base64,{Convert.ToBase64String(imageBytes)}"
            : string.Empty;

        List<object> inputMessages = new();

        if (!string.IsNullOrWhiteSpace(systemPrompt) && string.IsNullOrWhiteSpace(previousResponseId))
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

        if (string.IsNullOrWhiteSpace(previousResponseId) && conversationContext != null)
        {
            AppendResponsesMessage(inputMessages, "user", conversationContext.PreviousUserPrompt);
            AppendResponsesMessage(inputMessages, "assistant", conversationContext.PreviousAssistantResponse, assistantTextType: "output_text");
        }

        List<object> userContent = new()
        {
            new
            {
                type = "input_text",
                text = prompt
            }
        };

        if (includeImage)
        {
            userContent.Add(new
            {
                type = "input_image",
                image_url = dataUrl,
                detail = "auto"
            });
        }

        inputMessages.Add(new
        {
            role = "user",
            content = userContent.ToArray()
        });

        Dictionary<string, object?> request = new()
        {
            ["model"] = model.Name,
            ["input"] = inputMessages,
            ["max_output_tokens"] = 10000,
            ["stream"] = false
        };

        if (model.SupportsTemperature)
            request["temperature"] = temperature;

        if (!string.IsNullOrWhiteSpace(previousResponseId))
            request["previous_response_id"] = previousResponseId;

        string body = await PostJsonAsync(provider, request, cancellationToken);
        return ExtractResponsesResult(body, model.Name, provider.Name);
    }

    private async Task<AiResponse> SendViaChatCompletionsAsync(
        AiProviderDefinition provider,
        AiProviderModelDefinition model,
        double temperature,
        string prompt,
        string? systemPrompt,
        byte[] imageBytes,
        string imageMimeType,
        ConversationContext? conversationContext,
        CancellationToken cancellationToken)
    {
        List<object> messages = new();
        string effectiveSystemPrompt = string.IsNullOrWhiteSpace(systemPrompt)
            ? "You are a precise code generation assistant. Return exactly the requested output."
            : systemPrompt;

        messages.Add(new
        {
            role = UsesDeveloperRole(model.Name) ? "developer" : "system",
            content = effectiveSystemPrompt
        });

        if (conversationContext != null)
        {
            messages.Add(new
            {
                role = "user",
                content = conversationContext.PreviousUserPrompt
            });
            messages.Add(new
            {
                role = "assistant",
                content = conversationContext.PreviousAssistantResponse
            });
        }

        if (model.SupportsImages && imageBytes.Length > 0)
        {
            bool isOllamaChat = (provider.EndpointPath?.Contains("api/chat", StringComparison.OrdinalIgnoreCase) == true) ||
                                (provider.BaseUrl?.Contains("api/chat", StringComparison.OrdinalIgnoreCase) == true);

            if (isOllamaChat)
            {
                // Native Ollama schema expects content as a string and a separate images array
                messages.Add(new
                {
                    role = "user",
                    content = prompt,
                    images = new[] { Convert.ToBase64String(imageBytes) }
                });
            }
            else
            {
                // Standard OpenAI schema
                string dataUrl = $"data:{imageMimeType};base64,{Convert.ToBase64String(imageBytes)}";
                messages.Add(new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = prompt
                        },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = dataUrl,
                                detail = "auto"
                            }
                        }
                    }
                });
            }
        }
        else
        {
            messages.Add(new
            {
                role = "user",
                content = prompt
            });
        }

        Dictionary<string, object?> request = new()
        {
            ["model"] = model.Name,
            ["messages"] = messages,
            ["stream"] = false
        };

        if (model.SupportsTemperature)
            request["temperature"] = temperature;

        string body = await PostJsonAsync(provider, request, cancellationToken);
        return new AiResponse
        {
            Text = ExtractChatCompletionsText(body),
            ResponseId = ExtractOptionalId(body),
            Provider = provider.Name,
            Model = model.Name
        };
    }

    private async Task<AiResponse> SendViaClaudeMessagesAsync(
        AiProviderDefinition provider,
        AiProviderModelDefinition model,
        double temperature,
        string prompt,
        string? systemPrompt,
        byte[] imageBytes,
        string imageMimeType,
        ConversationContext? conversationContext,
        CancellationToken cancellationToken)
    {
        List<object> messages = new();

        if (conversationContext != null)
        {
            messages.Add(new
            {
                role = "user",
                content = conversationContext.PreviousUserPrompt
            });
            messages.Add(new
            {
                role = "assistant",
                content = conversationContext.PreviousAssistantResponse
            });
        }

        if (model.SupportsImages && imageBytes.Length > 0)
        {
            messages.Add(new
            {
                role = "user",
                content = new object[]
                {
                    new
                    {
                        type = "text",
                        text = prompt
                    },
                    new
                    {
                        type = "image",
                        source = new
                        {
                            type = "base64",
                            media_type = imageMimeType,
                            data = Convert.ToBase64String(imageBytes)
                        }
                    }
                }
            });
        }
        else
        {
            messages.Add(new
            {
                role = "user",
                content = prompt
            });
        }

        Dictionary<string, object?> request = new()
        {
            ["model"] = model.Name,
            ["messages"] = messages,
            ["max_tokens"] = 4096
        };

        if (!string.IsNullOrWhiteSpace(systemPrompt))
            request["system"] = systemPrompt;

        if (model.SupportsTemperature)
            request["temperature"] = temperature;

        string body = await PostJsonAsync(provider, request, cancellationToken);
        return new AiResponse
        {
            Text = ExtractClaudeMessagesText(body),
            ResponseId = ExtractOptionalId(body),
            Provider = provider.Name,
            Model = model.Name
        };
    }

    private async Task<AiResponse> SendViaClaudeWebChatAsync(
        AiProviderDefinition provider,
        AiProviderModelDefinition model,
        double temperature,
        string prompt,
        string? systemPrompt,
        byte[] imageBytes,
        string imageMimeType,
        ConversationContext? conversationContext,
        CancellationToken cancellationToken)
    {
        string baseUrl = (provider.BaseUrl ?? "https://claude.ai").Trim().TrimEnd('/');
        string orgId = ClaudeOAuthTokenManager.GetOrganizationId(provider.Id);

        if (string.IsNullOrWhiteSpace(orgId))
            throw new InvalidOperationException("Organization ID is not configured for this Claude OAuth provider. Please re-authenticate.");

        ClaudeWebViewBridge bridge = ClaudeWebViewBridge.Instance;
        await bridge.EnsureInitializedAsync();

        // Step 1: Create a new conversation
        string createUrl = $"{baseUrl}/api/organizations/{orgId}/chat_conversations";
        Dictionary<string, object?> createRequest = new()
        {
            ["name"] = "",
            ["uuid"] = Guid.NewGuid().ToString(),
            ["model"] = model.Name
        };

        string createJson = JsonSerializer.Serialize(createRequest);
        OnHttpRequestStarted?.Invoke(createUrl, createJson);

        var (createStatus, createBody) = await bridge.FetchPostAsync(createUrl, createJson, cancellationToken);
        OnHttpResponseReceived?.Invoke(createUrl, $"Status: {createStatus}\n{createBody}");

        if (createStatus < 200 || createStatus >= 300)
            throw new InvalidOperationException($"{provider.Name} API error {createStatus}{Environment.NewLine}{createBody}");

        string conversationId;
        using (JsonDocument createDoc = JsonDocument.Parse(createBody))
        {
            if (createDoc.RootElement.TryGetProperty("uuid", out JsonElement uuidEl) && uuidEl.ValueKind == JsonValueKind.String)
                conversationId = uuidEl.GetString() ?? throw new InvalidOperationException("Could not parse conversation UUID from Claude response.");
            else
                throw new InvalidOperationException("Claude did not return a conversation UUID.");
        }

        // Step 2: Upload image if present via the wiggle/upload-file endpoint
        List<string> fileUuids = new();
        if (model.SupportsImages && imageBytes.Length > 0)
        {
            string uploadUrl = $"{baseUrl}/api/organizations/{orgId}/conversations/{conversationId}/wiggle/upload-file";

            string extension = imageMimeType switch
            {
                "image/jpeg" => ".jpg",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                _ => ".png"
            };
            string uploadFileName = $"image{extension}";

            OnHttpRequestStarted?.Invoke(uploadUrl, $"[multipart upload: {uploadFileName}, {imageBytes.Length} bytes]");

            var (uploadStatus, uploadBody) = await bridge.FetchMultipartUploadAsync(
                uploadUrl, imageBytes, uploadFileName, imageMimeType, cancellationToken);

            OnHttpResponseReceived?.Invoke(uploadUrl, $"Status: {uploadStatus}\n{uploadBody}");

            if (uploadStatus >= 200 && uploadStatus < 300 && !string.IsNullOrWhiteSpace(uploadBody))
            {
                // The upload endpoint returns a JSON object with a "uuid" field.
                // The completion request references uploaded files by UUID in the "files" array.
                try
                {
                    using JsonDocument uploadDoc = JsonDocument.Parse(uploadBody);
                    if (uploadDoc.RootElement.TryGetProperty("uuid", out JsonElement uuidEl) &&
                        uuidEl.ValueKind == JsonValueKind.String)
                    {
                        string? fileUuid = uuidEl.GetString();
                        if (!string.IsNullOrWhiteSpace(fileUuid))
                            fileUuids.Add(fileUuid);
                    }
                }
                catch
                {
                    // Fallback: if response is not valid JSON, skip the file reference
                }
            }
        }

        // Step 3: Send the completion request (streaming via bridge) to trigger generation
        string completionUrl = $"{baseUrl}/api/organizations/{orgId}/chat_conversations/{conversationId}/completion";

        // Prepend system prompt into the user prompt (claude.ai web API does not accept a separate "system" field)
        string fullPrompt = !string.IsNullOrWhiteSpace(systemPrompt)
            ? systemPrompt + "\n\n" + prompt
            : prompt;

        Dictionary<string, object?> completionRequest = new()
        {
            ["prompt"] = fullPrompt,
            ["timezone"] = TimeZoneInfo.Local.Id,
            ["model"] = model.Name,
            ["attachments"] = Array.Empty<object>(),
            ["files"] = fileUuids,
            ["rendering_mode"] = "messages"
        };

        string completionJson = JsonSerializer.Serialize(completionRequest);
        OnHttpRequestStarted?.Invoke(completionUrl, completionJson);

        // Fire the streaming POST to trigger generation; consume the stream to completion
        var (completionStatus, _) = await bridge.FetchPostStreamAsync(completionUrl, completionJson, cancellationToken);
        OnHttpResponseReceived?.Invoke(completionUrl, $"Status: {completionStatus} (stream consumed)");

        if (completionStatus < 200 || completionStatus >= 300)
            throw new InvalidOperationException($"{provider.Name} API error {completionStatus}{Environment.NewLine}Completion stream failed.");

        // Step 4: GET the conversation to retrieve the assistant's response text
        string getConvUrl = $"{baseUrl}/api/organizations/{orgId}/chat_conversations/{conversationId}?tree=False&rendering_mode=messages&render_all_tools=false&consistency=eventual";
        OnHttpRequestStarted?.Invoke(getConvUrl, "[GET conversation]");

        var (getStatus, getBody) = await bridge.FetchGetAsync(getConvUrl, cancellationToken);
        OnHttpResponseReceived?.Invoke(getConvUrl, $"Status: {getStatus}\n{getBody}");

        if (getStatus < 200 || getStatus >= 300)
            throw new InvalidOperationException($"{provider.Name} API error {getStatus}{Environment.NewLine}{getBody}");

        // Parse the last assistant message from chat_messages
        string responseText = ExtractClaudeWebChatText(getBody);

        return new AiResponse
        {
            Text = responseText,
            ResponseId = conversationId,
            Provider = provider.Name,
            Model = model.Name
        };
    }

    private async Task<AiResponse> SendViaCodexResponsesAsync(
        AiProviderDefinition provider,
        AiProviderModelDefinition model,
        double temperature,
        string prompt,
        string? systemPrompt,
        byte[] imageBytes,
        string imageMimeType,
        string? previousResponseId,
        ConversationContext? conversationContext,
        CancellationToken cancellationToken)
    {
        List<object> inputMessages = new();

        if (conversationContext != null)
        {
            if (!string.IsNullOrWhiteSpace(conversationContext.PreviousUserPrompt))
                inputMessages.Add(new { role = "user", content = conversationContext.PreviousUserPrompt });
            if (!string.IsNullOrWhiteSpace(conversationContext.PreviousAssistantResponse))
                inputMessages.Add(new { role = "assistant", content = conversationContext.PreviousAssistantResponse });
        }

        // Add optional image support if necessary (user requested "image supported" but didn't specify exactly. 
        // We will default to a standard array of content if image is provided, else normal string).
        if (model.SupportsImages && imageBytes.Length > 0)
        {
            string dataUrl = $"data:{imageMimeType};base64,{Convert.ToBase64String(imageBytes)}";
            inputMessages.Add(new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "input_text", text = prompt },
                    new { type = "input_image", image_url = dataUrl }
                }
            });
        }
        else
        {
            inputMessages.Add(new { role = "user", content = prompt });
        }

        Dictionary<string, object?> request = new()
        {
            ["model"] = model.Name,
            ["instructions"] = systemPrompt ?? string.Empty,
            ["input"] = inputMessages,
            ["stream"] = true,
            ["store"] = false
        };

        if (model.SupportsTemperature)
            request["temperature"] = temperature;
        if (!string.IsNullOrWhiteSpace(previousResponseId))
            request["previous_response_id"] = previousResponseId;

        string json = JsonSerializer.Serialize(request);
        string url = BuildUrl(provider);

        using HttpRequestMessage message = new(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        await ApplyHeadersAsync(message, provider, cancellationToken);
        OnHttpRequestStarted?.Invoke(url, json);

        using HttpResponseMessage response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"{provider.Name} API error {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{errorBody}");
        }

        using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using StreamReader reader = new(stream);

        StringBuilder sb = new();
        string? responseId = null;
        string? currentEvent = null;

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            if (line.StartsWith("event: "))
            {
                currentEvent = line.Substring(7).Trim();
                continue;
            }

            if (line.StartsWith("data: "))
            {
                string data = line.Substring(6).Trim();
                if (data == "[DONE]") break;

                try
                {
                    using JsonDocument doc = JsonDocument.Parse(data);

                    if (currentEvent == "response.completed")
                    {
                        bool foundText = false;
                        if (doc.RootElement.TryGetProperty("response", out JsonElement responseEl) &&
                            responseEl.TryGetProperty("output", out JsonElement outputArray) && 
                            outputArray.ValueKind == JsonValueKind.Array)
                        {
                            StringBuilder tempSb = new StringBuilder();
                            foreach (JsonElement outputItem in outputArray.EnumerateArray())
                            {
                                if (outputItem.TryGetProperty("content", out JsonElement contentArray) && contentArray.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (JsonElement contentItem in contentArray.EnumerateArray())
                                    {
                                        if (contentItem.TryGetProperty("text", out JsonElement textEl) && textEl.ValueKind == JsonValueKind.String)
                                        {
                                            tempSb.Append(textEl.GetString());
                                            foundText = true;
                                        }
                                    }
                                }
                            }
                            
                            if (foundText)
                            {
                                sb.Clear();
                                sb.Append(tempSb.ToString());
                                break;
                            }
                        }
                    }

                    if (doc.RootElement.TryGetProperty("id", out JsonElement idEl) && idEl.ValueKind == JsonValueKind.String)
                        responseId = idEl.GetString();

                    if (doc.RootElement.TryGetProperty("choices", out JsonElement choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                    {
                        JsonElement choice = choices[0];
                        if (choice.TryGetProperty("delta", out JsonElement delta))
                        {
                            if (delta.TryGetProperty("text", out JsonElement textEl) && textEl.ValueKind == JsonValueKind.String)
                                sb.Append(textEl.GetString());
                            else if (delta.TryGetProperty("content", out JsonElement contentEl) && contentEl.ValueKind == JsonValueKind.String)
                                sb.Append(contentEl.GetString());   
                        }
                    }
                    else if (doc.RootElement.TryGetProperty("message", out JsonElement msgEl))
                    {
                        if (msgEl.TryGetProperty("content", out JsonElement contentEl) && contentEl.ValueKind == JsonValueKind.String)
                            sb.Append(contentEl.GetString());
                    }
                }
                catch { /* Ignore parse errors on partial streams */ }
            }
        }
        
        OnHttpResponseReceived?.Invoke(url, $"Status: {(int)response.StatusCode}\nStreamed Length: {sb.Length}");

        return new AiResponse
        {
            Text = sb.ToString(),
            ResponseId = responseId ?? previousResponseId,
            Provider = provider.Name,
            Model = model.Name
        };
    }

    private async Task<string> PostJsonAsync(
        AiProviderDefinition provider,
        object request,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(request);
        string url = BuildUrl(provider);

        using HttpRequestMessage message = new(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        await ApplyHeadersAsync(message, provider, cancellationToken);
        OnHttpRequestStarted?.Invoke(url, json);

        StringBuilder curlBuilder = new();
        curlBuilder.Append($"curl -X POST \"{url}\"");
        foreach (var header in message.Headers)
        {
            string headerValue = string.Join(", ", header.Value);
            curlBuilder.Append($" -H \"{header.Key}: {headerValue}\"");
        }
        if (message.Content != null)
        {
            foreach (var header in message.Content.Headers)
            {
                string headerValue = string.Join(", ", header.Value);
                curlBuilder.Append($" -H \"{header.Key}: {headerValue}\"");
            }
        }
        string truncatedJson = json.Length > 2000 ? json[..2000] + "...[truncated]" : json;
        curlBuilder.Append($" -d '{truncatedJson}'");
        Debug.WriteLine(curlBuilder.ToString());

        using HttpResponseMessage response = await _httpClient.SendAsync(message, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        OnHttpResponseReceived?.Invoke(url, $"Status: {(int)response.StatusCode}\n{body}");

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{provider.Name} API error {(int)response.StatusCode} {response.ReasonPhrase}{Environment.NewLine}{body}");

        return body;
    }

    private static void AppendResponsesMessage(List<object> inputMessages, string role, string text, string assistantTextType = "input_text")
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        inputMessages.Add(new
        {
            role,
            content = new object[]
            {
                new
                {
                    type = assistantTextType,
                    text
                }
            }
        });
    }

    private static string BuildUrl(AiProviderDefinition provider)
    {
        string baseUrl = (provider.BaseUrl ?? string.Empty).Trim().TrimEnd('/');
        string path = (provider.EndpointPath ?? string.Empty).Trim();
        if (!path.StartsWith('/'))
            path = "/" + path;

        return baseUrl + path;
    }

    private static async Task ApplyHeadersAsync(HttpRequestMessage request, AiProviderDefinition provider, CancellationToken cancellationToken)
    {
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        switch (provider.ProviderType)
        {
            case AiProviderTypes.AzureOpenAI:
                request.Headers.TryAddWithoutValidation("api-key", provider.ApiKey);
                break;
            case AiProviderTypes.Claude:
                request.Headers.TryAddWithoutValidation("x-api-key", provider.ApiKey);
                request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                break;
            case AiProviderTypes.ClaudeOAuth:
                string claudeToken = await ClaudeOAuthTokenManager.GetAccessTokenAsync(provider.Id, cancellationToken);
                if (claudeToken.StartsWith("sk-ant-", StringComparison.Ordinal))
                {
                    request.Headers.TryAddWithoutValidation("x-api-key", claudeToken);
                    request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                }
                else
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", claudeToken);
                }
                break;
            case AiProviderTypes.ChatGPTOAuth:
                string token = await ChatGPTOAuthTokenManager.GetAccessTokenAsync(provider.Id, cancellationToken);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                break;
            default:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
                break;
        }
    }

    private static EndpointKind ResolveEndpointKind(AiProviderDefinition provider)
    {
        string path = (provider.EndpointPath ?? string.Empty).Trim().ToLowerInvariant();
        string url = (provider.BaseUrl ?? string.Empty).Trim().ToLowerInvariant();

        // Claude OAuth uses claude.ai web chat endpoints
        if (provider.ProviderType == AiProviderTypes.ClaudeOAuth)
            return EndpointKind.ClaudeWebChat;

        if (path.Contains("/messages") || url.Contains("/messages"))
            return EndpointKind.ClaudeMessages;

        // Look for /api/chat in either the base URL or the explicit endpoint path
        if (path.Contains("chat/completions") || url.Contains("chat/completions") || 
            path.Contains("/api/chat") || url.Contains("/api/chat"))
            return EndpointKind.ChatCompletions;

        if (path.Contains("codex/responses") || url.Contains("codex/responses"))
            return EndpointKind.CodexResponses;

        return EndpointKind.Responses;
    }

    private static double NormalizeTemperature(double temperature)
    {
        if (double.IsNaN(temperature) || double.IsInfinity(temperature))
            return 1d;

        return Math.Max(0d, Math.Min(2d, temperature));
    }

    private static bool UsesDeveloperRole(string model)
    {
        string value = model.Trim().ToLowerInvariant();
        return value.StartsWith("o1") ||
               value.StartsWith("o3") ||
               value.StartsWith("o4") ||
               value.StartsWith("gpt-5") ||
               value.Contains("codex");
    }

    private static string? ExtractOptionalId(string body)
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        if (root.TryGetProperty("id", out JsonElement value) && value.ValueKind == JsonValueKind.String)
            return value.GetString();

        return null;
    }

    private static AiResponse ExtractResponsesResult(string body, string model, string provider)
    {
        return new AiResponse
        {
            Text = ExtractResponsesText(body),
            ResponseId = ExtractOptionalId(body),
            Provider = provider,
            Model = model
        };
    }

    private static string ExtractChatCompletionsText(string body)
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Ollama /api/chat natively returns a message object at the root instead of inside choices
        if (root.TryGetProperty("message", out JsonElement directMessage) && 
            directMessage.TryGetProperty("content", out JsonElement directContent))
        {
            if (directContent.ValueKind == JsonValueKind.String)
                return directContent.GetString() ?? string.Empty;
        }

        if (!root.TryGetProperty("choices", out JsonElement choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Chat Completions response did not contain choices.");
        }

        JsonElement first = choices[0];
        if (!first.TryGetProperty("message", out JsonElement message))
            throw new InvalidOperationException("Chat Completions response did not contain a message.");

        if (message.TryGetProperty("content", out JsonElement content))
        {
            if (content.ValueKind == JsonValueKind.String)
                return content.GetString() ?? string.Empty;

            if (content.ValueKind == JsonValueKind.Array)
            {
                StringBuilder sb = new();
                foreach (JsonElement item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out JsonElement textValue) && textValue.ValueKind == JsonValueKind.String)
                        sb.Append(textValue.GetString());

                }

                string combined = sb.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(combined))
                    return combined;
            }
        }

        throw new InvalidOperationException("Could not parse text from Chat Completions response.");
    }

    private static string ExtractResponsesText(string body)
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        // Standard Ollama /api/generate fallback
        if (root.TryGetProperty("response", out JsonElement ollamaResponseElement) &&
            ollamaResponseElement.ValueKind == JsonValueKind.String)
        {
            string? direct = ollamaResponseElement.GetString();
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;
        }

        // Standard Google/Vertex JSON extraction
        if (root.TryGetProperty("output_text", out JsonElement outputTextElement) &&
            outputTextElement.ValueKind == JsonValueKind.String)
        {
            string? direct = outputTextElement.GetString();
            if (!string.IsNullOrWhiteSpace(direct))
                return direct;
        }

        if (root.TryGetProperty("output", out JsonElement outputArray) && outputArray.ValueKind == JsonValueKind.Array)
        {
            StringBuilder sb = new();

            foreach (JsonElement outputItem in outputArray.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("content", out JsonElement contentArray) || contentArray.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement contentItem in contentArray.EnumerateArray())
                {
                    if (contentItem.TryGetProperty("text", out JsonElement textElement) && textElement.ValueKind == JsonValueKind.String)
                    {
                        sb.Append(textElement.GetString());
                        continue;
                    }

                    if (contentItem.TryGetProperty("type", out JsonElement typeElement) &&
                        typeElement.ValueKind == JsonValueKind.String &&
                        string.Equals(typeElement.GetString(), "output_text", StringComparison.OrdinalIgnoreCase) &&
                        contentItem.TryGetProperty("text", out JsonElement typedTextElement) &&
                        typedTextElement.ValueKind == JsonValueKind.String)
                    {
                        sb.Append(typedTextElement.GetString());
                    }
                }
            }

            string combined = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(combined))
                return combined;
        }

        throw new InvalidOperationException("Could not parse text from Responses API response.");
    }

    private static string ExtractClaudeMessagesText(string body)
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("content", out JsonElement contentArray) || contentArray.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Claude Messages response did not contain content.");

        StringBuilder sb = new();
        foreach (JsonElement item in contentArray.EnumerateArray())
        {
            if (item.TryGetProperty("type", out JsonElement typeElement) &&
                typeElement.ValueKind == JsonValueKind.String &&
                string.Equals(typeElement.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                item.TryGetProperty("text", out JsonElement textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            {
                sb.Append(textElement.GetString());
            }
        }

        string combined = sb.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(combined))
            return combined;

        throw new InvalidOperationException("Could not parse text from Claude Messages response.");
    }

    /// <summary>
    /// Extracts the last assistant message text from a Claude web chat conversation GET response.
    /// Walks chat_messages in reverse to find the last "assistant" sender, then concatenates
    /// all content[].text entries of type "text".
    /// </summary>
    private static string ExtractClaudeWebChatText(string body)
    {
        using JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;

        if (!root.TryGetProperty("chat_messages", out JsonElement messagesArray) ||
            messagesArray.ValueKind != JsonValueKind.Array ||
            messagesArray.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Claude conversation response did not contain chat_messages.");
        }

        // Walk messages in reverse to find the last assistant message
        for (int i = messagesArray.GetArrayLength() - 1; i >= 0; i--)
        {
            JsonElement msg = messagesArray[i];
            if (!msg.TryGetProperty("sender", out JsonElement senderEl) ||
                senderEl.ValueKind != JsonValueKind.String ||
                !string.Equals(senderEl.GetString(), "assistant", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!msg.TryGetProperty("content", out JsonElement contentArray) ||
                contentArray.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            StringBuilder sb = new();
            foreach (JsonElement contentItem in contentArray.EnumerateArray())
            {
                if (contentItem.TryGetProperty("type", out JsonElement typeEl) &&
                    typeEl.ValueKind == JsonValueKind.String &&
                    string.Equals(typeEl.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                    contentItem.TryGetProperty("text", out JsonElement textEl) &&
                    textEl.ValueKind == JsonValueKind.String)
                {
                    sb.Append(textEl.GetString());
                }
            }

            string result = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(result))
                return result;
        }

        throw new InvalidOperationException("Could not find assistant response in Claude conversation.");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private enum EndpointKind
    {
        Responses,
        ChatCompletions,
        ClaudeMessages,
        ClaudeWebChat,
        CodexResponses
    }
}
