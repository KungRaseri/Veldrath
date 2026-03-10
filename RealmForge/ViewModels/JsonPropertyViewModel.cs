using Newtonsoft.Json.Linq;
using ReactiveUI;

namespace RealmForge.ViewModels;

public enum JsonValueType { String, Number, Boolean, Object, Array, Null, Reference }

public class JsonPropertyViewModel : ReactiveObject
{
    private string _editableValue = string.Empty;
    private bool _boolValue;

    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public JsonValueType ValueType { get; init; }
    public JToken OriginalToken { get; init; } = JValue.CreateNull();
    public string ComplexSummary { get; init; } = string.Empty;

    public string EditableValue
    {
        get => _editableValue;
        set => this.RaiseAndSetIfChanged(ref _editableValue, value);
    }

    public bool BoolValue
    {
        get => _boolValue;
        set => this.RaiseAndSetIfChanged(ref _boolValue, value);
    }

    public bool IsEditable => ValueType is JsonValueType.String or JsonValueType.Number;
    public bool IsReference => ValueType == JsonValueType.Reference;
    public bool IsBool => ValueType == JsonValueType.Boolean;
    public bool IsComplex => ValueType is JsonValueType.Object or JsonValueType.Array;
    public bool IsNull => ValueType == JsonValueType.Null;
}
