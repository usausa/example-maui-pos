namespace Pos.Domain.Sales;

public sealed class AllocationTests
{
    // §4.6: 1,000 を 76,000 / 4,000 / 1,100 で按分。剰余 1 は raw の端数が最大の配送料へ
    [Fact]
    public void AllocateByLargestRemainder()
    {
        decimal[] weights = [76000m, 4000m, 1100m];

        var result = Allocation.Allocate(1000m, weights);

        Assert.Equal([937m, 49m, 14m], result);
    }

    // 剰余が複数のときは端数の大きい順 (§4.6 のポイント按分: 4,685 / 247 / 68)
    [Fact]
    public void AllocateMultipleRemainders()
    {
        decimal[] weights = [75063m, 3951m, 1086m];

        var result = Allocation.Allocate(5000m, weights);

        Assert.Equal([4685m, 247m, 68m], result);
    }

    // 端数が同値なら先頭 (lineNo の小さい方) から配る
    [Fact]
    public void AllocateTiesInOrder()
    {
        decimal[] weights = [1m, 1m, 1m];

        var result = Allocation.Allocate(2m, weights);

        Assert.Equal([1m, 1m, 0m], result);
    }

    // 割り切れるときは端数配分なし
    [Fact]
    public void AllocateExact()
    {
        decimal[] weights = [200m, 300m];

        var result = Allocation.Allocate(50m, weights);

        Assert.Equal([20m, 30m], result);
    }

    // 重みの合計が 0 (全額値引など) のときは全て 0
    [Fact]
    public void AllocateZeroWeights()
    {
        decimal[] weights = [0m, 0m];

        var result = Allocation.Allocate(0m, weights);

        Assert.Equal([0m, 0m], result);
    }

    [Fact]
    public void AllocateEmpty()
    {
        var result = Allocation.Allocate(100m, []);

        Assert.Empty(result);
    }
}
