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
        /// É^ÉCÉÄÉâÉCÉìÉ}ÉlÅ[ÉWÉÉ
        /// </summary>
        private TimeLineCreator _TimeLineManage = new TimeLineCreator();

        /// <summary>
        /// Ç±ÇÃÉtÉHÅ[ÉÄ
        /// </summary>
        private Form MainFormObj;

        public MainForm()
        {
            InitializeComponent();

            this.label1.Font = new FontLoader().LoadFontFromFile(FontLoader.FONT_SELECTOR.MATERIALICONS, this.label1.Font.Size);
            this.label1.Text = MaterialIcons.Keyboard;

            this.MainFormObj = this;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _TimeLineManage.CreateTimeLine(ref this.MainFormObj, "Main", "tpMain");

            _TimeLineManage.CreateTimeLineTab(ref this.MainFormObj, "misskeyio", "misskey.io");
            _TimeLineManage.CreateTimeLine(ref this.MainFormObj, "misskeyio", "misskeyio");

            _TimeLineManage.CreateTimeLineTab(ref this.MainFormObj, "misskeysj", "misskey.io");
            _TimeLineManage.CreateTimeLine(ref this.MainFormObj, "misskeysj", "misskeysj");

            // _TimeLineManage.CreateTimeLine(ref this.MainFormObj, "misskey.io", "tpMain");
            // TimeLineManage.DeleteTimeLine(ref this.MainFormObj, "Main", "tpMain");


            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.Public });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.SemiPublic });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.Home });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.Follower });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { PROTECTED = TimeLineContainer.PROTECTED_STATUS.Direct });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { RENOTED = true, DETAIL = "ÉäÉmÅ[Ég" });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { REPLAYED = true, DETAIL = "ÉäÉvÉâÉC" });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { ISLOCAL = true, DETAIL = "òAçá" });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { USERNAME = "ÇŸÇ∞ÇŸÇ∞" });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { USERID = "ANKIMO" });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { DETAIL = "DataGridViewÇÃçsí«â¡ÉCÉxÉìÉgÇ™Ç†ÇÒÇ‹ÇËÇ…Ç‡Ç®ÇΩÇÒÇ±Ç»Ç∑" });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { SOFTWARE = "misskey 2024.1.0" });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { SOURCE = "misskey.niri.la" });
            //this.dataGridTimeLine1.InsertTimeLineData(new TimeLineContainer() { UPDATEDAT = "2000/01/01 01:01:01" });

        }

        public void AddTimeLine(string InstanceURL, string TabName, string APIKey)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(AddTimeLine, InstanceURL, TabName, APIKey);
                return;
            }

            // ÔøΩ^ÔøΩuÔøΩ«âÔøΩ
            _TimeLineManage.CreateTimeLineTab(ref this.MainFormObj, InstanceURL, TabName);
            _TimeLineManage.CreateTimeLine(ref this.MainFormObj, InstanceURL, InstanceURL);

            var WSManager = WebSocketTimeLineHome.OpenTimeLine(InstanceURL, APIKey);
            if (WSManager.GetSocketState() != System.Net.WebSockets.WebSocketState.Open)
            {
                MessageBox.Show("ÔøΩCÔøΩÔøΩÔøΩXÔøΩ^ÔøΩÔøΩÔøΩXÔøΩÃì«Ç›çÔøΩÔøΩ›Ç…éÔøΩÔøΩsÔøΩÔøΩÔøΩ‹ÇÔøΩÔøΩÔøΩÔøΩB");
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
