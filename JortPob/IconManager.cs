using JortPob.Common;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace JortPob
{
    public class IconManager
    {
        public readonly List<IconInfo> icons;

        public int nextIconId = 19000; // all ids after this are free to use

        public IconManager(ESM esm)
        {
            icons = new();

            void FindIcons(ESM.Type recordType)
            {
                foreach(JsonNode json in esm.GetAllRecordsByType(recordType))
                {
                    if (json["icon"] != null && json["icon"].GetValue<string>().Trim() != "")
                    {
                        string recordId = json["id"]!.GetValue<string>().ToLower();
                        string iconPath = json["icon"]!.GetValue<string>().ToLower();

                        IconInfo iconInfo = GetIconByPath(iconPath);

                        if(iconInfo == null)
                        {
                            iconInfo = new IconInfo(iconPath, nextIconId++);
                            iconInfo.referers.Add(recordId);
                            icons.Add(iconInfo);
                        }
                        else
                        {
                            iconInfo.referers.Add(recordId);
                        }
                    }
                }
            }

            FindIcons(ESM.Type.Weapon);
            FindIcons(ESM.Type.Armor);
            FindIcons(ESM.Type.Clothing);
            FindIcons(ESM.Type.Ingredient);
            FindIcons(ESM.Type.Alchemy);
            FindIcons(ESM.Type.Book);
            FindIcons(ESM.Type.MiscItem);
            FindIcons(ESM.Type.Apparatus);
            FindIcons(ESM.Type.Probe);
            FindIcons(ESM.Type.Lockpick);
            FindIcons(ESM.Type.RepairItem);
        }

        /* This one is special, this is getting an icon based on a record that uses it. Multiple records can use the same icon so its a slow search */
        public IconInfo GetIconByRecord(string record)
        {
            foreach (IconInfo icon in icons)
            {
                if (icon.referers.Contains(record))
                {
                    return icon;
                }
            }
            return null;
        }

        public IconInfo GetIconByPath(string path)
        {
            foreach (IconInfo icon in icons)
            {
                if (icon.path == path)
                {
                    return icon;
                }
            }
            return null;
        }

        public IconInfo GetIconById(int id)
        {
            foreach(IconInfo icon in icons)
            {
                if (icon.id == id)
                {
                    return icon;
                }
            }
            return null;
        }

        public void Write()
        {
            /* Do regular icons for use in the inventory */
            const int ROWS = 12;
            const int COLS = 24;
            const int WIDTH = 2048;
            const int HEIGHT = 1024;
            const int ICON = 92;  // both width and height of individual icons

            List<Image> imgs = new();
            foreach(IconInfo icon in icons)
            {

            }

            /* Do large icons for use in the description area */
            string hiPath = $"menu\\hi\\00_solo";
            string lowPath = $"menu\\low\\00_solo";

            void AddIcons(string path)
            {
                /* Load BXF4 from elden ring directory (requires game unpacked!) */
                BXF4 bxf = BXF4.Read($"{Const.ELDEN_PATH}Game\\{path}.tpfbhd", $"{Const.ELDEN_PATH}Game\\{path}.tpfbdt");
                List<BinderFile> filesToInsert = new();

                /* Generate binder files to add from dds texture files */
                foreach(IconInfo icon in icons)
                {
                    byte[] data = System.IO.File.ReadAllBytes($"{Const.MORROWIND_PATH}Data Files\\icons\\{icon.path}");

                    int format = JortPob.Common.DDS.GetTpfFormatFromDdsBytes(data);

                    TPF tpf = new TPF();
                    tpf.Encoding = 1;
                    tpf.Flag2 = 3;
                    tpf.Platform = TPF.TPFPlatform.PC;
                    tpf.Compression = DCX.Type.DCX_KRAK;

                    TPF.Texture tex = new(icon.TextureName(), (byte)format, 0, data, TPF.TPFPlatform.PC);
                    tpf.Textures.Add(tex);

                    BinderFile bf = new();
                    bf.Name = icon.FileName();
                    bf.Bytes = tpf.Write();
                    bf.ID = bxf.Files.Count();

                    filesToInsert.Add(bf);
                }

                /* Reindex after inserting new files */
                int insertAt = 0;
                for(int i= 0;i<bxf.Files.Count();i++)
                {
                    BinderFile bf = bxf.Files[i];
                    if(bf.Name.StartsWith("00_Solo\\MENU_Knowledge_"))
                    {
                        insertAt = i;
                    }
                }

                bxf.Files.InsertRange(insertAt + 1, filesToInsert); // slam dunk those new filse into the bnd

                for (int i = 0; i < bxf.Files.Count(); i++)
                {
                    BinderFile bf = bxf.Files[i];
                    bf.ID = i;
                }

                /* Write */
                bxf.Write($"{Const.OUTPUT_PATH}{path}.tpfbhd", $"{Const.OUTPUT_PATH}{path}.tpfbdt");
            }

            AddIcons(hiPath);
            AddIcons(lowPath);
        }

        public class IconInfo
        {
            public readonly string path;
            public readonly int id;

            public readonly List<string> referers; // list of item record ids that use this icon

            public IconInfo(string path, int id)
            {
                this.path = path.Replace(".tga", ".dds");  // esm stores texture paths pointing to a TGA but the actual file on disk is DDS
                this.id = id;

                referers = new();
            }

            public string TextureName()
            {
                return $"MENU_Knowledge_{id:D5}";
            }

            public string FileName()
            {
                return $"00_Solo\\MENU_Knowledge_{id:D5}.tpf.dcx";
            }
        }
    }
}
