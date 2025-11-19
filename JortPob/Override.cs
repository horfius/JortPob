using JortPob.Common;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static JortPob.NpcContent;

namespace JortPob
{
    /* Loads override json files so we can referenec them */
    /* Static class since everything here is going to be static and readonly */
    public class Override
    {
        private static HashSet<string> DO_NOT_PLACE;
        private static HashSet<string> STATIC_COLLISION;

        private static List<PlayerClass> CHARACTER_CREATION_CLASS;
        private static List<PlayerRace> CHARACTER_CREATION_RACE;
        private static List<ItemRemap> ITEM_REMAPS;
        private static List<ItemDefinition> ITEM_DEFINITIONS;

        public static bool CheckDoNotPlace(string id)
        {
            return DO_NOT_PLACE.Contains(id.ToLower());
        }

        public static bool CheckStaticCollision(string id)
        {
            return STATIC_COLLISION.Contains(id.ToLower());
        }

        public static List<PlayerClass> GetCharacterCreationClasses()
        {
            return CHARACTER_CREATION_CLASS;
        }

        public static List<PlayerRace> GetCharacterCreationRaces()
        {
            return CHARACTER_CREATION_RACE;
        }

        public static ItemRemap GetItemRemap(string id)
        {
            foreach (ItemRemap remap in ITEM_REMAPS)
            {
                if (remap.id == id) { return remap; }
            }
            return null;
        }

        public static ItemDefinition GetItemDefinition(string id)
        {
            foreach (ItemDefinition def in ITEM_DEFINITIONS)
            {
                if (def.id == id) { return def; }
            }
            return null;
        }

        /* load all the override jsons into this class */
        public static void Initialize()
        {
            /* Load do_not_place overrides */
            JsonNode jsonDoNotPlace = JsonNode.Parse(File.ReadAllText(Utility.ResourcePath(@"overrides\do_not_place.json")));
            DO_NOT_PLACE = jsonDoNotPlace != null
                ? jsonDoNotPlace.AsArray().Select(node => node.ToString().ToLower()).ToHashSet()
                : [];

            /* Load static_collision overrides */
            JsonNode jsonStaticCollision = JsonNode.Parse(File.ReadAllText(Utility.ResourcePath(@"overrides\static_collision.json")));
            STATIC_COLLISION = jsonStaticCollision != null
                ? jsonStaticCollision.AsArray().Select(node => node.ToString().ToLower()).ToHashSet()
                : [];

            /* Load character creation class overrides */
            CHARACTER_CREATION_CLASS = JsonSerializer.Deserialize<List<PlayerClass>>(File.ReadAllText(Utility.ResourcePath(@"overrides\character_creation_class.json")), new JsonSerializerOptions { IncludeFields = true });

            /* Load character creation race overrides */
            CHARACTER_CREATION_RACE = JsonSerializer.Deserialize<List<PlayerRace>>(File.ReadAllText(Utility.ResourcePath(@"overrides\character_creation_race.json")), new JsonSerializerOptions { IncludeFields = true });

            /* Load item remapping list */
            ITEM_REMAPS = JsonSerializer.Deserialize<List<ItemRemap>>(File.ReadAllText(Utility.ResourcePath(@"overrides\item_remap.json")), new JsonSerializerOptions { IncludeFields = true, Converters = { new JsonStringEnumConverter() } });

            /* Load all item definitinos from resources/override/items */
            string[] itemFiles = Directory.GetFiles(Utility.ResourcePath(@"overrides\items"));
            ITEM_DEFINITIONS = new();
            foreach (string itemFile in itemFiles) {
                ITEM_DEFINITIONS.Add(new ItemDefinition(itemFile));
            }
        }

        /* Classes for serializing */
        public class PlayerClass
        {
            public string name, description;

            public PlayerClass() { }
        }

        public class PlayerRace
        {
            public string name, description;
            public byte id;  // this id matches the values of the NpcContent.Race enums

            public PlayerRace() { }
        }

        public class ItemRemap
        {
            public string id, comment;
            public ItemManager.Type type;
            public int row;

            public ItemText text;

            public ItemRemap() { }

            public bool HasTextChanges()
            {
                return text != null && (text.name != null || text.summary != null || text.description != null || text.effect != null);
            }
        }

        public class ItemText
        {
            public string name, summary, description, effect;

            public ItemText() { }
        }

        public class ItemDefinition
        {
            public readonly string id, comment;
            public readonly ItemManager.Type type;
            public readonly int row;

            public readonly ItemText text;
            public readonly Dictionary<string, string> data;

            public ItemDefinition(string jsonPath)
            {
                id = Utility.PathToFileName(jsonPath);

                JsonNode json = JsonNode.Parse(File.ReadAllText(jsonPath));

                comment = json["comment"].GetValue<string>();
                type = (ItemManager.Type)System.Enum.Parse(typeof(ItemManager.Type), json["type"].GetValue<string>());
                row = json["row"].GetValue<int>();

                text = new();
                text.name = json["text"]["name"] != null ? json["text"]["name"].GetValue<string>() : null;
                text.summary = json["text"]["summary"] != null ? json["text"]["summary"].GetValue<string>() : null;
                text.description = json["text"]["description"] != null ? json["text"]["description"].GetValue<string>() : null;
                text.effect = json["text"]["effect"] != null ? json["text"]["effect"].GetValue<string>() : null;

                data = new();
                foreach(var property in json["data"].AsObject())
                {
                    data.Add(property.Key, property.Value.ToString());
                }
            }
        }
    }
}
