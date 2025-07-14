using MiView.Common.AnalyzeData;
using MiView.Common.Connection.WebSocket;
using MiView.Common.Connection.WebSocket.Event;
using MiView.Common.Fonts;
using MiView.Common.Fonts.Material;
using MiView.Common.TimeLine;
using MiView.ScreenForms.DialogForm;
using System.ComponentModel;
using System.Reflection;
using System.Security.Policy;
using System.Text;
using System.Text.Json.Nodes;

namespace MiView
{
    public partial class MainForm : Form
    {
        /// <summary>
        /// タイムラインマネージャ
        /// </summary>
        private TimeLineCreator _TimeLineManage = new TimeLineCreator();

        /// <summary>
        /// このフォーム
        /// </summary>
        private MainForm MainFormObj;

        public MainForm()
        {
            InitializeComponent();

            this.SetStyle(ControlStyles.DoubleBuffer, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            this.MainFormObj = this;

            // 今のところ通知を表示しないようにする
            this.pnSub.Visible = false;
            this.pnMain.Location = new Point(this.pnMain.Location.X, this.pnMain.Location.Y + this.pnSub.Size.Height);
            this.tabControl1.Size = new Size(this.tabControl1.Size.Width, this.tabControl1.Size.Height + this.pnSub.Size.Height);
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _TimeLineManage.CreateTimeLine(ref this.MainFormObj, "Main", "tpMain");
        }

        public void AddTimeLine(string InstanceURL, string TabName, string APIKey)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(AddTimeLine, InstanceURL, TabName, APIKey);
                return;
            }

            // タブ追加
            _TimeLineManage.CreateTimeLineTab(ref this.MainFormObj, InstanceURL, TabName);
            _TimeLineManage.CreateTimeLine(ref this.MainFormObj, InstanceURL, InstanceURL);

            var WSManager = WebSocketTimeLineHome.OpenTimeLine(InstanceURL, APIKey);
            if (WSManager.GetSocketState() != System.Net.WebSockets.WebSocketState.Open)
            {
                MessageBox.Show("インスタンスの読み込みに失敗しました。");
                return;
            }
            WSManager.SetDataGridTimeLine(_TimeLineManage.GetTimeLineObjectDirect(ref this.MainFormObj, "Main"));
            WSManager.SetDataGridTimeLine(_TimeLineManage.GetTimeLineObjectDirect(ref this.MainFormObj, InstanceURL));
            WebSocketTimeLineHome.ReadTimeLineContinuous(WSManager);
        }

        private void cmdAddInstance_Click(object sender, EventArgs e)
        {
            AddInstanceWithAPIKey AddFrm = new AddInstanceWithAPIKey(this);
            AddFrm.ShowDialog();
        }

        private void tbMain_SelectedIndexChanged(object sender, EventArgs e)
        {
            ((TabControl)sender).SuspendLayout();

            var TPages = ((TabControl)sender).TabPages;
            foreach (TabPage TPage in TPages)
            {
                foreach (DataGridTimeLine DGView in TPage.Controls.Cast<Control>().ToList().FindAll(r => { return r.GetType() == typeof(DataGridTimeLine); }))
                {
                    DGView.Visible = TPages.IndexOf(TPage) == ((TabControl)sender).SelectedIndex;
                }
            }
            ((TabControl)sender).ResumeLayout(false);
        }

        public void SetTimeLineContents(string OriginalHost, JsonNode Node)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(SetTimeLineContents, OriginalHost, Node);
            }

            // 変換
            TimeLineContainer TL = ChannelToTimeLineContainer.ConvertTimeLineContainer(OriginalHost, Node);

            this.pnMain.SuspendLayout();

            this.txtDetail.Text = string.Empty;
            this.lblUser.Text = string.Empty;
            this.lblSoftware.Text = string.Empty;
            this.lblUpdatedAt.Text = string.Empty;

            // ユーザID/名
            string txtUserId = TL.USERID;
            string txtUserName = TL.USERNAME;
            string txtUserInstance = JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.User.Host ?? OriginalHost);
            this.lblUser.Text += "@" + txtUserId + "@" + txtUserInstance + "/" + txtUserName;

            //CW
            string txtCW = JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.CW);
            if (txtCW != string.Empty)
            {
                this.txtDetail.Text += "【CW】";
                this.txtDetail.Text += txtCW + "\r\n";
                this.txtDetail.Text += "\r\n";
            }

            // 本文
            string txtDetail = JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Text);
            if (txtDetail != string.Empty)
            {
                this.txtDetail.Text += txtDetail;
            }
            if (TL.RENOTED)
            {
                if (txtDetail != string.Empty)
                {
                    this.txtDetail.Text += "\r\n";
                    this.txtDetail.Text += "--------------------\r\n";
                }
                if (JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Renote.CW) != string.Empty)
                {
                    this.txtDetail.Text += "【CW】";
                    this.txtDetail.Text += JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Renote.CW) + "\r\n";
                    this.txtDetail.Text += "\r\n";
                }
                this.txtDetail.Text += "RN: \r\n\r\n";
                this.txtDetail.Text += JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Renote.User.UserName) + "/" + JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Renote.User.Name) + "\r\n";
                this.txtDetail.Text += JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Renote.Text) + "\r\n";
            }
            if (TL.REPLAYED)
            {
                if (txtDetail != string.Empty)
                {
                    this.txtDetail.Text += "\r\n";
                    this.txtDetail.Text += "--------------------\r\n";
                }
                if (JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Reply.CW) != string.Empty)
                {
                    this.txtDetail.Text += "【CW】";
                    this.txtDetail.Text += JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Reply.CW) + "\r\n";
                    this.txtDetail.Text += "\r\n";
                }
                this.txtDetail.Text += JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Reply.User.UserName) + "/" + JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Reply.User.Name) + "\r\n";
                this.txtDetail.Text += JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.Reply.Text) + "\r\n";
            }

            string txtSoftware = JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.User.Instance.SoftwareName);
            string txtSoftwareVer = JsonConverterCommon.GetStr(ChannelToTimeLineData.Get(Node).Note.User.Instance.SoftwareVersion);
            if (txtSoftware + txtSoftwareVer != string.Empty)
            {
                this.lblSoftware.Text += txtSoftware + txtSoftwareVer;
            }

            this.pnMain.ResumeLayout(false);
        }
    }
}
