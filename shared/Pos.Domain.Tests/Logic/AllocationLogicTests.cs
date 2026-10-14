namespace Pos.Domain.Logic;

public sealed class AllocationLogicTests
{
    // 1,000 を 76,000 / 4,000 / 1,100 で按分し、剰余の 1 は端数が最大の配送料に配る
    [Fact]
    public void AllocateByLargestRemainder()
    {
        // Arrange
        decimal[] weights = [76000m, 4000m, 1100m];

        // Act
        var result = AllocationLogic.Allocate(1000m, weights);

        // Assert
        Assert.Equal([937m, 49m, 14m], result);
    }

    // 剰余が複数のときは端数の大きい順に配る (ポイント 5,000 の按分: 4,685 / 247 / 68)
    [Fact]
    public void AllocateMultipleRemainders()
    {
        // Arrange
        decimal[] weights = [75063m, 3951m, 1086m];

        // Act
        var result = AllocationLogic.Allocate(5000m, weights);

        // Assert
        Assert.Equal([4685m, 247m, 68m], result);
    }

    // 端数が同値なら先頭 (lineNo の小さい方) から配る
    [Fact]
    public void AllocateTiesInOrder()
    {
        // Arrange
        decimal[] weights = [1m, 1m, 1m];

        // Act
        var result = AllocationLogic.Allocate(2m, weights);

        // Assert
        Assert.Equal([1m, 1m, 0m], result);
    }

    // 割り切れるときは端数配分なし
    [Fact]
    public void AllocateExact()
    {
        // Arrange
        decimal[] weights = [200m, 300m];

        // Act
        var result = AllocationLogic.Allocate(50m, weights);

        // Assert
        Assert.Equal([20m, 30m], result);
    }

    // 重みの合計が 0 (全額値引など) のときは全て 0
    [Fact]
    public void AllocateZeroWeights()
    {
        // Arrange
        decimal[] weights = [0m, 0m];

        // Act
        var result = AllocationLogic.Allocate(0m, weights);

        // Assert
        Assert.Equal([0m, 0m], result);
    }

    // 按分する先がなければ空
    [Fact]
    public void AllocateEmpty()
    {
        // Act
        var result = AllocationLogic.Allocate(100m, []);

        // Assert
        Assert.Empty(result);
    }
}
