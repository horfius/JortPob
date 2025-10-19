using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;

namespace JortPob.Common
{
    public class Settable
    {
        private static JsonNode json;
        public static string Get(string key)
        {
            if(json == null)
            {
                string tempRawJson = File.ReadAllText($"{AppDomain.CurrentDomain.BaseDirectory}settings.json");
                json = JsonNode.Parse(tempRawJson);
            }

            return json[key].ToString();
        }

        public static string[] GetArray(string key)
        {
            if (json == null)
            {
                string tempRawJson = File.ReadAllText($"{AppDomain.CurrentDomain.BaseDirectory}settings.json");
                json = JsonNode.Parse(tempRawJson);
            }

            List<string> strings = new();

            JsonArray array = json[key].AsArray();
            foreach(JsonNode jsonNode in array)
            {
                strings.Add(jsonNode.GetValue<string>());
            }

            return strings.ToArray();
        }
    }
}
