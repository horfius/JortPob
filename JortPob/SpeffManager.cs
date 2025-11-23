using HKLib.hk2018;
using JortPob.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging.Effects;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using WitchyFormats;
using static IronPython.Modules._ast;
using static JortPob.SpeffManager;
using static JortPob.SpeffManager.Speff.Effect;
using static JortPob.SpeffManager.SpeffEnchant;

namespace JortPob
{
    public class SpeffManager
    {
        public enum Type
        {
            Custom,        // Freeform gen from a json file
            Spell,         // This is a castable spell in morrowind. Some of these are used in scripts to cast an effect on the player (e.g. shrine restoration/cure disease) * most of these will go unused in ER though
            Enchanting,    // This is an enchantment effect on an item. 
            Alchemy        // Alchemy effet from drinking a potion
        }

        public readonly List<Speff> speffs;

        private Paramanager paramanager;
        private TextManager textManager;

        private int nextSpeffId = 1000000;

        public SpeffManager(ESM esm, Paramanager paramanager, TextManager textManager)
        {
            this.paramanager = paramanager;
            this.textManager = textManager;

            speffs = new();

            /* Generate SPEFF params from overrides. */
            foreach (Override.SpeffDefinition def in Override.GetSpeffDefinitions())
            {
                Speff speff = new SpeffCustom(def.id, nextSpeffId += 10);
                speffs.Add(speff);
            }

            bool SpeffExists(string id)
            {
                foreach(Speff speff in speffs)
                {
                    if (speff.id == id) { return true; }
                }
                return false;
            }

            /* Parse all magical effects from the esm, skip any that were already generated from override stuff above */
            foreach (JsonNode json in esm.GetAllRecordsByType(ESM.Type.Alchemy))
            {
                if(SpeffExists(json["id"].GetValue<string>().Trim().ToLower()) ) { continue; }

                SpeffAlchemy speff = new(nextSpeffId += 10, json);
                speffs.Add(speff);
            }

            foreach (JsonNode json in esm.GetAllRecordsByType(ESM.Type.Enchanting))
            {
                if (SpeffExists(json["id"].GetValue<string>().Trim().ToLower())) { continue; }

                bool isWeapon = false;
                foreach (JsonNode jsonW in esm.GetAllRecordsByType(ESM.Type.Weapon))
                {
                    if (jsonW["enchanting"] != null && jsonW["enchanting"].GetValue<string>().ToLower() == json["id"].GetValue<string>().ToLower())
                    {
                        isWeapon = true; break;
                    }
                }

                SpeffEnchant speff = new(nextSpeffId += 10, json, isWeapon?SpeffEnchant.EquipmentType.Weapon:SpeffEnchant.EquipmentType.Apparel);
                speffs.Add(speff);
            }

            foreach (JsonNode json in esm.GetAllRecordsByType(ESM.Type.Spell))
            {
                if (SpeffExists(json["id"].GetValue<string>().Trim().ToLower())) { continue; }

                SpeffSpell speff = new(nextSpeffId += 10, json);
                speffs.Add(speff);
            }

            /* Generate SPEFF params from the parsed data */
            foreach (Speff speff in speffs)
            {
                // Generate via custom json speff definition
                if (speff.type == SpeffManager.Type.Custom)
                {
                    Override.SpeffDefinition def = Override.GetSpeffDefinition(speff.id);
                    SillyJsonUtils.CopyRowAndModify(paramanager, this, Paramanager.ParamType.SpEffectParam, $"Custom :: {speff.id}", def.row, speff.row, def.data);
                }
                // Generate via data parsed from esm
                else 
                {
                    GenerateSpeff(speff);
                }
            }
        }

