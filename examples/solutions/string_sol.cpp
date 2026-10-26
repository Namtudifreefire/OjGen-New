// Lời giải mẫu cho dạng string: in xâu đảo ngược và số ký tự khác nhau.
// (Token cuối cùng trong input là xâu, nên chạy được cả khi printLen = 1.)
#include <bits/stdc++.h>
using namespace std;
int main() {
    string tok, s;
    while (cin >> tok) s = tok;
    set<char> d(s.begin(), s.end());
    reverse(s.begin(), s.end());
    cout << s << "\n" << d.size() << "\n";
}
