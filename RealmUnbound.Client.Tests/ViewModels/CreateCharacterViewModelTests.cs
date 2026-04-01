using System.Reactive.Linq;
using RealmUnbound.Client.Services;
using RealmUnbound.Client.Tests.Infrastructure;
using RealmUnbound.Client.ViewModels;
using RealmUnbound.Contracts.Characters;
using RealmUnbound.Contracts.Content;

namespace RealmUnbound.Client.Tests.ViewModels;

public class CreateCharacterViewModelTests : TestBase
{
    private static CreateCharacterViewModel MakeVm(
        FakeCharacterCreationService? creation = null,
        FakeContentService?           content = null,
        FakeNavigationService?        nav = null)
    {
        return new CreateCharacterViewModel(
            creation ?? new FakeCharacterCreationService(),
            FakeContentCache.Create(content),
            nav ?? new FakeNavigationService());
    }

    // Initialization
    [Fact]
    public async Task InitializeAsync_Sets_AvailableClasses_From_Catalog()
    {
        var content = new FakeContentService
        {
            Classes =
            [
                new ActorClassDto("warrior", "Warrior", "warriors", 10, "strength",     50),
                new ActorClassDto("mage",    "Mage",    "casters",   6, "intelligence", 40),
            ]
        };
        var vm = MakeVm(content: content);
        await Task.Yield();

        vm.AvailableClasses.Should().BeEquivalentTo(["Warrior", "Mage"]);
    }

