// Lời giải mẫu cho dạng custom (định dạng mặc định: n số trên một dòng).
// In tổng, giá trị nhỏ nhất và lớn nhất của các số.
#include <bits/stdc++.h>
using namespace std;
int main() {
    long long x, mn = LLONG_MAX, mx = LLONG_MIN;
    __int128 sum = 0;
    int cnt = 0;
    while (cin >> x) { sum += x; mn = min(mn, x); mx = max(mx, x); ++cnt; }
    // In __int128
    string s; bool neg = sum < 0; __int128 v = sum;
    if (v == 0) s = "0";
    while (v != 0) { int d = (int)(v % 10); s += char('0' + (d < 0 ? -d : d)); v /= 10; }
    if (neg) s += '-';
    reverse(s.begin(), s.end());
    cout << s << " " << mn << " " << mx << "\n";
}
