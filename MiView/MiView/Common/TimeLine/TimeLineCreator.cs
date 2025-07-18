using MiView.Common.AnalyzeData;
// using MiView.Common.Fonts;
// using MiView.Common.Fonts.Material;
using System;
using System.Collections.Generic;
using System.ComponentModel;
// using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static MiView.Common.TimeLine.TimeLineCreator;

namespace MiView.Common.TimeLine
{
    /// <summary>
    /// タイムラインコンテナ作成・識別
    /// </summary>
    internal class TimeLineCreator
    {
        public enum TIMELINE_ELEMENT
        {
            UNDESIGNATED = -1,
            /// <summary>
            /// 投稿識別キー
            /// </summary>
            IDENTIFIED,
            /// <summary>
            /// アイコン
            /// </summary>
            ICON,
            /// <summary>
            /// ユーザー名
            /// </summary>
            USERNAME,
            /// <summary>
            /// ユーザID
            /// </summary>
            USERID,
            /// <summary>
            /// リプライ
            /// </summary>
            REPLAYED,
            /// <summary>
            /// リプライ表示
            /// </summary>
            REPLAYED_DISP,
            /// <summary>
            /// 公開範囲
            /// </summary>
            PROTECTED,
            /// <summary>
            /// 公開範囲表示
            /// </summary>
            PROTECTED_DISP,
            /// <summary>
            /// リノート
            /// </summary>
            RENOTED,
            /// <summary>
            /// リノート表示
            /// </summary>
            RENOTED_DISP,
            /// <summary>
            /// ローカル
            /// </summary>
            ISLOCAL,
            /// <summary>
            /// ローカル表示
            /// </summary>
            ISLOCAL_DISP,
            /// <summary>
            /// チャンネル
            /// </summary>
            ISCHANNEL,
            /// <summary>
            /// チャンネル名
            /// </summary>
            CHANNEL_NAME,
            /// <summary>
            /// チャンネル表示
            /// </summary>
            ISCHANNEL_DISP,
            /// <summary>
            /// CW
            /// </summary>
            CW,
            /// <summary>
            /// CW表示
            /// </summary>
            CW_DISP,
            /// <summary>
            /// 投稿内容
            /// </summary>
            DETAIL,
            /// <summary>
            /// 投稿日時
            /// </summary>
            UPDATEDAT,
            /// <summary>
            /// 投稿元インスタンス
            /// </summary>
            SOURCE,
            /// <summary>
            /// 投稿元ソフトウェア情報
            /// </summary>
            SOFTWARE,
            /// <summary>
            /// ソフトウェア偽装有無
            /// </summary>
            SOFTWARE_INVALIDATED,
            /// <summary>
            /// 投稿元オリジナルjson情報
            /// </summary>
            ORIGINAL,
            /// <summary>
            /// 投稿元オリジナルホスト
            /// </summary>
            ORIGINAL_HOST,
            /// <summary>
            /// 読み取り元
            /// </summary>
            TLFROM,
        }

        /// <summary>
        /// 非表示対象
        /// </summary>
        public static TIMELINE_ELEMENT[] _DisabledElements = new TIMELINE_ELEMENT[]
        {
            TIMELINE_ELEMENT.CW,
            TIMELINE_ELEMENT.UNDESIGNATED,
            TIMELINE_ELEMENT.IDENTIFIED,
            TIMELINE_ELEMENT.RENOTED,
            TIMELINE_ELEMENT.PROTECTED,
            TIMELINE_ELEMENT.ISLOCAL,
            TIMELINE_ELEMENT.REPLAYED,
            TIMELINE_ELEMENT.ORIGINAL,
            TIMELINE_ELEMENT.CHANNEL_NAME,
            TIMELINE_ELEMENT.ISCHANNEL,
            // TIMELINE_ELEMENT.TLFROM,
            TIMELINE_ELEMENT.SOFTWARE_INVALIDATED,
            TIMELINE_ELEMENT.ORIGINAL_HOST,
        };

        public List<TimeLineContainer> TimeLineData = new List<TimeLineContainer>();

        // Windows Forms依存のコードをコメントアウト
        // private MainForm? _MainForm { get; set; }
        
        /// <summary>
        /// タイムライン管理オブジェクト
        /// </summary>
        // private Dictionary<string, DataGridTimeLine> Grids = new Dictionary<string, DataGridTimeLine>();

