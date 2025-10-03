using HKLib.hk2018.hkaiCollisionAvoidance;
using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Linq;
using WitchyFormats;

namespace JortPob.Common
{
    public static class Utility
    {
        private static readonly char[] _dirSep = { '\\', '/' };

        /* Take a full file path and returns just a file name without directory or extensions */
        public static string PathToFileName(string fileName)
        {
            if (fileName.EndsWith("\\") || fileName.EndsWith("/"))
                fileName = fileName.TrimEnd(_dirSep);

            if (fileName.Contains("\\") || fileName.Contains("/"))
                fileName = fileName.Substring(fileName.LastIndexOfAny(_dirSep) + 1);

            if (fileName.Contains("."))
                fileName = fileName.Substring(0, fileName.LastIndexOf('.'));

            return fileName;
        }

        public static string ResourcePath(string path)
        {
            return $"{AppDomain.CurrentDomain.BaseDirectory}Resources\\{path}";
        }

        public static uint FNV1_32(byte[] data)
        {
            const uint FNV_PRIME = 16777619;
            const uint FNV_OFFSET_BASIS = 2166136261;

            uint hash = FNV_OFFSET_BASIS;

            foreach (byte b in data)
            {
                hash *= FNV_PRIME;
                hash ^= b;
            }

            return hash;
        }

        /* Try to split a piece of text on punctuation to fit within the max size of a subtitle in Elden Ring */
        public static List<string> CivilizedSplit(string text)
        {
            // Break apart all text by punctuation
            List<string> lines = new();
            string concat = "";
            for(int i=0;i<text.Length;i++)
            {
                char c = text[i];
                concat += c;

                bool isPunc = c == '.' || c == ';' || c == '?' || c == '!';
                if (isPunc) { lines.Add(concat); concat = ""; }
            }
            if (concat.Trim() != "") { lines.Add(concat.Trim()); }

            // See if any lines desperately need to be split on a comma or in the middle of a sentence
            for(int i=0;i<lines.Count();i++)
            {
                string line = lines[i];
                if(line.Length > Const.MAX_CHAR_PER_TALK)
                {
                    // search from center for a place split
                    int bestComma = -1;
                    int bestSpace = -1;
                    int middle = line.Length / 2;
                    for(int j=0;j<line.Length/2;j++)
                    {
                        int left = Math.Max(0, middle - j);
                        int right = Math.Min(middle + j, line.Length-1);

                        if (line[left] == ' ' && bestSpace == -1) { bestSpace = left; }
                        if (line[right] == ' ' && bestSpace == -1) { bestSpace = right; }
                        if (line[left] == ',' && bestComma == -1) { bestComma = left; }
                        if (line[right] == ',' && bestComma == -1) { bestComma = right; }
                    }

                    int splitAt; bool onSpace;
                    // Split on a comma
                    if (bestComma != -1) { splitAt = bestComma; onSpace = false; }
                    // Split on a space
                    else { splitAt = bestSpace; onSpace = true; }

                    string a = $"{line.Substring(0, splitAt+1).Trim()}{(onSpace?"--":"")}";
                    string b = $"{(onSpace ? "--" : "")}{line.Substring(splitAt+1, line.Length - splitAt - 1).Trim()}";

                    // Add lines back in the right spot
                    lines.RemoveAt(i);
                    lines.Insert(i, b);
                    lines.Insert(i, a);
                }
            }

            // Reassemble within tolerence of a single subtitle
            List<string> recombined = new();
            concat = "";
            foreach(string line in lines)
            {
                if(line.Length > Const.MAX_CHAR_PER_TALK)
                {
                    if(concat != "") { recombined.Add(concat); }
                    concat = line;
                    //Lort.Log($"## WARNING ## Line exceeds character limit: {text}", Lort.Type.Debug);
                }
                else if(concat.Length + line.Length <= Const.MAX_CHAR_PER_TALK)
                {
                    concat += $" {line}";
                }
                else
                {
                    recombined.Add(concat);
                    concat = line;
                }
            }
            recombined.Add(concat);

            if (recombined.Count() >= 8) { Lort.Log($"## WARNING ## Line exceeds split limit: {text}", Lort.Type.Debug); }

            return recombined;
        }

        public static string[] StringAwareSplit(string text)
        {
            if(text.Trim() == "") { return new string[0]; } // empty string = emptry array

            List<string> split = text.Split(" ").ToList();
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
            return recomb.ToArray();
        }
      
        public static string SanitizeTextForComment(string text)
        {
            return text.Replace("\r", "").Replace("\n", "");
        }

        public static bool StringIsInteger(string text)
        {
            return int.TryParse(text, out _);
        }

        public static bool StringIsFloat(string text)
        {
            return float.TryParse(text, out _);
        }

        public static bool StringIsOperator(string text)
        {
            string allowableLetters = "+=<>!-*/";

            foreach (char c in text)
            {
                // This is using String.Contains for .NET 2 compat.,
                //   hence the requirement for ToString()
                if (!allowableLetters.Contains(c.ToString()))
                    return false;
            }

            return true;
        }

        /* Sort binderfiles by id */
        public static void SortBND4(BND4 bnd)
        {
            bnd.Files = bnd.Files.OrderBy(file => file.ID).ToList();
        }

        public static void SortFsParam(FsParam param)
        {
            param.Rows = param.Rows.AsParallel().OrderBy(row => row.ID).ToList();
        }

        public static void SortPARAM(SoulsFormats.PARAM param)
        {
            param.Rows = param.Rows.AsParallel().OrderBy(row => row.ID).ToList();
        }

        public static void SortFMG(FMG fmg)
        {
            fmg.Entries = fmg.Entries.AsParallel().OrderBy(entry => entry.ID).ToList();
        }

        public static long Pow(int x, uint pow)
        {
            int ret = 1;
            while (pow != 0)
            {
                if ((pow & 1) == 1)
                    ret *= x;
                x *= x;
                pow >>= 1;
            }
            return ret;
        }
    }


    public static class IListExtensions
    {
        /// <summary>
        /// Shuffles the element order of the specified list.
        /// </summary>
        public static void Shuffle<T>(this IList<T> ts)
        {
            var count = ts.Count;
            var last = count - 1;
            Random rand = new Random();
            for (var i = 0; i < last; ++i)
            {
                var r = rand.Next(i, count);
                var tmp = ts[i];
                ts[i] = ts[r];
                ts[r] = tmp;
            }
        }
    }
}
