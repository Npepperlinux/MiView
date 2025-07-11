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
        public static JsonNode? NoteBody(JsonNode Input) { return ResponseBody(Input)?["body"]; }
        public static JsonNode? NoteId(JsonNode Input) { return NoteBody(Input)?["Id"]; }
        public static JsonNode? NoteCreatedAt(JsonNode Input) { return NoteBody(Input)?["createdAt"]; }
        public static JsonNode? NoteUserId(JsonNode Input) { return NoteBody(Input)?["userId"]; }
        public static JsonNode? NoteUserDetail(JsonNode Input) { return NoteBody(Input)?["user"]; }
        public static JsonNode? NoteUserDetailId(JsonNode Input) { return NoteUserDetail(Input)?["id"]; }
        public static JsonNode? NoteUserDetailName(JsonNode Input) { return NoteUserDetail(Input)?["name"]; }
        public static JsonNode? NoteUserDetailUserName(JsonNode Input) { return NoteUserDetail(Input)?["username"]; }
        public static JsonNode? NoteUserDetailHost(JsonNode Input) { return NoteUserDetail(Input)?["host"]; }
        public static JsonNode? NoteUserDetailAvatarUrl(JsonNode Input) { return NoteUserDetail(Input)?["avatarUrl"]; }
        public static JsonNode? NoteUserDetailAvatarBlurhash(JsonNode Input) { return NoteUserDetail(Input)?["avatarBlurhash"]; }
        public static JsonNode? NoteUserDetailAvatarDecorations(JsonNode Input) { return NoteUserDetail(Input)?["avatarDecorations"]; }
        public static JsonNode? NoteUserDetailIsBot(JsonNode Input) { return NoteUserDetail(Input)?["isBot"]; }
        public static JsonNode? NoteUserDetailIsCat(JsonNode Input) { return NoteUserDetail(Input)?["isCat"]; }
        public static JsonNode? NoteUserDetailEmojis(JsonNode Input) { return NoteUserDetail(Input)?["emojis"]; }
        public static JsonNode? NoteUserDetailOnlineStatus(JsonNode Input) { return NoteUserDetail(Input)?["onlineStatus"]; }
        public static JsonNode? NoteUserDetailRoles(JsonNode Input) { return NoteUserDetail(Input)?["badgeRoles"]; }
        public static JsonNode? NoteText(JsonNode Input) { return NoteBody(Input)?["text"]; }
        public static JsonNode? NoteCW(JsonNode Input) { return NoteBody(Input)?["cw"]; }
        public static JsonNode? NoteVisibility(JsonNode Input) { return NoteBody(Input)?["visibility"]; }
        public static JsonNode? NoteLocalOnly(JsonNode Input) { return NoteBody(Input)?["localOnly"]; }
        public static JsonNode? NoteReactionAcceptance(JsonNode Input) { return NoteBody(Input)?["reactionAcceptance"]; }
        public static JsonNode? NoteRenoteCount(JsonNode Input) { return NoteBody(Input)?["renoteCount"]; }
        public static JsonNode? NoteReplyId(JsonNode Input) { return NoteBody(Input)?["replyId"]; }
        public static JsonNode? NoteRenoteId(JsonNode Input) { return NoteBody(Input)?["renoteId"]; }
        public static JsonNode? NoteChannelId(JsonNode Input) { return NoteBody(Input)?["channelId"]; }
        public static JsonNode? NoteChannel(JsonNode Input) { return NoteBody(Input)?["channel"]; }
        public static JsonNode? NoteChannelChannelId(JsonNode Input) { return NoteChannel(Input)?["id"]; }
        public static JsonNode? NoteChannelName(JsonNode Input) { return NoteChannel(Input)?["name"]; }
        public static JsonNode? NoteChannelColor(JsonNode Input) { return NoteChannel(Input)?["color"]; }
        public static JsonNode? NoteRenote(JsonNode Input) { return NoteBody(Input)?["renote"]; }
    }

    internal class ChannelToTimeLineContainer
    {
        public static TimeLineContainer ConvertTimeLineContainer(string OriginalHost,JsonNode? Input)
        {
            if (Input == null)
            {
                throw new ArgumentNullException(nameof(Input));
            }

            TimeLineContainer Container = new TimeLineContainer();
            string Protected = ChannelToTimeLineData.NoteVisibility(Input) != null ? JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteVisibility(Input)) : string.Empty;
            Container.IDENTIFIED = JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteUserDetailUserName(Input)) +
                                   JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteUserDetailName(Input)) +
                                   JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteCreatedAt(Input));
            Container.PROTECTED = StringToProtectedStatus(Protected);
            Container.ISLOCAL = bool.Parse(JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteLocalOnly(Input)));
            Container.RENOTED = JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteRenoteId(Input)) != string.Empty;
            Container.REPLAYED = JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteReplyId(Input)) != string.Empty;
            Container.CW = JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteCW(Input)) != string.Empty;
            Container.ISCHANNEL = JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteChannelId(Input)) != string.Empty;
            Container.CHANNEL_NAME = JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteChannelName(Input));
            Container.DETAIL = Container.CW ? JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteCW(Input)) : JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteText(Input));
            Container.USERID = JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteUserDetailUserName(Input));
            Container.USERNAME = JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteUserDetailName(Input));
            Container.UPDATEDAT = JsonConverterCommon.GetStr(ChannelToTimeLineData.NoteCreatedAt(Input));
            Container.TLFROM = OriginalHost;

            return Container;
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
