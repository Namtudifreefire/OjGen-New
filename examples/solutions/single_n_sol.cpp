// Lời giải mẫu cho dạng single_n: in tổng các chữ số của n (bỏ dấu).
#include <bits/stdc++.h>
using namespace std;
int main() {
    string s;
    cin >> s;
    long long sum = 0;
    for (char c : s)
        if (isdigit((unsigned char)c)) sum += c - '0';
    cout << sum << "\n";
}
