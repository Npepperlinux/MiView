using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MiView.Common.AnalyzeData
{
    internal class JsonConverterCommon
    {
        public static string GetStr(JsonNode? Input)
        {
            return Input == null ? string.Empty : Input.ToString();
        }
    }
}