    [Fact]
    public async Task InitializeAsync_Sets_ErrorMessage_When_Session_Fails()
    {
        var creation = new FakeCharacterCreationService { SessionIdResult = null };
        var vm = MakeVm(creation: creation);
        await Task.Yield();

        vm.ErrorMessage.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_Clears_IsBusy_After_Completion()
    {
        var vm = MakeVm();
        await Task.Yield();

        vm.IsBusy.Should().BeFalse();
    }

    // ConfirmCommand — CanExecute
    [Fact]
    public void ConfirmCommand_IsDisabled_When_Name_Is_Empty()
    {
        var vm = MakeVm();
        vm.SelectedClass = "Warrior";
        bool canExecute = false;
        vm.ConfirmCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void ConfirmCommand_IsDisabled_When_Class_Not_Selected()
    {
        var vm = MakeVm();
        vm.Name = "Hero";
        bool canExecute = false;
        vm.ConfirmCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeFalse();
    }

    [Fact]
    public void ConfirmCommand_IsEnabled_When_Name_And_Class_Are_Set()
    {
        var vm = MakeVm();
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";
        bool canExecute = false;
        vm.ConfirmCommand.CanExecute.Subscribe(v => canExecute = v);

        canExecute.Should().BeTrue();
    }

    // ConfirmCommand — happy path
    [Fact]
    public async Task InitializeAsync_Sets_AvailableSpecies_From_Catalog()
    {
        var content = new FakeContentService
        {
            Species = [new SpeciesDto("human", "Human", "humanoids", 10), new SpeciesDto("elf", "Elf", "fae", 6)]
        };
        var vm = MakeVm(content: content);
        await Task.Yield();

        vm.AvailableSpecies.Should().BeEquivalentTo(["Human", "Elf"]);
    }

    [Fact]
    public async Task InitializeAsync_Sets_AvailableBackgrounds_From_Catalog()
    {
        var content = new FakeContentService
        {
            Backgrounds = [new BackgroundDto("soldier", "Soldier", "martial", 10), new BackgroundDto("sage", "Sage", "scholarly", 8)]
        };
        var vm = MakeVm(content: content);
        await Task.Yield();

        vm.AvailableBackgrounds.Should().BeEquivalentTo(["Soldier", "Sage"]);
    }

    [Fact]
    public async Task ConfirmCommand_Navigates_To_CharacterSelect_On_Success()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav: nav);
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";

        await vm.ConfirmCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(CharacterSelectViewModel));
    }

    [Fact]
    public async Task ConfirmCommand_Clears_IsBusy_After_Success()
    {
        var vm = MakeVm();
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";

        await vm.ConfirmCommand.Execute();

        vm.IsBusy.Should().BeFalse();
    }

    // ConfirmCommand — failure
    [Fact]
    public async Task ConfirmCommand_Sets_ErrorMessage_When_Finalize_Fails()
    {
        var creation = new FakeCharacterCreationService
        {
            FinalizeResult = (null, new AppError("Name already taken"))
        };
        var vm = MakeVm(creation: creation);
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";

        await vm.ConfirmCommand.Execute();

        vm.ErrorMessage.Should().Be("Name already taken");
    }

    [Fact]
    public async Task ConfirmCommand_Sets_Fallback_Error_When_No_Message()
    {
        var creation = new FakeCharacterCreationService { FinalizeResult = (null, null) };
        var vm = MakeVm(creation: creation);
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";

        await vm.ConfirmCommand.Execute();

        vm.ErrorMessage.Should().Be("Failed to create character.");
    }

    [Fact]
    public async Task ConfirmCommand_Clears_IsBusy_On_Failure()
    {
        var creation = new FakeCharacterCreationService
        {
            FinalizeResult = (null, new AppError("Error"))
        };
        var vm = MakeVm(creation: creation);
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";

        await vm.ConfirmCommand.Execute();

        vm.IsBusy.Should().BeFalse();
    }

    [Fact]
    public async Task ConfirmCommand_Does_Not_Navigate_On_Finalize_Failure()
    {
        var nav      = new FakeNavigationService();
        var creation = new FakeCharacterCreationService
        {
            FinalizeResult = (null, new AppError("Error"))
        };
        var vm = MakeVm(creation: creation, nav: nav);
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";

        await vm.ConfirmCommand.Execute();

        nav.NavigationLog.Should().NotContain(typeof(CharacterSelectViewModel));
    }

    // Point-buy attributes
    [Fact]
    public void Strength_Defaults_To_8()
    {
        var vm = MakeVm();

        vm.Strength.Should().Be(8);
    }

    [Fact]
    public void RemainingPoints_Starts_At_27()
    {
        var vm = MakeVm();

        vm.RemainingPoints.Should().Be(27);
    }

    [Fact]
    public async Task IncreaseStatCommand_Increases_Stat_When_Points_Available()
    {
        var vm = MakeVm();

        await vm.IncreaseStatCommand.Execute("Strength");

        vm.Strength.Should().Be(9);
    }

    [Fact]
    public async Task IncreaseStatCommand_Reduces_RemainingPoints()
    {
        var vm = MakeVm();

        await vm.IncreaseStatCommand.Execute("Strength");

        vm.RemainingPoints.Should().Be(26);
    }

    [Fact]
    public async Task IncreaseStatCommand_Does_Not_Exceed_MaxValue()
    {
        var vm = MakeVm();
        // Increase Strength to 15 (costs 9 points)
        for (var i = 0; i < 7; i++)
            await vm.IncreaseStatCommand.Execute("Strength");

        await vm.IncreaseStatCommand.Execute("Strength");

        vm.Strength.Should().Be(15);
    }

    [Fact]
    public async Task IncreaseStatCommand_Does_Not_Increase_When_Points_Exhausted()
    {
        var vm = MakeVm();
        // Spend all 27 points on Strength (max 9) + Dexterity (max 9) + one Constitution point (9)
        // STR 8→15 costs 9, DEX 8→15 costs 9, CON 8→15 costs 9 = 27 total
        for (var i = 0; i < 7; i++) await vm.IncreaseStatCommand.Execute("Strength");
        for (var i = 0; i < 7; i++) await vm.IncreaseStatCommand.Execute("Dexterity");
        for (var i = 0; i < 7; i++) await vm.IncreaseStatCommand.Execute("Constitution");
        vm.RemainingPoints.Should().Be(0);

        await vm.IncreaseStatCommand.Execute("Intelligence");

        vm.Intelligence.Should().Be(8);
    }

    [Fact]
    public async Task DecreaseStatCommand_Decreases_Stat()
    {
        var vm = MakeVm();
        await vm.IncreaseStatCommand.Execute("Wisdom");

        await vm.DecreaseStatCommand.Execute("Wisdom");

        vm.Wisdom.Should().Be(8);
    }

    [Fact]
    public async Task DecreaseStatCommand_Does_Not_Go_Below_MinValue()
    {
        var vm = MakeVm();

        await vm.DecreaseStatCommand.Execute("Charisma");

        vm.Charisma.Should().Be(8);
    }

    [Fact]
    public async Task ConfirmCommand_Sends_Attribute_Allocations()
    {
        var creation = new FakeCharacterCreationService();
        var vm = MakeVm(creation: creation);
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";
        await vm.IncreaseStatCommand.Execute("Strength");

        await vm.ConfirmCommand.Execute();

        creation.LastAttributeAllocations.Should().ContainKey("Strength")
            .WhoseValue.Should().Be(9);
    }

    // Equipment preferences
    [Fact]
    public void AvailableArmorTypes_Is_Populated()
    {
        var vm = MakeVm();

        vm.AvailableArmorTypes.Should().NotBeEmpty();
    }

    [Fact]
    public void AvailableWeaponTypes_Is_Populated()
    {
        var vm = MakeVm();

        vm.AvailableWeaponTypes.Should().NotBeEmpty();
    }

    [Fact]
    public async Task InitializeAsync_Sets_AvailableLocations_From_Catalog()
    {
        var content = new FakeContentService
        {
            ZoneLocations =
            [
                new("starter-town", "Starter Town", "town", "zone-1", "landmark", 10, 1, 5),
                new("riverside",    "Riverside",    "outdoor", "zone-1", "outpost", 8, 1, 5),
            ]
        };
        var vm = MakeVm(content: content);
        await Task.Yield();

        vm.AvailableLocations.Should().BeEquivalentTo(["Starter Town", "Riverside"]);
    }

    [Fact]
    public async Task ConfirmCommand_Calls_SetEquipmentPreferences_When_ArmorType_Selected()
    {
        var creation = new FakeCharacterCreationService();
        var vm = MakeVm(creation: creation);
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";
        vm.SelectedArmorType = "Heavy Armor";

        await vm.ConfirmCommand.Execute();

        creation.SetEquipmentPreferencesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task ConfirmCommand_Does_Not_Call_SetEquipmentPreferences_When_Nothing_Selected()
    {
        var creation = new FakeCharacterCreationService();
        var vm = MakeVm(creation: creation);
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";

        await vm.ConfirmCommand.Execute();

        creation.SetEquipmentPreferencesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task ConfirmCommand_Calls_SetLocation_When_Location_Selected()
    {
        var content = new FakeContentService
        {
            ZoneLocations = [new("town-square", "Town Square", "town", "zone-1", "landmark", 10, 1, 5)]
        };
        var creation = new FakeCharacterCreationService();
        var vm = MakeVm(creation: creation, content: content);
        await Task.Yield();
        vm.Name = "Hero";
        vm.SelectedClass = "Warrior";
        vm.SelectedLocation = "Town Square";

        await vm.ConfirmCommand.Execute();

        creation.SetLocationCallCount.Should().Be(1);
    }

    // CancelCommand
    [Fact]
    public async Task CancelCommand_Calls_AbandonAsync_When_Session_Exists()    {
        var creation = new FakeCharacterCreationService();
        await Task.Yield();  // allow InitializeAsync to set _sessionId
        var vm = MakeVm(creation: creation);
        await Task.Yield();

        await vm.CancelCommand.Execute();

        creation.AbandonCallCount.Should().Be(1);
    }

    [Fact]
    public async Task CancelCommand_Navigates_To_CharacterSelect()
    {
        var nav = new FakeNavigationService();
        var vm  = MakeVm(nav: nav);

        await vm.CancelCommand.Execute();

        nav.NavigationLog.Should().Contain(typeof(CharacterSelectViewModel));
    }
}
