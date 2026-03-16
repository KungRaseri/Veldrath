using System.Reflection;
using System.Text.RegularExpressions;
using ReactiveUI;
using RealmUnbound.Contracts.Content;

namespace RealmForge.ViewModels;

public enum FieldKind { Text, Number, Bool }

/// <summary>
/// A single editable field row produced by reflecting over an entity's nested JSONB object.
/// Accepts an optional <see cref="ContentFieldDescriptor"/> from <see cref="ContentSchemaRegistry"/>
/// to supply explicit labels, numeric bounds, and hint text in place of reflection-inferred metadata.
/// </summary>
public partial class FieldRowViewModel : ReactiveObject
{
    private readonly PropertyInfo _prop;
    private readonly object _owner;
    private readonly ContentFieldDescriptor? _descriptor;

    public event EventHandler? FieldChanged;

    public FieldRowViewModel(PropertyInfo prop, object owner, ContentFieldDescriptor? descriptor = null)
    {
        _prop       = prop;
        _owner      = owner;
        _descriptor = descriptor;
        Label       = descriptor?.Label ?? FormatLabel(prop.Name);

        var underlying = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        IsNullable = Nullable.GetUnderlyingType(prop.PropertyType) != null
                     || (!prop.PropertyType.IsValueType && prop.PropertyType != typeof(string));
        Kind = underlying == typeof(bool) ? FieldKind.Bool
             : underlying == typeof(string) ? FieldKind.Text
             : FieldKind.Number;
    }

    public string  Label      { get; }
    public FieldKind Kind     { get; }
    public bool IsNullable    { get; }

    // ── Schema-driven metadata (falls back to safe defaults when no descriptor) ──
    public decimal Min        => _descriptor?.Min is double lo ? (decimal)lo : decimal.MinValue;
    public decimal Max        => _descriptor?.Max is double hi ? (decimal)hi : decimal.MaxValue;
    public string? Hint       => _descriptor?.Hint;
    public bool    IsRequired => _descriptor?.Required ?? false;

    public bool IsText   => Kind == FieldKind.Text;
    public bool IsNumber => Kind == FieldKind.Number;
    public bool IsBool   => Kind == FieldKind.Bool;

    // ── Typed accessors (one is active per Kind) ─────────────────────────

    public string? StringValue
    {
        get => _prop.GetValue(_owner) as string;
        set
        {
            _prop.SetValue(_owner, value);
            this.RaisePropertyChanged();
            FieldChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public decimal? NumberValue
    {
        get
        {
            var v = _prop.GetValue(_owner);
            if (v is null) return null;
            return v switch
            {
                int i    => (decimal)i,
                float f  => (decimal)f,
                double d => (decimal)d,
                _        => null
            };
        }
        set
        {
            var under = Nullable.GetUnderlyingType(_prop.PropertyType) ?? _prop.PropertyType;
            object? converted = value is null
                ? null
                : under == typeof(int)    ? (int)value
                : under == typeof(float)  ? (float)value
                : under == typeof(double) ? (double)value
                : (object)value;
            _prop.SetValue(_owner, converted);
            this.RaisePropertyChanged();
            FieldChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool? BoolValue
    {
        get
        {
            var v = _prop.GetValue(_owner);
            if (v is bool b) return b;
            return null;
        }
        set
        {
            _prop.SetValue(_owner,
                Nullable.GetUnderlyingType(_prop.PropertyType) != null ? value : value ?? false);
            this.RaisePropertyChanged();
            FieldChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static string FormatLabel(string name) =>
        string.Join(" ", SplitWords().Split(name).Where(s => !string.IsNullOrEmpty(s)));

    [GeneratedRegex(@"(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex SplitWords();
}
