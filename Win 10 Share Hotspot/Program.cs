using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Windows.Networking.Connectivity;
using Windows.Networking.NetworkOperators;

namespace Win10ShareHotspot
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    // ───────────────────────── 数据模型 ─────────────────────────
    internal sealed class ClientInfo
    {
        public string Name;
        public string Ip;
        public string Mac;
    }

    // ───────────────────────── 热点服务(WinRT) ─────────────────────────
    internal static class HotspotService
    {
        
        private static ConnectionProfile FindProfile()
        {
            // 1. 优先:已联网的无线连接(排除隧道)
            foreach (ConnectionProfile c in NetworkInformation.GetConnectionProfiles())
            {
                if (c.IsWlanConnectionProfile
                    && !IsTunnel(c)
                    && c.GetNetworkConnectivityLevel() == NetworkConnectivityLevel.InternetAccess)
                    return c;
            }

            // 2. 其次:系统默认连接,但不能是隧道
            ConnectionProfile p = NetworkInformation.GetInternetConnectionProfile();
            if (p != null && !IsTunnel(p)) return p;

            // 3. 兜底:任意有连通性的非隧道连接
            foreach (ConnectionProfile c in NetworkInformation.GetConnectionProfiles())
            {
                if (!IsTunnel(c) && c.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None)
                    return c;
            }
            return null;
        }

        // IANA 接口类型 131 = 隧道(singbox_tun 一类)
        private static bool IsTunnel(ConnectionProfile p)
        {
            return p.NetworkAdapter != null && p.NetworkAdapter.IanaInterfaceType == 131;
        }

        /// <summary>创建热点管理器;失败返回 null 并给出错误码(由界面翻译)。</summary>
        public static NetworkOperatorTetheringManager CreateManager(out string errorKey)
        {
            errorKey = null;
            ConnectionProfile profile = FindProfile();
            if (profile == null) { errorKey = "noNet"; return null; }

            TetheringCapability cap = NetworkOperatorTetheringManager.GetTetheringCapabilityFromConnectionProfile(profile);
            if (cap != TetheringCapability.Enabled) { errorKey = "noCap"; return null; }

            return NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);
        }

        public static async Task<List<ClientInfo>> GetClientsAsync(NetworkOperatorTetheringManager m)
        {
            List<ClientInfo> list = new List<ClientInfo>();
            bool needArp = false;

            foreach (NetworkOperatorTetheringClient c in m.GetTetheringClients())
            {
                string name = null, ip = null;
                foreach (Windows.Networking.HostName h in c.HostNames)
                {
                    if (h.Type == Windows.Networking.HostNameType.DomainName && name == null) name = h.DisplayName;
                    else if (h.Type == Windows.Networking.HostNameType.Ipv4 && ip == null) ip = h.CanonicalName;
                }
                if (ip == null) needArp = true;
                list.Add(new ClientInfo { Name = name, Ip = ip, Mac = c.MacAddress });
            }

            if (needArp)
            {
                Dictionary<string, string> arp = await Task.Run(ReadArp);
                foreach (ClientInfo ci in list)
                {
                    if (ci.Ip == null && arp.TryGetValue(NormMac(ci.Mac), out string found)) ci.Ip = found;
                }
            }

            foreach (ClientInfo ci in list)
            {
                if (string.IsNullOrEmpty(ci.Name)) ci.Name = ci.Mac;
                if (string.IsNullOrEmpty(ci.Ip)) ci.Ip = "-";
            }
            return list;
        }

        private static string NormMac(string s)
        {
            return Regex.Replace(s ?? "", "[^0-9a-fA-F]", "").ToLowerInvariant();
        }

        // IP 取不到时的兜底:读系统 ARP 表,按 MAC 匹配
        private static Dictionary<string, string> ReadArp()
        {
            Dictionary<string, string> d = new Dictionary<string, string>();
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("arp", "-a")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi))
                {
                    string o = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(2000);
                    foreach (Match m in Regex.Matches(o, @"(\d+\.\d+\.\d+\.\d+)\s+((?:[0-9a-fA-F]{2}-){5}[0-9a-fA-F]{2})"))
                        d[NormMac(m.Groups[2].Value)] = m.Groups[1].Value;
                }
            }
            catch { }
            return d;
        }

        /// <summary>本机作为热点网关的 IP(通常是 192.168.137.1)。</summary>
        public static string GetHostIp()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = ua.Address.ToString();
                    if (ni.Description.Contains("Wi-Fi Direct Virtual") || ip.StartsWith("192.168.137."))
                        return ip;
                }
            }
            return null;
        }
    }

    // ───────────────────────── 主窗体 ─────────────────────────
    internal sealed class MainForm : Form
    {
        private const string RegPath = @"Software\Win10ShareHotspot";

        // 双语文本 (zh, en)
        private static readonly Dictionary<string, (string zh, string en)> S = new Dictionary<string, (string, string)>
        {
            ["title"]      = ("Win 10 共享热点", "Win 10 Share Hotspot"),
            ["on"]         = ("热点:已开启", "Hotspot: ON"),
            ["off"]        = ("热点:已关闭", "Hotspot: OFF"),
            ["trans"]      = ("热点:切换中…", "Hotspot: switching…"),
            ["unknown"]    = ("热点:不可用", "Hotspot: unavailable"),
            ["btnOn"]      = ("开启热点", "Turn ON"),
            ["btnOff"]     = ("关闭热点", "Turn OFF"),
            ["ssid"]       = ("热点名称", "SSID"),
            ["pwd"]        = ("热点密码", "Password"),
            ["show"]       = ("显示密码", "Show"),
            ["apply"]      = ("应用名称与密码", "Apply SSID && password"),
            ["hostip"]     = ("本机热点 IP:", "Hotspot host IP: "),
            ["timeout"]    = ("自动关闭(分钟):", "Auto-off (min):"),
            ["timeoutTip"] = ("0 = 不自动关闭", "0 = never"),
            ["countdown"]  = ("距离自动关闭:", "Auto-off in: "),
            ["noCountdown"]= ("—", "—"),
            ["clients"]    = ("已连接设备", "Connected devices"),
            ["colName"]    = ("设备名称", "Device name"),
            ["colIp"]      = ("IP 地址", "IP address"),
            ["colMac"]     = ("MAC 地址", "MAC address"),
            ["refresh"]    = ("刷新", "Refresh"),
            ["light"]      = ("明", "Light"),
            ["dark"]       = ("暗", "Dark"),
            ["noNet"]      = ("没有可用的网络连接,无法开启热点。", "No usable network connection; cannot start hotspot."),
            ["noCap"]      = ("当前网络不支持共享热点。", "Current connection does not allow tethering."),
            ["ssidErr"]    = ("名称长度需为 1~32 个字符。", "SSID must be 1-32 characters."),
            ["pwdErr"]     = ("密码长度需为 8~63 个字符。", "Password must be 8-63 characters."),
            ["applied"]    = ("名称与密码已应用。", "SSID and password applied."),
            ["failed"]     = ("操作失败:", "Operation failed: "),
            ["autoOff"]    = ("已超时,热点已自动关闭。", "Timeout reached; hotspot turned off."),
            ["busy"]       = ("热点正在切换中,请稍候…", "Hotspot is switching, please wait…"),
            ["btnBusy"] = ("切换中…", "Switching…"),
            ["power"] = ("节能:无设备连接时自动关闭热点(系统设置)", "Power saving: auto-off when no device connected (system)"),
            ["powerOn"] = ("已开启系统节能:无设备连接时,系统会自动关闭热点。", "System power saving ON: Windows will turn off the hotspot when no device is connected."),
            ["powerOff"] = ("已关闭系统节能:无设备连接时,系统不会再自动关闭热点。", "System power saving OFF: Windows will no longer turn off the hotspot when idle."),
            ["powerFail"] = ("无法修改系统节能设置:", "Could not change the system power-saving setting: "),
            ["closeOff"] = ("关闭本程序时,同时关闭热点", "Turn off hotspot when closing this app"),
            ["closing"] = ("正在关闭热点,随后退出…", "Turning off hotspot, then exiting…"),
        };

        private bool _dark;
        private bool _zh = true;
        private bool _opBusy;
        private bool _refreshing;
        private DateTime? _deadline;
        private int _tick;
        private TetheringOperationalState? _lastState;
        private string _msgKey;
        private string _msgExtra = "";

        private readonly Label lblState = new Label();
        private readonly Button btnToggle = new Button();
        private readonly Button btnTheme = new Button();
        private readonly Button btnLang = new Button();
        private readonly Label lblSsid = new Label();
        private readonly TextBox txtSsid = new TextBox();
        private readonly Label lblPwd = new Label();
        private readonly TextBox txtPwd = new TextBox();
        private readonly CheckBox chkShow = new CheckBox();
        private readonly CheckBox chkPower = new CheckBox();
        private readonly CheckBox chkCloseOff = new CheckBox();
        //private bool _closeOff;          // 从注册表读取的设置
        private bool _closeOff = true;          // 默认:退出时关闭热点
        private bool _powerSave;          // 节能偏好,默认 false = 不节能
        private bool _closeApproved;     // 已完成善后,允许真正关闭
        private bool _closing;           // 正在善后,防止重复进入
        private bool _syncingPower;       // 程序同步系统值时,不触发写入
        private bool _powerSupported = true;
        private readonly Button btnApply = new Button();
        private readonly Label lblHostIp = new Label();
        private readonly Label lblTimeout = new Label();
        private readonly NumericUpDown numTimeout = new NumericUpDown();
        private readonly Label lblTimeoutTip = new Label();
        private readonly Label lblCountdown = new Label();
        private readonly Label lblClients = new Label();
        private readonly Button btnRefresh = new Button();
        private readonly ListView lv = new ListView();
        private readonly Label lblMsg = new Label();
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer();

        public MainForm()
        {
            LoadSettings();

            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ShowIcon = false;   // ← 新增:隐藏窗口左上角的小图标
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(Px(540), Px(640));
            Font = new Font("Microsoft YaHei UI", 9F);

            Place(lblState, 20, 16, 320, 28);
            lblState.Font = new Font(Font.FontFamily, 12F, FontStyle.Bold);
            lblState.TextAlign = ContentAlignment.MiddleLeft;
            Place(btnTheme, 350, 14, 90, 28);
            Place(btnLang, 450, 14, 70, 28);

            foreach (Button b in new[] { btnTheme, btnLang })
            {
                b.AutoSize = true;
                b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
                b.Padding = new Padding(Px(8), 0, Px(8), 0);
                b.MinimumSize = new Size(0, Px(28));
            }

            Place(btnToggle, 20, 52, 500, 44);
            btnToggle.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);

            Place(lblSsid, 20, 114, 90, 24);
            Place(txtSsid, 115, 110, 405, 26);
            Place(lblPwd, 20, 148, 90, 24);
            Place(txtPwd, 115, 144, 300, 26);
            txtPwd.UseSystemPasswordChar = true;
            Place(chkShow, 425, 146, 95, 24);
            Place(btnApply, 20, 182, 500, 32);

            Place(lblHostIp, 20, 228, 500, 24);
            Place(lblTimeout, 20, 260, 150, 24);
            Place(numTimeout, 175, 257, 70, 26);
            numTimeout.Minimum = 0;
            numTimeout.Maximum = 720;
            Place(lblTimeoutTip, 255, 260, 200, 24);
            Place(chkPower, 20, 288, 500, 24);
            Place(chkCloseOff, 20, 318, 500, 24);
            Place(lblCountdown, 20, 348, 500, 24);   

            Place(lblClients, 20, 384, 300, 22);
            lblClients.Font = new Font(Font.FontFamily, 10F, FontStyle.Bold);
            Place(btnRefresh, 420, 380, 100, 28);

            Place(lv, 20, 412, 500, 176);
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.GridLines = true;
            lv.HideSelection = false;
            lv.Columns.Add("", 170);
            lv.Columns.Add("", 120);
            lv.Columns.Add("", 190);

            Place(lblMsg, 20, 596, 500, 36);

            numTimeout.Value = Math.Min(numTimeout.Maximum, Math.Max(0, _savedTimeout));
            chkCloseOff.Checked = _closeOff;

            btnToggle.Click += async (s, e) => await RunOpAsync(ToggleCore);
            btnApply.Click += async (s, e) => await RunOpAsync(ApplyCore);
            btnRefresh.Click += async (s, e) => await RefreshAsync(false);
            btnTheme.Click += (s, e) => { _dark = !_dark; ApplyTheme(); SaveSettings(); };
            btnLang.Click += (s, e) => { _zh = !_zh; ApplyTexts(); SaveSettings(); };
            chkShow.CheckedChanged += (s, e) => txtPwd.UseSystemPasswordChar = !chkShow.Checked;
            chkPower.CheckedChanged += (s, e) => OnPowerToggled();
            chkCloseOff.CheckedChanged += (s, e) => SaveSettings();
            numTimeout.ValueChanged += (s, e) =>
            {
                SaveSettings();
                if (_lastState == TetheringOperationalState.On) ResetDeadline(); // 手动修改超时 → 重新计时
                UpdateCountdown();
            };

            _timer.Interval = 1000;
            _timer.Tick += async (s, e) => await OnTickAsync();

            Shown += async (s, e) =>
            {
                ApplyTheme();
                ApplyTexts();
                ApplyPowerPreference();
                await RefreshAsync(true);
                _timer.Start();
            };
            //FormClosing += (s, e) => { _timer.Stop(); SaveSettings(); };
            FormClosing += OnMainFormClosing;
        }

        private int _savedTimeout = 30;

        // 把 96 DPI 下的像素值换算成当前 DPI 下的像素值
        private int Px(int v)
        {
            return (int)Math.Round(v * DeviceDpi / 96.0);
        }

        // 列宽按列表实际宽度按比例分配(随 DPI 自动适配)
        private void FitColumns()
        {
            int w = lv.ClientSize.Width;
            if (w <= 0) return;
            int c0 = (int)(w * 0.34);
            int c1 = (int)(w * 0.24);
            lv.Columns[0].Width = c0;
            lv.Columns[1].Width = c1;
            lv.Columns[2].Width = Math.Max(50, w - c0 - c1 - SystemInformation.VerticalScrollBarWidth);
        }

        private void Place(Control c, int x, int y, int w, int h)
        {
            c.Location = new Point(Px(x), Px(y));
            c.Size = new Size(Px(w), Px(h));
            Controls.Add(c);
        }

        private string T(string key)
        {
            return _zh ? S[key].zh : S[key].en;
        }

        // ───────── 设置持久化(注册表) ─────────
        private void LoadSettings()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    if (k == null) return;
                    _dark = Convert.ToInt32(k.GetValue("Dark", 0)) == 1;
                    _zh = Convert.ToString(k.GetValue("Lang", "zh")) != "en";
                    _savedTimeout = Convert.ToInt32(k.GetValue("TimeoutMin", 30));
                    _closeOff = Convert.ToInt32(k.GetValue("CloseOff", 0)) == 1;
                    _powerSave = Convert.ToInt32(k.GetValue("PowerSave", 0)) == 1;
                }
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RegPath))
                {
                    k.SetValue("Dark", _dark ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("Lang", _zh ? "zh" : "en");
                    k.SetValue("TimeoutMin", (int)numTimeout.Value, RegistryValueKind.DWord);
                    k.SetValue("CloseOff", chkCloseOff.Checked ? 1 : 0, RegistryValueKind.DWord);
                    k.SetValue("PowerSave", _powerSave ? 1 : 0, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        // ───────── 主题与文本 ─────────
        private void ApplyTheme()
        {
            // 如需与上一个项目配色逐项一致,只改这几个颜色值即可
            Color bg = _dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(243, 243, 243);
            Color fg = _dark ? Color.FromArgb(240, 240, 240) : Color.FromArgb(30, 30, 30);
            Color input = _dark ? Color.FromArgb(55, 55, 55) : Color.White;
            Color btn = _dark ? Color.FromArgb(70, 70, 70) : Color.FromArgb(225, 225, 225);
            Color accent = Color.FromArgb(0, 120, 215);

            BackColor = bg;
            ForeColor = fg;
            foreach (Control c in Controls)
            {
                c.ForeColor = fg;
                if (c is Button b)
                {
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = _dark ? Color.FromArgb(90, 90, 90) : Color.FromArgb(180, 180, 180);
                    b.BackColor = btn;
                }
                else if (c is TextBox || c is NumericUpDown || c is ListView)
                {
                    c.BackColor = input;
                }
                else
                {
                    c.BackColor = bg;
                }
            }
            btnToggle.BackColor = accent;
            btnToggle.ForeColor = Color.White;
            UpdateStateLabel();
            UpdateTopButtons();
        }

        private void ApplyTexts()
        {
            Text = T("title");
            UpdateTopButtons();

            lblSsid.Text = T("ssid");
            lblPwd.Text = T("pwd");
            chkShow.Text = T("show");
            chkPower.Text = T("power");
            chkCloseOff.Text = T("closeOff");
            btnApply.Text = T("apply");
            lblTimeout.Text = T("timeout");
            lblTimeoutTip.Text = T("timeoutTip");
            lblClients.Text = T("clients");
            btnRefresh.Text = T("refresh");
            lv.Columns[0].Text = T("colName");
            lv.Columns[1].Text = T("colIp");
            lv.Columns[2].Text = T("colMac");
            FitColumns();
            UpdateStateLabel();
            UpdateCountdown();
            UpdateMsg();
            if (_hostIp != null || _lastState != null) lblHostIp.Text = T("hostip") + (_hostIp ?? "-");
        }

        private string _hostIp;

        private void UpdateTopButtons()
        {
            btnLang.Text = _zh ? "EN" : "中";
            btnTheme.Text = _dark ? T("light") : T("dark");

            // 右对齐:语言按钮贴右边,明/暗按钮排在它左边
            int right = ClientSize.Width - Px(20), gap = Px(10), top = Px(14);
            btnLang.Location = new Point(right - btnLang.Width, top);
            btnTheme.Location = new Point(right - btnLang.Width - gap - btnTheme.Width, top);
        }

        private void UpdateStateLabel()
        {
            string key;
            Color col = ForeColor;
            if (_opBusy)
            {
                key = "trans";
            }
            else
            {
                switch (_lastState)
                {
                    case TetheringOperationalState.On: key = "on"; col = Color.FromArgb(46, 160, 67); break;
                    case TetheringOperationalState.Off: key = "off"; break;
                    case TetheringOperationalState.InTransition: key = "trans"; break;
                    default: key = "unknown"; break;
                }
            }
            lblState.Text = T(key);
            lblState.ForeColor = col;

            bool switching = _opBusy || _lastState == TetheringOperationalState.InTransition;
            btnToggle.Text = switching ? T("btnBusy")
                           : (_lastState == TetheringOperationalState.On ? T("btnOff") : T("btnOn"));
            UpdateControlsEnabled();
        }

        private void SetMsg(string key, string extra = "")
        {
            _msgKey = key;
            _msgExtra = extra;
            UpdateMsg();
        }

        private void UpdateMsg()
        {
            lblMsg.Text = _msgKey == null ? "" : T(_msgKey) + _msgExtra;
        }

        // ───────── 系统节能设置(无设备连接时自动关闭热点) ─────────
        private void OnPowerToggled()
        {
            if (_syncingPower || _opBusy) return;
            bool want = chkPower.Checked;
            try
            {
                if (want) NetworkOperatorTetheringManager.EnableNoConnectionsTimeout();
                else NetworkOperatorTetheringManager.DisableNoConnectionsTimeout();
                _powerSave = want;      // ← 新增:用户明确选择才更新偏好
                SaveSettings();         // ← 新增
                SetMsg(want ? "powerOn" : "powerOff");
            }
            catch (Exception ex)
            {
                SetMsg("powerFail", ex.Message);
            }
            SyncPowerCheckbox();   // 最终以系统实际值为准
        }

        // 启动时:按程序保存的偏好写入系统(默认 = 不节能)
        private void ApplyPowerPreference()
        {
            try
            {
                bool actual = NetworkOperatorTetheringManager.IsNoConnectionsTimeoutEnabled();
                if (actual != _powerSave)
                {
                    if (_powerSave) NetworkOperatorTetheringManager.EnableNoConnectionsTimeout();
                    else NetworkOperatorTetheringManager.DisableNoConnectionsTimeout();
                    SetMsg(_powerSave ? "powerOn" : "powerOff");
                }
            }
            catch (Exception ex)
            {
                SetMsg("powerFail", ex.Message);
            }
            SyncPowerCheckbox();
        }

        private void SyncPowerCheckbox()
        {
            try
            {
                bool actual = NetworkOperatorTetheringManager.IsNoConnectionsTimeoutEnabled();
                if (chkPower.Checked != actual)
                {
                    _syncingPower = true;
                    try { chkPower.Checked = actual; }
                    finally { _syncingPower = false; }
                }
                _powerSupported = true;
            }
            catch
            {
                _powerSupported = false;   // 读取失败则禁用该开关,避免误导
            }
            UpdateControlsEnabled();
        }

        // ───────── 倒计时 / 定时器 ─────────
        private void ResetDeadline()
        {
            _deadline = numTimeout.Value > 0 ? DateTime.Now.AddMinutes((double)numTimeout.Value) : (DateTime?)null;
        }

        private void UpdateCountdown()
        {
            string v = T("noCountdown");
            if (_lastState == TetheringOperationalState.On && _deadline.HasValue)
            {
                TimeSpan r = _deadline.Value - DateTime.Now;
                if (r < TimeSpan.Zero) r = TimeSpan.Zero;
                v = ((int)r.TotalMinutes).ToString("00") + ":" + r.Seconds.ToString("00");
            }
            lblCountdown.Text = T("countdown") + v;
        }

        // ───────── 关闭窗口时的善后 ─────────
        private void OnMainFormClosing(object sender, FormClosingEventArgs e)
        {
            _timer.Stop();
            SaveSettings();

            if (_closeApproved || !chkCloseOff.Checked) return;
            if (e.CloseReason == CloseReason.WindowsShutDown) return;   // 系统关机时不拦截

            e.Cancel = true;           // 先取消本次关闭
            if (_closing) return;
            _closing = true;
            _ = StopHotspotAndCloseAsync();
        }

        private async Task StopHotspotAndCloseAsync()
        {
            try
            {
                SetMsg("closing");
                Enabled = false;   // 窗口期间不再响应点击

                // 如果有正在进行的开关操作,先等它结束(最多 20 秒)
                int waited = 0;
                while (_opBusy && waited < 20000) { await Task.Delay(200); waited += 200; }

                string err;
                NetworkOperatorTetheringManager m = HotspotService.CreateManager(out err);
                if (m != null)
                {
                    if (m.TetheringOperationalState == TetheringOperationalState.InTransition)
                    {
                        await WaitSettledAsync(15000);
                        m = HotspotService.CreateManager(out err);
                    }
                    if (m != null && m.TetheringOperationalState == TetheringOperationalState.On)
                    {
                        Task<NetworkOperatorTetheringOperationResult> t = m.StopTetheringAsync().AsTask();
                        await Task.WhenAny(t, Task.Delay(20000));   // 最多等 20 秒,避免窗口关不掉
                        await WaitSettledAsync(15000);
                    }
                }
            }
            catch { }
            finally
            {
                _closeApproved = true;
                Close();
            }
        }
        private async Task OnTickAsync()
        {
            if (_opBusy) return;
            UpdateCountdown();

            if (_lastState == TetheringOperationalState.On && _deadline.HasValue && DateTime.Now >= _deadline.Value)
            {
                await RunOpAsync(StopForTimeoutCore);
                return;
            }

            _tick++;
            if (_tick % 5 == 0) await RefreshAsync(false); // 每 5 秒自动刷新状态与设备列表
        }

        // ───────── 热点操作 ─────────

        private void UpdateControlsEnabled()
        {
            bool ok = !_opBusy && _lastState != TetheringOperationalState.InTransition;
            btnToggle.Enabled = ok;
            btnApply.Enabled = ok;
            btnRefresh.Enabled = !_opBusy;
            chkPower.Enabled = !_opBusy && _powerSupported;
        }

        private const int CooldownMs = 1500;   // 状态稳定后的额外冷却,可按需调整

        // 轮询(每次用新的管理器)直到状态不再是 InTransition
        private static async Task<bool> WaitSettledAsync(int timeoutMs)
        {
            int waited = 0;
            while (true)
            {
                string err;
                NetworkOperatorTetheringManager m = HotspotService.CreateManager(out err);
                if (m == null) return false;
                if (m.TetheringOperationalState != TetheringOperationalState.InTransition) return true;
                if (waited >= timeoutMs) return false;
                await Task.Delay(500);
                waited += 500;
            }
        }

        private async Task RunOpAsync(Func<NetworkOperatorTetheringManager, Task> op)
        {
            if (_opBusy) return;
            _opBusy = true;
            UpdateStateLabel();   // 立刻显示"切换中…"并禁用按钮
            try
            {
                SetMsg(null);
                string errKey;
                NetworkOperatorTetheringManager m = HotspotService.CreateManager(out errKey);
                if (m == null) { SetMsg(errKey); return; }

                await op(m);
                await WaitSettledAsync(15000);   // 等系统状态真正落定
                await Task.Delay(CooldownMs);    // 再冷却一下
            }
            catch (Exception ex)
            {
                SetMsg("failed", ex.Message);
            }
            finally
            {
                _opBusy = false;
                UpdateStateLabel();   // 此时才恢复按钮
            }
            await RefreshAsync(false);
        }

        private async Task ToggleCore(NetworkOperatorTetheringManager m)
        {
            if (m.TetheringOperationalState == TetheringOperationalState.InTransition)
            {
                SetMsg("busy");
                return;
            }
            if (m.TetheringOperationalState == TetheringOperationalState.On)
            {
                NetworkOperatorTetheringOperationResult r = await m.StopTetheringAsync();
                if (r.Status != TetheringOperationStatus.Success) SetMsg("failed", r.AdditionalErrorMessage ?? r.Status.ToString());
            }
            else
            {
                NetworkOperatorTetheringOperationResult r = await m.StartTetheringAsync();
                if (r.Status == TetheringOperationStatus.Success) ResetDeadline();
                else SetMsg("failed", r.AdditionalErrorMessage ?? r.Status.ToString());
            }
        }

        private async Task StopForTimeoutCore(NetworkOperatorTetheringManager m)
        {
            NetworkOperatorTetheringOperationResult r = await m.StopTetheringAsync();
            if (r.Status == TetheringOperationStatus.Success) SetMsg("autoOff");
            else SetMsg("failed", r.AdditionalErrorMessage ?? r.Status.ToString());
        }

        private static async Task<bool> WaitForStateAsync(NetworkOperatorTetheringManager m, TetheringOperationalState target, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                if (m.TetheringOperationalState == target) return true;
                await Task.Delay(300);
                waited += 300;
            }
            return m.TetheringOperationalState == target;
        }

        private async Task ApplyCore(NetworkOperatorTetheringManager m)
        {
            string ssid = txtSsid.Text.Trim();
            string pwd = txtPwd.Text;
            if (ssid.Length < 1 || ssid.Length > 32) { SetMsg("ssidErr"); return; }
            if (pwd.Length < 8 || pwd.Length > 63) { SetMsg("pwdErr"); return; }

            bool wasOn = m.TetheringOperationalState == TetheringOperationalState.On;

            if (wasOn)
            {
                NetworkOperatorTetheringOperationResult rs = await m.StopTetheringAsync();
                if (rs.Status != TetheringOperationStatus.Success)
                {
                    SetMsg("failed", rs.AdditionalErrorMessage ?? rs.Status.ToString());
                    return;
                }
                await WaitForStateAsync(m, TetheringOperationalState.Off, 8000);
                await Task.Delay(500);
            }

            NetworkOperatorTetheringAccessPointConfiguration cfg = new NetworkOperatorTetheringAccessPointConfiguration();
            cfg.Ssid = ssid;
            cfg.Passphrase = pwd;
            await m.ConfigureAccessPointAsync(cfg);

            if (wasOn)
            {
                NetworkOperatorTetheringOperationResult r = await m.StartTetheringAsync();
                if (r.Status == TetheringOperationStatus.Success) ResetDeadline();
                else { SetMsg("failed", r.AdditionalErrorMessage ?? r.Status.ToString()); return; }
            }
            SetMsg("applied");
        }

        // ───────── 刷新状态与设备列表 ─────────
        private async Task RefreshAsync(bool fillConfig)
        {
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                SyncPowerCheckbox();   // ← 新增:每次刷新都读一次系统值

                string errKey;
                NetworkOperatorTetheringManager m = HotspotService.CreateManager(out errKey);
                if (m == null)
                {
                    _lastState = null;
                    UpdateStateLabel();
                    lv.Items.Clear();
                    _hostIp = null;
                    lblHostIp.Text = T("hostip") + "-";
                    _deadline = null;
                    UpdateCountdown();
                    SetMsg(errKey);
                    return;
                }

                if (fillConfig)
                {
                    NetworkOperatorTetheringAccessPointConfiguration cfg = m.GetCurrentAccessPointConfiguration();
                    txtSsid.Text = cfg.Ssid;
                    txtPwd.Text = cfg.Passphrase;
                }

                TetheringOperationalState st = m.TetheringOperationalState;
                _lastState = st;
                UpdateStateLabel();

                if (st == TetheringOperationalState.On)
                {
                    if (!_deadline.HasValue) ResetDeadline(); // 热点已在运行(程序刚启动/外部开启)→ 从现在起计时

                    List<ClientInfo> clients = await HotspotService.GetClientsAsync(m);
                    lv.BeginUpdate();
                    lv.Items.Clear();
                    foreach (ClientInfo c in clients)
                        lv.Items.Add(new ListViewItem(new[] { c.Name, c.Ip, c.Mac }));
                    lv.EndUpdate();

                    _hostIp = HotspotService.GetHostIp();
                }
                else
                {
                    //_deadline = null;
                    if (st == TetheringOperationalState.Off) _deadline = null;
                    lv.Items.Clear();
                    _hostIp = null;
                }
                lblHostIp.Text = T("hostip") + (_hostIp ?? "-");
                UpdateCountdown();
            }
            catch (Exception ex)
            {
                SetMsg("failed", ex.Message);
            }
            finally
            {
                _refreshing = false;
            }
        }
    }
}
