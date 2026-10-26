// ConfigModel.cs - Mô hình dữ liệu của form tham số trên giao diện (không phụ thuộc WPF, nên kiểm thử được).
//
// Giao diện chỉ làm việc: hiển thị controls theo Problem.Params, chuyển văn bản người dùng gõ vào các hàm Edit*,
// rồi đọc lại IsVisible / FieldError / Validate / DisplayValue để cập nhật màn hình.
namespace OjGen.Core;

public sealed class ConfigModel
{
    private readonly Dictionary<string, string> _fieldErrors = new();

    public Problem Problem { get; }
    /// <summary>Cấu hình hiện tại (gồm cả tham số đang ẩn).</summary>
    public Config Config { get; }

    public ConfigModel(Problem problem)
    {
        Problem = problem;
        Config = problem.DefaultConfig();
    }

    public ParamSpec? Spec(string key) => Problem.Params.FirstOrDefault(p => p.Key == key);

    public bool IsVisible(ParamSpec spec) => spec.VisibleWhen == null || spec.VisibleWhen(Config);

    /// <summary>Lỗi định dạng của ô nhập (vd "abc" không phải số nguyên); null nếu ổn.</summary>
    public string? FieldError(string key) => _fieldErrors.TryGetValue(key, out var e) ? e : null;

    /// <summary>Văn bản hiển thị của một tham số số nguyên/văn bản/bool theo giá trị hiện tại.</summary>
    public string DisplayValue(ParamSpec spec) => spec.Type switch
    {
        ParamType.Text => Config.T(spec.Key),
        _ => Util.LL(Config.I(spec.Key)),
    };

    private IReadOnlyList<string> Commit(string key)
    {
        var before = new Config();
        foreach (var kv in Config.Ints) before.Ints[kv.Key] = kv.Value;
        foreach (var kv in Config.Texts) before.Texts[kv.Key] = kv.Value;
        Problem.AdjustConfig(Config, key);
        var changed = new List<string>();
        foreach (var kv in Config.Ints)
            if (!before.Ints.TryGetValue(kv.Key, out var old) || old != kv.Value)
            {
                if (kv.Key != key) changed.Add(kv.Key);
                _fieldErrors.Remove(kv.Key);
            }
        foreach (var kv in Config.Texts)
            if (!before.Texts.TryGetValue(kv.Key, out var old) || old != kv.Value)
                if (kv.Key != key) changed.Add(kv.Key);
        return changed;
    }

    /// <summary>Người dùng gõ một số nguyên. Trả về danh sách tham số KHÁC bị module tự đổi (giao diện cần cập nhật lại).</summary>
    public IReadOnlyList<string> EditInt(string key, string text)
    {
        var spec = Spec(key);
        if (spec == null) return Array.Empty<string>();
        if (!Util.ParseLL(text, out long v))
        {
            _fieldErrors[key] = $"'{spec.DisplayLabel}': \"{text.Trim()}\" không phải số nguyên hợp lệ (hoặc vượt phạm vi 64-bit).";
            return Array.Empty<string>();
        }
        _fieldErrors.Remove(key);
        Config.Set(key, v);
        return Commit(key);
    }

    /// <summary>Đặt giá trị số trực tiếp (ComboBox, CheckBox).</summary>
    public IReadOnlyList<string> SetInt(string key, long value)
    {
        _fieldErrors.Remove(key);
        Config.Set(key, value);
        return Commit(key);
    }

    public IReadOnlyList<string> EditText(string key, string text)
    {
        Config.SetText(key, text);
        return Commit(key);
    }

    /// <summary>Cấu hình thực sự dùng để sinh test: tham số Text đang ẩn lấy lại giá trị mặc định.</summary>
    public Config Effective()
    {
        var c = Config.Clone();
        foreach (var p in Problem.Params)
            if (p.Type == ParamType.Text && !IsVisible(p)) c.SetText(p.Key, p.DefText);
        return c;
    }

    /// <summary>Lỗi đầu tiên (ô nhập sai, hoặc cấu hình không hợp lệ theo module); null nếu hợp lệ.</summary>
    public string? Validate()
    {
        foreach (var p in Problem.Params)
            if (IsVisible(p) && FieldError(p.Key) is { } fe) return fe;
        return Generator.ValidateFullConfig(Problem, Effective());
    }
}

/// <summary>Dựng JobSpec từ các ô nhập trên giao diện (kiểm tra và đổi sang giá trị có kiểu).</summary>
public static class JobPlanner
{
    public sealed class Options
    {
        public string TestsText { get; set; } = "10";
        public string SeedText { get; set; } = "";
        public string SolutionPath { get; set; } = "";
        public string OutDirText { get; set; } = "";
        public bool Zip { get; set; } = true;
        public bool ZipIncomplete { get; set; }
        public bool Overwrite { get; set; }
        public bool Rebuild { get; set; }
    }

    public static string DefaultOutDir(Problem p, Settings st) => Path.Combine(st.OutputRoot, p.Id);

    /// <summary>Trả về null nếu thành công (spec, policy được gán), ngược lại là thông báo lỗi.</summary>
    public static string? Build(ConfigModel model, Options o, Settings st, out JobSpec? spec, out ExistingPolicy policy)
    {
        spec = null;
        policy = o.Overwrite ? ExistingPolicy.Overwrite : ExistingPolicy.NewDir;
        if (model.Validate() is { } ve) return ve;
        if (!Util.ParseLL(o.TestsText, out long tests) || tests < 1 || tests > Generator.MaxTests)
            return $"Số lượng test phải là số nguyên trong [1, {Generator.MaxTests}].";
        ulong? seed = null;
        if (o.SeedText.Trim().Length > 0)
        {
            if (!Util.ParseULL(o.SeedText, out ulong sv)) return "Seed phải là số nguyên không âm (để trống = chọn ngẫu nhiên).";
            seed = sv;
        }
        string outDir = o.OutDirText.Trim().Length > 0 ? o.OutDirText.Trim() : DefaultOutDir(model.Problem, st);
        spec = new JobSpec
        {
            Problem = model.Problem,
            Cfg = model.Effective(),
            TestCount = (int)tests,
            Seed = seed,
            SolutionPath = o.SolutionPath.Trim().Trim('"'),
            OutDir = outDir,
            Zip = o.Zip ? ZipMode.Yes : ZipMode.No,
            ZipIncomplete = o.ZipIncomplete,
            ForceRebuild = o.Rebuild,
        };
        return null;
    }

    /// <summary>Thư mục đã có file test (hoặc .zip cùng tên) - dùng để hỏi xác nhận trước khi ghi đè.</summary>
    public static bool OutDirHasData(string outDir) =>
        FileManager.CountTestFiles(outDir) > 0 || File.Exists(outDir.TrimEnd('/', '\\') + ".zip");
}
