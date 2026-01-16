using JortPob.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json.Nodes;

#nullable enable

namespace JortPob
{
    /* Content is effectively any physical object in the game world. Anything that has a physical position in a cell */
    public abstract class Content
    {
        public Cell? cell { get; init; }

        public readonly string id;   // record id
        public readonly string? name; // can be null!
        public readonly ESM.Type type;

        public uint entity;  // entity id, usually 0
        public string? papyrus { get; private set; } // papyrus script id if it has one (usually null)
        public Vector3 relative;
        public Int2 load { get; set; } // if a piece of content needs tile load data this is where it's stored

        public readonly Vector3 position;
        public Vector3 rotation;
        public readonly int scale;  // scale in converted to a int where 100 = 1.0f scale. IE:clamp to nearest 1%. this is to group scale for asset generation.

        public string? mesh;  // can be null!

        public Content(Cell cell, JsonNode json, Record record)
        {
            this.cell = cell;
            load = new(0, 0);
            id = json["id"]?.ToString() ?? throw new Exception("Could not find id from content json!");
            name = record.json["name"]?.GetValue<string>();

            type = record.type;
            entity = 0;

            papyrus = string.IsNullOrEmpty(record.json["script"]?.GetValue<string>()) ? null : record.json["script"]!.GetValue<string>();

            float x = float.Parse(json["translation"]?[0]?.ToString() ?? throw new Exception("Content translation x value is missing!"));
            float z = float.Parse(json["translation"]?[1]?.ToString() ?? throw new Exception("Content translation y value is missing!"));
            float y = float.Parse(json["translation"]?[2]?.ToString() ?? throw new Exception("Content translation z value is missing!"));

            float i = float.Parse(json["rotation"]?[0]?.ToString() ?? throw new Exception("Content rotation i value is missing!"));
            float j = float.Parse(json["rotation"]?[1]?.ToString() ?? throw new Exception("Content rotation j value is missing!"));
            float k = float.Parse(json["rotation"]?[2]?.ToString() ?? throw new Exception("Content rotation k value is missing!"));

            /* The following unholy code converts morrowind (Z up) euler rotations into dark souls (Y up) euler rotations */
            /* Big thanks to katalash, dropoff, and the TESUnity dudes for helping me sort this out */

            /* Katalashes code from MapStudio */
            Vector3 MatrixToEulerXZY(Matrix4x4 m)
            {
                const float Pi = (float)Math.PI;
                const float Deg2Rad = Pi / 180.0f;
                Vector3 ret;
                ret.Z = MathF.Asin(-Math.Clamp(-m.M12, -1, 1));

                if (Math.Abs(m.M12) < 0.9999999)
                {
                    ret.X = MathF.Atan2(-m.M32, m.M22);
                    ret.Y = MathF.Atan2(-m.M13, m.M11);
                }
                else
                {
                    ret.X = MathF.Atan2(m.M23, m.M33);
                    ret.Y = 0;
                }
                ret.X = ret.X <= -180.0f * Deg2Rad ? ret.X + 360.0f * Deg2Rad : ret.X;
                ret.Y = ret.Y <= -180.0f * Deg2Rad ? ret.Y + 360.0f * Deg2Rad : ret.Y;
                ret.Z = ret.Z <= -180.0f * Deg2Rad ? ret.Z + 360.0f * Deg2Rad : ret.Z;
                return ret;
            }

            /* Adapted code from https://github.com/ColeDeanShepherd/TESUnity */
            Quaternion xRot = Quaternion.CreateFromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f), i);
            Quaternion yRot = Quaternion.CreateFromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f), k);
            Quaternion zRot = Quaternion.CreateFromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f), j);
            Quaternion q = xRot * zRot * yRot;

            Vector3 eu = MatrixToEulerXZY(Matrix4x4.CreateFromQuaternion(q));

            relative = new();
            position = new Vector3(x, y, z) * Const.GLOBAL_SCALE;
            rotation = eu * (float)(180 / Math.PI);
            scale = (int)((json["scale"] != null ? float.Parse(json["scale"]!.ToString()) : 1f) * 100);
        }

        public Content(string id, ESM.Type type, Int2 load, Vector3 position, Vector3 rotation, int scale)
        {
            this.id = id;
            this.type = type;
            this.load = load;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
        }
    }

    /* npcs, humanoid only */
    public class NpcContent : Content
    {
        public enum Race { Any = 0, Argonian = 1, Breton = 2, DarkElf = 3, HighElf = 4, Imperial = 5, Khajiit = 6, Nord = 7, Orc = 8, Redguard = 9, WoodElf = 10 }
        public enum Sex { Any, Male, Female };
        public enum Service {
            OffersTraining, BartersIngredients, BartersApparatus, BartersAlchemy, BartersClothing, OffersSpells, BartersWeapons,
            BartersArmor, BartersBooks, BartersMiscItems, BartersEnchantedItems, OffersEnchanting, OffersSpellmaking, BartersRepairItems,
            OffersRepairs, BartersLockpicks, BartersProbes, BartersLights
        };

        public string job { get; init; } // class is job, cant used reserved word
        public string? faction { get; init; }
        public readonly Race race;
        public readonly Sex sex;

        public readonly int level, disposition, reputation, rank, gold;
        public readonly int hello, fight, flee, alarm;
        public readonly bool hostile, dead;

        public readonly bool essential; // player gets called dumb if they kill this dood
        public bool hasWitness; // this value is set based on local npcs. defaults false. if true then crimes comitted against this npc will cause bounty

        public readonly Stats stats; // skills and attributes

        public readonly List<Service> services;

        public List<(string id, int quantity)> inventory;
        public List<string> spells; // spells this character knows or sells as a vendor

        public List<(string id, int quantity)>? barter; // can be null

        public List<Travel> travel;  // travel destinations for silt strider people, mage guild teles, etc...

        public Script.Flag? treasure; // only used if this is a dead body npc and it has treasure. otherwise null. NEVER SET THIS FOR A LIVING NPC!!!

        public class Travel : DoorContent.Warp
        {
            public string? name { get; set; }
            public int cost;
            public Travel(JsonNode json) : base(json)
            {
                cost = 100;
            }
        }

        public class Stats
        {
            public enum Tier { Novice = 0, Apprentice = 25, Journeyman = 50, Expert = 75, Master = 100 }
            public enum Skill { Acrobatics, Alchemy, Alteration, Armorer, Athletics, Axe, Block, BluntWeapon, Conjuration, Destruction, Enchant, HandToHand, HeavyArmor, Illusion, LightArmor, LongBlade, Marksman, MediumArmor, Mercantile, Mysticism, Restoration, Security, ShortBlade, Sneak, Spear, Speechcraft, Unarmored };
            public enum Attribute { Strength, Intelligence, Willpower, Agility, Speed, Endurance, Personality, Luck };

            private readonly Dictionary<Skill, int> skills;
            private readonly Dictionary<Attribute, int> attributes;

            /* Defined stats constructor */
            public Stats(JsonNode json)
            {
                attributes = new();
                skills = new();

                JsonArray jsonAttributes = json["attributes"]?.AsArray() ?? throw new Exception("Stats attributes value is missing!");
                JsonArray jsonSkills = json["skills"]?.AsArray() ?? throw new Exception("Stats skills value is missing!");

                if (jsonAttributes.Count < Enum.GetValues<Attribute>().Length)
                    throw new Exception("Attributes array is not long enough!");
                else if (jsonSkills.Count < Enum.GetValues<Skill>().Length)
                    throw new Exception("Skills array is not long enough!");
                
                int i = 0;
                foreach (Attribute attribute in Enum.GetValues<Attribute>())
                {
                    attributes.Add(attribute, jsonAttributes[i++]!.GetValue<int>());
                }

                i = 0;
                foreach (Skill skill in Enum.GetValues<Skill>())
                {
                    skills.Add(skill, jsonSkills[i++]!.GetValue<int>());
                }
            }

            /* Autocalculated stats constructor */
            public Stats(Sex sex, RaceInfo raceInfo, JobInfo jobInfo, int level)
            {
                attributes = new();
                skills = new();

                foreach (Attribute attribute in Enum.GetValues(typeof(Attribute)))
                {
                    float baseVal = raceInfo.GetAttribute(sex, attribute);  // base racial value for attribute
                    float bonus = 0;
                    if(jobInfo.HasAttribute(attribute)) { baseVal += 10f; }
                    foreach(Skill skill in Enum.GetValues(typeof(Skill)))
                    {
                        if(attribute == GetParent(skill))
                        {
                            if(jobInfo.HasMajor(skill)) { bonus += 1f; }
                            else if(jobInfo.HasMinor(skill)) { bonus += .5f; }
                            else { bonus += .2f; }
                        }
                    }

                    int calculatedValue = (int)(baseVal + (bonus * (level - 1)));
                    attributes.Add(attribute, calculatedValue);
                }

                foreach (Skill skill in Enum.GetValues(typeof(Skill)))
                {
                    float baseVal = raceInfo.GetSkill(skill);
                    float bonus;
                    if (jobInfo.HasMajor(skill)) { baseVal += 30f; bonus = 1f; }
                    else if (jobInfo.HasMinor(skill)) { baseVal += 15f; bonus = 1f; }
                    else { baseVal += 5f; bonus = .1f; }

                    if(jobInfo.HasSpecialization(skill)) { baseVal += 5f; bonus += .5f; }

                    int calculatedValue = (int)(baseVal + (bonus * (level - 1)));
                    skills.Add(skill, calculatedValue);
                }
            }

            private Attribute GetParent(Skill skill)
            {
                switch (skill)
                {
                    case Skill.HeavyArmor:
                    case Skill.MediumArmor:
                    case Skill.Spear:
                        return Attribute.Endurance;
                    case Skill.Acrobatics:
                    case Skill.Armorer:
                    case Skill.Axe:
                    case Skill.BluntWeapon:
                    case Skill.LongBlade:
                        return Attribute.Strength;
                    case Skill.Block:
                    case Skill.LightArmor:
                    case Skill.Marksman:
                    case Skill.Sneak:
                        return Attribute.Agility;
                    case Skill.Athletics:
                    case Skill.HandToHand:
                    case Skill.ShortBlade:
                    case Skill.Unarmored:
                        return Attribute.Speed;
                    case Skill.Mercantile:
                    case Skill.Speechcraft:
                    case Skill.Illusion:
                        return Attribute.Personality;
                    case Skill.Security:
                    case Skill.Alchemy:
                    case Skill.Conjuration:
                    case Skill.Enchant:
                        return Attribute.Intelligence;
                    case Skill.Alteration:
                    case Skill.Destruction:
                    case Skill.Mysticism:
                    case Skill.Restoration:
                        return Attribute.Willpower;
                    default:
                        throw new Exception("What the fuck");
                }
            }

            public int Get(Skill skill) { return skills[skill]; }
            public int Get(Attribute attribute) { return attributes[attribute]; }

            public Tier GetTier(Skill skill) {
                int val = skills[skill];
                if (val >= (int)Tier.Master) { return Tier.Master; }
                else if(val >= (int)Tier.Expert) { return Tier.Expert; }
                else if(val >= (int)Tier.Journeyman) { return Tier.Journeyman; }
                else if(val >= (int)Tier.Apprentice) { return Tier.Apprentice; }
                else { return Tier.Novice; }
            }

            /* Return # highest skills. This is how MW determines trainer skills */
            public List<Skill> GetHighest(int num)
            {
                var list = skills.ToList();
                list.Sort((x, y) => y.Value.CompareTo(x.Value));

                List<Skill> highest = new();
                for(int i=0;i<num||i<list.Count();i++)
                {
                    highest.Add(list[i].Key);
                }

                return highest;
            }
        }

        public NpcContent(ESM esm, Cell cell, JsonNode json, Record record) : base(cell, json, record)
        {
            race = Enum.Parse<Race>(record.json["race"]?.ToString().Replace(" ", "") ?? throw new Exception("NpcContent json is missing race value!"));
            job = record.json["class"]?.ToString() ?? throw new Exception("NpcContent json is missing job value!");
            faction = string.IsNullOrEmpty(record.json["faction"]?.ToString()) ? null : record.json["faction"]!.ToString();

            sex = record.json["npc_flags"]?.ToString().ToLower().Contains("female") ?? true ? Sex.Female : Sex.Male; // Default to female

            essential = record.json["npc_flags"]?.GetValue<string>().ToLower().Contains("essential") ?? false;

            level = record.json["data"]?["level"]?.GetValue<int>() ?? throw new Exception("NpcContent is missing value data->level!");
            disposition = record.json["data"]?["disposition"]?.GetValue<int>() ?? throw new Exception("NpcContent is missing value data->disposition!");
            reputation = record.json["data"]?["reputation"]?.GetValue<int>() ?? throw new Exception("NpcContent is missing value data->reputation!");
            rank = record.json["data"]?["rank"]?.GetValue<int>() ?? throw new Exception("NpcContent is missing value data->rank!");
            gold = record.json["data"]?["gold"]?.GetValue<int>() ?? throw new Exception("NpcContent is missing value data->gold!");

            hello = record.json["ai_data"]?["hello"]?.GetValue<int>() ?? throw new Exception("NpcContent is missing value ai_data->hello!");
            fight = record.json["ai_data"]?["fight"]?.GetValue<int>() ?? throw new Exception("NpcContent is missing value ai_data->fight!");
            flee = record.json["ai_data"]?["flee"]?.GetValue<int>() ?? throw new Exception("NpcContent is missing value ai_data->flee!");
            alarm = record.json["ai_data"]?["alarm"]?.GetValue<int>() ?? throw new Exception("NpcContent is missing value ai_data->alarm!");

            hostile = fight >= 80; // @TODO: recalc with disposition mods based off UESP calc
            
            dead = int.TryParse(record.json["data"]?["stats"]?["health"]?.ToString(), out var res) && res <= 0;

            if (record.json["data"]?["stats"] != null)
            {
                stats = new(record.json["data"]!["stats"]!);
            }
            else
            {
                var jobInfo = esm.GetJob(job) ?? throw new Exception($"Could not get jobInfo from job '{job}'");
                var race = record.json["race"]?.ToString() ?? throw new Exception("NpcContent is missing value race!");
                var raceInfo = esm.GetRace(race) ?? throw new Exception($"Could not get raceInfo from race '{race}'");
                stats = new(sex, raceInfo, jobInfo, level);
            }

            string[] serviceFlags = record.json["ai_data"]?["services"]?.ToString().Split("|") ?? throw new Exception($"NpcContent is missing ai_data->services");
            services = new();
            foreach (string s in serviceFlags)
            {
                string trim = s.Trim().ToLower().Replace("_", "");
                try
                {
                    Service service = (Service)System.Enum.Parse(typeof(Service), trim, true);
                    services.Add(service);
                }
                catch { }
            }

            rotation += new Vector3(0f, 180f, 8);  // models are rotated during conversion, placements like this are rotated here during serializiation to match

            inventory = new();
            JsonArray invJson = record.json["inventory"]?.AsArray() ?? [];
            foreach(var node in invJson)
            {
                if (node == null)
                    continue;

                JsonArray item = node.AsArray();
                if (item.Count < 2)
                    throw new Exception("NpcContent inventory node is has less than the required elements!");
                inventory.Add(new(item[1]!.GetValue<string>().ToLower(), Math.Max(1, Math.Abs(item[0]!.GetValue<int>()))));
            }

            spells = new();
            if (record.json["spells"] != null)
            {
                JsonArray spellJson = record.json["spells"]!.AsArray();
                for(int i=0;i<spellJson.Count;i++)
                {
                    spells.Add(spellJson[i]!.GetValue<string>().ToLower());
                }
            }

            travel = new();
            JsonArray travelJson = record.json["travel_destinations"]?.AsArray() ?? [];
            foreach (var t in travelJson)
            {
                if (t == null) continue;
                travel.Add(new Travel(t));
            }
        }

        /* Return true if this npc is a generic guard that can arrest the player for crimes */
        public bool IsGuard() { return job == "Guard" || job == "Ordinator Guard"; }

        /* Return true if this npc has any barter service */
        public bool HasBarter()
        {
            return
                services.Contains(Service.BartersWeapons) ||
                services.Contains(Service.BartersArmor) ||
                services.Contains(Service.BartersClothing) ||
                services.Contains(Service.BartersIngredients) ||
                services.Contains(Service.BartersApparatus) ||
                services.Contains(Service.BartersAlchemy) ||
                services.Contains(Service.BartersBooks) ||
                services.Contains(Service.BartersMiscItems) ||
                services.Contains(Service.BartersEnchantedItems) ||
                services.Contains(Service.BartersRepairItems) ||
                services.Contains(Service.BartersLockpicks) ||
                services.Contains(Service.BartersProbes) ||
                services.Contains(Service.BartersLights);
        }

        public bool SellsSpells()
        {
            return services.Contains(Service.OffersSpells);
        }

        public bool OffersMemorize()
        {
            return
                services.Contains(Service.OffersSpells) ||
                services.Contains(Service.OffersSpellmaking) ||
                OffersTraining(Stats.Skill.Alteration) ||
                OffersTraining(Stats.Skill.Conjuration) ||
                OffersTraining(Stats.Skill.Destruction) ||
                OffersTraining(Stats.Skill.Illusion) ||
                OffersTraining(Stats.Skill.Mysticism) ||
                OffersTraining(Stats.Skill.Restoration);
        }

        public bool OffersEnchanting()
        {
            return job.ToLower() == "enchanter service" || services.Contains(Service.OffersEnchanting) || OffersTraining(Stats.Skill.Enchant);
        }

        public bool OffersTraining(Stats.Skill skill)
        {
            return services.Contains(Service.OffersTraining) && stats.GetHighest(3).Contains(skill) && stats.Get(skill) >= (int)Stats.Tier.Apprentice;
        }

        public bool OffersAlchemy()
        {
            return job.ToLower() == "alchemist service" || job.ToLower() == "apothecary service" || OffersTraining(Stats.Skill.Alchemy);
        }

        public bool OffersTailoring()
        {
            return job.ToLower() == "clothier";
        }

        public bool OffersSmithing()
        {
            return job.ToLower() == "smith" || OffersTraining(Stats.Skill.Armorer);
        }
    }

    /* creatures, both leveled and non-leveled */
    public class CreatureContent : Content
    {
        public CreatureContent(Cell cell, JsonNode json, Record record) : base(cell, json, record)
        {
            // Kinda stubby for now

            rotation += new Vector3(0f, 180f, 8);  // models are rotated during conversion, placements like this are rotated here during serializiation to match
        }
    }

    /* static meshes to be converted to assets */
    public class AssetContent : Content
    {
        public AssetContent(Cell cell, JsonNode json, Record record) : base(cell, json, record)
        {
            mesh = record.json["mesh"]?.ToString().ToLower();
        }

        public EmitterContent ConvertToEmitter()
        {
            return new EmitterContent(id, type, load, position, rotation, scale, mesh ?? throw new Exception("Mesh value is null, cannot convert to EmitterContent"));
        }
    }

    /* doors, both warp doors and activator doors */
    public class DoorContent : Content
    {
        public class Warp
        {
            // this data comes from the esm, we use it to resolve the actual data we will use
            public string? cell { get; init; }
            public readonly Vector3 position, rotation;

            // this is the actual warp data we generate
            public int map, x, y, block;
            public uint entity;
            public string prompt { get; set; } = "Morrowind"; // used for the action button prompt. this is either the cell name, region name, or a generic "Morrowind" as a last case

            public Warp(JsonNode json)
            {
                float x = float.Parse(json["translation"]?[0]?.ToString() ?? throw new Exception("Warp content missing value translation->x"));
                float z = float.Parse(json["translation"]?[1]?.ToString() ?? throw new Exception("Warp content missing value translation->y"));
                float y = float.Parse(json["translation"]?[2]?.ToString() ?? throw new Exception("Warp content missing value translation->z"));

                float i = float.Parse(json["rotation"]?[0]?.ToString() ?? throw new Exception("Warp content missing value rotation->x"));
                float j = float.Parse(json["rotation"]?[1]?.ToString() ?? throw new Exception("Warp content missing value rotation->y"));
                float k = float.Parse(json["rotation"]?[2]?.ToString() ?? throw new Exception("Warp content missing value rotation->z"));

                // Same rotation code as in content, just copy pasted because lol lmao
                /* Katalashes code from MapStudio */
                Vector3 MatrixToEulerXZY(Matrix4x4 m)
                {
                    const float Pi = (float)Math.PI;
                    const float Deg2Rad = Pi / 180.0f;
                    Vector3 ret;
                    ret.Z = MathF.Asin(-Math.Clamp(-m.M12, -1, 1));

                    if (Math.Abs(m.M12) < 0.9999999)
                    {
                        ret.X = MathF.Atan2(-m.M32, m.M22);
                        ret.Y = MathF.Atan2(-m.M13, m.M11);
                    }
                    else
                    {
                        ret.X = MathF.Atan2(m.M23, m.M33);
                        ret.Y = 0;
                    }
                    ret.X = ret.X <= -180.0f * Deg2Rad ? ret.X + 360.0f * Deg2Rad : ret.X;
                    ret.Y = ret.Y <= -180.0f * Deg2Rad ? ret.Y + 360.0f * Deg2Rad : ret.Y;
                    ret.Z = ret.Z <= -180.0f * Deg2Rad ? ret.Z + 360.0f * Deg2Rad : ret.Z;
                    return ret;
                }

                /* Adapted code from https://github.com/ColeDeanShepherd/TESUnity */
                Quaternion xRot = Quaternion.CreateFromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f), i);
                Quaternion yRot = Quaternion.CreateFromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f), k);
                Quaternion zRot = Quaternion.CreateFromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f), j);
                Quaternion q = xRot * zRot * yRot;

                Vector3 eu = MatrixToEulerXZY(Matrix4x4.CreateFromQuaternion(q));

                position = new Vector3(x, y, z) * Const.GLOBAL_SCALE;
                rotation = (eu * (float)(180 / Math.PI)) + new Vector3(0f, 180f, 0); // bonus rotation here, actual models get rotated 180 Y in the model itself, placements like this need it here
                cell = json["cell"]?.ToString().Trim();
                if (cell == "") { cell = null; }
            }
        }

        public Warp? warp;
        public DoorContent(Cell cell, JsonNode json, Record record) : base(cell, json, record)
        {
            mesh = record.json["mesh"]?.ToString().ToLower();

            if (json["destination"]  == null) { warp = null; }
            else
            {
                warp = new(json["destination"] ?? throw new Exception("DoorContent value destination is missing!"));
            }
        }
    }

    /* static mesh of a container in the world that can **CAN** (but not always) be lootable */
    public class ContainerContent : Content
    {
        public readonly string? ownerNpc; // npc record id of the owenr of this container, can be null
        public readonly string? ownerFaction; // faction id that owns this container, player can take it if they are in that faction. can be null

        public List<(string id, int quantity)> inventory;

        public Script.Flag? treasure; // if this container content has a treasure event and is a lootable container, this flag will be the "has been looted" flag. otherwise null

        public ContainerContent(Cell cell, JsonNode json, Record record) : base(cell, json, record)
        {
            mesh = record.json["mesh"]?.ToString().ToLower();
            if (json["owner"] != null) { ownerNpc = json["owner"]!.GetValue<string>(); }
            if (json["owner_faction"] != null) { ownerFaction = json["owner_faction"]!.GetValue<string>(); }

            inventory = new();
            JsonArray invJson = record.json["inventory"]?.AsArray() ?? [];
            foreach (var node in invJson)
            {
                if (node == null) 
                    continue;
                JsonArray item = node.AsArray();
                if (item.Count < 2)
                    throw new Exception("ContainerContent inventory node has less than the required elements!");

                inventory.Add(new(item[1]!.GetValue<string>().ToLower(), Math.Max(1, Math.Abs(item[0]!.GetValue<int>()))));  // get item record id and quantity from json
            }
        }

        // Generates button prompt text for looting this container
        public string ActionText()
        {
            if (ownerNpc != null || ownerFaction != null) { return $"Steal from {name}"; }
            return $"Loot {name}";
        }
    }

    /* PickableContent */    // plants you can pick for alchemy ingredients. EX: rowa berry bushes
    public class PickableContent : Content
    {
        public List<(string id, int quantity)> inventory;

        public PickableContent(Cell cell, JsonNode json, Record record) : base(cell, json, record)
        {
            mesh = record.json["mesh"]?.ToString().ToLower();

            inventory = new();
            JsonArray invJson = record.json["inventory"]?.AsArray() ?? [];
            foreach (var node in invJson)
            {
                if (node == null) continue;
                JsonArray item = node.AsArray();
                if (item.Count < 2)
                    throw new Exception("PickableContent inventory node has less than the required values!");

                inventory.Add(new(item[1]!.GetValue<string>().ToLower(), Math.Max(1, Math.Abs(item[0]!.GetValue<int>()))));  // get item record id and quantity from json
            }
        }

        // Generates button prompt text for looting this container
        public string ActionText()
        {
            return $"Harvest {name}";
        }
    }

    /* static mesh of an item placed in the world that can **CAN** (but not always) be pickupable */
    public class ItemContent : Content
    {
        public readonly string? ownerNpc; // npc record id of the owenr of this item, can be null
        public readonly string? ownerFaction; // faction id that owns this item, player can take it if they are in that faction. can be null

        public readonly int value; // morrowind gp value for this item

        public Script.Flag? treasure; // if this item content has a treasure event and is a lootable item, this flag will be the "is picked up" flag. otherwise null

        public ItemContent(Cell cell, JsonNode json, Record record) : base(cell, json, record)
        {
            mesh = record.json["mesh"]?.ToString().ToLower();
            if (json["owner"] != null ) { ownerNpc = json["owner"]!.GetValue<string>(); }
            if (json["owner_faction"] != null) { ownerFaction = json["owner_faction"]!.GetValue<string>(); }
            value = record.json["data"]?["value"]?.GetValue<int>() ?? 1000; // Default to 1k
        }

        // Generates button prompt text for looting this container
        public string ActionText()
        {
            if (ownerNpc != null || ownerFaction != null) { return $"Steal {name}"; }
            return $"Pick up {name}";
        }
    }

    /* static meshes that have emitters/lights EX: candles/campfires -- converted to assets but also generates ffx files and params to make them work */
    public class EmitterContent : Content
    {
        public EmitterContent(Cell cell, JsonNode json, Record record) : base(cell, json, record)
        {
            mesh = record.json["mesh"]?.ToString().ToLower();
        }

        public EmitterContent(string id, ESM.Type type, Int2 load, Vector3 position, Vector3 rotation, int scale, string mesh) : base(id, type, load, position, rotation, scale)
        {
            this.mesh = mesh;
        }
    }

    /* invisible lights with no static mesh associated */
    public class LightContent : Content 
    {
        public readonly Byte4 color;
        public readonly float radius, weight;
        public readonly int value, time;

        public bool dynamic, fire, negative, defaultOff;
        public Mode mode;

        public enum Mode { Flicker, FlickerSlow, Pulse, PulseSlow, Default }

        public LightContent(Cell cell, JsonNode json, Record record) : base(cell, json, record)
        {
            int r = int.Parse(record.json["data"]?["color"]?[0]?.ToString() ?? throw new Exception("LightContent is missing value data->color->r!"));
            int b = int.Parse(record.json["data"]?["color"]?[2]?.ToString() ?? throw new Exception("LightContent is missing value data->color->g!"));
            int g = int.Parse(record.json["data"]?["color"]?[1]?.ToString() ?? throw new Exception("LightContent is missing value data->color->b!"));
            int a = int.Parse(record.json["data"]?["color"]?[3]?.ToString() ?? throw new Exception("LightContent is missing value data->color->a!"));
            color = new(r, g, b, a);  // 0 -> 255 colors

            radius = float.Parse(record.json["data"]?["radius"]?.ToString() ?? throw new Exception("LightContent is missing value data->radius!")) * Const.GLOBAL_SCALE;
            weight = float.Parse(record.json["data"]?["weight"]?.ToString() ?? throw new Exception("LightContent is missing value data->weight!"));

            value = int.Parse(record.json["data"]?["value"]?.ToString() ?? throw new Exception("LightContent is missing value data->value!"));
            time = int.Parse(record.json["data"]?["time"]?.ToString() ?? throw new Exception("LightContent is missing value data->time!"));

            string flags = record.json["data"]?["flags"]?.ToString() ?? throw new Exception("LightContent is missing value data->flags!");

            dynamic = flags.Contains("DYNAMIC");
            fire = flags.Contains("FIRE");
            negative = flags.Contains("NEGATIVE");
            defaultOff = flags.Contains("OFF_BY_DEFAULT");

            if (flags.Contains("FLICKER_SLOW")) { mode = Mode.FlickerSlow; }
            else if (flags.Contains("FLICKER")) { mode = Mode.Flicker; }
            else if (flags.Contains("PULSE_SLOW")) { mode = Mode.PulseSlow; }
            else if (flags.Contains("PULSE")) { mode = Mode.Pulse; }
            else { mode = Mode.Default; }
        }
    }
}
