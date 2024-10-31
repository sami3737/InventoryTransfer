using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Database;
using Connection = Oxide.Core.Database.Connection;

namespace Oxide.Plugins
{
    [Info("InventoryTransfer", "sami37", "1.0.0")]
    [Description("Save and transfer your stuff accross different server using MSYQL")]
    public class InventoryTransfer : RustPlugin
    {
        private readonly Core.MySql.Libraries.MySql _mySql = Interface.GetMod().GetLibrary<Core.MySql.Libraries.MySql>();
        private Connection _mySqlConnection;
        private string db_name = "rust";

        private string ipadress = "127.0.0.1";
        private string password = "";
        private int port = 3306;
        private string user = "rust";

        #region Config
        Configuration config;

        class Configuration
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings Info = new Settings();

            public class Settings
            {

                [JsonProperty(PropertyName = "IP Address")]
                public string DbAddress = "127.0.0.1";

                [JsonProperty(PropertyName = "Port")]
                public int Port = 3306;

                [JsonProperty(PropertyName = "UserName")]
                public string Username = "root";

                [JsonProperty(PropertyName = "Password")]
                public string Password = "";

                [JsonProperty(PropertyName = "Database Name")]
                public string DbName = "";

                [JsonProperty(PropertyName = "Table Name")]
                public string Table = "";
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Data
        private void Init()
        {
            try
            {
                _mySqlConnection = _mySql.OpenDb(config.Info.DbAddress, config.Info.Port, config.Info.DbName,
                    config.Info.Username, config.Info.Password, this);
                var sqli = Sql.Builder.Append(
                    "SELECT COUNT(*) as count FROM information_schema.tables WHERE table_schema = '" +
                    config.Info.DbName +
                    "' AND table_name = '" + config.Info.Table + "';");
                _mySql.Query(sqli, _mySqlConnection, listed =>
                {
                    if (listed == null || listed.FirstOrDefault()?["count"].ToString() == "0")
                    {
                        _mySql.ExecuteNonQuery(
                            Sql.Builder.Append(
                                "CREATE TABLE `" + config.Info.DbName + "`.`" + config.Info.Table +
                                "` ( `Indexed` BIGINT NOT NULL AUTO_INCREMENT , `steamid` BIGINT(255) NOT NULL , `DATA` VARCHAR(250) NOT NULL, PRIMARY KEY (`Indexed`), UNIQUE KEY Usteamid (steamid)) ENGINE = InnoDB;"),
                            _mySqlConnection);
                    }
                });
            }
            catch (Exception e)
            {
                PrintWarning("Configure your mysql data from config file.");
            }
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Saved"] = "Your gear has been stored correctly.",
                ["NoData"] = "You have no data saved.",
                ["GearGave"] = "You received your saved gear.",
            }, this);
        }
        #endregion

        #region Commands

        public class stuff
        {
            public float condition;
            public int id;
            public int amount;
            public ulong skinid;
            public int slot;
            public Dictionary<string, object> magazine;
            public List<stuff> mods;

            public stuff(float cond, int ID, int Amount, ulong skin, int SLOT = -1, Dictionary<string, object> mag = null, List<stuff> mod = null)
            {
                condition = cond;
                id = ID;
                amount = Amount;
                skinid = skin;
                if(slot >= 0)
                    slot = SLOT;
                if(mag != null)
                    magazine = mag;
                if(mod != null)
                    mods = mod;
            }
        }

