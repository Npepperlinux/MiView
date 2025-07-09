using MiView.Common.Fonts;
using MiView.Common.Fonts.Material;
using MiView.Common.TimeLine;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace MiView
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            this.label1.Font = new FontLoader().LoadFontFromFile(FontLoader.FONT_SELECTOR.MATERIALICONS, this.label1.Font.Size);
            this.label1.Text = MaterialIcons.Keyboard;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
#if DEBUG
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Direct,
                ISLOCAL = true,
                DETAIL = "これはデバッグ実行時に表示されます。",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Direct,
                ISLOCAL = true,
                RENOTED = true,
                DETAIL = "リノート表示。",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Public,
                ISLOCAL = true,
                RENOTED = false,
                REPLAYED = true,
                DETAIL = "リプライ表示。",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Public,
                ISLOCAL = true,
                RENOTED = false,
                REPLAYED = true,
                CW = true,
                DETAIL = "CW",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Public,
                ISLOCAL = true,
                RENOTED = true,
                REPLAYED = true,
                CW = true,
                DETAIL = "ごった煮",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Public,
                ISLOCAL = true,
                RENOTED = false,
                DETAIL = "パブリック",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.SemiPublic,
                ISLOCAL = true,
                RENOTED = false,
                DETAIL = "セミパブリック",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Home,
                ISLOCAL = true,
                RENOTED = false,
                DETAIL = "ホーム",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Follower,
                ISLOCAL = true,
                RENOTED = false,
                DETAIL = "フォロワー",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Direct,
                ISLOCAL = true,
                RENOTED = false,
                DETAIL = "ダイレクトメッセージ",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Direct,
                ISLOCAL = false,
                RENOTED = false,
                DETAIL = "abcdefghijklmnopqrstuvwxyz",
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer()
            {
                PROTECTED = TimeLineContainer.PROTECTED_STATUS.Direct,
                ISLOCAL = false,
                RENOTED = false,
                DETAIL = "abcdefghijklmnopqrstuvwxyz".ToUpper(),
                USERID = "MiVIEW-SYSTEM",
                USERNAME = "アプリ",
                SOFTWARE = "MiView 0.0.1",
                SOURCE = "localhost",
                UPDATEDAT = "1960/01/01 00:00:00:000"
            });
#endif

            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.Public });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.SemiPublic });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.Home });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.Follower });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.Direct });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { RENOTED = true, DETAIL = "リノート" });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { REPLAYED = true, DETAIL = "リプライ" });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { ISLOCAL = true, DETAIL = "連合" });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { USERNAME = "ほげほげ" });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { USERID = "ANKIMO" });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { DETAIL = "DataGridViewの行追加イベントがあんまりにもおたんこなす" });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { SOFTWARE = "misskey 2024.1.0" });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { SOURCE = "misskey.niri.la" });
            this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { UPDATEDAT = "2000/01/01 01:01:01" });
        }
    }
}
