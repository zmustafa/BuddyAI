using System.Diagnostics;
using System.Globalization;
using BuddyAI.Forms;
using BuddyAI.Helpers;
using BuddyAI.Models;

namespace BuddyAI;

public sealed partial class AIQ
{
    private void LoadTemplates()
    {
        _templates.Clear();
        _templates.AddRange(_promptService.LoadOrSeed());
        _lstTemplates.BeginUpdate();
        _lstTemplates.Items.Clear();
        foreach (PromptItem template in _templates)
            _lstTemplates.Items.Add(new TemplateListEntry(template));
        _lstTemplates.EndUpdate();
        UpdateAutoComplete();
    }

    private void LoadSnippets()
    {
        _snippets.Clear();
        _snippets.AddRange(_snippetService.LoadOrSeed());
        _lstSnippets.BeginUpdate();
        _lstSnippets.Items.Clear();
        foreach (SnippetItem snippet in _snippets)
            _lstSnippets.Items.Add(new SnippetListEntry(snippet));
        _lstSnippets.EndUpdate();
    }

    private void LoadManagedSuggestions()
    {
        _managedSuggestions.Clear();
        _managedSuggestions.AddRange(_suggestionService.LoadOrSeed());
        RefreshSuggestions();
    }

    private void LoadPersonas()
    {
        _isLoadingPersonas = true;
        try
        {
            _personaRecords.Clear();
            _personaRecords.AddRange(_personaService.LoadOrSeed().Select(x => x.Clone()));

            string? previousPersona = _cmbPersona.SelectedItem as string;
            string? previousQuestion = (_cmbPrefilledQuestion.SelectedItem as PrefilledQuestionOption)?.Question;

            _cmbPersona.BeginUpdate();
            _cmbPersona.Items.Clear();
            foreach (string personaName in _personaRecords.Select(x => x.PersonaName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
                _cmbPersona.Items.Add(personaName);
            _cmbPersona.EndUpdate();

            if (!string.IsNullOrWhiteSpace(previousPersona))
            {
                int index = FindComboIndex(_cmbPersona, previousPersona);
                _cmbPersona.SelectedIndex = index >= 0 ? index : (_cmbPersona.Items.Count > 0 ? 0 : -1);
            }
            else if (_cmbPersona.Items.Count > 0)
            {
                _cmbPersona.SelectedIndex = 0;
            }

            // Rebuild the question list for the active persona after a persona reload.
            // This keeps the active persona usable even when the SelectedIndexChanged event
            // is intentionally suppressed during the bulk reload operation.
            SelectQuestionForCurrentPersona(previousQuestion);

            RefreshPersonaExplorer();
        }
        finally
        {
            _isLoadingPersonas = false;
        }
    }

    private void RefreshPersonaExplorer()
    {
        string filter = _txtPersonaExplorerSearch.Text.Trim();
        IEnumerable<PersonaRecord> rows = _personaRecords;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            rows = rows.Where(x =>
                x.PersonaName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.Category.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                x.MessageTemplate.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }

        _personaTree.BeginUpdate();
        _personaTree.Nodes.Clear();

        foreach (IGrouping<string, PersonaRecord> group in rows.GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "General" : x.Category).OrderBy(x => x.Key))
        {
            TreeNode categoryNode = new(group.Key)
            {
                Tag = new PersonaTreeTag(null, group.Key),
                ImageKey = "folder",
                SelectedImageKey = "folder"
            };

            foreach (PersonaRecord record in group.GroupBy(x => x.PersonaName).Select(g => g.First()).OrderBy(x => x.PersonaName))
            {
                string imageKey = record.Favorite ? "star" : "persona";
                TreeNode personaNode = new(record.PersonaName)
                {
                    Tag = new PersonaTreeTag(record.PersonaName, group.Key),
                    ImageKey = imageKey,
                    SelectedImageKey = imageKey
                };
                categoryNode.Nodes.Add(personaNode);
            }

            categoryNode.Expand();
            _personaTree.Nodes.Add(categoryNode);
        }

        _personaTree.EndUpdate();
    }

    private void ReloadPersonas()
    {
        LoadPersonas();
        SetStatus("Personas reloaded.");
        _diagnostics.Info("Personas reloaded from disk.");
    }

    private void OpenPersonaFile()
    {
        _personaService.EnsureFileExists();
        Process.Start(new ProcessStartInfo
        {
            FileName = _personaService.GetStoragePath(),
            UseShellExecute = true
        });
        SetStatus("Opened persona JSON.");
    }

    private void ManagePersonas()
    {
        OpenSettings("Personas");
    }

    private void ManageProviders()
    {
        OpenSettings("Providers");
    }

    private void ManageTemplates()
    {
        using TemplateEditorForm dialog = new(_promptService);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadTemplates();
            SetStatus("Templates saved.");
            _diagnostics.Info("Template editor changes applied.");
        }
        else
        {
            LoadTemplates();
        }
    }

