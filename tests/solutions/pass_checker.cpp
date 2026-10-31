#include <fstream>
#include <iterator>
#include <string>

int main(int argc, char** argv) {
    if (argc != 4) return 2;
    std::ifstream input(argv[1], std::ios::binary);
    std::ifstream output(argv[2], std::ios::binary);
    std::ifstream answer(argv[3], std::ios::binary);
    if (!input || !output || !answer) return 3;
    std::string inputText((std::istreambuf_iterator<char>(input)), {});
    std::string outputText((std::istreambuf_iterator<char>(output)), {});
    std::string answerText((std::istreambuf_iterator<char>(answer)), {});
    return !inputText.empty() && outputText == answerText ? 0 : 1;
}
