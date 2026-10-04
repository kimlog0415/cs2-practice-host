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
        private readonly ComboBox _mode = new ComboBox();
        private readonly ComboBox _map = new ComboBox();
        private readonly ComboBox _bots = new ComboBox();
        private readonly ComboBox _level = new ComboBox();
        private readonly ComboBox _team = new ComboBox();
        private readonly Button _lang = new Button();
        private readonly Button _help = new Button();
        private readonly Button _launch = new Button();
        private readonly Button _copyLink = new Button();
        private readonly Button _copyAddress = new Button();
        private readonly LinkLabel _update = new LinkLabel();
        private readonly ToolTip _tips = new ToolTip();
        private readonly Timer _watch = new Timer { Interval = 2000 };

        /// <summary>줄 이름은 언어를 바꿀 때 다시 써야 하므로 들고 있는다.</summary>
        private readonly List<Label> _rowLabels = new List<Label>();

        private GameMap[] _maps = new GameMap[0];
        private bool _loading = true;

        /// <summary>▶로 서버를 여는 중. 이 동안은 진행 상황 문구를 유지하고 버튼을 잠근다.</summary>
        private bool _launching;
        private DateTime _launchedAt;

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
            Shown += (s, e) => { RefreshStatus(); _watch.Start(); ShowUpdateIfAny(); };
        }

        // ---------- 화면 ----------

        private void BuildLayout()
        {
            // 포터블 exe라 설치 목록에도 안 뜬다. 쓰는 판이 몇인지 볼 곳이 창 제목뿐이다
            Text = Program.Title + " " + Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            Font = new Font(Strings.UiFont, 10);
            ClientSize = new Size(424, 612);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            // exe에 박아 둔 자기 아이콘 (Valve 자산을 쓰지 않는다)
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch (Exception) { }

            _banner.Location = new Point(20, 14);
            _banner.Size = new Size(312, 32);
            _banner.TextAlign = ContentAlignment.MiddleLeft;
            _banner.Padding = new Padding(8, 0, 0, 0);
            _banner.Font = new Font(Strings.UiFont, 10, FontStyle.Bold);
            Controls.Add(_banner);

            _lang.Location = new Point(336, 14);
            _lang.Size = new Size(32, 32);
            _lang.Font = new Font(Strings.UiFont, 9, FontStyle.Bold);
            _lang.TabStop = false;
            _lang.Click += OnLangClick;
            Controls.Add(_lang);

            _help.Text = "?";
            _help.Location = new Point(372, 14);
            _help.Size = new Size(32, 32);
            _help.Font = new Font(Strings.UiFont, 11, FontStyle.Bold);
            _help.TabStop = false;
            _help.Click += OnHelpClick;
            Controls.Add(_help);

            int y = 62;
            AddRow(_mode, ref y);
            AddRow(_map, ref y);
            AddRow(_bots, ref y);
            AddRow(_level, ref y);
            AddRow(_team, ref y);

            _status.Location = new Point(20, y + 2);
            _status.Size = new Size(384, 192);
            _status.ForeColor = Color.DarkGreen;
            Controls.Add(_status);

            _launch.Location = new Point(20, 462);
            _launch.Size = new Size(384, 46);
            _launch.Font = new Font(Strings.UiFont, 11, FontStyle.Bold);
            _launch.Click += OnLaunchClick;
            Controls.Add(_launch);

            _copyLink.Location = new Point(20, 516);
            _copyLink.Size = new Size(384, 40);
            _copyLink.Click += OnCopyLinkClick;
            Controls.Add(_copyLink);

            _copyAddress.Location = new Point(20, 564);
            _copyAddress.Size = new Size(384, 32);
            _copyAddress.Click += OnCopyAddressClick;
            Controls.Add(_copyAddress);

            // 새 버전이 있을 때만 보인다. 그때 창이 그만큼 늘어난다
            _update.Location = new Point(20, 604);
            _update.Size = new Size(384, 22);
            _update.TextAlign = ContentAlignment.MiddleCenter;
            _update.Visible = false;
            _update.LinkClicked += (s, e) => OpenInBrowser(UpdateCheck.DownloadPage);
            Controls.Add(_update);

            SetStaticText();
        }

        private void AddRow(ComboBox box, ref int y)
        {
            var text = new Label { Location = new Point(20, y + 4), AutoSize = true };
            box.DropDownStyle = ComboBoxStyle.DropDownList;
            box.Location = new Point(120, y);
            box.Width = 284;
            box.MaxDropDownItems = 16;
            Controls.Add(text);
            Controls.Add(box);
            _rowLabels.Add(text);
            y += 40;
        }

        /// <summary>고른 값과 무관하게 늘 같은 자리에 있는 문구.</summary>
        private void SetStaticText()
        {
            string[] rows = { Strings.RowMode, Strings.RowMap, Strings.RowBots, Strings.RowLevel, Strings.RowTeam };
            for (int i = 0; i < _rowLabels.Count && i < rows.Length; i++)
                _rowLabels[i].Text = rows[i];

            _lang.Text = Strings.LangToggle;
            _launch.Text = Strings.Launch;
            _copyLink.Text = Strings.CopyLink;
            _copyAddress.Text = Strings.CopyAddress;

            _tips.SetToolTip(_lang, Strings.LangToggleTip);
            _tips.SetToolTip(_help, Strings.HelpTip);

            if (_newerVersion != null) _update.Text = Strings.UpdateFound(_newerVersion);
        }

        private void FillControls()
        {
            FillChoices();

            _mode.SelectedIndexChanged += OnModeChanged;
            foreach (ComboBox box in new[] { _map, _bots, _level, _team })
                box.SelectedIndexChanged += (s, e) => SaveAll();
        }

        /// <summary>맵을 뺀 나머지 목록. 언어를 바꾸면 보이는 이름이 달라지므로 다시 채운다.</summary>
        private void FillChoices()
        {
            _mode.Items.Clear();
            foreach (GameMode m in GameData.Modes) _mode.Items.Add(m.Name);

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
            _maps = SelectedMode.Maps
                .Where(m => File.Exists(Path.Combine(Cs2Paths.MapsDir, m.Id + ".vpk")))
                .ToArray();

            _map.Items.Clear();
            foreach (GameMap m in _maps) _map.Items.Add(m.Name);

            if (_maps.Length == 0) return;   // 설치된 맵이 없으면 고를 것도 없다 (빈 목록에 SelectedIndex를 주면 예외)

            int index = Array.FindIndex(_maps, m => m.Id == wantedId);
            if (index < 0) index = Array.FindIndex(_maps, m => m.Id == "de_dust2");
            _map.SelectedIndex = Math.Max(0, index);
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

            CfgWriter.WriteMatchCfgs(mode, map.Id);
            CfgWriter.WriteBotCfgs(
                _bots.SelectedIndex,
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

            string note = mode.BotsConfigurable ? OneSidedBotNote() : Strings.RetakeBotsNote;
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

            if (_launching && (DateTime.Now - _launchedAt).TotalMinutes > 4)
            {
                _launching = false;
                _launch.Enabled = true;
                _status.Text = Strings.ServerNotFound;
            }
        }

        /// <summary>처음 보는 서버면 링크를 복사해 알린다. ▶로 열었든 맵 안에서 F10으로 열었든 같다.</summary>
        private void NoticeNewServer(ServerInfo server)
        {
            _launching = false;
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
                   " +map " + map.Id;
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

        /// <summary>사용법은 웹에 둔다. 설명이 바뀔 때마다 앱을 다시 내보내지 않아도 된다.</summary>
        private static string HelpUrl
        {
            get { return "https://cs2.logstone.net/" + (Strings.En ? "?lang=en" : ""); }
        }

        /// <summary>확인은 네트워크를 타므로 화면이 멈추지 않게 뒤에서 돌린다.</summary>
        private async void ShowUpdateIfAny()
        {
            string newer = await Task.Run(() => UpdateCheck.NewerVersion());
            if (newer == null || IsDisposed) return;

            _newerVersion = newer;
            _update.Text = Strings.UpdateFound(newer);
            _update.Visible = true;
            ClientSize = new Size(ClientSize.Width, 642);
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
            string url = HelpUrl;
            try { Process.Start(url); }
            catch (Exception)
            {
                CopyToClipboard(url);
                Tell(Strings.CannotOpenGuide(url));
            }
        }
    }
}
