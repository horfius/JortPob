using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WitchyFormats;

namespace JortPob
{
    public class ItemManager
    {
        public enum Type {
            None = -1,
            Goods = 3,
            Weapon = 0,
            Armor = 1,
            Accessory = 2,
            Enchant = 100    // enchant is an ash of war itemy thing
        }
        public readonly List<ItemInfo> items; // string is record if of item from MW. int is the row id of the item in Elden Ring, type is type of item in ER

        private Paramanager paramanager;
        private TextManager textManager;

        private int nextWeaponId, nextArmorId, nextAccessoryId, nextGoodsId;

        public ItemManager(ESM esm, Paramanager paramanager, TextManager textManager)
        {
            this.paramanager = paramanager;
            this.textManager = textManager;

            items = new();

            nextWeaponId = 70000000;
            nextArmorId = 6000000;
            nextGoodsId = 30000;
            nextAccessoryId = 8500;

            /* Search all scripts for references to items. Items used by scripts are critical and must be created */
            List<Papyrus.Call> itemCalls = new();
            List<string> scriptItems = new();  // list of record ids for items used in scripts
            foreach (Papyrus papyrus in esm.scripts)
            {
                itemCalls.AddRange(papyrus.GetCalls(Papyrus.Call.Type.AddItem));
                itemCalls.AddRange(papyrus.GetCalls(Papyrus.Call.Type.RemoveItem));
                itemCalls.AddRange(papyrus.GetCalls(Papyrus.Call.Type.GetItemCount));
            }

            /* Search all dialog papyrus calls and filters as well */
            foreach(Dialog.DialogRecord dialog in esm.dialog)
            {
                foreach (Dialog.DialogInfoRecord info in dialog.infos)
                {
                    if(info.script != null)
                    {
                        foreach (Papyrus.Call call in info.script.calls)
                        {
                            switch(call.type)
                            {
                                case Papyrus.Call.Type.RemoveItem:
                                case Papyrus.Call.Type.AddItem:
                                    itemCalls.Add(call);
                                    break;
                            }
                        }
                    }

                    foreach(Dialog.DialogFilter filter in info.filters)
                    {
                        if (filter.type == Dialog.DialogFilter.Type.Item)
                        {
                            if (!scriptItems.Contains(filter.id.ToLower())) { scriptItems.Add(filter.id.ToLower()); }
                        }
                    }
                }
            }

            /* Grab item record ids from calls */
            foreach(Papyrus.Call itemCall in itemCalls)
            {
                switch(itemCall.type)
                {
                    case Papyrus.Call.Type.GetItemCount:
                    case Papyrus.Call.Type.RemoveItem:
                    case Papyrus.Call.Type.AddItem:
                        if (!scriptItems.Contains(itemCall.parameters[0].ToLower())) { scriptItems.Add(itemCall.parameters[0].ToLower()); }
                        break;
                }
            }

            /* Generate params for items */
            /* If the item has an override mapping we use that */
            /* If the item is used in a script we generate a placeholder item of whatever type seems relevant */
            /* If an item is not mapped and not used in a script we discard it */

            /* Misc Items */ // This category contains keys, unique items like the bittercup, souls gems, and random clutter such as bottles and plates
            /* Weapons */
            /* Armor */
            /* Clothing */   // Contains both clothes AND jewelrey 
            /* Ingredient */
            /* Alchemy */    // Potions
            /* Light */      // Torches, lanterns, candles... We are actually already processing these as statics in the world space. Hopefully we will not need to change that.
            /* Apparatus */  // Alchemy tools. Probably won't use these at all in this mod
            /* Book */
            /* Lockpick */   // These last three are tools as well
            /* Probe */
            /* Repair Item */
            void HandleItemsByRecord(ESM.Type recordType)
            {
                foreach (JsonNode json in esm.GetAllRecordsByType(recordType))
                {
                    string id = json["id"].GetValue<string>().ToLower();
                    int value = json["data"]["value"].GetValue<int>();

                    bool scriptItem = scriptItems.Contains(id);
                    Override.ItemRemap remap = Override.GetItemRemap(id);

                    if (remap != null)
                    {
                        ItemInfo it = new(id, remap.type, remap.row, value, scriptItem);
                        items.Add(it);
                    }
                    else if (scriptItem)
                    {
                        if (json["has_script_reference"] == null) { json["has_script_reference"] = scriptItem; } // add this data to the json so i dont have to pass this var through parameters
                        GenerateItem(recordType, json);
                    }
                }
            }

            HandleItemsByRecord(ESM.Type.Weapon);
            HandleItemsByRecord(ESM.Type.Armor);
            HandleItemsByRecord(ESM.Type.Clothing);
            HandleItemsByRecord(ESM.Type.Ingredient);
            HandleItemsByRecord(ESM.Type.Alchemy);
            HandleItemsByRecord(ESM.Type.Book);
            HandleItemsByRecord(ESM.Type.MiscItem);
            HandleItemsByRecord(ESM.Type.Apparatus);
            HandleItemsByRecord(ESM.Type.Probe);
            HandleItemsByRecord(ESM.Type.Lockpick);
            HandleItemsByRecord(ESM.Type.RepairItem);

            }