        private void GenerateSpeff(Speff speff)
        {
            /* Decide on a source row to copy as a template based on the type of magical effect */
            int sourceRow;
            if (speff.type == SpeffManager.Type.Alchemy) { sourceRow = 1642100; }  // Temporary buff speff
            else if (speff.type == SpeffManager.Type.Spell && ((SpeffSpell)speff).spellType == SpeffSpell.SpellType.Spell) { sourceRow = 1642100; } // Temporary buff speff
            else if (speff.type == SpeffManager.Type.Spell) { sourceRow = 310000; } // Permanent buff speff
            else if (speff.type == SpeffManager.Type.Enchanting && ((SpeffEnchant)speff).enchantType != EnchantType.CastOnStrike) { sourceRow = 310000; } // Permanent buff speff
            else if (speff.type == SpeffManager.Type.Enchanting) { sourceRow = 6600; } // On hit speff
            else { throw new Exception("Something weird happened!"); }

            // Temp buffs use 1642100 as a base which is the basic Heal incantation effect
            // Permanent buffs use 310000 as a base which is effect of the crimson amber medallion
            // On hit effects use 1642100 as well for now. I think this will work but i'm not super sure so uhhhh w/e guh

            /* Clone our row */
            FsParam param = paramanager.param[Paramanager.ParamType.SpEffectParam];
            FsParam.Row row = paramanager.CloneRow(param[sourceRow], $"{speff.type} :: {speff.id}", speff.row);

            /* Nullify some values from our templates to make our speff as neutral as possible */
            switch (sourceRow)
            {
                case 1642100: // Heal
                    row["motionInterval"].Value.SetValue(1f);              // for effects like regen this sets the interval between stat changes. morrowind uses 1 second intervals so we will as well!
                    row["spCategory"].Value.SetValue((ushort)0);           // behaviour when stacking or whatever (default is NONE)
                    row["stateInfo"].Value.SetValue((ushort)0);            // visual effect of spefff (i think??)
                    row["changeHpPoint"].Value.SetValue(0);
                    row["bAdjustFaithAblity"].Value.SetValue((byte)0);
                    row["vfxId"].Value.SetValue(-1);                     // also visual effect idk guh??
                    row["effectEndurance"].Value.SetValue((float)speff.Duration());
                    break;
                case 310000: // Crimson Amber Mediallion
                    row["motionInterval"].Value.SetValue(1f);            // for effects like regen this sets the interval between stat changes. morrowind uses 1 second intervals so we will as well!
                    row["iconId"].Value.SetValue(-1);                    // buff icon
                    row["maxHpRate"].Value.SetValue(1f);
                    break;
            }

            /* Apply some values to our speffs based on what stuff its got in its magic effects */
            foreach (Speff.Effect effect in speff.effects)
            {
                switch (effect.effect)
                {
                    case MagicEffect.RestoreHealth:
                        row["changeHpPoint"].Value.SetValue(-effect.Magnitude());
                        break;
                    case MagicEffect.RestoreMagicka:
                        row["changeMpPoint"].Value.SetValue(-effect.Magnitude());
                        break;
                    case MagicEffect.RestoreFatigue:
                        row["changeStaminaPoint"].Value.SetValue(-effect.Magnitude());
                        break;
                    case MagicEffect.FortifyHealth:
                        row["maxHpRate"].Value.SetValue(effect.PositiveScalarMagnitude(0.2f));
                        break;
                    case MagicEffect.FortifyMagicka:
                        row["maxMpRate"].Value.SetValue(effect.PositiveScalarMagnitude(0.2f));
                        break;
                    case MagicEffect.FortifyFatigue:
                        row["maxStaminaRate"].Value.SetValue(effect.PositiveScalarMagnitude(0.2f));
                        break;
                    case MagicEffect.FortifyAttribute:
                        switch (effect.attribute)
                        {
                            case Speff.Effect.Attribute.Endurance:
                                row["addLifeForceStatus"].Value.SetValue((sbyte)effect.Magnitude());
                                break;
                            case Speff.Effect.Attribute.Willpower:
                                row["addWillpowerStatus"].Value.SetValue((sbyte)effect.Magnitude());
                                break;
                            case Speff.Effect.Attribute.Strength:
                                row["addStrengthStatus"].Value.SetValue((sbyte)effect.Magnitude());
                                break;
                            case Speff.Effect.Attribute.Agility:
                                row["addDexterityStatus"].Value.SetValue((sbyte)effect.Magnitude());
                                break;
                            case Speff.Effect.Attribute.Intelligence:
                                row["addMagicStatus"].Value.SetValue((sbyte)effect.Magnitude());
                                break;
                            case Speff.Effect.Attribute.Speed:
                                row["addEndureStatus"].Value.SetValue((sbyte)effect.Magnitude());
                                break;
                            case Speff.Effect.Attribute.Personality:
                                row["addLuckStatus"].Value.SetValue((sbyte)effect.Magnitude());
                                break;
                            case Speff.Effect.Attribute.Luck:
                                row["addLuckStatus"].Value.SetValue((sbyte)effect.Magnitude());
                                break;
                        }
                        break;
                }
            }

            /* Plonk our new row into the param */
            paramanager.AddRow(param, row);
        }

