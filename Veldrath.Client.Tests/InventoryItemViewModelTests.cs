using Veldrath.Client.ViewModels;

namespace Veldrath.Client.Tests;

public class InventoryItemViewModelTests : TestBase
{
    // DisplayName
    [Fact]
    public void DisplayName_Converts_Underscore_Slug_To_Human_Readable()
    {
        var vm = new InventoryItemViewModel("iron_sword", 1, null);
        vm.DisplayName.Should().Be("iron sword");
    }

    [Fact]
    public void DisplayName_Converts_Hyphen_Slug_To_Human_Readable()
    {
        var vm = new InventoryItemViewModel("health-potion", 1, null);
        vm.DisplayName.Should().Be("health potion");
    }

    [Fact]
    public void DisplayName_Uses_Last_Segment_Of_Path_Slug()
    {
        var vm = new InventoryItemViewModel("items/weapons/iron_sword", 1, null);
        vm.DisplayName.Should().Be("iron sword");
    }

    [Fact]
    public void DisplayName_Uses_Last_Segment_Of_Colon_Separated_Slug()
    {
        var vm = new InventoryItemViewModel("realm:items:potion", 1, null);
        vm.DisplayName.Should().Be("potion");
    }

    // QuantityText
    [Fact]
    public void QuantityText_Is_Empty_For_Single_Item()
    {
        var vm = new InventoryItemViewModel("iron_sword", 1, null);
        vm.QuantityText.Should().BeEmpty();
    }

    [Fact]
    public void QuantityText_Returns_Formatted_Count_For_Stack()
    {
        var vm = new InventoryItemViewModel("arrow", 50, null);
        vm.QuantityText.Should().Be("×50");
    }

    [Fact]
    public void Setting_Quantity_Raises_QuantityText_PropertyChanged()
    {
        var vm = new InventoryItemViewModel("arrow", 1, null);
        using var monitor = vm.Monitor();

        vm.Quantity = 10;

        monitor.Should().RaisePropertyChangeFor(x => x.QuantityText);
    }

    // DurabilityText
    [Fact]
    public void DurabilityText_Is_Empty_When_Durability_Is_Null()
    {
        var vm = new InventoryItemViewModel("arrow", 10, null);
        vm.DurabilityText.Should().BeEmpty();
    }

    [Fact]
    public void DurabilityText_Returns_Percentage_String()
    {
        var vm = new InventoryItemViewModel("iron_sword", 1, 75);
        vm.DurabilityText.Should().Be("75%");
    }

    [Fact]
    public void DurabilityText_Shows_Zero_Percent_When_Fully_Broken()
    {
        var vm = new InventoryItemViewModel("iron_sword", 1, 0);
        vm.DurabilityText.Should().Be("0%");
    }

    [Fact]
    public void Setting_Durability_Raises_DurabilityText_PropertyChanged()
    {
        var vm = new InventoryItemViewModel("iron_sword", 1, 100);
        using var monitor = vm.Monitor();

        vm.Durability = 50;

        monitor.Should().RaisePropertyChangeFor(x => x.DurabilityText);
    }

    // HasDurability
    [Fact]
    public void HasDurability_Is_False_When_Durability_Is_Null()
    {
        var vm = new InventoryItemViewModel("arrow", 5, null);
        vm.HasDurability.Should().BeFalse();
    }

    [Fact]
    public void HasDurability_Is_True_When_Durability_Is_Set()
    {
        var vm = new InventoryItemViewModel("iron_sword", 1, 80);
        vm.HasDurability.Should().BeTrue();
    }

    [Fact]
    public void Setting_Durability_Raises_HasDurability_PropertyChanged()
    {
        var vm = new InventoryItemViewModel("iron_sword", 1, null);
        using var monitor = vm.Monitor();

        vm.Durability = 90;

        monitor.Should().RaisePropertyChangeFor(x => x.HasDurability);
    }

    // ItemRef
    [Fact]
    public void ItemRef_Is_Set_From_Constructor()
    {
        var vm = new InventoryItemViewModel("magic_staff", 1, null);
        vm.ItemRef.Should().Be("magic_staff");
    }
}
