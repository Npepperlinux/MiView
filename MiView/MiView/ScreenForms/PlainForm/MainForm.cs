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
        private Form MainFormObj;

        public MainForm()
        {
            InitializeComponent();

            this.SetStyle(ControlStyles.DoubleBuffer, true);
            this.SetStyle(ControlStyles.UserPaint, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);

            this.MainFormObj = this;
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
    }
}