        public Speff GetSpeff(string id, SpeffManager.Type type)
        {
            foreach(Speff speff in speffs)
            {
                if(speff.id == id && speff.type == type)
                {
                    return speff;
                }
            }
            return null;
        }

        public Speff GetSpeff(string id)
        {
            foreach (Speff speff in speffs)
            {
                if (speff.id == id)
                {
                    return speff;
                }
            }
            return null;
        }

        public SpeffAlchemy GetAlchemySpeff(string id) { return GetSpeff(id, SpeffManager.Type.Alchemy) as SpeffAlchemy; }
        public SpeffEnchant GetEnchantingSpeff(string id) { return GetSpeff(id, SpeffManager.Type.Enchanting) as SpeffEnchant; }
        public SpeffSpell GetSpellSpeff(string id) { return GetSpeff(id, SpeffManager.Type.Spell) as SpeffSpell; }

        /* Stores info on an item */
        [DebuggerDisplay("Speff :: {type} :: {id} :: {row}")]
        public abstract class Speff
        {
            public readonly string id;  // morrowind record id
            public readonly Type type;  // type of record from morrowind
            public readonly int row;    // param row

            public readonly List<Effect> effects;

            public Speff(int row, JsonNode json)
            {
                id = json["id"].GetValue<string>().Trim().ToLower();
                type = (SpeffManager.Type)System.Enum.Parse(typeof(SpeffManager.Type), json["type"].GetValue<string>());
                this.row = row;

                effects = new();
                JsonArray jsonEffects = json["effects"].AsArray();
                foreach(JsonNode jsonEffect in jsonEffects)
                {
                    effects.Add(new Effect(jsonEffect));
                }
            }

            public Speff(string id, int row)
            {
                this.id = id;
                this.row = row;
                type = SpeffManager.Type.Custom;
                effects = new();
            }

            public int Duration()
            {
                int longest = 0;
                foreach(Effect effect in effects)
                {
                    if (effect.duration > longest) { longest = effect.duration; }
                }
                return longest;
            }

            [DebuggerDisplay("MagicEffect :: {effect} :: {skill} :: {range}")]
            public class Effect
            {
                public enum MagicEffect
                {
                    Levitate, FortifyAttribute, DrainAttribute, AlmsiviIntervention, Burden, Chameleon, CureBlightDisease, CureCommonDisease, CureParalyzation, CurePoison, DetectAnimal, DetectEnchantment,
                    DetectKey, ResistCommonDisease, Dispel, SlowFall, SwiftSwim, DrainMagicka, Feather, ResistFire, FireShield, FortifyAttackBonus, FortifyFatigue, FortifyHealth, FortifyMagicka,
                    ResistFrost, FrostShield, RestoreHealth, RestoreFatigue, Shield, Invisibility, Jump, Light, LightningShield, ResistMagicka, Mark, NightEye, Paralyze, ResistPoison, Telekinesis,
                    Recall, Reflect, RestoreAttribute, RestoreMagicka, ResistShock, Silence, SpellAbsorption, WaterBreathing, WaterWalking, DamageAttribute, FortifySkill, DisintegrateArmor,
                    CommandCreature, CommandHumanoid, ResistNormalWeapons, DamageHealth, Charm, FrostDamage, Blind, SummonFlameAtronach, SummonGhost, Sanctuary, FireDamage, WeaknessToFire, 
                    WeaknessToShock, ShockDamage, WeaknessToPoison, Poison, SummonFrostAtronach, SummonStormAtronach, DisintegrateWeapon, AbsorbFatigue, DrainFatigue, DamageSkill, AbsorbAttribute,
                    AbsorbHealth, SummonSkeleton, DamageFatigue, BoundBattleAxe, BoundBoots, BoundCuirass, BoundDagger, BoundGloves, BoundHelm, BoundLongbow, BoundLongsword, BoundMace, BoundShield,
                    BoundSpear, SummonDremora, DemoralizeHumanoid, RallyHumanoid, AbsorbSkill, Sound, SummonScamp, DemoralizeCreature, WeaknessToFrost, WeaknessToMagicka, DivineIntervention,
                    DamageMagicka, Lock, DrainHealth, SoulTrap, TurnUndead, DrainSkill, AbsorbMagicka, Open, ResistParalysis, CalmCreature, FrenzyCreature, FrenzyHumanoid, SummonGoldenSaint,
                    RallyCreature, CalmHumanoid, SummonBonelord, SummonClannfear, SummonDaedroth, SummonGreaterBonewalker, SummonLeastBonewalker, SummonHunger, SummonTwilight, FortifyMagickaMultiplier,
                    ResistBlightDisease, RestoreSkill, Corprus, ResistCorprus, CureCorprus, SummonCenturionSphere, SunDamage, Vampirism, WeaknessToBlightDisease, WeaknessToCommonDisease,
                    WeaknessToCorprus, StuntedMagicka
                }

