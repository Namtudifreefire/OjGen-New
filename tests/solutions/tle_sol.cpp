// Lời giải CỐ Ý CHẠY QUÁ GIỜ khi tổng là số lẻ (dùng để kiểm thử ojgen).
#include <bits/stdc++.h>
using namespace std;
int main() {
    long long x, s = 0;
    while (cin >> x) s += x;
    if (s % 2 != 0) { volatile unsigned long long k = 0; for (;;) ++k; }
    cout << s << "\n";
}
