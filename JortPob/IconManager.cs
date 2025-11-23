using DirectXTexNet;
using JortPob.Common;
using SharpAssimp;
using SoulsFormats;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using TeximpNet.DDS;
using static IronPython.Modules._ast;

namespace JortPob
{
    public class IconManager
    {
        public readonly List<IconInfo> icons;

        public ushort nextIconId = 19000; // all ids after this are free to use

        public IconManager(ESM esm)
        {
            if (Const.DEBUG_SKIP_ICONS) { return; }

            Lort.Log($"Loading icons...", Lort.Type.Main);
            Lort.NewTask("Loading Icons", 1);

            icons = new();
            void FindIcons(ESM.Type recordType)
            {
                foreach(JsonNode json in esm.GetAllRecordsByType(recordType))
                {
                    if (json["icon"] != null && json["icon"].GetValue<string>().Trim() != "")
                    {
                        string recordId = json["id"].GetValue<string>().ToLower();
                        string iconPath = json["icon"].GetValue<string>().ToLower();

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
            if (Const.DEBUG_SKIP_ICONS) { return new IconInfo("Default", 0); }

            foreach (IconInfo icon in icons)
            {
                if (icon.referers.Contains(record.ToLower()))
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
                if (icon.path == path.Replace(".tga", ".dds")) // guh
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
            if (Const.DEBUG_SKIP_ICONS) { return; }

            /* Do regular icons for use in the inventory */
            const int WIDTH = 4096;
            const int HEIGHT = 2048;
            const int ROWS = 12;
            const int COLS = 24;
            const int PAD = 4;
            const int ICON = 160;  // both width and height of individual icons
            const int FIRST_SHEET = 9;

            Lort.Log($"Generating sheets for {icons.Count()} icons...", Lort.Type.Main);
            Lort.NewTask("Stitching Icons", icons.Count());

            /* Load all icons into system.drawing.image objects */
            List<(IconInfo iconInfo, Bitmap bitmap)> bitmaps = new();
            foreach (IconInfo icon in icons)
            {
                byte[] ddsBytes = System.IO.File.ReadAllBytes($"{Const.MORROWIND_PATH}Data Files\\icons\\{icon.path}");
                Bitmap bitmap = Common.DDS.DDStoBitmap(ddsBytes);
                bitmap = Common.Utility.XbrzUpscale(bitmap, 5);
                bitmaps.Add((icon, bitmap));
            }

            /* Make sheets */
            List<(IconLayout layout, Bitmap bitmap)> sheets = new();
            int i = 0; // icon index
            for (int sheetIndex = 0; i < bitmaps.Count(); sheetIndex++)
            {
                Bitmap sheet = new Bitmap(WIDTH, HEIGHT);
                IconLayout layout = new($"SB_Icon_{(FIRST_SHEET + sheetIndex):D2}");

                using (Graphics g = Graphics.FromImage(sheet))
                {
                    g.Clear(Color.Empty);

                    for (int y = 0; y < ROWS; y++)
                    {
                        for (int x = 0; x < COLS; x++)
                        {
                            if (i >= bitmaps.Count()) { break; } // Out of icons!

                            IconInfo icon = bitmaps[i].iconInfo;
                            Bitmap bitmap = bitmaps[i].bitmap;
                            g.DrawImage(bitmap, x * (ICON + PAD), y * (ICON + PAD));

                            layout.entries.Add(new(icon.id, new Int2(x * (ICON + PAD), y * (ICON + PAD)), new Int2(ICON, ICON)));

                            i++;
                            Lort.TaskIterate();
                        }
                    }
                }

                sheet.Save(@$"I:\SteamLibrary\steamapps\common\ELDEN RING\sheet_test{sheetIndex}.bmp"); // @TODO: DEBUG DELETE ME!
                sheets.Add((layout, sheet));
            }

            /* Write sheets and layouts to bnds and tpfs */
            const string hiLayoutPath = @"menu\hi\01_common.sblytbnd.dcx";
            const string hiTpfPath = @"menu\hi\01_common.tpf.dcx";
            const string lowLayoutPath = @"menu\low\01_common.sblytbnd.dcx";
            const string lowTpfPath = @"menu\low\01_common.tpf.dcx";

            void AddSheets(string layoutPath, string tpfPath, string hilow)
            {
                TPF tpf = TPF.Read($"{Const.ELDEN_PATH}Game\\{tpfPath}");
                BND4 bnd = BND4.Read($"{Const.ELDEN_PATH}Game\\{layoutPath}");

                foreach ((IconLayout layout, Bitmap bitmap) tuple in sheets)
                {
                    /* Add sheet to TPF */
                    TPF.Texture texture = new();
                    Bitmap linearBitmap = Common.Utility.LinearToSRGB(tuple.bitmap);
                    texture.Bytes = Common.DDS.BitmapToDDS(linearBitmap, DXGI_FORMAT.BC2_UNORM);
                    linearBitmap.Dispose();
                    texture.Format = (byte)Common.DDS.GetTpfFormatFromDdsBytes(texture.Bytes);
                    texture.Name = tuple.layout.name;
                    tpf.Textures.Add(texture);

                    /* Add layout xml to BND */
                    BinderFile bf = new();
                    bf.Bytes = tuple.layout.Write();
                    bf.ID = bnd.Files.Count();
                    bf.Name = $"N:\\GR\\data\\Menu\\ScaleForm\\SBLayout\\01_Common\\{hilow}\\{tuple.layout.name}.layout";
                    bnd.Files.Add(bf);
                }

                tpf.Write($"{Const.OUTPUT_PATH}{tpfPath}");
                bnd.Write($"{Const.OUTPUT_PATH}{layoutPath}");
            }

            Lort.Log($"Binding {sheets.Count()} sheets...", Lort.Type.Main);
            Lort.NewTask("Binding Sheets", 2);
            AddSheets(hiLayoutPath, hiTpfPath, "Hi"); Lort.TaskIterate();
            AddSheets(lowLayoutPath, lowTpfPath, "Low"); Lort.TaskIterate();

            /* Clean up */
            foreach ((IconLayout layout, Bitmap bitmap) tuple in sheets) { tuple.bitmap.Dispose(); }
            foreach ((IconInfo iconInfo, Bitmap bitmap) tuple in bitmaps) { tuple.bitmap.Dispose(); }

            /* Do large icons for use in the description area */
            const string hiPath = @"menu\hi\00_solo";
            const string lowPath = @"menu\low\00_solo";

            Lort.Log($"Generating {icons.Count()} previews...", Lort.Type.Main);
            Lort.NewTask("Generating Previews", icons.Count());

            void AddIcons(string path)
            {
                /* Load BXF4 from elden ring directory (requires game unpacked!) */
                BXF4 bxf = BXF4.Read($"{Const.ELDEN_PATH}Game\\{path}.tpfbhd", $"{Const.ELDEN_PATH}Game\\{path}.tpfbdt");
                List<BinderFile> filesToInsert = new();

                /* Generate binder files to add from dds texture files */
                foreach(IconInfo icon in icons)
                {
                    byte[] data = System.IO.File.ReadAllBytes($"{Const.MORROWIND_PATH}Data Files\\icons\\{icon.path}");

                    Bitmap bitmap = Common.DDS.DDStoBitmap(data);
                    Bitmap scaledBitmap = Common.Utility.XbrzUpscale(bitmap, 6); // 32x32x -> 192x192
                    Bitmap linearScaledBitmap = Common.Utility.LinearToSRGB(scaledBitmap);
                    byte[] scaledDDS = Common.DDS.BitmapToDDS(linearScaledBitmap, DXGI_FORMAT.BC2_UNORM);
                    scaledBitmap.Dispose();
                    linearScaledBitmap.Dispose();

                    int format = JortPob.Common.DDS.GetTpfFormatFromDdsBytes(scaledDDS);

                    TPF tpf = new TPF();
                    tpf.Encoding = 1;
                    tpf.Flag2 = 3;
                    tpf.Platform = TPF.TPFPlatform.PC;
                    tpf.Compression = DCX.Type.DCX_KRAK;

                    TPF.Texture tex = new(icon.TextureName(), (byte)format, 0, scaledDDS, TPF.TPFPlatform.PC);
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

            Lort.Log($"Binding {icons.Count()} previews...", Lort.Type.Main);
            Lort.NewTask("Binding Previews", 2);
            AddIcons(hiPath); Lort.TaskIterate();
            AddIcons(lowPath); Lort.TaskIterate();

            Lort.TaskIterate();
        }

        public class IconInfo
        {
            public readonly string path;
            public readonly ushort id;

            public readonly List<string> referers; // list of item record ids that use this icon

            public IconInfo(string path, ushort id)
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

        public class IconLayout
        {
            public readonly string name;
            public readonly List<IconLayoutEntry> entries;

            public IconLayout(string name)
            {
                this.name = name;
                entries = new();
            }

            public byte[] Write()
            {
                string output = $"<TextureAtlas imagePath=\"{name}.png\">\r\n";
                foreach(IconLayoutEntry entry in entries)
                {
                    output += entry.Write();
                }
                output += $"</TextureAtlas>";

                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    using (TextWriter tw = new StreamWriter(ms))
                    {
                        tw.Write(output);
                        tw.Flush();
                        ms.Position = 0;
                        bytes = ms.ToArray();
                    }

                }

                return bytes;
            }

            public class IconLayoutEntry
            {
                public readonly ushort id;
                public Int2 position, size;

                public IconLayoutEntry(ushort id, Int2 position, Int2 size)
                {
                    this.id = id;
                    this.position = position;
                    this.size = size;
                }

                public string Write()
                {
                    return $"\t<SubTexture name=\"MENU_ItemIcon_{id:D5}.png\" x=\"{position.x}\" y=\"{position.y}\" width=\"{size.x}\" height=\"{size.y}\" half=\"0\"/>\r\n";
                }
            }
        }
    }
}