        public TimeLineCreator()
        {
        }

        // Windows Forms依存のメソッドをコメントアウト
        /*
        /// <summary>
        /// フォームオブジェクトの取得
        /// </summary>
        /// <param name="MainForm"></param>
        /// <param name="Definition"></param>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public DataGridTimeLine GetTimeLineObjectDirect(ref MainForm MainForm, string Definition)
        {
            if (!this.Grids.ContainsKey(Definition))
            {
                throw new KeyNotFoundException();
            }
            return this.Grids[Definition];
        }

        /// <summary>
        /// メインフォームへタイムラインを追加
        /// </summary>
        /// <param name="MainForm"></param>
        public void CreateTimeLine(ref MainForm MainForm, string Definition, string? ChildDefinition = null)
        {
            // Windows Forms依存のコードをコメントアウト
        }
        */

        // Avalonia用の代用メソッド
        /// <summary>
        /// タイムラインデータの追加
        /// </summary>
        /// <param name="container"></param>
        public void AddTimeLineData(TimeLineContainer container)
        {
            TimeLineData.Add(container);
        }

        /// <summary>
        /// タイムラインデータの取得
        /// </summary>
        /// <returns></returns>
        public List<TimeLineContainer> GetTimeLineData()
        {
            return TimeLineData;
        }

        /// <summary>
        /// タイムラインデータのクリア
        /// </summary>
        public void ClearTimeLineData()
        {
            TimeLineData.Clear();
        }
    }

    internal class TimeLineNotFoundException : Exception
    {
        /// <summary>
        /// 定義名
        /// </summary>
        private string Definition { get; set; } = string.Empty;

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public TimeLineNotFoundException()
        {
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message"></param>
        public TimeLineNotFoundException(string? message) : base(message)
        {
        }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="message"></param>
        /// <param name="Definition"></param>
        public TimeLineNotFoundException(string? message, string Definition)
        {
            this.Definition = Definition;
        }

        public override string ToString()
        {
            return this.Definition != string.Empty ? this.Definition : base.ToString();
        }

        public string CallDefinition()
        {
            return this.Definition;
        }
    }

    /// <summary>
    /// タイムラインオブジェクト
    /// </summary>
    public class TimeLineContainer
    {
        public TimeLineContainer() { }

        public enum PROTECTED_STATUS
        {
            Public,
            SemiPublic,
            Home,
            Follower,
            Direct,
        }

        public string IDENTIFIED { get; set; } = string.Empty;
        public string ICON { get; set; } = string.Empty;
        public string USERNAME { get; set; } = string.Empty;
        public string USERID { get; set; } = string.Empty;
        public bool REPLAYED { get; set; } = false;
        public string? REPLAYED_DISP { get; set; }
        public PROTECTED_STATUS PROTECTED { get; set; } = PROTECTED_STATUS.Public;
        public string? PROTECTED_DISP { get; set; }
        public bool RENOTED { get; set; } = false;
        public string? RENOTED_DISP { get; set; }
        public bool ISLOCAL { get; set; } = false;
        public string? ISLOCAL_DISP { get; set; }
        public bool ISCHANNEL { get; set; } = false;
        public string? CHANNEL_NAME { get; set; }
        public string? ISCHANNEL_DISP { get; set; }
        public bool CW { get; set; } = false;
        public string? CW_DISP { get; set; }
        public string DETAIL { get; set; } = string.Empty;
        public string CONTENT { get; set; } = string.Empty;
        public string UPDATEDAT { get; set; } = string.Empty;
        public string SOURCE { get; set; } = string.Empty;
        public string SOFTWARE { get; set; } = string.Empty;
        public bool SOFTWARE_INVALIDATED { get; set; } = false;
        public JsonNode ORIGINAL { get; set; } = JsonNode.Parse("{}")!;
        public string ORIGINAL_HOST { get; set; } = string.Empty;
        public string TLFROM { get; set; } = string.Empty;
    }

    // Windows Forms依存のDataGridTimeLineクラスをコメントアウト
    /*
    /// <summary>
    /// タイムラインコントロール
    /// </summary>
    partial class DataGridTimeLine : System.Windows.Forms.DataGridView
    {
        // Windows Forms依存のコードをコメントアウト
    }
    */
}