                public enum Skill
                {
                    None,
                    Armorer, Athletics, Axe, Block, BluntWeapon, HeavyArmor, LongBlade, MediumArmor, Spear,
                    Alchemy, Alteration, Conjuration, Destruction, Enchant, Illusion, Mysticism, Restoration, Unarmored,
                    Acrobatics, HandToHand, LightArmor, Marksman, Mercantile, Security, ShortBlade, Sneak, Speechcraft
                }

                public enum Attribute
                {
                    None, Strength, Intelligence, Willpower, Agility, Speed, Endurance, Personality, Luck
                }

                public enum Range
                {
                    OnTouch, OnTarget, OnSelf
                }

                public readonly MagicEffect effect;
                public readonly Skill skill;
                public readonly Attribute attribute;
                public readonly Range range;

                public readonly int area, duration;
                private readonly int min, max;

                public Effect(JsonNode json)
                {
                    effect = (MagicEffect)System.Enum.Parse(typeof(MagicEffect), json["magic_effect"].GetValue<string>());
                    skill = (Skill)System.Enum.Parse(typeof(Skill), json["skill"].GetValue<string>());
                    attribute = (Attribute)System.Enum.Parse(typeof(Attribute), json["attribute"].GetValue<string>());
                    range = (Range)System.Enum.Parse(typeof(Range), json["range"].GetValue<string>());

                    area = json["area"].GetValue<int>();
                    duration = json["duration"].GetValue<int>();
                    min = json["min_magnitude"].GetValue<int>();
                    max = json["max_magnitude"].GetValue<int>();
                }

                /* Random magnitude doesn't exist in ER really so instead we are just going to average the min/max magnitude and use that */
                public int Magnitude()
                {
                    return (min + max) / 2;
                }

                /* Magnitude adjusted from flat value to a % buff. Example: fortify health is flat in morrowind, in ER it is a % increase to max health. This math makes that conversion okayish */
                /* Setting adjustment will let you make the effect weaker or stronger. EX: 0.2f adjustment means a 100 magnitude fortify health will give a 20% buff instead of 100% */
                public float PositiveScalarMagnitude(float adjustment = 1f)
                {
                    int magnitude = Magnitude();
                    magnitude = Math.Max(1, Math.Min(magnitude, 500)); // clamps
                    return 1f + ((magnitude / 100f) * 1f);
                }
            }
        }

        public class SpeffCustom : Speff
        {
            public SpeffCustom(string id, int row) : base(id, row)
            {
                // no extra data from custom effects rn
            }
        }

        public class SpeffAlchemy : Speff
        {
            public SpeffAlchemy(int row, JsonNode json) : base(row, json)
            {
                // no extra data from alchemy effects rn
            }
        }

        public class SpeffEnchant : Speff
        {
            public enum EquipmentType
            {
                Weapon, Apparel
            }

            public enum EnchantType
            {
                CastOnce, CastWhenUsed, CastOnStrike, ConstantEffect
            }

            public readonly EquipmentType equipmentType;
            public readonly EnchantType enchantType; 

            public SpeffEnchant(int row, JsonNode json, EquipmentType equipmentType) : base(row, json)
            {
                this.equipmentType = equipmentType;
                enchantType = (EnchantType)System.Enum.Parse(typeof(EnchantType), json["data"]["enchant_type"].GetValue<string>());
            }
        }

        public class SpeffSpell : Speff
        {
            public enum SpellType
            {
                Spell, Ability, Power, Blight, Disease
            }

            public readonly SpellType spellType;
            public readonly int cost;

            public SpeffSpell(int row, JsonNode json) : base(row, json)
            {
                spellType = (SpellType)System.Enum.Parse(typeof(SpellType), json["data"]["spell_type"].GetValue<string>());
                cost = json["data"]["cost"].GetValue<int>();
            }
        }
    }
}
