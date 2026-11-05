using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Tests.Infrastructure;

namespace RealmEngine.Shared.Tests.Models;

[Trait("Category", "Unit")]
/// <summary>
/// Comprehensive tests for TraitValue class.
/// Target: 81% -> 100% coverage.
/// </summary>
public class TraitValueTests
{
    #region Constructor Tests

    [Fact]
    public void TraitValue_Should_Initialize_With_Default_Constructor()
    {
        // Act
        var traitValue = new TraitValue();

        // Assert
        traitValue.Value.Should().BeNull();
        traitValue.Type.Should().Be(default(TraitType));
    }

    [Fact]
    public void TraitValue_Should_Initialize_With_Parameterized_Constructor()
    {
        // Act
        var traitValue = new TraitValue(42, TraitType.Number);

        // Assert
        traitValue.Value.Should().Be(42);
        traitValue.Type.Should().Be(TraitType.Number);
    }

    [Fact]
    public void TraitValue_Should_Accept_Null_Value_In_Constructor()
    {
        // Act
        var traitValue = new TraitValue(null, TraitType.String);

        // Assert
        traitValue.Value.Should().BeNull();
        traitValue.Type.Should().Be(TraitType.String);
    }

    #endregion

    #region AsInt Tests

    [Fact]
    public void AsInt_Should_Return_Zero_For_Null_Value()
    {
        // Arrange
        var traitValue = new TraitValue(null, TraitType.Number);

        // Act
        var result = traitValue.AsInt();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void AsInt_Should_Convert_Integer_Value()
    {
        // Arrange
        var traitValue = new TraitValue(42, TraitType.Number);

        // Act
        var result = traitValue.AsInt();

        // Assert
        result.Should().Be(42);
    }

    [Fact]
    public void AsInt_Should_Convert_Double_To_Integer()
    {
        // Arrange
        var traitValue = new TraitValue(42.7, TraitType.Number);

        // Act
        var result = traitValue.AsInt();

        // Assert - Convert.ToInt32 rounds, so 42.7 becomes 43
        result.Should().Be(43);
    }

    [Fact]
    public void AsInt_Should_Convert_String_Number_To_Integer()
    {
        // Arrange
        var traitValue = new TraitValue("123", TraitType.String);

        // Act
        var result = traitValue.AsInt();

        // Assert
        result.Should().Be(123);
    }

    [Fact]
    public void AsInt_Should_Convert_Boolean_True_To_One()
    {
        // Arrange
        var traitValue = new TraitValue(true, TraitType.Boolean);

        // Act
        var result = traitValue.AsInt();

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public void AsInt_Should_Convert_Boolean_False_To_Zero()
    {
        // Arrange
        var traitValue = new TraitValue(false, TraitType.Boolean);

        // Act
        var result = traitValue.AsInt();

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region AsDouble Tests

    [Fact]
    public void AsDouble_Should_Return_Zero_For_Null_Value()
    {
        // Arrange
        var traitValue = new TraitValue(null, TraitType.Number);

        // Act
        var result = traitValue.AsDouble();

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void AsDouble_Should_Convert_Double_Value()
    {
        // Arrange
        var traitValue = new TraitValue(42.5, TraitType.Number);

        // Act
        var result = traitValue.AsDouble();

        // Assert
        result.Should().Be(42.5);
    }

    [Fact]
    public void AsDouble_Should_Convert_Integer_To_Double()
    {
        // Arrange
        var traitValue = new TraitValue(42, TraitType.Number);

        // Act
        var result = traitValue.AsDouble();

        // Assert
        result.Should().Be(42.0);
    }

    [Fact]
    [UseCulture("en-US")]
    public void AsDouble_Should_Convert_String_Number_To_Double()
    {
        // Arrange
        var traitValue = new TraitValue("123.45", TraitType.String);

        // Act
        var result = traitValue.AsDouble();

        // Assert
        result.Should().Be(123.45);
    }

    [Fact]
    public void AsDouble_Should_Preserve_Decimal_Precision()
    {
        // Arrange
        var traitValue = new TraitValue(3.14159, TraitType.Number);

        // Act
        var result = traitValue.AsDouble();

        // Assert
        result.Should().Be(3.14159);
    }

    #endregion

    #region AsString Tests

    [Fact]
    public void AsString_Should_Return_Empty_String_For_Null_Value()
    {
        // Arrange
        var traitValue = new TraitValue(null, TraitType.String);

        // Act
        var result = traitValue.AsString();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void AsString_Should_Return_String_Value()
    {
        // Arrange
        var traitValue = new TraitValue("Hello", TraitType.String);

        // Act
        var result = traitValue.AsString();

        // Assert
        result.Should().Be("Hello");
    }

    [Fact]
    public void AsString_Should_Convert_Integer_To_String()
    {
        // Arrange
        var traitValue = new TraitValue(42, TraitType.Number);

        // Act
        var result = traitValue.AsString();

        // Assert
        result.Should().Be("42");
    }

    [Fact]
    [UseCulture("en-US")]
    public void AsString_Should_Convert_Double_To_String()
    {
        // Arrange
        var traitValue = new TraitValue(42.5, TraitType.Number);

        // Act
        var result = traitValue.AsString();

        // Assert
        result.Should().Be("42.5");
    }

    [Fact]
    public void AsString_Should_Convert_Boolean_To_String()
    {
        // Arrange
        var traitValue = new TraitValue(true, TraitType.Boolean);

        // Act
        var result = traitValue.AsString();

        // Assert
        result.Should().Be("True");
    }

    #endregion

    #region AsBool Tests

    [Fact]
    public void AsBool_Should_Return_False_For_Null_Value()
    {
        // Arrange
        var traitValue = new TraitValue(null, TraitType.Boolean);

        // Act
        var result = traitValue.AsBool();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AsBool_Should_Return_True_For_True_Value()
    {
        // Arrange
        var traitValue = new TraitValue(true, TraitType.Boolean);

        // Act
        var result = traitValue.AsBool();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AsBool_Should_Return_False_For_False_Value()
    {
        // Arrange
        var traitValue = new TraitValue(false, TraitType.Boolean);

        // Act
        var result = traitValue.AsBool();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AsBool_Should_Convert_Nonzero_Integer_To_True()
    {
        // Arrange
        var traitValue = new TraitValue(1, TraitType.Number);

        // Act
        var result = traitValue.AsBool();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AsBool_Should_Convert_Zero_To_False()
    {
        // Arrange
        var traitValue = new TraitValue(0, TraitType.Number);

        // Act
        var result = traitValue.AsBool();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AsBool_Should_Convert_String_True_To_Boolean()
    {
        // Arrange
        var traitValue = new TraitValue("true", TraitType.String);

        // Act
        var result = traitValue.AsBool();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AsBool_Should_Convert_String_False_To_Boolean()
    {
        // Arrange
        var traitValue = new TraitValue("false", TraitType.String);

        // Act
        var result = traitValue.AsBool();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region AsStringList Tests

    [Fact]
    public void AsStringList_Should_Return_Empty_List_For_Null_Value()
    {
        // Arrange
        var traitValue = new TraitValue(null, TraitType.StringArray);

        // Act
        var result = traitValue.AsStringList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void AsStringList_Should_Return_List_Of_Strings()
    {
        // Arrange
        var list = new List<string> { "one", "two", "three" };
        var traitValue = new TraitValue(list, TraitType.StringArray);

        // Act
        var result = traitValue.AsStringList();

        // Assert
        result.Should().BeEquivalentTo(["one", "two", "three"]);
    }

    [Fact]
    public void AsStringList_Should_Convert_String_Array_To_List()
    {
        // Arrange
        var array = new[] { "red", "green", "blue" };
        var traitValue = new TraitValue(array, TraitType.StringArray);

        // Act
        var result = traitValue.AsStringList();

        // Assert
        result.Should().BeEquivalentTo(["red", "green", "blue"]);
    }

    [Fact]
    public void AsStringList_Should_Return_Empty_List_For_Non_String_Array()
    {
        // Arrange
        var traitValue = new TraitValue(42, TraitType.Number);

        // Act
        var result = traitValue.AsStringList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void AsStringList_Should_Return_Empty_List_For_Integer_List()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3 };
        var traitValue = new TraitValue(list, TraitType.NumberArray);

        // Act
        var result = traitValue.AsStringList();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region AsIntList Tests

    [Fact]
    public void AsIntList_Should_Return_Empty_List_For_Null_Value()
    {
        // Arrange
        var traitValue = new TraitValue(null, TraitType.NumberArray);

        // Act
        var result = traitValue.AsIntList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void AsIntList_Should_Return_List_Of_Integers()
    {
        // Arrange
        var list = new List<int> { 1, 2, 3, 4, 5 };
        var traitValue = new TraitValue(list, TraitType.NumberArray);

        // Act
        var result = traitValue.AsIntList();

        // Assert
        result.Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void AsIntList_Should_Convert_Integer_Array_To_List()
    {
        // Arrange
        var array = new[] { 10, 20, 30 };
        var traitValue = new TraitValue(array, TraitType.NumberArray);

        // Act
        var result = traitValue.AsIntList();

        // Assert
        result.Should().BeEquivalentTo(new[] { 10, 20, 30 });
    }

    [Fact]
    public void AsIntList_Should_Return_Empty_List_For_Non_Int_Array()
    {
        // Arrange
        var traitValue = new TraitValue("not a list", TraitType.String);

        // Act
        var result = traitValue.AsIntList();

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void AsIntList_Should_Return_Empty_List_For_String_List()
    {
        // Arrange
        var list = new List<string> { "one", "two", "three" };
        var traitValue = new TraitValue(list, TraitType.StringArray);

        // Act
        var result = traitValue.AsIntList();

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public void TraitValue_Should_Handle_Empty_String()
    {
        // Arrange
        var traitValue = new TraitValue("", TraitType.String);

        // Act & Assert
        traitValue.AsString().Should().BeEmpty();
    }

    [Fact]
    public void TraitValue_Should_Handle_Negative_Numbers()
    {
        // Arrange
        var traitValue = new TraitValue(-42, TraitType.Number);

        // Act & Assert
        traitValue.AsInt().Should().Be(-42);
        traitValue.AsDouble().Should().Be(-42.0);
        traitValue.AsString().Should().Be("-42");
    }

    [Fact]
    public void TraitValue_Should_Handle_Large_Numbers()
    {
        // Arrange
        var traitValue = new TraitValue(1000000, TraitType.Number);

        // Act & Assert
        traitValue.AsInt().Should().Be(1000000);
        traitValue.AsDouble().Should().Be(1000000.0);
    }

    [Fact]
    public void TraitValue_Should_Handle_Empty_Lists()
    {
        // Arrange
        var emptyStringList = new TraitValue(new List<string>(), TraitType.StringArray);
        var emptyIntList = new TraitValue(new List<int>(), TraitType.NumberArray);

        // Act & Assert
        emptyStringList.AsStringList().Should().BeEmpty();
        emptyIntList.AsIntList().Should().BeEmpty();
    }

    [Fact]
    public void TraitValue_Should_Preserve_Type_Information()
    {
        // Arrange
        var numberTrait = new TraitValue(42, TraitType.Number);
        var stringTrait = new TraitValue("hello", TraitType.String);
        var boolTrait = new TraitValue(true, TraitType.Boolean);

        // Assert
        numberTrait.Type.Should().Be(TraitType.Number);
        stringTrait.Type.Should().Be(TraitType.String);
        boolTrait.Type.Should().Be(TraitType.Boolean);
    }

    [Fact]
    public void TraitValue_Should_Allow_Value_Reassignment()
    {
        // Arrange
        var traitValue = new TraitValue(42, TraitType.Number);

        // Act
        traitValue.Value = 100;

        // Assert
        traitValue.AsInt().Should().Be(100);
    }

    [Fact]
    public void TraitValue_Should_Allow_Type_Reassignment()
    {
        // Arrange
        var traitValue = new TraitValue(42, TraitType.Number);

        // Act
        traitValue.Type = TraitType.String;

        // Assert
        traitValue.Type.Should().Be(TraitType.String);
    }

    [Fact]
    public void TraitValue_Should_Support_All_TraitTypes()
    {
        // Arrange & Act
        var number = new TraitValue(42, TraitType.Number);
        var text = new TraitValue("test", TraitType.String);
        var flag = new TraitValue(true, TraitType.Boolean);
        var stringArr = new TraitValue(new List<string>(), TraitType.StringArray);
        var numberArr = new TraitValue(new List<int>(), TraitType.NumberArray);

        // Assert
        number.Type.Should().Be(TraitType.Number);
        text.Type.Should().Be(TraitType.String);
        flag.Type.Should().Be(TraitType.Boolean);
        stringArr.Type.Should().Be(TraitType.StringArray);
        numberArr.Type.Should().Be(TraitType.NumberArray);
    }

    #endregion
}
