# Form System

**Last Updated**: January 12, 2026

---

## Overview

RealmForge uses a reflection-based dynamic form generation system that creates type-safe form inputs from any RealmEngine.Shared model. This eliminates manual form creation and ensures forms stay in sync with model changes.

---

## Architecture

### DynamicFormEditor Component

**Location:** `RealmForge/Components/Shared/DynamicFormEditor.razor`

**Generic Component:**
```razor
<DynamicFormEditor TModel="Item" 
                  Model="@itemModel" 
                  OnSave="@HandleSave" 
                  OnCancel="@HandleCancel" />
```

**Type Parameter:**
- `TModel` - The C# model type to generate form for
- Model must be a class with public properties
- Works with any RealmEngine.Shared model

### Reflection-Based Generation

**Property Inspection:**
```csharp
var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance)
    .Where(p => p.CanRead && p.CanWrite)
    .Where(p => !p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
    .ToArray();
```

**Process:**
1. Get all public instance properties
2. Filter to readable and writable properties
3. Skip "Id" properties (internal use only)
4. Group by type for rendering

---

## Supported Property Types

### String Properties ✅

**Detection:**
```csharp
bool IsStringProperty(PropertyInfo prop) => prop.PropertyType == typeof(string);
```

**Input:**
```razor
<InputText class="form-control" 
          @bind-Value="GetStringValue(prop)" 
          ValueChanged="@((string val) => SetValue(prop, val))" />
```

**Examples:**
- `Name` → Text input
- `Description` → Text input (future: multiline textarea)
- `Category` → Text input (future: dropdown if limited values)

### Integer Properties ✅

**Detection:**
```csharp
bool IsIntProperty(PropertyInfo prop) => prop.PropertyType == typeof(int);
```

**Input:**
```razor
<InputNumber class="form-control" 
            @bind-Value="GetIntValue(prop)" 
            ValueChanged="@((int val) => SetValue(prop, val))" />
```

**Examples:**
- `Level` → Number input
- `Damage` → Number input
- `Count` → Number input

### Double/Float Properties ✅

**Detection:**
```csharp
bool IsDoubleProperty(PropertyInfo prop) => 
    prop.PropertyType == typeof(double) || 
    prop.PropertyType == typeof(float);
```

**Input:**
```razor
<InputNumber class="form-control" 
            @bind-Value="GetDoubleValue(prop)" 
            ValueChanged="@((double val) => SetValue(prop, val))" 
            step="0.01" />
```

**Examples:**
- `Weight` → Decimal input
- `DropRate` → Decimal input (0.0-1.0)
- `Multiplier` → Decimal input

### Boolean Properties ✅

**Detection:**
```csharp
bool IsBoolProperty(PropertyInfo prop) => prop.PropertyType == typeof(bool);
```

**Input:**
```razor
<InputCheckbox class="form-check-input" 
              @bind-Value="GetBoolValue(prop)" 
              ValueChanged="@((bool val) => SetValue(prop, val))" />
```

**Examples:**
- `IsQuestItem` → Checkbox
- `IsStackable` → Checkbox
- `IsTradeable` → Checkbox

### Enum Properties ✅

**Detection:**
```csharp
bool IsEnumProperty(PropertyInfo prop) => prop.PropertyType.IsEnum;
```

**Input:**
```razor
<select class="form-select" 
        value="@GetEnumValue(prop)" 
        @onchange="@(e => SetEnumValue(prop, e))">
    @foreach (var enumVal in Enum.GetValues(prop.PropertyType))
    {
        <option value="@enumVal">@enumVal</option>
    }
</select>
```

**Examples:**
- `Rarity` → Dropdown (Common, Uncommon, Rare, etc.)
- `DamageType` → Dropdown (Physical, Fire, Ice, etc.)
- `EquipSlot` → Dropdown (Head, Chest, Hands, etc.)

### Complex Types (Read-Only) ✅

**Detection:**
- Not string, int, double, bool, or enum
- Includes lists, dictionaries, nested objects

**Display:**
```razor
<div class="form-text text-muted">
    Complex type: @prop.PropertyType.Name
</div>
```

**Examples:**
- `List<Ability>` → "Complex type: List`1"
- `Dictionary<string, int>` → "Complex type: Dictionary`2"
- `Stats` → "Complex type: Stats"

**Future:** Recursive form generation or inline JSON editor

---

## Form Features

### Label Generation

**Automatic Formatting:**
```csharp
string GetDisplayName(PropertyInfo prop)
{
    // Convert PascalCase to Title Case
    var name = prop.Name;
    return string.Concat(name.Select((x, i) => 
        i > 0 && char.IsUpper(x) ? " " + x : x.ToString()));
}
```

**Examples:**
- `ItemName` → "Item Name"
- `BaseDamage` → "Base Damage"
- `IsQuestItem` → "Is Quest Item"

### Value Getters

**Type-Specific Getters:**
```csharp
string GetStringValue(PropertyInfo prop) => 
    prop.GetValue(Model) as string ?? string.Empty;

int GetIntValue(PropertyInfo prop) => 
    (int)(prop.GetValue(Model) ?? 0);

double GetDoubleValue(PropertyInfo prop) => 
    Convert.ToDouble(prop.GetValue(Model) ?? 0);

bool GetBoolValue(PropertyInfo prop) => 
    (bool)(prop.GetValue(Model) ?? false);

object GetEnumValue(PropertyInfo prop) => 
    prop.GetValue(Model) ?? Enum.GetValues(prop.PropertyType).GetValue(0)!;
```

