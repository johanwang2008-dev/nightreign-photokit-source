using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

static class Kit
{
    internal static readonly string Root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    internal static readonly string Data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuroraNightreignPhotoKit");
    internal static string P(string path) { return Path.Combine(Root, path); }
    internal static string Hash(string p) { using (var s = File.OpenRead(p)) using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", ""); }
    internal static void Atomic(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        string backup = temp + ".rollback";
        try {
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Replace(temp, path, backup); else File.Move(temp, path);
        } finally { if (File.Exists(temp)) File.Delete(temp); if (File.Exists(backup)) File.Delete(backup); }
    }
    internal static void Text(string path, string text) { Atomic(path, new UTF8Encoding(true).GetBytes(text)); }
    internal static string Quote(string text)
    {
        return "\"" + Regex.Replace(Regex.Replace(text, "(\\\\*)\"", "$1$1\\\""), "(\\\\+)$", "$1$1") + "\"";
    }
    internal static bool Running(string name) { return Process.GetProcessesByName(name).Any(p => { p.Dispose(); return true; }); }
    internal static string RegistryValue(RegistryHive hive, RegistryView view, string key, string name)
    {
        try { using (var b = RegistryKey.OpenBaseKey(hive, view)) using (var k = b.OpenSubKey(key)) return k == null ? null : Convert.ToString(k.GetValue(name)); } catch { return null; }
    }
    internal static bool VCInstalled()
    {
        return new[] { RegistryView.Registry64, RegistryView.Registry32 }.Any(v => RegistryValue(RegistryHive.LocalMachine, v, @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64", "Installed") == "1");
    }
    internal static bool WebViewInstalled()
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine }) foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 }) {
            try { using (var b = RegistryKey.OpenBaseKey(hive, view)) using (var k = b.OpenSubKey(@"SOFTWARE\Microsoft\EdgeUpdate\Clients")) {
                if (k == null) continue;
                foreach (string n in k.GetSubKeyNames()) using (var c = k.OpenSubKey(n)) {
                    string name = Convert.ToString(c.GetValue("name")), version = Convert.ToString(c.GetValue("pv"));
                    if (name.IndexOf("WebView2", StringComparison.OrdinalIgnoreCase) >= 0 && !String.IsNullOrEmpty(version) && version != "0.0.0.0") return true;
                }
            } } catch { }
        }
        return false;
    }
    internal static string Steam()
    {
        var paths = new List<string>();
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 }) {
            paths.Add(RegistryValue(RegistryHive.CurrentUser, view, @"Software\Valve\Steam", "SteamPath"));
            paths.Add(RegistryValue(RegistryHive.LocalMachine, view, @"Software\Valve\Steam", "InstallPath"));
        }
        paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        return paths.FirstOrDefault(p => !String.IsNullOrEmpty(p) && File.Exists(Path.Combine(p, "steam.exe")));
    }
    internal static IEnumerable<string> Libraries(string steam)
    {
        var paths = new List<string> { steam };
        string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
        if (File.Exists(vdf)) foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\"")) paths.Add(m.Groups[1].Value.Replace("\\\\", "\\"));
        return paths.Distinct(StringComparer.OrdinalIgnoreCase);
    }
    internal static string FindGame()
    {
        string saved = Path.Combine(Data, "game-path.txt");
        if (File.Exists(saved)) { string p = File.ReadAllText(saved).Trim(); if (ValidGame(p)) return p; }
        string steam = Steam(); if (steam == null) return "";
        foreach (string lib in Libraries(steam)) {
            string manifest = Path.Combine(lib, @"steamapps\appmanifest_2622380.acf");
            string folder = "ELDEN RING NIGHTREIGN";
            if (File.Exists(manifest)) { var m = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s*\"([^\"]+)\""); if (m.Success) folder = m.Groups[1].Value; }
            string p = Path.Combine(lib, "steamapps", "common", folder, "Game", "nightreign.exe");
            if (ValidGame(p)) return p;
        }
        return "";
    }
    internal static bool ValidGame(string p) { return !String.IsNullOrWhiteSpace(p) && Path.GetFileName(p).Equals("nightreign.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(p); }
    internal static string Profile(string mode) { if (mode != "Sandbox" && mode != "Progression") throw new ArgumentException("未知模式"); return P(@"components\bossarena\Launch NR Boss Arena (" + mode + ").me3"); }
    internal static string LaunchArgs(string mode, string game)
    {
        return "--windows-binaries-dir " + Quote(P(@"components\me3\bin")) + " --crash-reporting=false launch -g nightreign -e " + Quote(game) + " -p " + Quote(Profile(mode)) + " --online=false --savefile " + Quote("NRBossArena" + mode + ".sl2");
    }
    internal static void Verify(string root, string scope = "all")
    {
        string[] lines;
        using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("required.sha256")) {
            if (stream == null) throw new IOException("启动器缺少内置校验清单，请重新解压启动器。");
            using (var reader = new StreamReader(stream, Encoding.UTF8)) lines = reader.ReadToEnd().Split('\n');
        }
        if (lines.Length < 10) throw new IOException("内置校验清单不完整。");
        int selected = 0;
        foreach (string line in lines) {
            if (String.IsNullOrWhiteSpace(line)) continue;
            if (line.Length < 67 || line.Substring(64, 2) != "  ") throw new IOException("校验清单格式错误。");
            string rel = line.Substring(66).TrimEnd('\r').Replace('/', '\\');
            bool needed = scope == "all" || (scope == "game" && (rel.StartsWith(@"components\me3\", StringComparison.OrdinalIgnoreCase) || rel.StartsWith(@"components\bossarena\", StringComparison.OrdinalIgnoreCase))) || (scope == "camera" && rel.StartsWith(@"components\crittercam\", StringComparison.OrdinalIgnoreCase)) || (scope.StartsWith("runtime:", StringComparison.Ordinal) && rel.Equals(@"runtimes\" + scope.Substring(8), StringComparison.OrdinalIgnoreCase));
            if (!needed) continue;
            selected++;
            string path = Path.GetFullPath(Path.Combine(root, rel));
            if (!path.StartsWith(Path.GetFullPath(root).TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("校验清单路径错误。");
            if (!File.Exists(path)) throw new IOException("缺少文件：" + rel + "\r\n请完整解压后重试。");
            string actual = Hash(path);
            if (!actual.Equals(line.Substring(0, 64), StringComparison.OrdinalIgnoreCase)) throw new IOException("文件校验不符：" + rel + "\r\n请从原压缩包重新解压，保留你自行修改的文件。");
        }
        if (selected == 0) throw new IOException("没有找到该功能的校验项目：" + scope);
    }
    internal static string Diagnose()
    {
        return "系统：" + Environment.OSVersion + " / x64=" + Environment.Is64BitOperatingSystem + "\r\nSteam：" + (Steam() ?? "未找到") + "\r\n游戏：" + FindGame() + "\r\nVC++ x64：" + (VCInstalled() ? "已安装" : "未检测到") + "\r\nWebView2：" + (WebViewInstalled() ? "已安装" : "未检测到");
    }
    internal static string SelfTest(string test)
    {
        Verify(Root);
        string fixture = Path.Combine(test, "fixture-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(fixture);
        string args = LaunchArgs("Sandbox", @"D:\测试 空格 & 路径\Game\nightreign.exe");
        if (!args.Contains("--online=false") || !args.Contains("NRBossArenaSandbox.sl2") || !args.Contains(Quote(P(@"components\me3\bin")))) throw new Exception("launch plan failed");
        if (!LaunchArgs("Progression", "nightreign.exe").Contains("NRBossArenaProgression.sl2")) throw new Exception("progression failed");
        return "PASS: manifest; Sandbox/Progression argument generation.\r\n" + Diagnose() + "\r\n" + args + "\r\nGame launch/gameplay: NOT EXECUTED.\r\nFixture: " + fixture;
    }
    [STAThread] static int Main(string[] args)
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try {
            if (args.Length > 0) {
                if (args[0] == "--verify") { Verify(Root); Text(args[1], "PASS: all package hashes verified."); return 0; }
                if (args[0] == "--verify-scope") { Verify(Root, args[1]); Text(args[2], "PASS: " + args[1]); return 0; }
                if (args[0] == "--self-test") { Text(args[2], SelfTest(args[1])); return 0; }
                if (args[0] == "--diagnose") { Text(args[1], Diagnose()); return 0; }
                if (args[0] == "--render" || args[0] == "--render-wide") { using (var f = new MainForm()) { f.RenderOnly = true; f.ShowInTaskbar = false; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-20000, -20000); if (args[0] == "--render-wide") f.ClientSize = new Size(2560, 1400); f.Show(); Application.DoEvents(); using (var b = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(b, new Rectangle(Point.Empty, b.Size)); if (b.Width > 1280) { using (var preview = new Bitmap(b, new Size(1280, (int)(b.Height * 1280.0 / b.Width)))) preview.Save(args[1]); } else b.Save(args[1]); } f.Close(); } return 0; }
                throw new ArgumentException("未知参数");
            }
            Application.Run(new MainForm()); return 0;
        } catch (Exception e) {
            if (args.Length > 0) { string report = args[args.Length - 1]; try { Text(report, "FAIL: " + e); } catch { } }
            else MessageBox.Show(e.Message, "启动器错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}

class MainForm : Form
{
    internal bool RenderOnly;
    protected override bool ShowWithoutActivation { get { return RenderOnly; } }
    readonly TextBox game = new TextBox(); readonly TextBox log = new TextBox(); readonly Label runtime = new Label();
    readonly List<Button> actions = new List<Button>();
    public MainForm()
    {
        Text = "黑夜君临 · 摄影与 Boss 练习工具包 1.0.8"; Font = new Font("Microsoft YaHei UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi; ClientSize = new Size(980, 650); MinimumSize = new Size(996, 689); StartPosition = FormStartPosition.CenterScreen;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 1, RowCount = 8 };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        Controls.Add(layout);
        layout.Controls.Add(new Label { Text = "黑夜君临摄影工具包\r\n拍摄 / 自由选 Boss：进入 Boss 练习场 → 圆桌厅堂 → 打开相机并注入", AutoSize = true, Dock = DockStyle.Fill }, 0, 0);
        var pathPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 115));
        pathPanel.Controls.Add(new Label { Text = "游戏程序（nightreign.exe）", AutoSize = true }, 0, 0);
        game.Dock = DockStyle.Fill; game.Text = Kit.FindGame(); pathPanel.Controls.Add(game, 0, 1);
        var browse = new Button { Text = "选择游戏…", Dock = DockStyle.Fill }; browse.Click += delegate { using (var d = new OpenFileDialog { Filter = "游戏程序|nightreign.exe", Title = "选择 Game 文件夹内的 nightreign.exe" }) if (d.ShowDialog() == DialogResult.OK) { game.Text = d.FileName; Kit.Text(Path.Combine(Kit.Data, "game-path.txt"), game.Text); } }; pathPanel.Controls.Add(browse, 1, 1); layout.Controls.Add(pathPanel, 0, 1);
        runtime.Dock = DockStyle.Fill; runtime.TextAlign = ContentAlignment.MiddleLeft; RefreshRuntime(); layout.Controls.Add(runtime, 0, 2);
        var first = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, Margin = Padding.Empty }; var second = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, Margin = Padding.Empty }; var third = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 1, Margin = Padding.Empty };
        Add(first, "① 检查 / 安装环境", InstallRuntime, 165); Add(first, "② Boss 练习场（推荐拍摄）", () => Launch("Sandbox"), 218); Add(first, "奖励闯关（Boss Rush）", () => Launch("Progression"), 190); Add(first, "③ 打开中文相机", Camera, 165);
        var tips = new ToolTip(); tips.SetToolTip(first.Controls[1], "用于自由选 Boss、练习和拍摄；相机 Boss 选择功能请用这个模式。"); tips.SetToolTip(first.Controls[2], "连续挑战 Boss 获得奖励和解锁；一般拍摄不需要选这个。");
        Add(third, "校验完整性", () => Work(() => Kit.Verify(Kit.Root), "程序组件及离线安装器校验通过。说明文件不参与校验。"), 105);
        Add(third, "打开使用说明", Help, 145);
        Add(third, "打开日志", () => { Directory.CreateDirectory(Kit.Data); Process.Start(new ProcessStartInfo(Kit.Data) { UseShellExecute = true }); }, 115);
        layout.Controls.Add(first, 0, 3); layout.Controls.Add(second, 0, 4); layout.Controls.Add(third, 0, 5);
        log.Multiline = true; log.ReadOnly = true; log.ScrollBars = ScrollBars.Vertical; log.Dock = DockStyle.Fill; log.BackColor = Color.FromArgb(245, 247, 250); layout.Controls.Add(log, 0, 6);
        layout.Controls.Add(new Label { Text = "默认离线 · 两种模式使用各自存档 · 游戏内文字需选简体中文", AutoSize = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 7);
        log.Text = "先完整解压再打开。components 是功能组件，说明文件不影响启动。\r\n首次运行点“检查 / 安装环境”；环境齐全时会直接提示。\r\n拍摄或自由选 Boss：点“Boss 练习场（推荐拍摄）”。\r\n“奖励闯关”是连续打 Boss 解锁奖励，普通拍摄不用选。\r\n游戏版本更新后，需重新确认 Mod 兼容性。\r\n当前版本的进游戏、实战及界面文字排版尚未完成测试。";
    }
    void Add(TableLayoutPanel p, string text, Action action, int width)
    {
        int column = p.Controls.Count; p.ColumnCount = column + 1; p.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 1F)); var b = new Button { Text = text, Dock = DockStyle.Fill, Margin = new Padding(3, 5, 3, 5) };
        b.Click += delegate { try { action(); } catch (Exception e) { Note(e.Message); MessageBox.Show(this, e.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Information); } };
        actions.Add(b); p.Controls.Add(b, column, 0);
    }
    void Note(string text) { if (IsDisposed) return; log.AppendText("\r\n[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text); }
    void Help()
    {
        string path = Kit.P(@"说明\使用说明.txt");
        if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        else MessageBox.Show(this, "先登录 Steam，检查安装环境，再启动 Boss 练习场。进入圆桌后打开中文相机并点击注入相机。\r\n\r\nBoss练习场简体汉化默认已应用，游戏语言需选简体中文。\r\n\r\n详细说明文件已移走或删除，不影响游戏或相机启动。", "内置使用说明", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
    void RefreshRuntime() { runtime.Text = "环境：VC++ x64 " + (Kit.VCInstalled() ? "✓ 已安装" : "未检测到") + "     WebView2 " + (Kit.WebViewInstalled() ? "✓ 已安装" : "未检测到"); }
    async void Work(Action action, string success)
    {
        foreach (var b in actions) b.Enabled = false;
        try { Note("正在处理…"); await Task.Run(action); Note(success); }
        catch (Exception e) { Note("操作未完成：" + e.Message); MessageBox.Show(this, e.Message, "操作未完成", MessageBoxButtons.OK, MessageBoxIcon.Information); }
        finally { if (!IsDisposed) { foreach (var b in actions) b.Enabled = true; RefreshRuntime(); } }
    }
    void InstallRuntime()
    {
        bool vc = Kit.VCInstalled(), web = Kit.WebViewInstalled();
        if (vc && web) { Note("运行环境齐全，无需安装。可以启动 Boss 练习场。"); return; }
        Work(() => {
            foreach (string installer in new[] { vc ? null : "vc_redist.x64.exe", web ? null : "MicrosoftEdgeWebView2RuntimeInstallerX64.exe" }) {
                if (installer == null) continue;
                Kit.Verify(Kit.Root, "runtime:" + installer);
                using (var p = Process.Start(new ProcessStartInfo(Kit.P(@"runtimes\" + installer)) { UseShellExecute = true })) {
                    p.WaitForExit(); if (p.ExitCode == 3010) throw new IOException("安装器要求重启电脑。重启后再打开工具包检查环境。");
                    if (p.ExitCode != 0 && p.ExitCode != 1638) throw new IOException(installer + " 返回代码 " + p.ExitCode + "，请检查安装器提示。");
                }
            }
            if (!Kit.VCInstalled() || !Kit.WebViewInstalled()) throw new IOException("安装完成后仍有环境未检测到。请查看微软安装器提示，必要时重启后重试。");
        }, "环境安装完成，可以启动 Boss 练习场。");
    }
    void Launch(string mode)
    {
        string path = game.Text.Trim();
        if (!Kit.ValidGame(path)) throw new IOException("请选择正确的 Game\\nightreign.exe。");
        if (Kit.Running("nightreign")) throw new IOException("游戏已在运行。切换启动模式前请先退出游戏。");
        if (!Kit.Running("steam")) throw new IOException("请先打开 Steam 并登录拥有游戏的账号。");
        if (!Kit.VCInstalled()) throw new IOException("请先点击“检查 / 安装环境”。");
        Work(() => {
            Kit.Verify(Kit.Root, "game"); Kit.Text(Path.Combine(Kit.Data, "game-path.txt"), path);
            string file = Path.Combine(Kit.Data, "launch-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
            var info = new ProcessStartInfo(Kit.P(@"components\me3\bin\me3.exe"), Kit.LaunchArgs(mode, path)) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Kit.Root, RedirectStandardOutput = true, RedirectStandardError = true };
            var proc = new Process { StartInfo = info }; var gate = new object();
            DataReceivedEventHandler capture = (sender, ev) => { if (ev.Data == null) return; lock (gate) File.AppendAllText(file, ev.Data + Environment.NewLine, Encoding.UTF8); };
            Kit.Text(file, "Mode: " + mode + "\r\n" + info.Arguments + "\r\n");
            proc.OutputDataReceived += capture; proc.ErrorDataReceived += capture; proc.Start(); proc.BeginOutputReadLine(); proc.BeginErrorReadLine();
            Task.Run(() => { proc.WaitForExit(); int exit = proc.ExitCode; lock (gate) File.AppendAllText(file, "\r\nME3 exit: " + exit); proc.Dispose(); if (!IsDisposed && IsHandleCreated) try { BeginInvoke((Action)(() => Note("ME3 已退出，代码 " + exit + "。日志：" + file))); } catch (InvalidOperationException) { } });
        }, "已提交 " + mode + " 启动请求（尚未验证游戏启动成功）。进入圆桌后再打开中文相机。");
    }
    void Camera()
    {
        if (!Kit.WebViewInstalled() || !Kit.VCInstalled()) throw new IOException("请先点击“检查 / 安装环境”。");
        if (Kit.Running("crittercam")) { Note("crittercam 已在运行，请切换到它的窗口。"); return; }
        Work(() => { Kit.Verify(Kit.Root, "camera"); Process.Start(new ProcessStartInfo(Kit.P(@"components\crittercam\crittercam.exe")) { UseShellExecute = true, WorkingDirectory = Kit.P(@"components\crittercam") }); }, "中文相机已打开。进入圆桌后点击“注入相机”，再按 Insert 开启自由镜头。");
    }
}
