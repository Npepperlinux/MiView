using MiView.Common.AnalyzeData.Format;
using MiView.Common.TimeLine;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MiView.Common.AnalyzeData
{
    /// <summary>
    /// チャンネルレスポンス→タイムライン用データ
    /// </summary>
    internal class ChannelToTimeLineData
    {
        public static JsonNode? ResponseType(JsonNode Input) { return Input["type"]; }
        public static JsonNode? ResponseBody(JsonNode Input) { return Input["body"]; }
        public static JsonNode? ResponseId(JsonNode Input) { return ResponseBody(Input)?["id"]; }
        public static JsonNode? ResponseNoteType(JsonNode Input) { return ResponseBody(Input)?["type"]; }
        public static Note Note(JsonNode Input) { return new Note() { Node = ResponseBody(Input)?["body"] }; }




        //public static JsonNode? NoteChannelChannelId(JsonNode Input) { return NoteChannel(Input)?["id"]; }
        //public static JsonNode? NoteChannelName(JsonNode Input) { return NoteChannel(Input)?["name"]; }
        //public static JsonNode? NoteChannelColor(JsonNode Input) { return NoteChannel(Input)?["color"]; }
    }

    internal class ChannelToTimeLineContainer
    {
        private const string _RenoteSign = " RN:";

        public static TimeLineContainer ConvertTimeLineContainer(string OriginalHost, JsonNode? Input)
        {
            if (Input == null)
            {
                throw new ArgumentNullException(nameof(Input));
            }
            TimeLineContainer Container = new TimeLineContainer();

            string Protected = ChannelToTimeLineData.Note(Input).Visibility != null ? JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).Visibility) : string.Empty;
            Container.IDENTIFIED = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).User.UserName) +
                                   JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).User.Name) +
                                   JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).CreatedAt);
            Container.PROTECTED = StringToProtectedStatus(Protected);
            Container.ISLOCAL = bool.Parse(JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).LocalOnly));
            Container.RENOTED = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).RenoteId) != string.Empty;
            Container.REPLAYED = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).ReplyId) != string.Empty;
            // Container.CW = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).CW) != string.Empty;
            Container.ISCHANNEL = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).ChannelId) != string.Empty;
            Container.CHANNEL_NAME = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).Channel.Name);
            // Container.DETAIL = Container.CW ? JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).CW) : JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).Text);
            Container.USERID = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).User.UserName);
            Container.USERNAME = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).User.Name);
            Container.UPDATEDAT = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).CreatedAt);
            Container.TLFROM = OriginalHost;

            GetCW(Input, ref Container);
            GetDetail(Input, ref Container);


            return Container;
        }

        private static void GetCW(JsonNode Input, ref TimeLineContainer Container)
        {
            Container.CW = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).CW) != string.Empty ||
                           JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).Renote.CW) != string.Empty;
        }

        private static void GetDetail(JsonNode Input, ref TimeLineContainer Container)
        {
            string CW = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).CW);
            string ReNoteCW = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).Renote.CW);

            string NoteText = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).Text);
            string ReNoteText = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).Renote.Text);

            string ReNoteSourceUser = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).Renote.User.UserName);
            string ReNoteSourceUserName = JsonConverterCommon.GetStr(ChannelToTimeLineData.Note(Input).Renote.User.Name);

            // Renoteのみ
            if (Container.RENOTED && NoteText == string.Empty)
            {
                if (Container.CW)
                {
                    Container.DETAIL = _RenoteSign + ReNoteSourceUser + "/" + ReNoteSourceUserName + " " + CW;
                }
                else
                {
                    Container.DETAIL = _RenoteSign + ReNoteSourceUser + "/" + ReNoteSourceUserName + " " + ReNoteText;
                }
                return;
            }
            // 引用RN
            if (Container.RENOTED && NoteText != string.Empty)
            {
                if (Container.CW)
                {
                    Container.DETAIL = NoteText + _RenoteSign + ReNoteSourceUser + "/" + ReNoteSourceUserName + " " + ReNoteCW;
                }
                else
                {
                    Container.DETAIL = NoteText + _RenoteSign + ReNoteSourceUser + "/" + ReNoteSourceUserName + " " + ReNoteText;
                }
                return;
            }

            if (Container.CW)
            {
                Container.DETAIL = CW;
            }
            else
            {
                Container.DETAIL = NoteText;
            }
        }

        public static TimeLineContainer.PROTECTED_STATUS StringToProtectedStatus(string Str)
        {
            TimeLineContainer.PROTECTED_STATUS Status = TimeLineContainer.PROTECTED_STATUS.Public;
            switch (Str)
            {
                case "public":
                    Status = TimeLineContainer.PROTECTED_STATUS.Public;
                    break;
                case "home":
                    Status = TimeLineContainer.PROTECTED_STATUS.Home;
                    break;
                case "followers":
                    Status = TimeLineContainer.PROTECTED_STATUS.Follower;
                    break;
                case "specified":
                    Status = TimeLineContainer.PROTECTED_STATUS.Direct;
                    break;
                default:
                    break;
            }
            return Status;
        }
    }
}
