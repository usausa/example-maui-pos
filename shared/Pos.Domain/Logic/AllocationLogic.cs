namespace Pos.Domain.Logic;

// 最大剰余法 (Largest remainder) の按分。金額は整数 (JPY) 前提
public static class AllocationLogic
{
    // total を weights に比例して整数で配る。端数は raw − floor の大きい順、同値は先頭から
    public static decimal[] Allocate(decimal total, IReadOnlyList<decimal> weights)
    {
        var count = weights.Count;
        var result = new decimal[count];
        if (count == 0)
        {
            return result;
        }

        var weightTotal = 0m;
        foreach (var weight in weights)
        {
            weightTotal += weight;
        }

        if (weightTotal == 0m)
        {
            return result;
        }

        var remainders = new decimal[count];
        var allocated = 0m;
        for (var i = 0; i < count; i++)
        {
            var raw = total * weights[i] / weightTotal;
            var floor = Math.Floor(raw);
            result[i] = floor;
            remainders[i] = raw - floor;
            allocated += floor;
        }

        var remaining = (int)(total - allocated);
        if (remaining <= 0)
        {
            return result;
        }

        var order = new int[count];
        for (var i = 0; i < count; i++)
        {
            order[i] = i;
        }

        // 安定ソート: 剰余の降順、同値はインデックス順
        Array.Sort(order, (x, y) =>
        {
            var compare = remainders[y].CompareTo(remainders[x]);
            return compare != 0 ? compare : x.CompareTo(y);
        });

        for (var i = 0; i < remaining && i < count; i++)
        {
            result[order[i]] += 1m;
        }

        return result;
    }
}
