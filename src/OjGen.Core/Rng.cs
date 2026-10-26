// Rng.cs - Bộ sinh số ngẫu nhiên MT19937-64 tự cài đặt.
//
// Bản C++ dùng std::mt19937_64 cùng các hàm chia khoảng/xáo trộn tự viết để kết quả
// không phụ thuộc thư viện chuẩn. Bản C# cài lại ĐÚNG thuật toán đó nên CÙNG seed +
// CÙNG cấu hình cho CÙNG bộ test, giống từng byte, với bản C++ (đã đối chiếu bằng kiểm thử).
namespace OjGen.Core;

public sealed class Rng
{
    private const int N = 312;
    private const int M = 156;
    private const ulong MatrixA = 0xB5026F5AA96619E9UL;
    private const ulong UpperMask = 0xFFFFFFFF80000000UL;
    private const ulong LowerMask = 0x7FFFFFFFUL;

    private readonly ulong[] _mt = new ulong[N];
    private int _idx;

    public Rng(ulong seed)
    {
        _mt[0] = seed;
        for (int i = 1; i < N; i++)
            _mt[i] = unchecked(6364136223846793005UL * (_mt[i - 1] ^ (_mt[i - 1] >> 62)) + (ulong)i);
        _idx = N;
    }

    private void Twist()
    {
        int k = 0;
        for (; k < N - M; k++)
        {
            ulong x = (_mt[k] & UpperMask) | (_mt[k + 1] & LowerMask);
            _mt[k] = _mt[k + M] ^ (x >> 1) ^ ((x & 1UL) != 0 ? MatrixA : 0UL);
        }
        for (; k < N - 1; k++)
        {
            ulong x = (_mt[k] & UpperMask) | (_mt[k + 1] & LowerMask);
            _mt[k] = _mt[k + (M - N)] ^ (x >> 1) ^ ((x & 1UL) != 0 ? MatrixA : 0UL);
        }
        ulong xl = (_mt[N - 1] & UpperMask) | (_mt[0] & LowerMask);
        _mt[N - 1] = _mt[M - 1] ^ (xl >> 1) ^ ((xl & 1UL) != 0 ? MatrixA : 0UL);
        _idx = 0;
    }

    public ulong Next()
    {
        if (_idx >= N) Twist();
        ulong y = _mt[_idx++];
        y ^= (y >> 29) & 0x5555555555555555UL;
        y ^= (y << 17) & 0x71D67FFFEDA60000UL;
        y ^= (y << 37) & 0xFFF7EEE000000000UL;
        y ^= y >> 43;
        return y;
    }

    /// <summary>Số nguyên đều trong [0, bound) với bound &gt; 0 (rejection sampling, không lệch).</summary>
    public ulong Below(ulong bound)
    {
        if (bound == 0) return Next();
        ulong limit = ulong.MaxValue - (ulong.MaxValue % bound);
        ulong x;
        do x = Next(); while (x >= limit);
        return x % bound;
    }

    /// <summary>Số nguyên đều trong đoạn [lo, hi], an toàn trên toàn bộ phạm vi long.</summary>
    public long Range(long lo, long hi)
    {
        if (lo > hi) (lo, hi) = (hi, lo);
        ulong span = Util.SpanOf(lo, hi);
        if (span == ulong.MaxValue) return unchecked((long)Next());
        ulong off = Below(span + 1);
        return unchecked((long)((ulong)lo + off));
    }

    /// <summary>true với xác suất percent%.</summary>
    public bool Chance(int percent) => (int)Below(100) < percent;

    public void Shuffle<T>(IList<T> v)
    {
        for (int i = v.Count; i > 1; i--)
        {
            int j = (int)Below((ulong)i);
            (v[i - 1], v[j]) = (v[j], v[i - 1]);
        }
    }
}
