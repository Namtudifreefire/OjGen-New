// Lời giải mẫu cho dạng two_ints: in a + b (dùng __int128 để không tràn số).
#include <bits/stdc++.h>
using namespace std;
string toStr(__int128 v) {
    if (v == 0) return "0";
    bool neg = v < 0;
    string s;
    while (v != 0) { int d = (int)(v % 10); s += char('0' + (d < 0 ? -d : d)); v /= 10; }
    if (neg) s += '-';
    reverse(s.begin(), s.end());
    return s;
}
int main() {
    long long a, b;
    cin >> a >> b;
    cout << toStr((__int128)a + b) << "\n";
}