        /* Generate an item from the morrowind record data. This is a "Best guess" situation. */
        /* For some item types this is fine. Books and keys for example can be generated like this without any issue. */
        /* But for things like weapons or armor we should make sure those get defined by hand eventually. */
        private void GenerateItem(ESM.Type recordType, JsonNode json)
        {
            switch(recordType)
            {
                case ESM.Type.Weapon:
                    string weaponType = json["data"]["weapon_type"].GetValue<string>().ToLower();
                    switch(weaponType)
                    {
                        case "marksmanthrown":
                            GenerateItemThrown(json);
                            break;
                        default:
                            GenerateItemWeapon(json);
                            break;
                    }
                    break;
                case ESM.Type.Armor:
                    string armorType = json["data"]["armor_type"].GetValue<string>().ToLower();
                    switch(armorType)
                    {
                        case "shield":
                            GenerateItemShield(json);
                            break;
                        default:
                            GenerateItemArmor(json);
                            break;
                    }

                    break;
                case ESM.Type.Clothing:
                    string clothingType = json["data"]["clothing_type"].GetValue<string>().ToLower();
                    switch(clothingType)
                    {
                        case "amulet":
                        case "ring":
                        case "belt":
                            GenerateItemAccessory(json);
                            break;
                        default:
                            GenerateItemClothing(json);
                            break;
                    }
                    break;
                case ESM.Type.Ingredient:
                    GenerateItemIngredient(json);
                    break;
                case ESM.Type.Alchemy:
                    GenerateItemPotion(json);
                    break;
                case ESM.Type.Book:
                    GenerateItemBook(json);
                    break;
                case ESM.Type.Apparatus:
                case ESM.Type.Probe:
                case ESM.Type.Lockpick:
                case ESM.Type.RepairItem:
                    GenerateItemPlaceholder(json);
                    break;
                case ESM.Type.MiscItem:
                    string id = json["id"].GetValue<string>().ToLower();
                    if (id.Contains("key_")) { GenerateItemKey(json); }
                    else { GenerateItemPlaceholder(json); }
                    break;
            }
        }

