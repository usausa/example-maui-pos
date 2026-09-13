namespace Pos.Server.SampleData;

// シードで再現できる簡単な乱数 (xorshift64*)。暗号用途ではないので System.Random の代わりに自前で持つ
internal sealed class SampleRandom
{
    private ulong state;

    public SampleRandom(int seed)
    {
        state = 0x9E3779B97F4A7C15UL ^ (uint)seed;
        if (state == 0)
        {
            state = 1;
        }
    }

    // 0 以上 max 未満
    public int Next(int max)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(max);

        state ^= state >> 12;
        state ^= state << 25;
        state ^= state >> 27;
        var value = state * 0x2545F4914F6CDD1DUL;
        return (int)(value % (ulong)max);
    }

    // min 以上 max 未満
    public int Next(int min, int max) => min + Next(max - min);
}
