using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using AvaloniaEdit.Document;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using RealmForge.Models;
using RealmForge.Services;

namespace RealmForge.ViewModels;

public enum EditorMode { Json, Form }

public class JsonEditorViewModel : ReactiveObject
{
    private readonly ModelValidationService _validator;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly ContentTreeService _contentTree;
    private readonly ContentEditorService _contentEditor;
    private readonly ILogger<JsonEditorViewModel> _logger;

    private Guid? _currentEntityId;
    private string? _currentTableName;
    private string? _currentFileName;
    private EditorMode _editorMode = EditorMode.Json;
    private bool _isDirty;
    private bool _isBusy;
    private string _statusMessage = string.Empty;
    private bool _hasValidationResult;
    private bool _isValid;
    private bool _isLoadingContent;

    // Interaction for opening the reference picker dialog
    public Interaction<string?, string?> ShowReferencePickerInteraction { get; } = new();

    public JsonEditorViewModel(
        ModelValidationService validator,
        ReferenceResolverService referenceResolver,
        ContentTreeService contentTree,
        ContentEditorService contentEditor,
        ILogger<JsonEditorViewModel> logger)
    {
        _validator = validator;
        _referenceResolver = referenceResolver;
        _contentTree = contentTree;
        _contentEditor = contentEditor;
        _logger = logger;

        FileTree = new ObservableCollection<FileTreeNodeViewModel>();
        FormProperties = new ObservableCollection<JsonPropertyViewModel>();
        ValidationErrors = new ObservableCollection<string>();
        TextDocument = new TextDocument();

        TextDocument.Changed += (_, _) =>
        {
            if (!_isLoadingContent)
                IsDirty = true;
        };

        var canSave = this.WhenAnyValue(x => x.CurrentFileName, x => x.IsDirty,
            (n, d) => n != null && d);
        var canValidate = this.WhenAnyValue(x => x.CurrentFileName, (string? n) => n != null);

        LoadEntityCommand = ReactiveCommand.CreateFromTask<FileTreeNodeViewModel>(LoadEntityAsync);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveAsync, canSave);
        ValidateCommand = ReactiveCommand.CreateFromTask(ValidateAsync, canValidate);
        RefreshTreeCommand = ReactiveCommand.CreateFromTask(RefreshTreeAsync);
        ToggleModeCommand = ReactiveCommand.Create(ToggleMode);
        OpenReferencePickerCommand = ReactiveCommand.CreateFromTask<JsonPropertyViewModel>(OpenReferencePickerAsync);
        ToggleNodeCommand = ReactiveCommand.Create<FileTreeNodeViewModel>(node => node.IsExpanded = !node.IsExpanded);

