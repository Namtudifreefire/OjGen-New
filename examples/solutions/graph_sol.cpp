// Lời giải mẫu cho dạng graph: số thành phần liên thông (coi cạnh là vô hướng)
// và tổng trọng số (nếu có trọng số).
#include <bits/stdc++.h>
using namespace std;
vector<int> p;
int f(int x) { while (p[x] != x) x = p[x] = p[p[x]]; return x; }
int main() {
    int n, m;
    cin >> n >> m;
    string line;
    getline(cin, line);
    p.resize(n + 1);
    iota(p.begin(), p.end(), 0);
    int comps = n;
    long long total = 0;
    for (int i = 0; i < m; ++i) {
        getline(cin, line);
        stringstream ss(line);
        long long u, v, w = 0;
        ss >> u >> v;
        if (ss >> w) total += w;
        int a = f((int)u), b = f((int)v);
        if (a != b) { p[a] = b; --comps; }
    }
    cout << comps << " " << total << "\n";
}
