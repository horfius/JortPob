using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static JortPob.Papyrus;

namespace JortPob
{
    public class PapyrusESD
    {
        private readonly ESM esm;
        private readonly ScriptManager scriptManager;
        private readonly Paramanager paramanager;
        private readonly TextManager textManager;
        private readonly Script areaScript;
        private readonly Content content;

        private readonly string esd;

        public PapyrusESD(ESM esm, ScriptManager scriptManager, Paramanager paramanager, TextManager textManager, Script areaScript, Content content, Papyrus papyrus, uint id)
        {
            this.esm = esm;
            this.scriptManager = scriptManager;
            this.paramanager = paramanager;
            this.textManager = textManager;
            this.areaScript = areaScript;
            this.content = content;

            string s;
            s =  $"# script '{papyrus.id}' for object '{content.id}' with with entity id '{content.entity}'\r\n";
            s += $"def t{id:D9}_1():\r\n";
            s += $"    while True:\r\n";

            for (int i = 0; i < papyrus.scope.calls.Count(); i++)
            {
                Papyrus.Call call = papyrus.scope.calls[i];
                s += HandlePapyrus(call);
            }

            s += $"    Quit()\r\n";

            esd = s;
        }

        private string HandlePapyrus(Papyrus.Call call)
        {
            switch (call)
            {
                case Papyrus.ConditionalBranch celif:
                    return HandleElif(celif);
                case Papyrus.Conditional cif:
                    return HandleIf(cif);
                case Papyrus.Main cm:
                    return " ## INVALID BEGIN CALL ##";
                default:
                    return HandleCall(call);
            }
        }

        private string HandleElif(Papyrus.ConditionalBranch call)
        {
            return $"if False:\r\n{HandleScope(call.pass)}else:\r\n{HandleScope(call.fail)}";
        }

        private string HandleIf(Papyrus.Conditional call)
        {
            return $"if False:\r\n{HandleScope(call.pass)}else:\r\n{HandleScope(call.fail)}";
        }

        private string HandleScope(Papyrus.Scope scope)
        {
            if(scope.calls.Count() <= 0) { return "pass\r\n"; } // empty scope becomes a 'pass' for now @TODO: temp

            string s = "";
            foreach(Papyrus.Call call in scope.calls)
            {
                s += HandlePapyrus(call);
            }
            return s;
        }

        private string HandleCall(Papyrus.Call call)
        {
            return $"{call.type}\r\n";
        }
    }
}
