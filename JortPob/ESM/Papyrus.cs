using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace JortPob
{
    public class Papyrus
    {
        public readonly string id;
        public readonly Scope scope;

        public Papyrus(JsonNode json)
        {
            id = json["id"].GetValue<string>();

            Stack<Call> stack = new();
            string raw = json["text"].GetValue<string>();
            string[] rawLines = raw.Split("\r\n");
            bool condFail = false;
            for(int i=0;i< rawLines.Length;i++)
            {
                string sanitize = rawLines[i].ToLower().Trim();

                // Replace any tabs with spaces for consistency
                if (sanitize.Contains("\t")) { sanitize = sanitize.Replace("\t", " "); }

                // Remove trailing comments
                if (sanitize.Contains(";"))
                {
                    sanitize = sanitize.Split(";")[0].Trim();
                }

                // Remove any multi spaces
                while (sanitize.Contains("  "))
                {
                    sanitize = sanitize.Replace("  ", " ");
                }

                // Skip comments and empty lines
                if (sanitize.Contains(";") || sanitize == "")
                {
                    continue;
                }

                /* Fun begins here */

                // Handle IF statement
                if (sanitize.StartsWith("if"))
                {
                    Conditional call = new(sanitize);

                    Call parent = stack.Peek();
                    if (parent is Conditional)
                    {
                        if (condFail) { ((Conditional)parent).fail.Add(call); }
                        else { ((Conditional)parent).pass.Add(call); }
                    }
                    else { ((Main)parent).scope.Add(call); }

                    stack.Push(call);
                    condFail = false;
                }
                // Handle ELSEIF statement
                else if(sanitize.StartsWith("elseif"))
                {
                    ConditionalBranch call = new(sanitize);
                    ((Conditional)stack.Peek()).fail.Add(call);
                    stack.Push(call);
                    condFail = false;
                }
                // Handle ELSE statement
                else if (sanitize.StartsWith("else"))
                {
                    condFail = true;
                }
                // Handle ENDIF
                else if (sanitize.StartsWith("endif")) {
                    while(stack.Peek() is ConditionalBranch)
                    {
                        stack.Pop(); // pop all elseif branches
                    }

                    stack.Pop();
                    condFail = false;
                }
                // Handle script BEGIN
                else if (sanitize.StartsWith("begin"))
                {
                    Main call = new(sanitize);
                    stack.Push(call);
                }
                // Handle script END
                else if (sanitize.StartsWith("end"))
                {
                    scope = ((Main)stack.Pop()).scope;
                    break;
                }
                // Handle normal call
                else
                {
                    Call call = new(sanitize);
                    Call parent = stack.Peek();
                    if (parent is Conditional)
                    {
                        if (condFail) { ((Conditional)parent).fail.Add(call); }
                        else { ((Conditional)parent).pass.Add(call); }
                    }
                    else { ((Main)parent).scope.Add(call); }
                }
            }
        }


        public class Scope
        {
            public readonly List<Call> calls;

            public Scope()
            {
                calls = new();
            }

            public void Add(Call call)
            {
                calls.Add(call);
            }
        }

        [DebuggerDisplay("Call :: {type}")]
        public class Call
        {
            public enum Type
            {
                /* Default */
                None,

                /* Papyrus calls that are fully implemented in dialog and script */

                /* Papyrus calls that are fully implemented in dialog */
                Set,
                Journal, Choice, AddTopic,
                ModDisposition, SetDisposition, ModReputation,
                ModPcFacRep, PcJoinFaction, PcClearExpelled, PcRaiseRank, PcExpell,
                SetPcCrimeLevel, MessageBox,

                /* Papyrus calls that are partially implemented */
                Goodbye,
                StartCombat,
                AddItem, RemoveItem,
                PayFine, GoToJail,

                /* Papyrus calls that are not implemented yet */
                Disable, Enable,
                Activate, Playgroup, Say, GetTarget, GetItemCount, Lock, Unlock, GetSpell, SayDone, GetRace, ModRegion, GetDetected, ForceSneak, ClearForceSneak,
                ChangeWeather, GetPos, RotateWorld, DontSaveObject, HasSoulGem, WakeUpPc, Resurrect, PlayBink, SetAtStart, RemoveSoulGem, Fall,
                MoveWorld, Position, StreamMusic, GetDisposition, Move, LoopGroup, FadeOut, FadeIn,
                GetDistance, Drop, GetDisabled, OnDeath, SetFlee, GetBlightDisease, OnActivate, HurtStandingActor,
                GetPcRank, SetPos, GetAttacked, GetCommonDisease, GetEffect, SetFight, ShowMap, AddSpell, RemoveSpell, RaiseRank, StopCombat,
                ModFactionReaction, ModFlee, SetAlarm, PlaceAtPc, ClearInfoActor, Cast, ForceGreeting, SetHello, GetJournalIndex, PayFineThief,
                AiWander, AiFollow, AiFollowCell, AiEscort, GetAiPackageDone, GetCurrentAiPackage, AiTravel, AiFollowCellPlayer, PositionCell, ModFight,

                GetHealth, GetMagicka, GetFatigue, GetSecurity,

                SetHealth, SetMagicka, SetFatigue,
                ModHealth, ModMagicka, ModFatigue, ModCurrentHealth, ModCurrentMagicka, ModCurrentFatigue,

                GetStrength, GetIntelligence, GetWillpower, GetAgility, GetSpeed, GetEndurance, GetPersonality, GetLuck,
                SetStrength, SetIntelligence, SetWillpower, SetAgility, SetSpeed, SetEndurance, SetPersonality, SetLuck,
                ModStrength, ModIntelligence, ModWillpower, ModAgility, ModSpeed, ModEndurance, ModPersonality, ModLuck,

                SetAthletics, SetMarksman, SetLongBlade, SetAlchemy, SetBlock, SetMercantile, SetEnchant, SetDestruction, SetAlteration, SetIllusion, SetConjuration, SetMysticism, SetRestoration, SetSpear, SetAxe, SetBluntWeapon, SetArmorer, SetHeavyArmor, SetMediumArmor,
                ModRestoration, ModAthletics, ModLongBlade, ModHeavyArmor, ModMediumArmor, ModBlock, ModSpear, ModAxe, ModBluntWeapon, ModArmorer, ModMarksman, ModMercantile,

                ShowRestMenu,
                EnableStatsMenu, EnableMapMenu, EnableRaceMenu, EnableMagicMenu, EnableStatReviewMenu, EnableBirthMenu, EnableClassMenu, EnableInventoryMenu, EnableNameMenu,
                EnableVanityMode, EnableRest, EnablePlayerJumping, EnablePlayerFighting, EnablePlayerControls, EnablePlayerMagic, EnableTeleporting, EnablePlayerViewSwitch,
                DisablePlayerViewSwitch, DisableTeleporting, DisablePlayerFighting, DisablePlayerJumping, DisablePlayerControls, DisableVanityMode, DisablePlayerMagic,
                PlaySound3D, PlaySound3DVP, StopSound, PlayLoopSound3d, PlayLoopSound3DVP, PlaySound, PlayLoopSoundD3DVP, PlaySoundVP,

                /* Papyrus calls we (probably) cannot implement and will discard */
                Rotate, SetAngle, GetAngle,

                /* Variable declarations */
                Short, Float,

                /* Conditionals and Scopes */
                Begin, End, If, Else, ElseIf, EndIf,

                /* Misc */
                StartScript, StopScript,
                Return
            }

            public readonly string RAW; // raw original data for debug only

            public readonly Type type;
            public readonly string target;         // can be null, this is set if a papyrus call is on an object like "player->additem cbt 1"
            public readonly string[] parameters;

            public Call(string line)
            {
                RAW = line;

                string sanitize = line.Trim().ToLower();
                if (sanitize.StartsWith(";") || sanitize == "") { type = Type.None; target = null; parameters = new string[0]; return; } // line does nothing

                // Replace any tabs with spaces for consistency
                if (sanitize.Contains("\t")) { sanitize = sanitize.Replace("\t", " "); }

                // Remove trailing comments
                if (sanitize.Contains(";"))
                {
                    sanitize = sanitize.Split(";")[0].Trim();
                }

                // Remove any multi spaces
                while (sanitize.Contains("  "))
                {
                    sanitize = sanitize.Replace("  ", " ");
                }

                // Remove any commas as they are not actually needed for papyrus syntax and are used somewhat randomly lol
                // @TODO: this is fine except on choice calls which have strings with dialog text in them. the dialogs can have commas but we are just erasing them rn. should fix, low prio
                sanitize = sanitize.Replace(",", "");

                // Fix a specific single case where a stupid -> has a space in it
                if (sanitize.Contains("-> ")) { sanitize = sanitize.Replace("-> ", "->"); }

                // Fix a specific single case of weird syntax
                if (sanitize.Contains("\"1 ")) { sanitize = sanitize.Replace("\"1 ", "\" 1 "); }

                // Fix a specific case where a single quote is used at random for no fucking reason
                if (sanitize.Contains("land deed'")) { sanitize = sanitize.Replace("land deed'", "land deed\""); }

                // Fix a specific case in Tribunal.esm where some weirdo used a colon after the choice command for no reason
                if (sanitize.Contains("choice:")) { sanitize = sanitize.Replace("choice:", "choice"); }

                // Handle targeted call
                if (sanitize.Contains("->"))
                {
                    // Special split because targets can be in quotes and have spaces in them
                    string[] split = sanitize.Split("->");
                    List<string> ps = Regex.Matches(split[1], @"[\""].+?[\""]|[^ ]+")
                        .Cast<Match>()
                        .Select(m => m.Value)
                        .ToList();

                    type = (Type)Enum.Parse(typeof(Type), ps[0], true);
                    target = split[0].Replace("\"", "");
                    ps.RemoveAt(0);
                    parameters = ps.ToArray();
                }
                // Handle normal call
                else
                {
                    List<string> split = sanitize.Split(" ").ToList();

                    type = (Type)Enum.Parse(typeof(Type), split[0], true);
                    target = null;
                    split.RemoveAt(0);

                    /* Handle special case where you have a call like this :: Set "Manilian Scerius".slaveStatus to 2 */
                    /* Seems to be fairly rare that we have syntax like this but it does happen. */
                    /* Recombine the 2 halves of that "name" and remove the quotes */
                    List<string> recomb = new();
                    for (int i = 0; i < split.Count(); i++)
                    {
                        string s = split[i];
                        if (s.StartsWith("\""))
                        {
                            if (s.Split("\"").Length - 1 == 2) { recomb.Add(s.Replace("\"", "")); }
                            else
                            {
                                string itrNxt = split[++i];
                                while (!itrNxt.Contains("\""))
                                {
                                    itrNxt += $" {split[++i]}";
                                }
                                recomb.Add(($"{s} {itrNxt}").Replace("\"", ""));
                            }
                            continue;
                        }

                        recomb.Add(s);
                    }

                    parameters = recomb.ToArray();
                }
            }
        }

        [DebuggerDisplay("Main Scope :: MAIN")]
        public class Main : Call
        {
            public readonly Scope scope;
            public Main(string line) : base(line)
            {
                scope = new();
            }
        }

        [DebuggerDisplay("Conditional :: IF")]
        public class Conditional : Call
        {
            public readonly Scope pass;
            public readonly Scope fail;

            public Conditional(string line) : base(line.Replace("(", " ").Replace(")", " "))
            {
                pass = new();
                fail = new();
            }
        }

        [DebuggerDisplay("ConditionalBranch :: ELIF")]
        public class ConditionalBranch : Conditional
        {
            public ConditionalBranch(string line) : base(line)
            {

            }
        }
    }
}
