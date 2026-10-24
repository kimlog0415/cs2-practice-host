using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CS2PracticeHost
{
    internal sealed class MainForm : Form
    {
        private readonly Settings _settings;

        private readonly Label _banner = new Label();
        private readonly Label _status = new Label();

        // 고른 모드·맵은 보이지 않는 콤보 둘이 들고 있다. 위쪽 탭과 맵 타일은 그 값을 그리고 바꾸는 쪽이다 —
        // 저장·F10 파일·언어 전환이 전부 이 둘의 「몇 번째를 골랐나」로 짜여 있어서, 화면만 갈고 흐름은 그대로 둔다
        private readonly ComboBox _mode = new ComboBox { Visible = false };
        private readonly ComboBox _map = new ComboBox { Visible = false };
        private readonly Panel _tabs = new Panel();
        private readonly FlowLayoutPanel _tileArea = new FlowLayoutPanel();
        private readonly RadioButton _kindBuiltIn = new RadioButton();
        private readonly RadioButton _kindWorkshop = new RadioButton();

        /// <summary>지금 타일 칸에 보이는 쪽. 기본 맵과 창작마당을 왼쪽 탭으로 갈라 하나씩 보여 준다.</summary>
        private bool _showWorkshop;

        private readonly ComboBox _bots = new ComboBox();
        private readonly ComboBox _level = new ComboBox();
        private readonly ComboBox _team = new ComboBox();
        private readonly Button _lang = new Button();
        private readonly Button _help = new Button();
        private readonly Button _launch = new Button();
        private readonly Button _copyLink = new Button();
        private readonly Button _copyAddress = new Button();
        private readonly Panel _update = new Panel();
        private readonly Label _updateTitle = new Label();
        private readonly Label _updateVersions = new Label();
        private readonly Label _updateHow = new Label();
        private readonly Button _updateGet = new Button();
        private readonly LinkLabel _updateLater = new LinkLabel();
        private readonly ToolTip _tips = new ToolTip();
        private readonly Timer _watch = new Timer { Interval = 2000 };

        /// <summary>줄 이름은 언어를 바꿀 때 다시 써야 하므로 들고 있는다.</summary>
        private readonly List<Label> _rowLabels = new List<Label>();

        private GameMap[] _maps = new GameMap[0];
        private bool _loading = true;

        /// <summary>▶로 서버를 여는 중. 이 동안은 진행 상황 문구를 유지하고 버튼을 잠근다.</summary>
        private bool _launching;
        private DateTime _launchedAt;

        /// <summary>이번 ▶로 CS2가 실제로 떴는지. 떴다가 사라지면 사용자가 끈 것이다.</summary>
        private bool _sawCs2;

        /// <summary>이미 복사해 준 서버. 맵 안에서 F10으로 연 서버도 이걸로 알아챈다.</summary>
        private string _copiedAddress;

        /// <summary>언어를 바꿔도 알림 문구를 다시 쓸 수 있게 남겨 둔다.</summary>
        private string _newerVersion;

        public MainForm(Settings settings)
        {
            _settings = settings;
            BuildLayout();
            FillControls();
            RestoreSelection();
            _loading = false;

            CfgWriter.WriteBindCfg();
            SaveAll();

            _watch.Tick += (s, e) => RefreshStatus();
            // 고른 타일로 내려 주는 것은 창이 뜬 뒤에야 먹는다. 그 전엔 칸이 맨 위에 서서 고른 맵이 안 보인다
            Shown += (s, e) =>
            {
                BeginInvoke((Action)SyncTileSelection);   // 타일 배치가 끝난 다음 차례에
                RefreshStatus(); _watch.Start(); ShowUpdateIfAny(); LookUpWorkshopIfAny(); LoadBuiltInPictures();
            };
        }

        // ---------- 화면 ----------

        private void BuildLayout()
        {
            // 포터블 exe라 설치 목록에도 안 뜬다. 쓰는 판이 몇인지 볼 곳이 창 제목뿐이다
            Text = Program.Title + " " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            Font = new Font(Strings.UiFont, 10);
            ClientSize = new Size(760, 636);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // exe에 박아 둔 자기 아이콘 (Valve 자산을 쓰지 않는다)
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            _banner.Location = new Point(20, 14);
            _banner.Size = new Size(648, 32);
            _banner.TextAlign = ContentAlignment.MiddleLeft;
            _banner.Padding = new Padding(8, 0, 0, 0);
            _banner.Font = new Font(Strings.UiFont, 10, FontStyle.Bold);
            Controls.Add(_banner);

            _lang.Location = new Point(672, 14);
            _lang.Size = new Size(32, 32);
            _lang.Font = new Font(Strings.UiFont, 9, FontStyle.Bold);
            _lang.TabStop = false;
            _lang.Click += OnLangClick;
            Controls.Add(_lang);

            _help.Text = "?";
            _help.Location = new Point(708, 14);
            _help.Size = new Size(32, 32);
            _help.Font = new Font(Strings.UiFont, 11, FontStyle.Bold);
            _help.TabStop = false;
            _help.Click += OnHelpClick;
            Controls.Add(_help);

            Controls.Add(_mode);
            Controls.Add(_map);

            _tabs.Location = new Point(20, 56);
            _tabs.Size = new Size(720, 36);
            Controls.Add(_tabs);

            BuildTileArea();

            int x = 20;
            AddRow(_bots, 150, ref x);
            AddRow(_level, 150, ref x);
            AddRow(_team, 0, ref x);

            _status.Location = new Point(20, 452);
            _status.Size = new Size(720, 104);
            _status.ForeColor = Color.DarkGreen;
            Controls.Add(_status);

            _launch.Location = new Point(20, 566);
            _launch.Size = new Size(330, 52);
            _launch.Font = new Font(Strings.UiFont, 11, FontStyle.Bold);
            _launch.Click += OnLaunchClick;
            Controls.Add(_launch);

            _copyLink.Location = new Point(360, 566);
            _copyLink.Size = new Size(220, 52);
            _copyLink.Click += OnCopyLinkClick;
            Controls.Add(_copyLink);

            _copyAddress.Location = new Point(590, 566);
            _copyAddress.Size = new Size(150, 52);
            _copyAddress.Click += OnCopyAddressClick;
            Controls.Add(_copyAddress);

            BuildUpdateOverlay();

            SetStaticText();
        }

        /// <summary>
        /// 새 판 알림. 띠 아래 한 줄로 두니 놓치고 지나가서 창을 덮는 판으로 바꿨다(2026-10-17 디렉터 지시).
        /// 닫을 수는 있게 둔다 — 버전 확인은 GitHub에 묻는 일이라 실패할 수 있고,
        /// 못 닫게 하면 그때 멀쩡한 도구가 안 열린다. 닫아도 다음에 열면 또 뜬다.
        /// </summary>
        private void BuildUpdateOverlay()
        {
            _update.Location = new Point(0, 0);
            _update.Size = ClientSize;
            _update.BackColor = Color.White;
            _update.Visible = false;

            SetUpdateFonts();

            int mid = ClientSize.Width / 2;
            _updateTitle.Location = new Point(mid - 192, 180);
            _updateTitle.Size = new Size(384, 40);
            _updateTitle.TextAlign = ContentAlignment.MiddleCenter;
            _update.Controls.Add(_updateTitle);

            _updateVersions.Location = new Point(mid - 192, 228);
            _updateVersions.Size = new Size(384, 30);
            _updateVersions.TextAlign = ContentAlignment.MiddleCenter;
            _update.Controls.Add(_updateVersions);

            _updateHow.Location = new Point(mid - 192, 282);
            _updateHow.Size = new Size(384, 56);
            _updateHow.TextAlign = ContentAlignment.MiddleCenter;
            _update.Controls.Add(_updateHow);

            _updateGet.Location = new Point(mid - 120, 360);
            _updateGet.Size = new Size(240, 44);
            _updateGet.Click += (s, e) => OpenInBrowser(SiteUrl);
            _update.Controls.Add(_updateGet);

            _updateLater.Location = new Point(mid - 120, 416);
            _updateLater.Size = new Size(240, 22);
            _updateLater.TextAlign = ContentAlignment.MiddleCenter;
            _updateLater.LinkClicked += (s, e) => _update.Visible = false;
            _update.Controls.Add(_updateLater);

            Controls.Add(_update);
        }

        private void SetUpdateFonts()
        {
            _updateTitle.Font = new Font(Strings.UiFont, 15, FontStyle.Bold);
            _updateVersions.Font = new Font(Strings.UiFont, 12, FontStyle.Bold);
            _updateGet.Font = new Font(Strings.UiFont, 11, FontStyle.Bold);
        }

        /// <summary>
        /// 왼쪽 탭(기본 맵 / 창작마당)과 그 옆 타일 칸. 한 번에 한 쪽만 보여 준다 — 둘을 위아래로 쌓으니
        /// 창작마당이 많을 때 기본 맵이 밀려 올라가 안 보였다(2026-10-24 디렉터 지시로 탭을 나눔).
        /// </summary>
        private void BuildTileArea()
        {
            int y = 100;
            foreach (RadioButton kind in new[] { _kindBuiltIn, _kindWorkshop })
            {
                kind.Appearance = Appearance.Button;
                kind.AutoCheck = false;
                kind.FlatStyle = FlatStyle.Flat;
                kind.TextAlign = ContentAlignment.MiddleCenter;
                kind.Location = new Point(20, y);
                kind.Size = new Size(96, 56);
                kind.TabStop = false;
                kind.FlatAppearance.CheckedBackColor = Color.FromArgb(25, 118, 210);
                kind.FlatAppearance.BorderColor = Color.FromArgb(200, 204, 210);
                Controls.Add(kind);
                y += 60;
            }
            _kindBuiltIn.Click += (s, e) => ShowKind(false);
            _kindWorkshop.Click += (s, e) => ShowKind(true);

            _tileArea.Location = new Point(124, 100);
            _tileArea.Size = new Size(616, 300);
            _tileArea.FlowDirection = FlowDirection.LeftToRight;
            _tileArea.WrapContents = true;
            _tileArea.AutoScroll = true;
            _tileArea.BackColor = Color.FromArgb(246, 247, 249);
            _tileArea.Padding = new Padding(4);
            Controls.Add(_tileArea);
        }

        private void ShowKind(bool workshop)
        {
            if (workshop && !_maps.Any(m => m.IsWorkshop)) return;   // 이 모드에 맞는 창작마당 맵이 없다
            if (workshop == _showWorkshop) { RescanWorkshop(this, EventArgs.Empty); return; }
            _showWorkshop = workshop;
            RenderTiles();
            SyncTileSelection();
        }

        /// <summary>봇 칸 셋은 타일 아래 한 줄에 나란히. 마지막 칸은 남은 폭을 다 쓴다.</summary>
        private void AddRow(ComboBox box, int width, ref int x)
        {
            const int y = 412;
            var text = new Label { Location = new Point(x, y + 4), AutoSize = true };
            Controls.Add(text);
            _rowLabels.Add(text);

            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.Location = new Point(x + 74, y);
            box.Width = width > 0 ? width : 740 - (x + 74);
            box.MaxDropDownItems = 16;
            Controls.Add(box);
            x += 74 + box.Width + 16;
        }

        /// <summary>고른 값과 무관하게 늘 같은 자리에 있는 문구.</summary>
        private void SetStaticText()
        {
            string[] rows = { Strings.RowBots, Strings.RowLevel, Strings.RowTeam };
            for (int i = 0; i < _rowLabels.Count && i < rows.Length; i++)
                _rowLabels[i].Text = rows[i];

            SyncKindTabs();

            _lang.Text = Strings.LangToggle;
            _launch.Text = Strings.Launch;
            _copyLink.Text = Strings.CopyLink;
            _copyAddress.Text = Strings.CopyAddress;

            _tips.SetToolTip(_lang, Strings.LangToggleTip);
            _tips.SetToolTip(_help, Strings.HelpTip);

            _updateTitle.Text = Strings.UpdateTitle;
            _updateHow.Text = Strings.UpdateHow;
            _updateGet.Text = Strings.UpdateGet;
            _updateLater.Text = Strings.UpdateLater;
            if (_newerVersion != null)
                _updateVersions.Text = Strings.UpdateVersions(MyVersion, _newerVersion);
        }

        private void FillControls()
        {
            FillChoices();

            _mode.SelectedIndexChanged += OnModeChanged;
            _mode.SelectedIndexChanged += (s, e) => SyncTabs();
            _map.SelectedIndexChanged += (s, e) => SyncTileSelection();
            foreach (ComboBox box in new[] { _map, _bots, _level, _team })
                box.SelectedIndexChanged += (s, e) => SaveAll();

            // Steam에서 새로 구독하고 돌아오면 창이 다시 앞으로 온다 — 그때 폴더를 다시 본다
            Activated += RescanWorkshop;
        }

        /// <summary>
        /// 앱을 켜 둔 채 Steam에서 새로 구독한 창작마당 맵이 바로 보이게 폴더를 다시 본다.
        /// 창이 앞으로 올 때와 지금 탭을 한 번 더 누를 때 부른다.
        /// 달라진 게 없으면 손대지 않는다 — 부를 때마다 타일이 깜빡이지 않게.
        /// </summary>
        private void RescanWorkshop(object sender, EventArgs e)
        {
            if (_mode.SelectedIndex < 0) return;

            string[] now = WorkshopMaps.For(SelectedMode).Select(m => m.Id).ToArray();
            string[] shown = _maps.Where(m => m.IsWorkshop).Select(m => m.Id).ToArray();
            if (now.SequenceEqual(shown)) return;

            GameMap keep = SelectedMap;
            bool viewing = _showWorkshop;
            bool wasLoading = _loading;
            _loading = true;
            FillMaps(keep != null ? keep.Id : null);
            _loading = wasLoading;
            SaveAll();

            // 창작마당 칸을 보다가 새 맵이 생겼으면 그 칸에 머문다 — 고른 맵 쪽으로 튀면 새 맵이 안 보인다
            if (viewing != _showWorkshop) ShowKind(viewing);

            LookUpWorkshopIfAny();   // 새로 받은 맵의 제목·태그 — 모르는 번호만 묻는다
        }

        /// <summary>맵을 뺀 나머지 목록. 언어를 바꾸면 보이는 이름이 달라지므로 다시 채운다.</summary>
        private void FillChoices()
        {
            _mode.Items.Clear();
            foreach (GameMode m in GameData.Modes) _mode.Items.Add(m.Name);
            BuildTabs();

            _bots.Items.Clear();
            foreach (string c in GameData.BotCounts) _bots.Items.Add(c);

            _level.Items.Clear();
            foreach (Choice<int> l in GameData.BotLevels) _level.Items.Add(l.Name);

            _team.Items.Clear();
            foreach (Choice<string> t in GameData.BotTeams) _team.Items.Add(t.Name);
        }

        private void RestoreSelection()
        {
            _mode.SelectedIndex = Math.Max(0, GameData.IndexOfMode(_settings.Mode));
            FillMaps(_settings.Map ?? "de_dust2");

            _bots.SelectedIndex = _settings.Bots >= 0 && _settings.Bots < _bots.Items.Count
                ? _settings.Bots : 0;

            // 난이도만 기본값이 첫 칸이 아니다. Math.Max로 받으면 제대로 찾은 0(쉬움)까지
            // 1(보통)로 끌어올려, 쉬움을 골라 둔 사람이 다시 켤 때마다 보통으로 돌아간다
            int level = GameData.IndexOfLevel(_settings.Level);
            _level.SelectedIndex = level >= 0 ? level : 1;

            _team.SelectedIndex = Math.Max(0, GameData.IndexOfTeam(_settings.Team));
        }

        private GameMode SelectedMode { get { return GameData.Modes[_mode.SelectedIndex]; } }

        private GameMap SelectedMap
        {
            get { return _map.SelectedIndex >= 0 ? _maps[_map.SelectedIndex] : null; }
        }

        /// <summary>설치된 맵만 보여준다. 모드를 바꿔도 같은 맵이 있으면 유지한다.</summary>
        private void FillMaps(string wantedId)
        {
            // 받아 둔 창작마당 맵은 제작자가 단 태그로 걸러 그 모드에 맞는 것만 붙인다
            _maps = SelectedMode.Maps
                .Where(m => File.Exists(Path.Combine(Cs2Paths.MapsDir, m.Id + ".vpk")))
                .Concat(WorkshopMaps.For(SelectedMode))
                .ToArray();

            _map.Items.Clear();
            foreach (GameMap m in _maps) _map.Items.Add(m.Name);

            int index = Array.FindIndex(_maps, m => m.Id == wantedId);
            if (index < 0) index = Array.FindIndex(_maps, m => m.Id == "de_dust2");
            if (_maps.Length > 0) index = Math.Max(0, index);

            // 왼쪽 탭은 고른 맵이 있는 쪽을 연다 — 창작마당 맵을 골라 둔 채 켜면 창작마당 칸부터
            _showWorkshop = index >= 0 && _maps[index].IsWorkshop;
            RenderTiles();

            if (index < 0) return;   // 설치된 맵이 없으면 고를 것도 없다 (빈 목록에 SelectedIndex를 주면 예외)
            _map.SelectedIndex = index;
            SyncTileSelection();   // 같은 자리를 다시 고르면 SelectedIndexChanged가 안 와서 직접 맞춘다
        }

        // ---------- 탭과 타일 ----------

        /// <summary>모드 탭. 게임 '연습' 탭의 순서 그대로다. 언어를 바꾸면 이름이 바뀌니 다시 만든다.</summary>
        private void BuildTabs()
        {
            _tabs.Controls.Clear();
            int count = GameData.Modes.Length;
            int width = (_tabs.Width - (count - 1) * 4) / count;
            for (int i = 0; i < count; i++)
            {
                int index = i;
                var tab = new RadioButton
                {
                    Text = GameData.Modes[i].Name,
                    Appearance = Appearance.Button,
                    AutoCheck = false,              // 고른 것은 _mode가 정한다. 눌렀다고 먼저 바뀌면 둘이 어긋난다
                    TextAlign = ContentAlignment.MiddleCenter,
                    FlatStyle = FlatStyle.Flat,
                    Location = new Point(i * (width + 4), 0),
                    Size = new Size(width, _tabs.Height),
                    TabStop = false,
                };
                tab.FlatAppearance.CheckedBackColor = Color.FromArgb(25, 118, 210);
                tab.FlatAppearance.BorderColor = Color.FromArgb(200, 204, 210);
                tab.Click += (s, e) => OnTabClick(index);
                _tabs.Controls.Add(tab);
            }
            SyncTabs();
        }

        private void OnTabClick(int index)
        {
            if (index == _mode.SelectedIndex) { RescanWorkshop(this, EventArgs.Empty); return; }
            _mode.SelectedIndex = index;   // OnModeChanged가 맵 타일을 다시 채운다
        }

        private void SyncTabs()
        {
            for (int i = 0; i < _tabs.Controls.Count; i++)
            {
                var tab = (RadioButton)_tabs.Controls[i];
                tab.Checked = i == _mode.SelectedIndex;
                tab.ForeColor = tab.Checked ? Color.White : SystemColors.ControlText;
                tab.Font = new Font(Strings.UiFont, 10, tab.Checked ? FontStyle.Bold : FontStyle.Regular);
            }
        }

        /// <summary>_maps 중 왼쪽 탭이 가리키는 쪽만 타일로.</summary>
        private void RenderTiles()
        {
            _tileArea.SuspendLayout();
            foreach (Control old in _tileArea.Controls.Cast<Control>().ToArray()) old.Dispose();
            _tileArea.Controls.Clear();

            for (int i = 0; i < _maps.Length; i++)
            {
                if (_maps[i].IsWorkshop != _showWorkshop) continue;
                int index = i;
                var tile = new MapTile(_maps[i]);
                tile.Click += (s, e) => _map.SelectedIndex = index;
                _tileArea.Controls.Add(tile);
            }

            _tileArea.AutoScrollPosition = new Point(0, 0);
            _tileArea.ResumeLayout();
            SyncKindTabs();
        }

        /// <summary>왼쪽 탭 — 지금 보이는 쪽을 켜고, 맵 수를 붙인다. 창작마당 맵이 없는 모드면 그 탭은 흐리게.</summary>
        private void SyncKindTabs()
        {
            int workshop = _maps.Count(m => m.IsWorkshop);
            _kindBuiltIn.Text = Strings.KindBuiltIn + "\n" + (_maps.Length - workshop);
            _kindWorkshop.Text = Strings.KindWorkshop + "\n" + workshop;
            _kindWorkshop.Enabled = workshop > 0;

            foreach (RadioButton kind in new[] { _kindBuiltIn, _kindWorkshop })
            {
                kind.Checked = (kind == _kindWorkshop) == _showWorkshop;
                kind.ForeColor = kind.Checked ? Color.White : SystemColors.ControlText;
                kind.Font = new Font(Strings.UiFont, 10, kind.Checked ? FontStyle.Bold : FontStyle.Regular);
            }
        }

        private IEnumerable<MapTile> Tiles
        {
            get { return _tileArea.Controls.OfType<MapTile>(); }
        }

        private void SyncTileSelection()
        {
            GameMap selected = SelectedMap;
            foreach (MapTile tile in Tiles)
            {
                tile.Selected = selected != null && tile.Map.Id == selected.Id;
                if (tile.Selected) ScrollTo(tile);
            }
        }

        /// <summary>
        /// 고른 타일이 칸 밖이면 보이게 내린다. 창이 뜨기 전엔 먹지 않아서 Shown 뒤에 한 번 더 부른다.
        /// </summary>
        private void ScrollTo(MapTile tile)
        {
            if (!_tileArea.IsHandleCreated) return;
            _tileArea.PerformLayout();   // 막 다시 채운 직후면 타일이 아직 제자리가 아니라 위치를 잘못 잰다
            _tileArea.ScrollControlIntoView(tile);
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            // 목록을 비우는 동안에도 불린다. 그때는 고른 모드가 없어 읽을 것이 없다
            if (_mode.SelectedIndex < 0) return;

            GameMap keep = SelectedMap;

            // 시작할 때도 불리므로 불러오는 중이라는 표시를 덮어쓰면 안 된다
            bool wasLoading = _loading;
            _loading = true;
            FillMaps(keep != null ? keep.Id : null);
            _loading = wasLoading;

            SaveAll();
        }

        // ---------- 한국어 / 영어 ----------

        private void OnLangClick(object sender, EventArgs e)
        {
            Strings.En = !Strings.En;
            _settings.Lang = Strings.Code;
            _settings.Save();
            ApplyLanguage();
        }

        /// <summary>
        /// 보이는 이름이 전부 바뀌므로 목록을 다시 채운다. 고른 자리는 언어와 무관한 값으로
        /// 들고 있다가 되살린다 — 이름으로 찾으면 바뀐 이름과 안 맞아 기본값으로 떨어진다.
        /// </summary>
        private void ApplyLanguage()
        {
            string mode = SelectedMode.Key;
            GameMap map = SelectedMap;
            string mapId = map != null ? map.Id : null;
            int bots = _bots.SelectedIndex;
            int level = _level.SelectedIndex;
            int team = _team.SelectedIndex;

            bool wasLoading = _loading;
            _loading = true;

            Font = new Font(Strings.UiFont, 10);
            _banner.Font = new Font(Strings.UiFont, 10, FontStyle.Bold);
            _lang.Font = new Font(Strings.UiFont, 9, FontStyle.Bold);
            _help.Font = new Font(Strings.UiFont, 11, FontStyle.Bold);
            _launch.Font = new Font(Strings.UiFont, 11, FontStyle.Bold);
            SetUpdateFonts();

            SetStaticText();
            FillChoices();

            _mode.SelectedIndex = Math.Max(0, GameData.IndexOfMode(mode));
            FillMaps(mapId);
            if (bots >= 0 && bots < _bots.Items.Count) _bots.SelectedIndex = bots;
            if (level >= 0) _level.SelectedIndex = level;
            if (team >= 0) _team.SelectedIndex = team;

            _loading = wasLoading;
            SaveAll();
            RefreshStatus();
        }

        // ---------- 저장 ----------

        private void SaveAll()
        {
            // 하나라도 아직 안 정해졌으면 저장하지 않는다 (되살리는 도중에 불릴 수 있다)
            if (_loading) return;
            foreach (ComboBox box in new[] { _mode, _map, _bots, _level, _team })
                if (box.SelectedIndex < 0) return;

            GameMode mode = SelectedMode;
            GameMap map = SelectedMap;

            // 탈환은 모드 규칙이 봇을 정해서 설정이 먹지 않는다
            foreach (ComboBox box in new[] { _bots, _level, _team })
                box.Enabled = mode.BotsConfigurable;

            CfgWriter.WriteMatchCfgs(mode, map);
            // 잠긴 칸의 값을 쓰면 안 된다 — 탈환은 gamemode_casual_server.cfg도 읽어서
            // 그 값이 모드 규칙을 덮는다(봇이 1명만 나왔다). 0번(기본)이면 파일을 지운다
            CfgWriter.WriteBotCfgs(
                mode.BotsConfigurable ? _bots.SelectedIndex : 0,
                GameData.BotLevels[_level.SelectedIndex].Value,
                GameData.BotTeams[_team.SelectedIndex].Value);

            // 보이는 이름은 언어에 따라 바뀌므로, 남길 때는 언어와 무관한 값으로
            _settings.Mode = mode.Key;
            _settings.Map = map.Id;
            _settings.Bots = _bots.SelectedIndex;
            _settings.Level = GameData.BotLevels[_level.SelectedIndex].Value.ToString();
            _settings.Team = GameData.BotTeams[_team.SelectedIndex].Value;
            _settings.Save();

            if (_launching) return;   // 서버 여는 중에는 진행 상황 문구를 유지한다

            string note = mode.BotsConfigurable ? OneSidedBotNote() : Strings.ModeBotsNote(mode.Key);
            if (map.IsWorkshop) note += Strings.WorkshopF10Note;
            _status.Text = Strings.Saved(mode.Name, map.Name, note);
        }

        /// <summary>
        /// 봇을 한쪽 팀으로 몰면 게임이 팀 인원 차이를 2명으로 제한해 「사람 수 + 2」에서 멈춘다.
        /// 고를 수는 있는데 그만큼 안 나오는 상태라, 이유를 알려주지 않으면 고장으로 보인다.
        /// </summary>
        private string OneSidedBotNote()
        {
            bool oneSided = _team.SelectedIndex >= 0
                && GameData.BotTeams[_team.SelectedIndex].Value != "any";

            if (!oneSided || _bots.SelectedIndex <= 1) return "";

            return Strings.OneSidedBotNote;
        }

        // ---------- 상태 표시줄 ----------

        /// <summary>상태를 읽어 표시줄을 맞추고, 새 서버가 보이면 링크를 복사한다.</summary>
        private void RefreshStatus()
        {
            Cs2Status status = Cs2Watcher.Read();

            _launch.Text = status.State == Cs2State.Off ? Strings.Launch : Strings.Relaunch;

            switch (status.State)
            {
                case Cs2State.Off:
                    SetBanner(Color.Gainsboro, Color.DimGray, Strings.BannerOff);
                    break;
                case Cs2State.NoLog:
                    SetBanner(Color.FromArgb(255, 243, 205), Color.DarkGoldenrod, Strings.BannerNoLog);
                    break;
                case Cs2State.NoServer:
                    SetBanner(Color.FromArgb(209, 231, 248), Color.SteelBlue, Strings.BannerNoServer);
                    break;
                case Cs2State.ServerOpen:
                    SetBanner(Color.FromArgb(212, 237, 218), Color.DarkGreen,
                        Strings.BannerServerOpen(status.Server.OpenedAt.ToString("HH:mm")));
                    NoticeNewServer(status.Server);
                    return;
            }

            if (!_launching) return;

            if (status.State != Cs2State.Off) { _sawCs2 = true; return; }

            // 떴던 CS2가 사라졌다 = 서버를 열기 전에 껐다. 바로 풀어 준다 —
            // 안 풀면 아래 4분이 지날 때까지 아무것도 못 한다
            if (_sawCs2) { EndLaunch(Strings.Cs2ClosedBeforeServer); return; }

            // 아직 뜨지도 않았다. Steam이 느릴 수 있으니 조금 기다렸다 푼다
            if ((DateTime.Now - _launchedAt).TotalSeconds > 90) EndLaunch(Strings.ServerNotFound);
        }

        private void EndLaunch(string why)
        {
            _launching = false;
            _sawCs2 = false;
            _launch.Enabled = true;
            _status.Text = why;
        }

        /// <summary>처음 보는 서버면 링크를 복사해 알린다. ▶로 열었든 맵 안에서 F10으로 열었든 같다.</summary>
        private void NoticeNewServer(ServerInfo server)
        {
            _launching = false;
            _sawCs2 = false;
            _launch.Enabled = true;

            if (server.Address == _copiedAddress) return;
            _copiedAddress = server.Address;

            SystemSounds.Asterisk.Play();
            _status.Text = Strings.ServerOpened(CopyToClipboard(server.JoinLink), server.JoinLink);
        }

        private void SetBanner(Color back, Color fore, string text)
        {
            _banner.BackColor = back;
            _banner.ForeColor = fore;
            _banner.Text = text;
        }

        // ---------- CS2 켜고 서버 열기 ----------

        private async void OnLaunchClick(object sender, EventArgs e)
        {
            if (Cs2Paths.SteamExe == null || !File.Exists(Cs2Paths.SteamExe))
            {
                Tell(Strings.SteamNotFound);
                return;
            }

            Cs2Status before = Cs2Watcher.Read();
            if (before.State != Cs2State.Off)
            {
                // 버튼에 "다시 켜고"라고 적혀 있으니 그대로 한다.
                // 서버가 열려 있을 때만 묻는다 — 친구들이 들어와 있으면 말없이 끊으면 안 된다.
                if (before.State == Cs2State.ServerOpen)
                {
                    DialogResult answer = MessageBox.Show(
                        Strings.RelaunchWarning,
                        Program.Title, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                    if (answer != DialogResult.Yes) return;
                }

                _launch.Enabled = false;
                Cursor = Cursors.WaitCursor;
                await Task.Run(() => Cs2Process.CloseAll());
                Cursor = Cursors.Default;
                _launch.Enabled = true;
            }

            GameMode mode = SelectedMode;
            GameMap map = SelectedMap;
            if (map == null)
            {
                Tell(Strings.NoMaps);
                return;
            }

            CfgWriter.WriteBindCfg();
            Process.Start(Cs2Paths.SteamExe, LaunchArguments(mode, map));

            _launchedAt = DateTime.Now;
            _launching = true;
            _sawCs2 = false;
            _launch.Enabled = false;
            _status.Text = Strings.Launching(mode.Name, map.Name);
        }

        /// <summary>-condebug·바인드까지 인자로 붙여 사용자가 Steam 설정을 건드릴 일이 없게 한다.</summary>
        private static string LaunchArguments(GameMode mode, GameMap map)
        {
            return "-applaunch 730 -condebug" +
                   " +exec " + CfgWriter.BindCfg +
                   " +game_type " + mode.Type +
                   " +game_mode " + mode.Mode +
                   " " + map.LaunchArgument;
        }

        // ---------- 친구에게 보내기 ----------

        private void OnCopyLinkClick(object sender, EventArgs e)
        {
            ServerInfo server = FindServer();
            if (server == null) return;

            Tell(Strings.LinkCopied(CopyToClipboard(server.JoinLink), server.JoinLink));
        }

        private void OnCopyAddressClick(object sender, EventArgs e)
        {
            ServerInfo server = FindServer();
            if (server == null) return;

            Tell(Strings.AddressCopied(CopyToClipboard(server.ConnectCommand), server.ConnectCommand));
        }

        /// <summary>지금 살아 있는 서버만 돌려준다. 죽은 주소를 친구에게 보내면 안 된다.</summary>
        private ServerInfo FindServer()
        {
            Cs2Status status = Cs2Watcher.Read();
            switch (status.State)
            {
                case Cs2State.Off:
                    Tell(Strings.Cs2Off);
                    return null;

                case Cs2State.NoLog:
                    Tell(Strings.Cs2NotOurs);
                    return null;

                // F10은 맵을 바꾸는 키라 서버가 돌고 있어야 먹는다. 여기선 ▶ 말고 길이 없다.
                case Cs2State.NoServer:
                    Tell(Strings.NoServerOpen);
                    return null;

                default:
                    return status.Server;
            }
        }

        /// <summary>
        /// Windows는 클립보드를 한 번에 한 프로그램만 잡는다. 서버가 열리는 순간은 CS2가
        /// 전면이라 배경에 있는 우리가 쓰려 하면 거부당한다. 그래서 몇 번 다시 시도하고,
        /// 그래도 안 되면 실패를 알린다 — 조용히 삼키면 복사된 줄 알고 엉뚱한 걸 붙여넣는다.
        /// </summary>
        private static bool CopyToClipboard(string text)
        {
            try
            {
                Clipboard.SetDataObject(text, true, 20, 100);
                return true;
            }
            catch (Exception) { return false; }
        }

        private void Tell(string text)
        {
            MessageBox.Show(text, Program.Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 사용법과 받는 버튼이 다 있는 우리 페이지. 설명이 바뀔 때마다 앱을 다시 내보내지 않아도 된다.
        /// 새 버전 알림도 여기로 보낸다 — 저장소 릴리스 페이지는 받을 것이 넷이고(소스 묶음 둘은
        /// GitHub이 자동으로 붙여 뺄 수 없다) 그중 맞는 건 하나라, 묻지도 않고 뜨는 알림이
        /// 보낼 곳이 아니다.
        /// </summary>
        private static string SiteUrl
        {
            get { return "https://cs2.logstone.net/" + (Strings.En ? "?lang=en" : ""); }
        }

        /// <summary>
        /// 받아 둔 창작마당 맵의 제목·태그·대표 그림을 Steam에 물어본다. 적어 둔 것이 있으면 안 나간다.
        /// 받아 오기 전에는 맵 이름만, Classic 자리에 있다가, 받아 오면 그 자리에서 바뀐다.
        /// </summary>
        private async void LookUpWorkshopIfAny()
        {
            string[] ids = WorkshopMaps.InstalledIds();
            if (ids.Length == 0) return;

            bool gained = await Task.Run(() => WorkshopInfo.FetchMissing(ids));
            if (IsDisposed) return;
            if (gained)
            {
                GameMap keep = SelectedMap;
                bool wasLoading = _loading;
                _loading = true;
                FillMaps(keep != null ? keep.Id : null);
                _loading = wasLoading;
                SaveAll();
            }

            // 그림은 제목보다 늦게 와도 된다 — 받은 것만 그 타일에서 갈아 끼운다
            bool pictures = await Task.Run(() => Thumbs.FetchMissing(ids));
            if (!pictures || IsDisposed) return;
            foreach (MapTile tile in Tiles) tile.ReloadPicture();
        }

        /// <summary>
        /// 기본 맵 그림을 처음 한 번 게임 파일에서 꺼낸다. 게임 데이터 목차를 읽는 데 시간이 들어 뒤에서 하고,
        /// 다 되면 그 자리에서 갈아 끼운다. 이미 꺼내 둔 맵만 있으면 아무것도 안 연다.
        /// </summary>
        private async void LoadBuiltInPictures()
        {
            string[] ids = GameData.Modes.SelectMany(m => m.Maps).Select(m => m.Id).Distinct()
                .Where(id => File.Exists(Path.Combine(Cs2Paths.MapsDir, id + ".vpk"))).ToArray();

            bool gained = await Task.Run(() => Thumbs.FetchBuiltInMissing(ids));
            if (!gained || IsDisposed) return;
            foreach (MapTile tile in Tiles) tile.ReloadPicture();
        }

        /// <summary>확인은 네트워크를 타므로 화면이 멈추지 않게 뒤에서 돌린다.</summary>
        private async void ShowUpdateIfAny()
        {
            string newer = await Task.Run(() => UpdateCheck.NewerVersion());
            if (newer == null || IsDisposed) return;

            _newerVersion = newer;
            _updateVersions.Text = Strings.UpdateVersions(MyVersion, newer);
            _update.Visible = true;
            _update.BringToFront();
        }

        private static string MyVersion
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(3); }
        }

        private void OpenInBrowser(string url)
        {
            try { Process.Start(url); }
            catch (Exception)
            {
                CopyToClipboard(url);
                Tell(Strings.CannotOpenPage(url));
            }
        }

        private void OnHelpClick(object sender, EventArgs e)
        {
            string url = SiteUrl;
            try { Process.Start(url); }
            catch (Exception)
            {
                CopyToClipboard(url);
                Tell(Strings.CannotOpenGuide(url));
            }
        }
    }
}