    private void ManageSnippets()
    {
        using SnippetEditorForm dialog = new(_snippetService);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadSnippets();
            SetStatus("Snippets saved.");
            _diagnostics.Info("Snippet editor changes applied.");
        }
        else
        {
            LoadSnippets();
        }
    }

    private void ManageSuggestions()
    {
        using SuggestionEditorForm dialog = new(_suggestionService);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            LoadManagedSuggestions();
            SetStatus("Suggestions saved.");
            _diagnostics.Info("Suggestion editor changes applied.");
        }
        else
        {
            LoadManagedSuggestions();
        }
    }

    private void OpenTemplateFile()
    {
        _promptService.EnsureFileExists();
        Process.Start(new ProcessStartInfo
        {
            FileName = _promptService.GetStoragePath(),
            UseShellExecute = true
        });
        SetStatus("Opened template JSON.");
    }

    private void OpenSnippetFile()
    {
        _snippetService.EnsureFileExists();
        Process.Start(new ProcessStartInfo
        {
            FileName = _snippetService.GetStoragePath(),
            UseShellExecute = true
        });
        SetStatus("Opened snippet JSON.");
    }

    private void OpenSuggestionFile()
    {
        _suggestionService.EnsureFileExists();
        Process.Start(new ProcessStartInfo
        {
            FileName = _suggestionService.GetStoragePath(),
            UseShellExecute = true
        });
        SetStatus("Opened suggestion JSON.");
    }

    private void ImportPersonas()
    {
        using OpenFileDialog dialog = new()
        {
            Filter = "JSON Files|*.json|All Files|*.*",
            Title = "Import Personas"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        DialogResult merge = MessageBox.Show(this, "Merge imported personas with existing personas?", "Import Personas", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
        if (merge == DialogResult.Cancel)
            return;

        List<PersonaRecord> records = _personaService.Import(dialog.FileName, merge == DialogResult.Yes);
        _personaService.Save(records);
        LoadPersonas();
        SetStatus("Personas imported.");
        _diagnostics.Info("Personas imported from file.");
    }

    private void ExportPersonas()
    {
        using SaveFileDialog dialog = new()
        {
            Filter = "JSON Files|*.json",
            FileName = "BuddyAI.personas.json"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _personaService.Export(dialog.FileName, _personaRecords);
        SetStatus("Personas exported.");
        _diagnostics.Info("Personas exported to file.");
    }

    private void ImportAllSettings()
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Select a folder to import all settings from",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        string sourceDir = dialog.SelectedPath;
        string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BuddyAIDesktop");

        try
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir, "*.json"))
            {
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }

            // Clear current selections so we don't accidentally preserve old values
            _cmbProvider.SelectedIndex = -1;
            _cmbProvider.Text = string.Empty;
            _cmbModel.SelectedIndex = -1;
            _cmbModel.Text = string.Empty;
            _cmbPersona.SelectedIndex = -1;
            _cmbPersona.Text = string.Empty;
            _cmbPrefilledQuestion.SelectedIndex = -1;
            _cmbPrefilledQuestion.Text = string.Empty;

            // Reload configurations
            LoadProviders();
            LoadPersonas();
            
            RestoreWorkspace(); // Ensures profile selections and window layout matches new settings
            ApplyTheme(_workspaceSettings.Theme);
            RefreshPersonaExplorer();
            UpdateUiState();
            UpdateStatusBar();

            SetStatus("Settings imported successfully.");
            _diagnostics.Info("All settings imported and reloaded.");
            MessageBox.Show(this, "Settings imported completely.", "Import Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"Failed to import settings: {ex.Message}");
            MessageBox.Show(this, $"Error importing settings: {ex.Message}", "Import Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportAllSettings()
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Select a folder to export all settings to",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        string sourceDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "BuddyAIDesktop");
        string targetDir = dialog.SelectedPath;

        try
        {
            if (Directory.Exists(sourceDir))
            {
                foreach (string file in Directory.GetFiles(sourceDir, "*.json"))
                {
                    File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
                }
            }

            SetStatus("Settings exported successfully.");
            _diagnostics.Info("All settings exported to folder.");
            MessageBox.Show(this, "Settings exported successfully.", "Export Settings", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _diagnostics.Error($"Failed to export settings: {ex.Message}");
            MessageBox.Show(this, $"Error exporting settings: {ex.Message}", "Export Settings", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void TryAutoSelectInitialPersona()
    {
        if (_cmbProfile.Items.Count > 0 && _cmbProfile.SelectedIndex < 0)
        {
            string targetProfile = _workspaceSettings.ActiveProfile;
            _cmbProfile.SelectedItem = _profiles.ContainsKey(targetProfile) ? targetProfile : _profiles.Keys.First();
        }

        if (_cmbPersona.Items.Count > 0 && _cmbPersona.SelectedIndex < 0)
            _cmbPersona.SelectedIndex = 0;

        if (_cmbPrefilledQuestion.Items.Count > 0 && _cmbPrefilledQuestion.SelectedIndex < 0)
            _cmbPrefilledQuestion.SelectedIndex = 0;
    }

    private void OnPersonaChanged()
    {
        if (_isLoadingPersonas)
            return;

        PopulateQuestionListForPersona(_cmbPersona.SelectedItem as string, preserveQuestion: null);
        ApplyPersonaDefaults(_cmbPersona.SelectedItem as string);
        UpdateStatusBar();
    }

    private void SelectQuestionForCurrentPersona(string? question)
    {
        // Rebuild the available question templates for the currently selected persona.
        // This is used after persona reloads because persona selection change events are
        // deliberately suppressed while the combo boxes are being repopulated.
        PopulateQuestionListForPersona(_cmbPersona.SelectedItem as string, preserveQuestion: question);

        // Re-apply persona defaults so model selection and the status bar stay in sync
        // with the freshly loaded persona metadata.
        ApplyPersonaDefaults(_cmbPersona.SelectedItem as string);
        UpdateStatusBar();
    }

    private void PopulateQuestionListForPersona(string? personaName, string? preserveQuestion)
    {
        _cmbPrefilledQuestion.BeginUpdate();
        _cmbPrefilledQuestion.Items.Clear();

        if (string.IsNullOrWhiteSpace(personaName))
        {
            _cmbPrefilledQuestion.EndUpdate();
            _txtSystemPrompt.Clear();
            return;
        }

        List<PersonaRecord> rows = _personaRecords
            .Where(x => string.Equals(x.PersonaName, personaName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (PersonaRecord row in rows)
            _cmbPrefilledQuestion.Items.Add(new PrefilledQuestionOption(row.MessageTemplate, row.SystemPrompt));

        _cmbPrefilledQuestion.EndUpdate();

        if (_cmbPrefilledQuestion.Items.Count == 0)
        {
            _txtSystemPrompt.Clear();
            return;
        }

        int selectedIndex = -1;
        if (!string.IsNullOrWhiteSpace(preserveQuestion))
        {
            for (int i = 0; i < _cmbPrefilledQuestion.Items.Count; i++)
            {
                if (_cmbPrefilledQuestion.Items[i] is PrefilledQuestionOption option &&
                    string.Equals(option.Question, preserveQuestion, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }
        }

        _cmbPrefilledQuestion.SelectedIndex = selectedIndex >= 0 ? selectedIndex : 0;
        ApplySelectedQuestionOption();
    }

    private void ApplyPersonaDefaults(string? personaName)
    {
        if (string.IsNullOrWhiteSpace(personaName))
            return;

        PersonaRecord? record = _personaRecords.FirstOrDefault(x => string.Equals(x.PersonaName, personaName, StringComparison.OrdinalIgnoreCase));
        if (record == null)
            return;

        if (!string.IsNullOrWhiteSpace(record.DefaultModel))
            TrySelectProviderForModel(record.DefaultModel, allowMissingModel: true);
    }

    private void OnPrefilledQuestionChanged()
    {
        if (_isLoadingPersonas)
            return;

        ApplySelectedQuestionOption();
    }

    private void ApplySelectedQuestionOption()
    {
        if (_cmbPrefilledQuestion.SelectedItem is not PrefilledQuestionOption option)
            return;

        _txtSystemPrompt.Text = option.SystemPrompt;

        _suppressQuestionTextChanged = true;
        try
        {
            _txtQuestion.Text = option.Question;
        }
        finally
        {
            _suppressQuestionTextChanged = false;
        }

        UpdateUiState();
    }

    private void ApplyProfile(string profileName)
    {
        if (!_profiles.TryGetValue(profileName, out WorkspaceProfile? profile))
            return;

        _workspaceSettings.ActiveProfile = profile.Name;
        SelectPersona(profile.DefaultPersona);
        if (!string.IsNullOrWhiteSpace(profile.DefaultModel))
            TrySelectProviderForModel(profile.DefaultModel, allowMissingModel: true);

        string? quickTheme = profile.PreferredTheme;
        if (!string.IsNullOrWhiteSpace(quickTheme))
            ApplyTheme(quickTheme);

        SynchronizeQuickAccessCombos();
        SetStatus("Profile switched to " + profile.Name + ".");
    }

    private void SelectPersona(string personaName)
    {
        int index = FindComboIndex(_cmbPersona, personaName);
        if (index >= 0)
            _cmbPersona.SelectedIndex = index;
    }

    private void SynchronizeQuickAccessCombos()
    {
    }

    private static int FindComboIndex(ComboBox combo, string value)
        => PromptAssistanceHelper.FindComboIndex(combo, value);

    private static string GetComboSelectionText(ComboBox combo)
        => PromptAssistanceHelper.GetComboSelectionText(combo);

    private static string NormalizeTemperatureValue(string? value)
        => PromptAssistanceHelper.NormalizeTemperatureValue(value, DefaultTemperature);

    private string GetSelectedTemperatureText()
    {
        return NormalizeTemperatureValue(_cmbTemperature.Text);
    }

    private double GetSelectedTemperature()
    {
        string value = GetSelectedTemperatureText();
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double temperature)
            ? temperature
            : 1d;
    }

    private void SelectTemperature(string? value)
    {
        string normalized = NormalizeTemperatureValue(value);
        int index = FindComboIndex(_cmbTemperature, normalized);
        if (index >= 0)
            _cmbTemperature.SelectedIndex = index;
    }

    private void OpenSettings(string? initialPage = null)
    {
        using SettingsForm dialog = new(
            _providerService,
            _personaService,
            _workspaceSettingsService,
            _workspaceSettings,
            ApplyTheme,
            initialPage);

        string currentProvider = _cmbProvider.Text.Trim();
        string currentModel = _cmbModel.Text.Trim();

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            if (dialog.ProvidersChanged)
            {
                LoadProviders(currentProvider, currentModel);
                SetStatus("Providers saved.");
                _diagnostics.Info("AI provider settings applied.");
            }

            if (dialog.PersonasChanged)
            {
                LoadPersonas();
                SetStatus("Personas saved.");
                _diagnostics.Info("Persona settings applied.");
            }
        }
        else
        {
            // Reload to pick up any external changes
            LoadProviders(currentProvider, currentModel);
            LoadPersonas();
        }
    }

    private void OpenImportProviderWizard()
    {
        using ProviderImportWizardForm wizard = new();
        if (wizard.ShowDialog(this) == DialogResult.OK && wizard.ImportedProvider != null)
        {
            _providers.Add(wizard.ImportedProvider);
            _providerService.Save(_providers);
            LoadProviders(wizard.ImportedProvider.Name);
            SetStatus($"Provider '{wizard.ImportedProvider.Name}' imported successfully.");
            _diagnostics.Info($"Imported AI provider: {wizard.ImportedProvider.Name} ({wizard.ImportedProvider.ProviderType})");
        }
    }
}