        [ChatCommand("savegear")]
        void ChatCmdSave(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            Dictionary<string, List<stuff>> itemdata = new Dictionary<string, List<stuff>>
            {
                {"Wear", new List<stuff>()}, {"Belt", new List<stuff>()}, {"Main", new List<stuff>()}
            };
            foreach (var item in player.inventory.containerBelt.itemList)
            {
                bool mag = false;
                List<stuff> mod = new List<stuff>();
                var heldEnt = item.GetHeldEntity();
                if (heldEnt != null)
                {
                    var projectiles = heldEnt.GetComponent<BaseProjectile>();
                    if (projectiles != null)
                    {
                        var magazine = projectiles.primaryMagazine;
                        if (magazine != null) mag = true;
                    }
                }

                if (item.contents?.itemList != null)
                    mod.AddRange(item.contents.itemList.Select(item2 =>
                        new stuff(item2.condition, item2.info.itemid, item2.amount, item2.skin)));

                itemdata["Belt"].Add(new stuff(item.condition, item.info.itemid, item.amount, item.skin,
                    item.position, mag ?
                    new Dictionary<string, object>
                    {
                        {
                            item.GetHeldEntity().GetComponent<BaseProjectile>().primaryMagazine.ammoType.itemid
                                .ToString(),
                            item.GetHeldEntity().GetComponent<BaseProjectile>().primaryMagazine.contents
                        }
                    } : null,
                    mod.Count > 0 ? mod : null));
            }

            foreach (var item in player.inventory.containerMain.itemList)
            {
                bool mag = false;
                List<stuff> mod = new List<stuff>();
                var heldEnt = item.GetHeldEntity();
                if (heldEnt != null)
                {
                    var projectiles = heldEnt.GetComponent<BaseProjectile>();
                    if (projectiles != null)
                    {
                        var magazine = projectiles.primaryMagazine;
                        if (magazine != null) mag = true;
                    }
                }

                if (item.contents?.itemList != null)
                    mod.AddRange(item.contents.itemList.Select(item2 =>
                        new stuff(item2.condition, item2.info.itemid, item2.amount, item2.skin)));

                itemdata["Main"].Add(new stuff(item.condition, item.info.itemid, item.amount, item.skin,
                    item.position, mag ?
                    new Dictionary<string, object>
                    {
                        {
                            item.GetHeldEntity().GetComponent<BaseProjectile>().primaryMagazine.ammoType.itemid
                                .ToString(),
                            item.GetHeldEntity().GetComponent<BaseProjectile>().primaryMagazine.contents
                        }
                    } : null,
                    mod.Count > 0 ? mod : null));
            }
            foreach (var item in player.inventory.containerBelt.itemList)
            {
                bool mag = false;
                List<stuff> mod = new List<stuff>();
                var heldEnt = item.GetHeldEntity();
                if (heldEnt != null)
                {
                    var projectiles = heldEnt.GetComponent<BaseProjectile>();
                    if (projectiles != null)
                    {
                        var magazine = projectiles.primaryMagazine;
                        if (magazine != null) mag = true;
                    }
                }

                if (item.contents?.itemList != null)
                    mod.AddRange(item.contents.itemList.Select(item2 =>
                        new stuff(item2.condition, item2.info.itemid, item2.amount, item2.skin)));

                itemdata["Wear"].Add(new stuff(item.condition, item.info.itemid, item.amount, item.skin,
                    item.position, mag ?
                    new Dictionary<string, object>
                    {
                        {
                            item.GetHeldEntity().GetComponent<BaseProjectile>().primaryMagazine.ammoType.itemid
                                .ToString(),
                            item.GetHeldEntity().GetComponent<BaseProjectile>().primaryMagazine.contents
                        }
                    } : null,
                    mod.Count > 0 ? mod : null));
            }

            if (itemdata.Count > 0)
            {
                string json = JsonConvert.SerializeObject(itemdata, Formatting.Indented);
                _mySqlConnection = _mySql.OpenDb(config.Info.DbAddress, config.Info.Port, config.Info.DbName, config.Info.Username, config.Info.Password, this);
                _mySql.Insert(
                    Sql.Builder.Append(
                        "INSERT INTO `" + config.Info.Table + "` (`Indexed`, `steamid`, `data`) VALUES (NULL, '" + player.UserIDString + "', '" + json + "') ON DUPLICATE KEY UPDATE data = '" + json + "'"),
                    _mySqlConnection);
            }
            if (_mySqlConnection != null)
                _mySql.CloseDb(_mySqlConnection);
        }

