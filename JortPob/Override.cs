using JortPob.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

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

        public static bool CheckDoNotPlace(string id)
        {
            return DO_NOT_PLACE.Contains(id);
        }

        public static bool CheckStaticCollision(string id)
        {
            return STATIC_COLLISION.Contains(id);
        }

        public static List<PlayerClass> GetCharacterCreationClasses()
        {
            return CHARACTER_CREATION_CLASS;
        }

        public static List<PlayerRace> GetCharacterCreationRaces()
        {
            return CHARACTER_CREATION_RACE;
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
    }
}
