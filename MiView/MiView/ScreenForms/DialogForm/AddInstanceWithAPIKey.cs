using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MiView.ScreenForms.DialogForm
{
    public partial class AddInstanceWithAPIKey : Form
    {
        private MainForm? _MainForm = null;

        public AddInstanceWithAPIKey(MainForm MainForm)
        {
            InitializeComponent();
            this._MainForm = MainForm;
        }

        private void cmdApply_Click(object sender, EventArgs e)
        {
            // 状況的に発生しない
            if (_MainForm == null)
            {
                return;
            }
            // 入力が足らない
            if (txtAPIKey.Text == string.Empty || 
                txtInstanceURL.Text == string.Empty ||
                txtTabName.Text == string.Empty)
            {
                MessageBox.Show("インスタンスURLもしくはAPIキー、タブ名称が入力されていません。", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            string SelectTL = string.Empty;
            if (cmbTLKind.SelectedItem == null || cmbTLKind.SelectedItem.ToString() == "")
            {
                MessageBox.Show("TLの種類を選択してください。", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else
            {
#pragma warning disable CS8600 // Null リテラルまたは Null の可能性がある値を Null 非許容型に変換しています。
                SelectTL = cmbTLKind.SelectedItem.ToString();
#pragma warning restore CS8600 // Null リテラルまたは Null の可能性がある値を Null 非許容型に変換しています。
            }
            _ = Task.Run(async () =>
            {
                // URLが存在するかチェック
                HttpClient Clt = new HttpClient();
                try
                {
                    var HttpResult = await Clt.GetAsync(string.Format(@"http://{0}/", txtInstanceURL.Text));

                    if (HttpResult.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        MessageBox.Show("存在しないURLです。", "", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                }
                catch (Exception ex)
                {
                }

                this._MainForm.AddTimeLine(txtInstanceURL.Text, txtTabName.Text, txtAPIKey.Text, SelectTL);
            });
        }
    }
}