        [ChatCommand("getgear")]
        void ChatCmdGet(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;
            _mySqlConnection = _mySql.OpenDb(config.Info.DbAddress, config.Info.Port, config.Info.DbName, config.Info.Username, config.Info.Password, this);
            var sqli = Sql.Builder.Append("SELECT DATA FROM " + config.Info.Table + " WHERE steamid = '" + player.UserIDString + "';");
            _mySql.Query(sqli, _mySqlConnection, listed =>
            {
                if (listed == null || listed.Count == 0)
                {
                    _mySql.CloseDb(_mySqlConnection);
                    return;
                }

                string dated = null;
                foreach (var dataTable in listed)
                {
                    var listarray = dataTable.Values.ToArray();
                    var currentvalue = listarray[0];
                    dated = currentvalue.ToString();
                }

                if (string.IsNullOrEmpty(dated))
                {
                    SendReply(player, lang.GetMessage("NoData", this, player.UserIDString));
                        return;
                }

                try
                {
                    Dictionary<string, List<stuff>> ItemList = JsonConvert.DeserializeObject<Dictionary<string, List<stuff>>>(dated);
                    foreach (var ItemDetails in ItemList)
                    {
                        ItemContainer container = null;
                        switch (ItemDetails.Key)
                        {
                            case "Wear":
                                container = player.inventory.containerWear;
                                break;
                            case "Main":
                                container = player.inventory.containerMain;
                                break;
                            case "Belt":
                                container = player.inventory.containerBelt;
                                break;
                        }

                        foreach (var Items in ItemDetails.Value)
                        {
                            var item = ItemManager.CreateByItemID(Items.id, Items.amount, Items.skinid);
                            if (item != null)
                            {
                                if (item.info.category.ToString() == "Weapon")
                                {
                                    var list = new List<int>();
                                    if (Items.mods != null)
                                        foreach (var variable in Items.mods)
                                        {
                                            if (!list.Contains(variable.id))
                                                list.Add(variable.id);
                                        }

                                    GiveItem(player, BuildWeapon(Items.id, Items.skinid, list), container);
                                    continue;
                                }

                                GiveItem(player,
                                    BuildItem(Items.id, Items.amount, Items.skinid, 0),
                                    container);
                            }
                        }
                    }
                    SendReply(player, lang.GetMessage("GearGave", this, player.UserIDString));
                    var delete = Sql.Builder.Append("DELETE FROM " + config.Info.Table + " WHERE steamid = '" + player.UserIDString + "';");
                    _mySql.Delete(delete, _mySqlConnection);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                
            });
            if(_mySqlConnection != null)
                _mySql.CloseDb(_mySqlConnection);
        }

        void GiveItem(BasePlayer inv, Item item, ItemContainer container = null)
        {
            if (item == null)
            {
                return;
            }
            int position = -1;
            if (!item.MoveToContainer(container, position))
                item.Drop(inv.transform.position, inv.transform.forward);
        }

        private Item BuildItem(int itemid, int amount, ulong skin, int blueprintTarget)
        {
            if (amount < 1) amount = 1;
            Item item = CreateByItemID(itemid, amount, skin);
            if (blueprintTarget != 0)
                item.blueprintTarget = blueprintTarget;
            return item;
        }

        private Item BuildWeapon(int id, ulong skin, List<int> mods)
        {
            Item item = CreateByItemID(id, 1, skin);
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                ((BaseProjectile) item.GetHeldEntity()).primaryMagazine.contents = ((BaseProjectile) item.GetHeldEntity()).primaryMagazine.capacity;
            }
            if (mods != null)
            {
                foreach (var mod in mods)
                {
                    item.contents.AddItem(BuildItem(mod, 1, 0, 0).info, 1);
                }
            }
            return item;
        }

        private Item CreateByItemID(int itemId, int amount = 1, ulong skin = 0)
        {
            return ItemManager.CreateByItemID(itemId, amount, skin);
        }

        #endregion
    }
}