### Value Setter

**Generic Setter:**
```csharp
void SetValue(PropertyInfo prop, object value)
{
    if (Model != null)
    {
        prop.SetValue(Model, value);
        StateHasChanged();  // Trigger Blazor re-render
    }
}
```

**Enum Setter:**
```csharp
void SetEnumValue(PropertyInfo prop, ChangeEventArgs e)
{
    if (Model != null && e.Value != null)
    {
        var enumValue = Enum.Parse(prop.PropertyType, e.Value.ToString() ?? string.Empty);
        prop.SetValue(Model, enumValue);
        StateHasChanged();
    }
}
```

### Event Callbacks

**OnSave:**
```csharp
[Parameter] public EventCallback<TModel> OnSave { get; set; }

async Task HandleSubmit()
{
    if (Model != null)
    {
        await OnSave.InvokeAsync(Model);
    }
}
```

**OnCancel:**
```csharp
[Parameter] public EventCallback OnCancel { get; set; }

async Task HandleCancel()
{
    await OnCancel.InvokeAsync();
}
```

---

## Limitations (Current)

### Complex Properties
- **Lists:** Cannot add/remove/reorder items
- **Dictionaries:** Cannot edit key-value pairs
- **Nested Objects:** Cannot edit properties
- **Workaround:** Switch to JSON mode for manual editing

### Validation
- No visual validation feedback
- No inline error messages
- No field-level validation rules
- **Planned:** FluentValidation integration (v3.1)

### Layout
- All fields in single column
- No grouping or sections
- No collapsible areas
- **Planned:** Categorized sections (v3.2)

### Advanced Inputs
- No date/time pickers
- No color pickers
- No file upload
- No rich text editor
- **Planned:** Specialized inputs (v3.3)

---

## Planned Enhancements

### Complex Property Support (v3.1)

**List Editor:**
```razor
<div class="list-editor">
    <button @onclick="AddItem">Add Item</button>
    @foreach (var item in list)
    {
        <div class="list-item">
            <DynamicFormEditor TModel="@item.GetType()" Model="@item" />
            <button @onclick="() => RemoveItem(item)">Remove</button>
        </div>
    }
</div>
```

**Dictionary Editor:**
```razor
<div class="dictionary-editor">
    <button @onclick="AddEntry">Add Entry</button>
    @foreach (var kvp in dictionary)
    {
        <div class="entry">
            <input @bind="kvp.Key" placeholder="Key" />
            <input @bind="kvp.Value" placeholder="Value" />
            <button @onclick="() => RemoveEntry(kvp.Key)">Remove</button>
        </div>
    }
</div>
```

**Nested Object Editor:**
```razor
<details class="nested-object">
    <summary>@prop.Name</summary>
    <DynamicFormEditor TModel="@prop.PropertyType" 
                      Model="@prop.GetValue(Model)" />
</details>
```

### Validation Display (v3.1)

**FluentValidation Integration:**
```razor
<ValidationMessage For="@(() => Model.Name)" />

@if (validationErrors.ContainsKey(prop.Name))
{
    <div class="text-danger">@validationErrors[prop.Name]</div>
}
```

**Features:**
- Inline error messages
- Error summary panel
- Field highlighting (red border)
- Real-time validation
- Custom validators per model

### Form Layout (v3.2)

**Categorized Sections:**
```razor
@foreach (var category in GetCategories(Properties))
{
    <details open class="form-section">
        <summary>@category.Name</summary>
        @foreach (var prop in category.Properties)
        {
            <!-- Render property -->
        }
    </details>
}
```

**Categories:**
- Basic Info (name, description)
- Stats (damage, defense, health)
- Requirements (level, class)
- Effects (bonuses, abilities)
- Meta (rarity, value, weight)

**Attributes:**
```csharp
[Category("Basic Info")]
[DisplayOrder(1)]
public string Name { get; set; }

[Category("Stats")]
[DisplayOrder(10)]
public int Damage { get; set; }
```

### Advanced Inputs (v3.3)

**Date/Time Picker:**
```razor
<input type="datetime-local" 
       @bind="dateValue" 
       class="form-control" />
```

**Color Picker:**
```razor
<input type="color" 
       @bind="colorValue" 
       class="form-control" />
```

**Reference Picker:**
```razor
<button @onclick="() => OpenReferencePicker(prop)">
    Browse...
</button>
<span>@GetReferenceName(prop)</span>
```

**Rich Text Editor:**
```razor
<QuillEditor @bind-Value="description" 
            Toolbar="BasicToolbar" />
```

---

## Performance

### Current Performance
- Fast for models with <50 properties
- Instant rendering for simple types
- Reflection overhead negligible

### Planned Optimizations
- Cache PropertyInfo arrays
- Lazy render for hidden sections
- Virtual scrolling for >100 properties
- Memoize display names
- Compiled expressions instead of reflection

---

## Testing

### Current State
- Manual testing only
- No automated tests

### Planned Tests
- Unit tests for value getters/setters
- Property detection tests
- Label generation tests
- Type conversion tests
- Integration tests with real models

---

## Future Vision

### AI-Assisted Forms (v5.0+)
- Smart field suggestions
- Auto-complete from game data
- Validate against game balance
- Generate descriptions
- Suggest stat values

### Conditional Fields
- Show/hide fields based on other values
- Example: Show "Two-Handed Damage" only if "IsTwoHanded" is true
- Dependency chains

### Form Templates
- Save form layout as template
- Quick-apply to similar items
- Share templates with community
