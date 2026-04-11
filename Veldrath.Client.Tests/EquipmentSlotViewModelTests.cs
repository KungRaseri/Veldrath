using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Client.ViewModels;

namespace Veldrath.Client.Tests;

public class EquipmentSlotViewModelTests : TestBase
{
    // Initial state
    [Fact]
    public void IsEmpty_Should_Be_True_When_No_ItemRef_Set()
    {
        var slot = new EquipmentSlotViewModel("MainHand", "Main Hand");
        slot.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsOccupied_Should_Be_False_When_No_ItemRef_Set()
    {
        var slot = new EquipmentSlotViewModel("MainHand", "Main Hand");
        slot.IsOccupied.Should().BeFalse();
    }

    [Fact]
    public void DisplayIcon_Should_Be_Null_When_No_Icons_Set()
    {
        var slot = new EquipmentSlotViewModel("MainHand", "Main Hand");
        slot.DisplayIcon.Should().BeNull();
    }

    // After setting ItemRef
    [Fact]
    public void IsEmpty_Should_Be_False_When_ItemRef_Set()
    {
        var slot = new EquipmentSlotViewModel("MainHand", "Main Hand");
        slot.ItemRef = "iron_sword";
        slot.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void IsOccupied_Should_Be_True_When_ItemRef_Set()
    {
        var slot = new EquipmentSlotViewModel("MainHand", "Main Hand");
        slot.ItemRef = "iron_sword";
        slot.IsOccupied.Should().BeTrue();
    }

    [Fact]
    public void IsOccupied_Is_Inverse_Of_IsEmpty()
    {
        var slot = new EquipmentSlotViewModel("Head", "Head");

        slot.IsEmpty.Should().Be(!slot.IsOccupied);
        slot.ItemRef = "iron_helm";
        slot.IsEmpty.Should().Be(!slot.IsOccupied);
    }

    // Clearing ItemRef resets state
    [Fact]
    public void Clearing_ItemRef_Should_Return_Slot_To_Empty_State()
    {
        var slot = new EquipmentSlotViewModel("MainHand", "Main Hand");
        slot.ItemRef  = "iron_sword";
        slot.ItemRef  = null;

        slot.IsEmpty.Should().BeTrue();
        slot.DisplayIcon.Should().BeNull();
    }

    // PropertyChanged notifications
    [Fact]
    public void Setting_ItemRef_Should_Raise_IsEmpty_PropertyChanged()
    {
        var slot    = new EquipmentSlotViewModel("MainHand", "Main Hand");
        var changes = new List<string>();
        slot.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        slot.ItemRef = "iron_sword";

        changes.Should().Contain(nameof(EquipmentSlotViewModel.IsEmpty));
    }

    [Fact]
    public void Setting_ItemRef_Should_Raise_IsOccupied_PropertyChanged()
    {
        var slot    = new EquipmentSlotViewModel("MainHand", "Main Hand");
        var changes = new List<string>();
        slot.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        slot.ItemRef = "iron_sword";

        changes.Should().Contain(nameof(EquipmentSlotViewModel.IsOccupied));
    }

    [Fact]
    public void Setting_ItemRef_Should_Raise_DisplayIcon_PropertyChanged()
    {
        var slot    = new EquipmentSlotViewModel("MainHand", "Main Hand");
        var changes = new List<string>();
        slot.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        slot.ItemRef = "iron_sword";

        changes.Should().Contain(nameof(EquipmentSlotViewModel.DisplayIcon));
    }

    [Fact]
    public void Setting_ItemIcon_Should_Raise_DisplayIcon_PropertyChanged()
    {
        var slot    = new EquipmentSlotViewModel("MainHand", "Main Hand");
        slot.ItemRef = "iron_sword";
        var changes  = new List<string>();
        slot.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);

        // ItemIcon is a Bitmap which requires real assets to instantiate in headless tests.
        // Setting it to null (a valid no-icon sentinel) still triggers the notification.
        slot.ItemIcon = null;

        changes.Should().Contain(nameof(EquipmentSlotViewModel.DisplayIcon));
    }

    // SlotName / Label initialisation
    [Fact]
    public void SlotName_And_Label_Should_Be_Set_From_Constructor()
    {
        var slot = new EquipmentSlotViewModel("Chest", "Chest Armour");
        slot.SlotName.Should().Be("Chest");
        slot.Label.Should().Be("Chest Armour");
    }
}
