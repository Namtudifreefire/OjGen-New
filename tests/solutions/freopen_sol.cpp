// Lời giải dùng freopen kiểu thi Việt Nam, bọc bằng #ifndef OJGEN nên chạy được với ojgen.
#include <bits/stdc++.h>
using namespace std;
int main() {
#ifndef OJGEN
    freopen("BAI1.INP", "r", stdin);
    freopen("BAI1.OUT", "w", stdout);
#endif
    long long x, s = 0;
    while (cin >> x) s += x;
    cout << s << "\n";
}
