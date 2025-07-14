using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MiView.Common.AnalyzeData.Format
{
    internal class User
    {
        public JsonNode? Node { get; set; }
        public JsonNode? Id { get { return this.Node?["id"]; } }
        public JsonNode? Name { get { return this.Node?["name"]; } }
        public JsonNode? UserName { get { return this.Node?["username"]; } }
        public JsonNode? Host { get { return this.Node?["host"]; } }
        public JsonNode? AvatarUrl { get { return this.Node?["avatarUrl"]; } }
        public JsonNode? AvatarBlurhash { get { return this.Node?["avatarBlurhash"]; } }
        public JsonNode? AvatarDecorations { get { return this.Node?["avatarDecorations"]; } }
        public JsonNode? IsBot { get { return this.Node?["isBot"]; } }
        public JsonNode? IsCat { get { return this.Node?["isCat"]; } }
        public JsonNode? Emojis { get { return this.Node?["emojis"]; } }
        public Instance Instance { get { return new Instance() { Node = this.Node?["instance"] }; } }
        public JsonNode? OnlineStatus { get { return this.Node?["onlineStatus"]; } }
        public JsonNode? Roles { get { return this.Node?["badgeRoles"]; } }
    }
}
