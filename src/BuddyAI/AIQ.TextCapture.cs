using BuddyAI.Forms;
using BuddyAI.Models;
using BuddyAI.Services;
using SelectionCaptureApp;

namespace BuddyAI;

public sealed partial class AIQ
{
    private const int TextCaptureHotKeyId = 0x4242;
    private bool _globalTextCaptureShortcutRegistered;

    // Track all active forms instead of just one
    private readonly List<TextCaptureForm> _activeTextCaptureForms = new();

    private void RegisterTextCaptureHotKey()
    {
        if (!IsHandleCreated)
            return;

        ReleaseTextCaptureHotKey();

        try
        {
            uint modifiers = (uint)(HotKeyModifiers.Win | HotKeyModifiers.Shift | HotKeyModifiers.NoRepeat);
            uint vk = (uint)Keys.Z;

            bool registered = RegisterHotKey(Handle, TextCaptureHotKeyId, modifiers, vk);

            if (registered)
            {
                _globalTextCaptureShortcutRegistered = true;
                _diagnostics.Info("Text capture shortcut registered: Win+Shift+Z.");
            }
            else
            {
                _diagnostics.Warn("Failed to register text capture shortcut Win+Shift+Z. " +
                                  "Another application may already be using it.");
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Error("Text capture shortcut registration failed: " + ex.Message);
        }
    }

    private void ReleaseTextCaptureHotKey()
    {
        if (!_globalTextCaptureShortcutRegistered)
            return;

        try
        {
            if (IsHandleCreated)
                UnregisterHotKey(Handle, TextCaptureHotKeyId);
        }
        catch
        {
        }
        finally
        {
            _globalTextCaptureShortcutRegistered = false;
        }
    }

    private void ShowTextCaptureForm()
    {
        _diagnostics.Info("Text capture shortcut invoked (Win+Shift+Z).");

        // Capture text from the previously focused window BEFORE showing our form
        CaptureResult? captureResult = null;
        try
        {
            captureResult = SelectionCaptureEngine.TryCapture();

            if (captureResult.Success)
            {
                _diagnostics.Info($"Text captured via {captureResult.Method} " +
                                  $"({captureResult.SelectedText.Length} chars, " +
                                  $"wasSelection={captureResult.WasSelection}).");
            }
            else if (captureResult.TextBoxFound)
            {
                _diagnostics.Warn($"Textbox detected ({captureResult.Method}) but no text could be read.");
            }
            else
            {
                _diagnostics.Warn("No textbox or selected text detected in the focused window.");
            }
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"SelectionCaptureEngine failed: {ex.Message}");
        }

        TextCaptureForm form = new();
        _activeTextCaptureForms.Add(form);

        // Pre-fill the context area with the captured text and store the capture result
        if (captureResult is { Success: true } && !string.IsNullOrWhiteSpace(captureResult.SelectedText))
        {
            form.SetContextText(captureResult.SelectedText);
            form.SetCaptureResult(captureResult);
        }

        // Wire up the AI execution callback
        form.OnRunRequested = ProcessTextCaptureInFormAsync;

        // Wire up the model image support check
        form.CheckModelSupportsImages = () =>
        {
            AiProviderDefinition? provider = GetSelectedProvider();
            string model = _cmbModel.Text.Trim();
            return AiProviderService.ModelSupportsImages(provider, model);
        };

        form.FormClosed += (_, __) =>
        {
            _activeTextCaptureForms.Remove(form);
        };

        // Show as non-modal to avoid DPI context corruption of the parent window
        form.Show();
    }

    private async Task ProcessTextCaptureInFormAsync(TextCaptureForm form)
    {
        string capturedText = form.CapturedText;
        PersonaRecord? persona = form.SelectedPersona;
        string customAsk = form.CustomAsk;

        if (string.IsNullOrWhiteSpace(capturedText) || persona == null)
            return;

        _diagnostics.Info($"Text captured ({capturedText.Length} chars), " +
                          $"persona={persona.PersonaName}, " +
                          $"customAsk={(!string.IsNullOrWhiteSpace(customAsk) ? $"\"{customAsk}\"" : "none")}, " +
                          $"hasImage={form.SnipImageBytes is { Length: > 0 }}.");

        if (_isBusy)
        {
            form.ShowError("BuddyAI is already processing another request.");
            return;
        }

        AiProviderDefinition? provider = GetSelectedProvider();
        if (provider == null)
        {
            form.ShowError("No AI provider is selected. Open the composer and select a provider first.");
            return;
        }

        string model = _cmbModel.Text.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            form.ShowError("No model is selected. Open the composer and select a model first.");
            return;
        }

        double temperature = GetSelectedTemperature();
        string systemPrompt = form.EditableSystemPrompt;

        // Build the user prompt
        string userPrompt;
        if (!string.IsNullOrWhiteSpace(customAsk))
        {
            userPrompt = customAsk + Environment.NewLine + Environment.NewLine + capturedText;
        }
        else if (!string.IsNullOrWhiteSpace(form.EditableMessageTemplate))
        {
            userPrompt = form.EditableMessageTemplate + Environment.NewLine + Environment.NewLine + capturedText;
        }
        else
        {
            userPrompt = capturedText;
        }

        // Image handling
        byte[] imageBytes = form.SnipImageBytes ?? Array.Empty<byte>();
        string imageMimeType = form.SnipImageMimeType ?? "image/png";

        if (imageBytes.Length > 0 && !AiProviderService.ModelSupportsImages(provider, model))
        {
            form.ShowError($"The selected model '{model}' does not support image input. Clear the image or switch models.");
            return;
        }

        _requestCts?.Dispose();
        _requestCts = new CancellationTokenSource();

        form.ShowProcessing(persona.PersonaName);
        SetStatus($"Sending text to {persona.PersonaName} via {provider.Name}/{model}...");

        try
        {
            _isBusy = true;
            UpdateUiState();
            _diagnostics.Info($"Text capture request started: provider={provider.Name}, model={model}, persona={persona.PersonaName}, imageSize={imageBytes.Length}.");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            AiProviderClient.AiResponse response = await _client.SendWithImageAndStateAsync(
                provider,
                model,
                temperature,
                userPrompt,
                systemPrompt,
                imageBytes,
                imageMimeType,
                cancellationToken: _requestCts.Token);
            stopwatch.Stop();

            string aiText = response.Text ?? string.Empty;

            _diagnostics.Info($"Text capture completed in {stopwatch.Elapsed.TotalSeconds:F1}s ({aiText.Length} chars).");
            SetStatus($"AI response received in {stopwatch.Elapsed.TotalSeconds:F1}s.");

            bool canReplace = form.SourceCaptureResult is { Success: true, FocusInfo: not null };
            form.ShowResult(aiText, persona.PersonaName, canReplace);
        }
        catch (OperationCanceledException)
        {
            SetStatus("Text capture request cancelled.");
            _diagnostics.Info("Text capture request was cancelled by user.");
            form.ShowError("Request cancelled.");
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"Text capture request failed: {ex.Message}");
            SetStatus("Text capture request failed.");
            form.ShowError($"AI request failed: {ex.Message}");
        }
        finally
        {
            _isBusy = false;
            UpdateUiState();
        }
    }
}