        /* In most cases I want to have paramamanger do param edits itself, but in this case we will let ItemManager do them to prevent excessive clutter in paramanager */
        private void GenerateItemWeapon(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            string weaponType = json["data"]["weapon_type"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            int rowToCopy;
            switch (weaponType)
            {
                case "arrow":
                    rowToCopy = 50000000; // standard arrow
                    break;
                case "bolt":
                    rowToCopy = 52000000; // standard bolt
                    break;
                case "marksmanbow":
                    rowToCopy = 40000000; // shortbow
                    break;
                case "marksmancrossbow":
                    rowToCopy = 43020000; // light crossbow
                    break;
                case "axeonehand":
                    rowToCopy = 14020000; // hand axe
                    break;
                case "axetwoclose":
                    rowToCopy = 15000000; // greataxe
                    break;
                case "bluntonehand":
                    rowToCopy = 11000000; // mace
                    break;
                case "blunttwowide":
                case "blunttwoclose":
                    rowToCopy = 12060000; // great mace
                    break;
                case "longbladeonehand":
                    rowToCopy = 2010000; // short sword
                    break;
                case "longbladetwoclose":
                    rowToCopy = 3180000; // claymore
                    break;
                case "shortbladeonehand":
                    rowToCopy = 1000000; // dagger
                    break;
                case "speartwowide":
                default:
                    rowToCopy = 16000000; // short spear
                    break;
            }

            FsParam weaponParam = paramanager.param[Paramanager.ParamType.EquipParamWeapon];
            FsParam.Row row = paramanager.CloneRow(weaponParam[rowToCopy], id, nextWeaponId);

            textManager.AddWeapon(row.ID, json["name"].GetValue<string>(), "Descriptions describe things!");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(weaponParam, row);
            items.Add(new(id, Type.Weapon, row.ID, value, hasScriptReference));
            nextWeaponId += 10000;
        }

        private void GenerateItemShield(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            FsParam weaponParam = paramanager.param[Paramanager.ParamType.EquipParamWeapon];
            FsParam.Row row = paramanager.CloneRow(weaponParam[31330000], id, nextWeaponId); // 31330000 is a basic heater shield

            textManager.AddWeapon(row.ID, json["name"].GetValue<string>(), "Descriptions describe things!");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(weaponParam, row);
            items.Add(new(id, Type.Weapon, row.ID, value, hasScriptReference));
            nextWeaponId += 10000;
        }

        private void GenerateItemArmor(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            string armorType = json["data"]["armor_type"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            int rowToCopy;
            switch(armorType)
            {
                case "helmet":
                    rowToCopy = 1100000; // chain coif
                    break;
                case "rightbracer":
                case "leftbracer":
                case "rightgauntlet":
                case "leftgauntlet":
                    rowToCopy = 1100200; // chain gloves
                    break;
                case "greaves":
                case "boots":
                    rowToCopy = 1100300; // chain legs
                    break;
                case "rightpauldron":
                case "leftpauldron":
                case "cuirass":
                default:
                    rowToCopy = 1100100; // chain armor
                    break;
            }

            FsParam armorParam = paramanager.param[Paramanager.ParamType.EquipParamProtector];
            FsParam.Row row = paramanager.CloneRow(armorParam[rowToCopy], id, nextArmorId);

            textManager.AddArmor(row.ID, json["name"].GetValue<string>(), "Information informs you.", "Descriptions describe things!");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(armorParam, row);
            items.Add(new(id, Type.Armor, row.ID, value, hasScriptReference));
            nextArmorId += 10000;
        }

        private void GenerateItemClothing(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            string clothingType = json["data"]["clothing_type"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            int rowToCopy;
            switch (clothingType)
            {
                case "robe":
                    rowToCopy = 630100; // astrologers robe
                    break;
                case "leftglove":
                case "rightglove":
                    rowToCopy = 630200; // astrologers gloves
                    break;
                case "skirt":
                case "pants":
                case "shoes":
                    rowToCopy = 630300; // astrologers pants
                    break;
                case "shirt":
                default:
                    rowToCopy = 631100; // astrologers robe altered
                    break;
            }

            FsParam armorParam = paramanager.param[Paramanager.ParamType.EquipParamProtector];
            FsParam.Row row = paramanager.CloneRow(armorParam[rowToCopy], id, nextArmorId);

            textManager.AddArmor(row.ID, json["name"].GetValue<string>(), "Information informs you.", "Descriptions describe things!");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(armorParam, row);
            items.Add(new(id, Type.Armor, row.ID, value, hasScriptReference));
            nextArmorId += 10000;
        }

        private void GenerateItemAccessory(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            string clothingType = json["data"]["clothing_type"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            int rowToCopy;
            switch (clothingType)
            {
                case "belt":
                    rowToCopy = 4000; // dragoncrest shield talisman
                    break;
                case "amulet":
                    rowToCopy = 1040; // erdtree favor
                    break;
                case "ring":
                default:
                    rowToCopy = 1010; // cerulean amber medaliion
                    break;
            }

            FsParam accessoryParam = paramanager.param[Paramanager.ParamType.EquipParamAccessory];
            FsParam.Row row = paramanager.CloneRow(accessoryParam[rowToCopy], id, nextAccessoryId);

            textManager.AddAccessory(row.ID, json["name"].GetValue<string>(), "Information informs you.", "Descriptions describe things!");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(accessoryParam, row);
            items.Add(new(id, Type.Accessory, row.ID, value, hasScriptReference));
            nextAccessoryId += 10;
        }

        private void GenerateItemThrown(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            FsParam goodsParam = paramanager.param[Paramanager.ParamType.EquipParamGoods];
            FsParam.Row row = paramanager.CloneRow(goodsParam[1700], id, nextGoodsId); // 1700 is a throwing knife

            textManager.AddGoods(row.ID, json["name"].GetValue<string>(), "Information informs you.", "Descriptions describe things!", "More information.");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(goodsParam, row);
            items.Add(new(id, Type.Goods, row.ID, value, hasScriptReference));
            nextGoodsId += 10;
        }

        private void GenerateItemPotion(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            FsParam goodsParam = paramanager.param[Paramanager.ParamType.EquipParamGoods];
            FsParam.Row row = paramanager.CloneRow(goodsParam[2000900], id, nextGoodsId); // 2000900 is a divine blesing

            textManager.AddGoods(row.ID, json["name"].GetValue<string>(), "Information informs you.", "Descriptions describe things!", "More information.");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(goodsParam, row);
            items.Add(new(id, Type.Goods, row.ID, value, hasScriptReference));
            nextGoodsId += 10;
        }

        private void GenerateItemIngredient(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            FsParam goodsParam = paramanager.param[Paramanager.ParamType.EquipParamGoods];
            FsParam.Row row = paramanager.CloneRow(goodsParam[20690], id, nextGoodsId); // 20690 is a herba

            textManager.AddGoods(row.ID, json["name"].GetValue<string>(), "Information informs you.", "Descriptions describe things!", "More information.");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(goodsParam, row);
            items.Add(new(id, Type.Goods, row.ID, value, hasScriptReference));
            nextGoodsId += 10;
        }

        public void GenerateItemBook(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            FsParam goodsParam = paramanager.param[Paramanager.ParamType.EquipParamGoods];
            FsParam.Row row = paramanager.CloneRow(goodsParam[8859], id, nextGoodsId); // 8859 is the assasins prayerbook

            textManager.AddGoods(row.ID, json["name"].GetValue<string>(), "Information informs you.", "Descriptions describe things!", "More information.");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(goodsParam, row);
            items.Add(new(id, Type.Goods, row.ID, value, hasScriptReference));
            nextGoodsId += 10;
        }

        private void GenerateItemPlaceholder(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            FsParam goodsParam = paramanager.param[Paramanager.ParamType.EquipParamGoods];
            FsParam.Row row = paramanager.CloneRow(goodsParam[2120], id, nextGoodsId); // 2120 is soap

            textManager.AddGoods(row.ID, json["name"].GetValue<string>(), "Information informs you.", "Descriptions describe things!", "More information.");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(goodsParam, row);
            items.Add(new(id, Type.Goods, row.ID, value, hasScriptReference));
            nextGoodsId += 10;
        }

        private void GenerateItemKey(JsonNode json)
        {
            string id = json["id"].GetValue<string>().ToLower();
            bool hasScriptReference = json["has_script_reference"] != null ? json["has_script_reference"].GetValue<bool>() : false;
            int value = json["data"]["value"].GetValue<int>();
            FsParam goodsParam = paramanager.param[Paramanager.ParamType.EquipParamGoods];
            FsParam.Row row = paramanager.CloneRow(goodsParam[8197], id, nextGoodsId); // 8197 is the sewer gaol key

            textManager.AddGoods(row.ID, json["name"].GetValue<string>(), "Information informs you.", "Descriptions describe things!", "More information.");

            row["rarity"].Value.SetValue((byte)0);

            paramanager.AddRow(goodsParam, row);
            items.Add(new(id, Type.Goods, row.ID, value, hasScriptReference));
            nextGoodsId += 10;
        }

        public ItemInfo GetItem(string id)
        {
            foreach(ItemInfo item in items)
            {
                if(item.id.ToLower() == id.ToLower()) { return item; }
            }
            return null;
        }

        public List<ItemInfo> GetInventory(NpcContent npc)
        {
            List<ItemInfo> inv = new();
            foreach ((string id, int quantity) invItem in npc.inventory)
            {
                ItemInfo item = GetItem(invItem.id);
                if (item != null) { inv.Add(item); }
            }
            return inv;
        }

        public List<ItemInfo> GetInventory(ContainerContent container)
        {
            List<ItemInfo> inv = new();
            foreach ((string id, int quantity) invItem in container.inventory)
            {
                ItemInfo item = GetItem(invItem.id);
                if (item != null) { inv.Add(item); }
            }
            return inv;
        }

        /* Stores info on an item */
        [DebuggerDisplay("Item :: {id} :: {type}->{row}")]
        public class ItemInfo
        {
            public readonly Type type;  // param type
            public readonly int row;    // param row
            public readonly string id;  // morrowind record id
            public readonly int value;  // value for shops to use
            public readonly bool quest; // item is referenced in a script. this doesn't 100% mean its a quest item but it does mean it's needed to compile scripts

            public ItemInfo(string id, Type type, int row, int value, bool quest)
            {
                this.id = id;
                this.type = type;
                this.row = row;
                this.value = value;
                this.quest = quest;
            }
        }
    }
}
