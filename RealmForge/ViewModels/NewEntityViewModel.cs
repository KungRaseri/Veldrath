using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using ReactiveUI;
using RealmForge.Services;

namespace RealmForge.ViewModels;

public partial class NewEntityViewModel : ReactiveObject
{
    private readonly ContentEditorService _editorService;
    private readonly Action<EntityEditorViewModel> _onCreated;
    private readonly Action _onCancel;
    private bool _slugManuallyEdited;
    private string _displayName = string.Empty;
    private string _slug = string.Empty;
    private bool _isCreating;
    private string? _errorMessage;

    public NewEntityViewModel(
        FileTreeNodeViewModel typeKeyNode,
        ContentEditorService editorService,
        Action<EntityEditorViewModel> onCreated,
        Action onCancel)
    {
        TypeKeyNode = typeKeyNode;
        _editorService = editorService;
        _onCreated = onCreated;
        _onCancel = onCancel;

        // Auto-derive slug from display name unless the user has manually edited it
        this.WhenAnyValue(x => x.DisplayName)
            .Where(_ => !_slugManuallyEdited)
            .Subscribe(name => _slug = ToSlug(name));

        var canCreate = this.WhenAnyValue(
            x => x.DisplayName, x => x.Slug, x => x.IsCreating,
            (d, s, cr) => !string.IsNullOrWhiteSpace(d) && !string.IsNullOrWhiteSpace(s) && !cr);

        CreateCommand = ReactiveCommand.CreateFromTask(CreateAsync, canCreate);
        CancelCommand = ReactiveCommand.Create(_onCancel);
    }

    public FileTreeNodeViewModel TypeKeyNode { get; }

    public string DisplayName
    {
        get => _displayName;
        set => this.RaiseAndSetIfChanged(ref _displayName, value);
    }

    public string Slug
    {
        get => _slug;
        set
        {
            _slugManuallyEdited = true;
            this.RaiseAndSetIfChanged(ref _slug, value);
        }
    }

    public bool IsCreating
    {
        get => _isCreating;
        private set => this.RaiseAndSetIfChanged(ref _isCreating, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public ReactiveCommand<Unit, Unit> CreateCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    private async Task CreateAsync()
    {
        IsCreating = true;
        ErrorMessage = null;
        try
        {
            var result = await _editorService.CreateEntityAsync(
                TypeKeyNode.TableName!,
                TypeKeyNode.Domain!,
                TypeKeyNode.TypeKey!,
                Slug.Trim(),
                DisplayName.Trim());

            if (result is null)
            {
                ErrorMessage = "Failed to create entity. Check database connection and logs.";
                return;
            }

            var editor = new EntityEditorViewModel(
                result.Value.EntityId,
                TypeKeyNode.TableName!,
                DisplayName.Trim(),
                _editorService,
                result.Value.Json);

            _onCreated(editor);
        }
        finally
        {
            IsCreating = false;
        }
    }

    private static string ToSlug(string name) =>
        string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : SlugWhitespace().Replace(name.ToLowerInvariant().Trim(), "-").Trim('-');

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex SlugWhitespace();
}
