using System.Reactive.Linq;
using Veldrath.Client;
using Veldrath.Client.Services;
using Veldrath.Client.Tests.Infrastructure;
using Veldrath.Client.ViewModels;

namespace Veldrath.Client.Tests;

public class AttributeAllocationViewModelTests : TestBase
{
    private static GameViewModel MakeGameVm(int unspentPoints = 5,
        int str = 10, int dex = 10, int con = 10,
        int intel = 10, int wis = 10, int cha = 10)
    {
        var vm = new GameViewModel(
            new FakeServerConnectionService(),
            new FakeZoneService(),
            new TokenStore(),
            new FakeNavigationService(),
            new ClientSettings("http://localhost"));

        vm.SeedInitialStats(new SeedInitialStatsArgs(
            Level: 1, Experience: 0,
            CurrentHealth: 100, MaxHealth: 100,
            CurrentMana: 50, MaxMana: 50,
            Gold: 0, UnspentAttributePoints: unspentPoints,
            Strength: str, Dexterity: dex, Constitution: con,
            Intelligence: intel, Wisdom: wis, Charisma: cha));

        return vm;
    }

    // -- Budget accounting ----------------------------------------------------

    [Fact]
    public void PointsToAllocate_Should_Equal_UnspentAttributePoints_On_Open()
    {
        var gameVm = MakeGameVm(unspentPoints: 5);
        var vm = new AttributeAllocationViewModel(gameVm);

        vm.PointsToAllocate.Should().Be(5);
    }

    [Fact]
    public void Incrementing_An_Attribute_Should_Reduce_PointsToAllocate()
    {
        var gameVm = MakeGameVm(unspentPoints: 5);
        var vm = new AttributeAllocationViewModel(gameVm);
        var str = vm.Attributes.First(a => a.Name == "Strength");

        str.IncrementCommand.Execute(default).Subscribe();

        vm.PointsToAllocate.Should().Be(4);
    }

    [Fact]
    public void Decrementing_An_Attribute_Should_Restore_PointsToAllocate()
    {
        var gameVm = MakeGameVm(unspentPoints: 5);
        var vm = new AttributeAllocationViewModel(gameVm);
        var str = vm.Attributes.First(a => a.Name == "Strength");

        str.IncrementCommand.Execute(default).Subscribe();
        str.DecrementCommand.Execute(default).Subscribe();

        vm.PointsToAllocate.Should().Be(5);
    }

    [Fact]
    public void All_Six_Attributes_Are_Present()
    {
        var gameVm = MakeGameVm();
        var vm = new AttributeAllocationViewModel(gameVm);

        vm.Attributes.Select(a => a.Name).Should()
            .BeEquivalentTo(["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"]);
    }

    // -- Base values ----------------------------------------------------------

    [Fact]
    public void DisplayValue_Should_Equal_BaseValue_Before_Any_Increments()
    {
        var gameVm = MakeGameVm(str: 14, dex: 12);
        var vm = new AttributeAllocationViewModel(gameVm);

        vm.Attributes.First(a => a.Name == "Strength").DisplayValue.Should().Be(14);
        vm.Attributes.First(a => a.Name == "Dexterity").DisplayValue.Should().Be(12);
    }

    [Fact]
    public void DisplayValue_Should_Reflect_Draft_After_Increment()
    {
        var gameVm = MakeGameVm(str: 10, unspentPoints: 3);
        var vm = new AttributeAllocationViewModel(gameVm);
        var str = vm.Attributes.First(a => a.Name == "Strength");

        str.IncrementCommand.Execute(default).Subscribe();
        str.IncrementCommand.Execute(default).Subscribe();

        str.DisplayValue.Should().Be(12);
        str.Draft.Should().Be(2);
    }

    // -- Guard conditions -----------------------------------------------------

    [Fact]
    public void IncrementCommand_Should_Be_Disabled_When_Budget_Is_Zero()
    {
        var gameVm = MakeGameVm(unspentPoints: 1);
        var vm = new AttributeAllocationViewModel(gameVm);
        var str = vm.Attributes.First(a => a.Name == "Strength");
        var dex = vm.Attributes.First(a => a.Name == "Dexterity");

        str.IncrementCommand.Execute(default).Subscribe();

        bool canIncrement = false;
        dex.IncrementCommand.CanExecute.Take(1).Subscribe(c => canIncrement = c);

        canIncrement.Should().BeFalse();
    }

    [Fact]
    public void DecrementCommand_Should_Be_Disabled_When_Draft_Is_Zero()
    {
        var gameVm = MakeGameVm(unspentPoints: 5);
        var vm = new AttributeAllocationViewModel(gameVm);
        var str = vm.Attributes.First(a => a.Name == "Strength");

        bool canDecrement = false;
        str.DecrementCommand.CanExecute.Take(1).Subscribe(c => canDecrement = c);

        canDecrement.Should().BeFalse();
    }

    // -- Confirm delta map ----------------------------------------------------

    [Fact]
    public void ConfirmCommand_Should_Be_Disabled_When_No_Points_Allocated()
    {
        var gameVm = MakeGameVm(unspentPoints: 5);
        var vm = new AttributeAllocationViewModel(gameVm);

        bool canConfirm = false;
        vm.ConfirmCommand.CanExecute.Take(1).Subscribe(c => canConfirm = c);

        canConfirm.Should().BeFalse();
    }

    [Fact]
    public void ConfirmCommand_Should_Be_Enabled_After_At_Least_One_Increment()
    {
        var gameVm = MakeGameVm(unspentPoints: 5);
        var vm = new AttributeAllocationViewModel(gameVm);
        var str = vm.Attributes.First(a => a.Name == "Strength");

        str.IncrementCommand.Execute(default).Subscribe();

        bool canConfirm = false;
        vm.ConfirmCommand.CanExecute.Take(1).Subscribe(c => canConfirm = c);

        canConfirm.Should().BeTrue();
    }

    // -- Open/close overlay ---------------------------------------------------

    [Fact]
    public void OpenAttributeAllocationCommand_Should_Set_IsAttributeAllocationOpen()
    {
        var gameVm = MakeGameVm(unspentPoints: 3);

        gameVm.OpenAttributeAllocationCommand.Execute(default).Subscribe();

        gameVm.IsAttributeAllocationOpen.Should().BeTrue();
        gameVm.AttributeAllocation.Should().NotBeNull();
    }

    [Fact]
    public void CloseAttributeAllocationCommand_Should_Clear_IsAttributeAllocationOpen()
    {
        var gameVm = MakeGameVm(unspentPoints: 3);

        gameVm.OpenAttributeAllocationCommand.Execute(default).Subscribe();
        gameVm.CloseAttributeAllocationCommand.Execute(default).Subscribe();

        gameVm.IsAttributeAllocationOpen.Should().BeFalse();
    }

    [Fact]
    public void OpenAttributeAllocationCommand_Should_Create_Fresh_Allocation_Vm_Each_Time()
    {
        var gameVm = MakeGameVm(unspentPoints: 3);

        gameVm.OpenAttributeAllocationCommand.Execute(default).Subscribe();
        var first = gameVm.AttributeAllocation;
        first!.Attributes.First().IncrementCommand.Execute(default).Subscribe();

        gameVm.CloseAttributeAllocationCommand.Execute(default).Subscribe();
        gameVm.OpenAttributeAllocationCommand.Execute(default).Subscribe();
        var second = gameVm.AttributeAllocation;

        second.Should().NotBeSameAs(first);
        second!.PointsToAllocate.Should().Be(3);
    }
}