        _ = InitializeAsync();
    }

    public TextDocument TextDocument { get; }
    public ObservableCollection<FileTreeNodeViewModel> FileTree { get; }
    public ObservableCollection<JsonPropertyViewModel> FormProperties { get; }
    public ObservableCollection<string> ValidationErrors { get; }

    public string? CurrentFileName
    {
        get => _currentFileName;
        private set => this.RaiseAndSetIfChanged(ref _currentFileName, value);
    }

    public EditorMode EditorMode
    {
        get => _editorMode;
        private set => this.RaiseAndSetIfChanged(ref _editorMode, value);
    }

    public bool IsJsonMode => EditorMode == EditorMode.Json;
    public bool IsFormMode => EditorMode == EditorMode.Form;

    public bool IsDirty
    {
        get => _isDirty;
        set => this.RaiseAndSetIfChanged(ref _isDirty, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }


    public bool HasValidationResult
    {
        get => _hasValidationResult;
        private set => this.RaiseAndSetIfChanged(ref _hasValidationResult, value);
    }

    public bool IsValid
    {
        get => _isValid;
        private set => this.RaiseAndSetIfChanged(ref _isValid, value);
    }

    public ReactiveCommand<FileTreeNodeViewModel, Unit> LoadEntityCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ValidateCommand { get; }
    public ReactiveCommand<Unit, Unit> RefreshTreeCommand { get; }
    public ReactiveCommand<Unit, Unit> ToggleModeCommand { get; }
    public ReactiveCommand<JsonPropertyViewModel, Unit> OpenReferencePickerCommand { get; }
    public ReactiveCommand<FileTreeNodeViewModel, Unit> ToggleNodeCommand { get; }

    private async Task InitializeAsync()
    {
        try
        {
            await RefreshTreeAsync();
            _ = _referenceResolver.BuildReferenceCatalogAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize editor");
            StatusMessage = "Failed to initialize: " + ex.Message;
        }
    }

    private async Task LoadEntityAsync(FileTreeNodeViewModel node)
    {
        if (node.EntityId == null || node.TableName == null) return;
        try
        {
            IsBusy = true;
            StatusMessage = $"Loading {node.Name}...";

            var json = await _contentEditor.GetEntityJsonAsync(node.EntityId.Value, node.TableName);
            if (json == null)
            {
                StatusMessage = $"Entity not found: {node.Name}";
                return;
            }

            _isLoadingContent = true;
            TextDocument.Text = json;
            _isLoadingContent = false;

            _currentEntityId = node.EntityId.Value;
            _currentTableName = node.TableName;
            CurrentFileName = node.Name;
            IsDirty = false;

            if (EditorMode == EditorMode.Form)
                SyncFormFromText();

            ValidationErrors.Clear();
            HasValidationResult = false;
            StatusMessage = $"Opened: {node.Name}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load entity: {EntityId} ({Table})", node.EntityId, node.TableName);
            StatusMessage = $"Error loading entity: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (_currentEntityId == null || _currentTableName == null) return;
        try
        {
            IsBusy = true;
            StatusMessage = "Saving...";

            string jsonText;
            if (EditorMode == EditorMode.Form)
            {
                var jobj = SerializeFormProperties();
                jsonText = jobj.ToString(Formatting.Indented);
                _isLoadingContent = true;
                TextDocument.Text = jsonText;
                _isLoadingContent = false;
            }
            else
            {
                jsonText = TextDocument.Text;
                // Validate JSON syntax before saving
                JObject.Parse(jsonText);
            }

            await _contentEditor.SaveEntityJsonAsync(_currentEntityId.Value, _currentTableName, jsonText);
            IsDirty = false;
            StatusMessage = $"Saved: {CurrentFileName}";
        }
        catch (JsonException ex)
        {
            StatusMessage = $"Invalid JSON: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save entity");
            StatusMessage = $"Error saving: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ValidateAsync()
    {
        if (_currentTableName == null) return;
        try
        {
            IsBusy = true;
            StatusMessage = "Validating...";
            ValidationErrors.Clear();

            JObject jobj;
            try { jobj = JObject.Parse(TextDocument.Text); }
            catch (JsonException ex)
            {
                ValidationErrors.Add($"JSON syntax error: {ex.Message}");
                HasValidationResult = true;
                IsValid = false;
                StatusMessage = "Validation failed: invalid JSON";
                return;
            }

            var result = await _validator.ValidateAsync(jobj, _currentTableName);
            HasValidationResult = true;
            IsValid = result.IsValid;

            if (result.IsValid)
            {
                StatusMessage = "✓ Validation passed";
            }
            else
            {
                foreach (var error in result.Errors)
                    ValidationErrors.Add($"{error.PropertyName}: {error.ErrorMessage}");
                StatusMessage = $"Validation failed: {result.Errors.Count} error(s)";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation error");
            StatusMessage = $"Validation error: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshTreeAsync()
    {
        FileTree.Clear();
        try
        {
            var nodes = await _contentTree.BuildTreeAsync();
            foreach (var node in nodes)
                FileTree.Add(node);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh content tree");
        }
    }

    private void ToggleMode()
    {
        if (EditorMode == EditorMode.Json)
        {
            EditorMode = EditorMode.Form;
            SyncFormFromText();
        }
        else
        {
            // Sync form changes back to text before switching
            if (FormProperties.Count > 0)
            {
                var jobj = SerializeFormProperties();
                _isLoadingContent = true;
                TextDocument.Text = jobj.ToString(Formatting.Indented);
                _isLoadingContent = false;
            }
            EditorMode = EditorMode.Json;
        }
        this.RaisePropertyChanged(nameof(IsJsonMode));
        this.RaisePropertyChanged(nameof(IsFormMode));
    }

    private void SyncFormFromText()
    {
        FormProperties.Clear();
        try
        {
            var jobj = JObject.Parse(TextDocument.Text);
            foreach (var prop in jobj.Properties())
                FormProperties.Add(CreatePropertyViewModel(prop));
        }
        catch (JsonException)
        {
            // Can't parse — leave form empty
        }
    }

    private async Task OpenReferencePickerAsync(JsonPropertyViewModel property)
    {
        var result = await ShowReferencePickerInteraction.Handle(property.EditableValue).FirstAsync();
        if (result != null)
            property.EditableValue = result;
    }

    private JObject SerializeFormProperties()
    {
        var result = new JObject();
        foreach (var prop in FormProperties)
        {
            result[prop.Key] = prop.ValueType switch
            {
                JsonValueType.Boolean => new JValue(prop.BoolValue),
                JsonValueType.Number when int.TryParse(prop.EditableValue, out int i) => new JValue(i),
                JsonValueType.Number when double.TryParse(prop.EditableValue, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double d) => new JValue(d),
                JsonValueType.Object or JsonValueType.Array => prop.OriginalToken,
                _ => new JValue(prop.EditableValue)
            };
        }
        return result;
    }

    private static JsonPropertyViewModel CreatePropertyViewModel(JProperty prop)
    {
        var valueStr = prop.Value.Type == JTokenType.String ? (string)prop.Value! : string.Empty;

        var valueType = prop.Value.Type switch
        {
            JTokenType.String when valueStr.StartsWith('@') => JsonValueType.Reference,
            JTokenType.String => JsonValueType.String,
            JTokenType.Integer or JTokenType.Float => JsonValueType.Number,
            JTokenType.Boolean => JsonValueType.Boolean,
            JTokenType.Object => JsonValueType.Object,
            JTokenType.Array => JsonValueType.Array,
            _ => JsonValueType.Null
        };

        return new JsonPropertyViewModel
        {
            Key = prop.Name,
            DisplayName = FormatLabel(prop.Name),
            ValueType = valueType,
            OriginalToken = prop.Value,
            EditableValue = valueType is JsonValueType.String or JsonValueType.Number or JsonValueType.Reference
                ? prop.Value.ToString()
                : string.Empty,
            BoolValue = valueType == JsonValueType.Boolean && (bool)prop.Value,
            ComplexSummary = valueType == JsonValueType.Object
                ? $"{{ {((JObject)prop.Value).Properties().Count()} properties }}"
                : valueType == JsonValueType.Array
                    ? $"[ {((JArray)prop.Value).Count} items ]"
                    : string.Empty
        };
    }

    private static string FormatLabel(string key)
    {
        // camelCase / PascalCase → "Title Case"
        var result = Regex.Replace(key, "([A-Z])", " $1").Trim();
        return char.ToUpper(result[0]) + result[1..];
    }
}
