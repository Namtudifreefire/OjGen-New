# Authoring workspace tools

These tools extend the existing `.ojproject`, test plan, package audit, and solution matrix workflows.

## Reusable presets — item 25

GUI: **Tệp → Lưu preset ra đề…** and **Dùng preset ra đề…**. The default preset folder is the user's OjGen data directory under `Presets`.

CLI:

```powershell
ojgen --preset-save "graph stress" --problem graph --tests 80 --seed 2026 --connected 1 --weighted 1 --preset-dir .\presets
ojgen --preset-list --preset-dir .\presets
ojgen --problem graph --preset ".\presets\graph stress.ojpreset.json" --out .\output\graph-stress
```

A preset can contain a test plan, generator parameters, required coverage labels, fixed/random seed policy, validator, checker, candidate solution, contract suite, and checker timeout. A fixed seed is reused unless `--seed` overrides it. A preset with audit tools automatically enables release audit during generation.

## Judge data interchange — items 26–27

Import existing flat ZIPs/folders with `ojgen --import-tests SOURCE --out NEW_DIRECTORY`; the importer recognizes `.inp`/`.in` inputs and `.out`/`.ans` answers. It does not recursively unpack archives nested inside another ZIP or interpret every judge's metadata. Export only the test data with:

```powershell
ojgen --export-tests .\tests --format dmoj --out .\dmoj-testdata.zip
ojgen --export-tests .\tests --format domjudge --out .\domjudge-testdata.zip
ojgen --export-tests .\tests --format polygon --out .\polygon-testdata.zip
ojgen --format-info dmoj
```

These archives are test-data interchange files; they do not convert statements, scoring, validators, checkers, or full judge metadata. The Polygon/Testlib archive is deliberately labelled as non-native. OjGen's Kattis export remains the `2025-09` release candidate and says so in the UI and package; verify support at the destination before importing. The upstream PPF page lists `2025-09` as a release candidate and `legacy`/`legacy-icpc` as current stable specifications: [PPF overview](https://icpc.io/problem-package-format/), [PPF changelog](https://icpc.io/problem-package-format/spec/changelog.html).

## Contest workspaces — item 28

Create a JSON file like this and run `ojgen --contest contest.ojcontest.json`. Relative project paths resolve from the JSON file's folder.

```json
{
  "Version": 1,
  "Name": "Practice contest",
  "DurationMinutes": 180,
  "Problems": [
    { "Code": "A", "ProjectPath": "A.ojproject", "PackagePath": "packages/A", "Order": 1, "TimeLimitMinutes": 180 },
    { "Code": "B", "ProjectPath": "B.ojproject", "PackagePath": "packages/B.zip", "Order": 2, "TimeLimitMinutes": 180 }
  ]
}
```

Validation checks duration, duplicate codes/order, project existence, supported generator IDs, and project-code mismatches. Add `PackagePath` for every problem and pass `--contest-export contest.zip` to combine package files under `problems/<code>/`. This is an aggregate archive; it does not rewrite judge metadata. The command produces a JSON report with `--json-report FILE`.

## Local problem library and comparison — items 29–30

```powershell
ojgen --library .\problems --library-search "graph"
ojgen --library-clone .\problems\old.ojproject --out .\problems\new.ojproject
ojgen --compare-problem .\release-a --against .\release-b
```

The library searches `.ojproject`, `.ojdraft.json`, and packaged `.ojproblem.json` files. Cloning a project clears its previous generated-test history while retaining the editable setup. Version comparison reports added, removed, and changed files and flags changes that may affect seed reproducibility.

## Statement assets and translations — items 32–33

`ojgen --check-resources FILE.ojdraft.json` finds missing local Markdown/image references, unsafe paths, and external URLs that could not be checked offline.

Link localized drafts using a manifest:

```json
{
  "Version": 1,
  "SourceStatement": "problem.ojdraft.json",
  "Translations": [
    { "Locale": "en-US", "StatementPath": "problem.en.ojdraft.json" }
  ]
}
```

Run `ojgen --check-translations translations.ojtranslation.json`. It checks required statement sections, equal time/memory limits, code-block sample parity, duplicate locales, and warns when numeric constraints differ. It does not translate text or judge linguistic accuracy.

## Headless CI — item 34

`ojgen --ci-project path/to/problem.ojproject --json-report audit.json` audits the last generated test set from a saved GUI project. It copies tests into a temporary directory so audit files do not alter the source test folder; it exits 0 when clean, 1 for warnings, and 2 for errors. CI should have the same generator plugins, compilers, runtimes, validator/checker files, and test assets available as the authoring machine.

Example GitHub Actions step:

```yaml
- name: Audit problem package
  run: dotnet run --project src/OjGen.Cli -- --ci-project problems/graph.ojproject --json-report audit.json
```

## Multi-solution verdict matrix — item 35

The GUI solution matrix now counts AC, WA, TLE, and runtime/start/output errors for every candidate against the selected reference and shows per-test verdicts in each row's tooltip. Output equality is token-normalized; problems with multiple valid outputs must use a checker in release audit instead. A candidate matching the reference does not prove the reference solution itself is correct.

