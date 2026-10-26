// Lời giải mẫu cho dạng queries: cây Fenwick.
//   1 i x : gán a[i] = x
//   2 l r : in tổng a[l..r]
#include <bits/stdc++.h>
using namespace std;
int n;
vector<long long> bit;
void add(int i, long long v) { for (; i <= n; i += i & -i) bit[i] += v; }
long long sum(int i) { long long s = 0; for (; i > 0; i -= i & -i) s += bit[i]; return s; }
int main() {
    ios::sync_with_stdio(false);
    cin.tie(nullptr);
    int q;
    cin >> n >> q;
    vector<long long> a(n + 1);
    bit.assign(n + 1, 0);
    for (int i = 1; i <= n; ++i) { cin >> a[i]; add(i, a[i]); }
    while (q--) {
        int t; long long x, y;
        cin >> t >> x >> y;
        if (t == 1) { add((int)x, y - a[x]); a[x] = y; }
        else cout << sum((int)y) - sum((int)x - 1) << "\n";
    }
}
