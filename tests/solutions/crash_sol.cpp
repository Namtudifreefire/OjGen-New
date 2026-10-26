// Lời giải CỐ Ý CRASH với một số test (dùng để kiểm thử ojgen):
// tổng chia hết cho 4 -> truy cập con trỏ null; chia 4 dư 1 -> gọi abort().
#include <bits/stdc++.h>
using namespace std;
int main() {
    long long x, s = 0;
    while (cin >> x) s += x;
    if (s % 4 == 0) { volatile int* p = nullptr; *p = 1; }
    if (s % 4 == 1) abort();
    cout << s << "\n";
}
