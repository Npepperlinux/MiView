using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MiView.Common.AnalyzeData.Format
{
    internal class Note
    {
        public JsonNode? Node {  get; set; }
        public JsonNode? Id { get { return this.Node?["Id"]; } }
        public JsonNode? CreatedAt { get { return this.Node?["createdAt"]; } }
        public JsonNode? UserId { get { return this.Node?["userId"]; } }
        public JsonNode? UserDetail { get { return this.Node?["user"]; } }
        public User User { get { return new User() { Node = this.Node?["user"] }; } }
        public JsonNode? Text { get { return this.Node?["text"]; } }
        public JsonNode? CW { get { return this.Node?["cw"]; } }
        public JsonNode? Visibility { get { return this.Node?["visibility"]; } }
        public JsonNode? LocalOnly { get { return this.Node?["localOnly"]; } }
        public JsonNode? ReactionAcceptance { get { return this.Node?["reactionAcceptance"]; } }
        public JsonNode? RenoteCount { get { return this.Node?["renoteCount"]; } }
        public JsonNode? ReplyId { get { return this.Node?["replyId"]; } }
        public Note Reply { get { return new Note() { Node = this.Node?["reply"] }; } }
        public JsonNode? RenoteId { get { return this.Node?["renoteId"]; } }
        public Note Renote { get { return new Note() { Node = this.Node?["renote"] }; } }
        public JsonNode? ChannelId { get { return this.Node?["channelId"]; } }
        public Channel Channel { get { return new Channel() { Node = this.Node?["channel"] }; } }
    }
}
