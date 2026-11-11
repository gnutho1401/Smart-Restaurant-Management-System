using SmartRestaurant.Core.Entities;
using SmartRestaurant.Core.Enums;
using SmartRestaurant.Core.Exceptions;
using Xunit;

namespace SmartRestaurant.Tests;

public class DiningTableDfaTests
{
    [Theory]
    [InlineData(TableStatus.Available, TableStatus.Occupied, true)]
    [InlineData(TableStatus.Available, TableStatus.Reserved, true)]
    [InlineData(TableStatus.Reserved, TableStatus.Occupied, true)]
    [InlineData(TableStatus.Reserved, TableStatus.Available, true)]
    [InlineData(TableStatus.Occupied, TableStatus.Cleaning, true)]
    [InlineData(TableStatus.Cleaning, TableStatus.Available, true)]
    [InlineData(TableStatus.Available, TableStatus.Cleaning, false)]
    [InlineData(TableStatus.Occupied, TableStatus.Available, false)]
    [InlineData(TableStatus.Occupied, TableStatus.Reserved, false)]
    [InlineData(TableStatus.Cleaning, TableStatus.Occupied, false)]
    public void CanTransitionTo_ValidatesPermittedTransitions(TableStatus current, TableStatus target, bool expectedPermitted)
    {
        // Arrange
        var table = new DiningTable
        {
            TableId = 1,
            TableName = "Bàn 1",
            Status = current
        };

        // Act
        bool canTransition = table.CanTransitionTo(target);

        // Assert
        Assert.Equal(expectedPermitted, canTransition);
    }

    [Fact]
    public void TransitionTo_DirectOccupiedToAvailable_ThrowsInvalidStatusTransitionException()
    {
        // Arrange
        var table = new DiningTable
        {
            TableId = 2,
            TableName = "Bàn 2",
            Status = TableStatus.Occupied
        };

        // Act & Assert
        Assert.Throws<InvalidStatusTransitionException>(() => table.TransitionTo(TableStatus.Available));
        Assert.Equal(TableStatus.Occupied, table.Status); // State must remain unchanged
    }

    [Fact]
    public void TransitionTo_SameStatus_IsIdempotentNoOp()
    {
        // Arrange
        var table = new DiningTable
        {
            TableId = 3,
            TableName = "Bàn 3",
            Status = TableStatus.Occupied
        };

        // Act
        table.TransitionTo(TableStatus.Occupied);

        // Assert
        Assert.Equal(TableStatus.Occupied, table.Status);
    }
}

