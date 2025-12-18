using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Crops;
using StardewValley.Buildings;
using HarmonyLib;

namespace DynamicEconomy
{
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private ModData Data;
        private static ModConfig StaticConfig;
        private static ModData StaticData;

        public override void Entry(IModHelper helper)
        {
            this.Config = helper.ReadConfig<ModConfig>();
            StaticConfig = this.Config;

            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.Patch(
                original: AccessTools.Method(typeof(Item), nameof(Item.salePrice)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Item_salePrice_Postfix))
            );
            harmony.Patch(
                original: AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.sellToStorePrice)),
                postfix: new HarmonyMethod(typeof(ModEntry), nameof(ModEntry.Item_salePrice_Postfix))
            );

            helper.Events.GameLoop.GameLaunched += OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += OnSaveLoaded;
            helper.Events.GameLoop.DayStarted += OnDayStarted;
            helper.Events.GameLoop.DayEnding += OnDayEnding;
            helper.Events.Content.AssetRequested += OnAssetRequested;
            helper.Events.Input.ButtonPressed += OnButtonPressed;
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            this.Data = this.Helper.Data.ReadSaveData<ModData>("market-data") ?? new ModData();
            StaticData = this.Data;
        }

        public static void Item_salePrice_Postfix(Item __instance, ref int __result)
        {
            if (StaticConfig == null || StaticData == null || !Context.IsWorldReady) return;
            float multiplier = 1.0f;

            if (StaticConfig.LuxuryCrops.TryGetValue(__instance.ItemId, out bool luxury) && luxury) multiplier -= StaticConfig.LuxuryTaxRate;
            if (StaticConfig.SubsidizedCrops.TryGetValue(__instance.ItemId, out bool sub) && sub) multiplier += StaticConfig.SubsidyBonusRate;
            if (StaticData.ActiveCrashes.ContainsKey(__instance.ItemId)) multiplier -= StaticConfig.PriceDropRate;

            var bin = Game1.getFarm().getShippingBin(Game1.player);
            int binCount = bin?.Where(i => i != null && i.ItemId == __instance.ItemId).Sum(i => i.Stack) ?? 0;
            if (binCount > StaticConfig.SaturationLimit) multiplier -= StaticConfig.PriceDropRate;

            __result = (int)(__result * Math.Max(0, multiplier));
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var configMenu = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu is null) return;

            configMenu.Register(mod: this.ModManifest, reset: () => this.Config = new ModConfig(), save: () => {
                this.Helper.WriteConfig(this.Config);
                StaticConfig = this.Config;
            });

            this.BuildMainMenu(configMenu);
            this.BuildSeasonalPages(configMenu);
        }

        private void BuildMainMenu(IGenericModConfigMenuApi api)
        {
            // 1. WEALTH TAXES
            api.AddSectionTitle(this.ModManifest, () => "Wealth Tax Brackets");
            api.AddBoolOption(this.ModManifest, name: () => "Enable Wealth Tax", tooltip: () => "Toggle all wealth tax collection.", getValue: () => this.Config.EnableWealthTax, setValue: v => this.Config.EnableWealthTax = v);
            api.AddTextOption(this.ModManifest, name: () => "Tax Frequency", tooltip: () => "Daily, Weekly (Sun), or Bi-Weekly (14/28).", getValue: () => this.Config.TaxFrequency, setValue: v => this.Config.TaxFrequency = v, allowedValues: new string[] { "Daily", "Weekly", "Bi-Weekly" });
            
            api.AddNumberOption(this.ModManifest, name: () => "Starter Rate (>5k)", getValue: () => this.Config.TaxStarter, setValue: v => this.Config.TaxStarter = v, min: 0f, max: 0.10f, interval: 0.005f);
            api.AddNumberOption(this.ModManifest, name: () => "Humble Rate (>25k)", getValue: () => this.Config.TaxHumble, setValue: v => this.Config.TaxHumble = v, min: 0f, max: 0.15f, interval: 0.01f);
            api.AddNumberOption(this.ModManifest, name: () => "Comfort Rate (>100k)", getValue: () => this.Config.TaxComfort, setValue: v => this.Config.TaxComfort = v, min: 0f, max: 0.20f, interval: 0.01f);
            api.AddNumberOption(this.ModManifest, name: () => "Wealthy Rate (>1M)", getValue: () => this.Config.TaxWealthy, setValue: v => this.Config.TaxWealthy = v, min: 0f, max: 0.25f, interval: 0.01f);
            api.AddNumberOption(this.ModManifest, name: () => "Tycoon Rate (>10M)", getValue: () => this.Config.TaxTycoon, setValue: v => this.Config.TaxTycoon = v, min: 0f, max: 0.40f, interval: 0.01f);

            // 2. CROP RATES (RESTORED SECTION)
            api.AddSectionTitle(this.ModManifest, () => "Crop Economic Rates");
            api.AddNumberOption(this.ModManifest, name: () => "Luxury Tax %", tooltip: () => "Price penalty for crops toggled as 'Luxury'.", getValue: () => this.Config.LuxuryTaxRate, setValue: v => this.Config.LuxuryTaxRate = v, min: 0f, max: 0.90f, interval: 0.01f);
            api.AddNumberOption(this.ModManifest, name: () => "Subsidy Bonus %", tooltip: () => "Price bonus for crops toggled as 'Subsidized'.", getValue: () => this.Config.SubsidyBonusRate, setValue: v => this.Config.SubsidyBonusRate = v, min: 0f, max: 1.0f, interval: 0.01f);
            api.AddNumberOption(this.ModManifest, name: () => "Crash Penalty %", tooltip: () => "Value lost when an item market crashes.", getValue: () => this.Config.PriceDropRate, setValue: v => this.Config.PriceDropRate = v, min: 0f, max: 0.95f, interval: 0.01f);

            // 3. MARKET & PROPERTY
            api.AddSectionTitle(this.ModManifest, () => "Market & Property Settings");
            api.AddNumberOption(this.ModManifest, name: () => "Crash Duration (Days)", getValue: () => (float)this.Config.CrashDuration, setValue: v => this.Config.CrashDuration = (int)v, min: 1f, max: 28f);
            api.AddNumberOption(this.ModManifest, name: () => "Saturation Limit", getValue: () => (float)this.Config.SaturationLimit, setValue: v => this.Config.SaturationLimit = (int)v, min: 5f, max: 500f);
            api.AddBoolOption(this.ModManifest, name: () => "Enable Property Tax", getValue: () => this.Config.EnablePropertyTax, setValue: v => this.Config.EnablePropertyTax = v);
            api.AddNumberOption(this.ModManifest, name: () => "Gold Per Building", getValue: () => (float)this.Config.BuildingTaxRate, setValue: v => this.Config.BuildingTaxRate = (int)v, min: 0f, max: 5000f);

            // 4. SEASON LINKS
            api.AddSectionTitle(this.ModManifest, () => "Crop Management");
            api.AddPageLink(this.ModManifest, "spring_crops", () => "Spring Crops >");
            api.AddPageLink(this.ModManifest, "summer_crops", () => "Summer Crops >");
            api.AddPageLink(this.ModManifest, "fall_crops", () => "Fall Crops >");
            api.AddPageLink(this.ModManifest, "winter_crops", () => "Winter Crops >");
        }

        private void BuildSeasonalPages(IGenericModConfigMenuApi api)
        {
            string[] seasons = { "Spring", "Summer", "Fall", "Winter" };
            foreach (var sName in seasons)
            {
                api.AddPage(this.ModManifest, $"{sName.ToLower()}_crops", () => $"{sName} Market");
                var crops = Game1.cropData.Where(k => k.Value.Seasons.Contains(Enum.Parse<Season>(sName, true))).OrderBy(k => GetDisplayName(k.Key));
                foreach (var entry in crops)
                {
                    string id = entry.Key;
                    api.AddBoolOption(this.ModManifest, name: () => $"{GetDisplayName(id)} (Lux)", tooltip: () => "Apply Luxury Tax.", getValue: () => this.Config.LuxuryCrops.GetValueOrDefault(id), setValue: v => this.Config.LuxuryCrops[id] = v);
                    api.AddBoolOption(this.ModManifest, name: () => $"{GetDisplayName(id)} (Sub)", tooltip: () => "Apply Subsidy.", getValue: () => this.Config.SubsidizedCrops.GetValueOrDefault(id), setValue: v => this.Config.SubsidizedCrops[id] = v);
                }
            }
        }

        private void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            bool shouldTaxToday = false;
            if (this.Config.EnableWealthTax)
            {
                if (this.Config.TaxFrequency == "Daily") shouldTaxToday = true;
                else if (this.Config.TaxFrequency == "Weekly" && Game1.dayOfMonth % 7 == 0) shouldTaxToday = true;
                else if (this.Config.TaxFrequency == "Bi-Weekly" && (Game1.dayOfMonth == 14 || Game1.dayOfMonth == 28)) shouldTaxToday = true;
            }

            if (shouldTaxToday)
            {
                int gold = Game1.player.Money;
                float rate = gold >= 10000000 ? this.Config.TaxTycoon : (gold >= 1000000 ? this.Config.TaxWealthy : (gold >= 100000 ? this.Config.TaxComfort : (gold >= 25000 ? this.Config.TaxHumble : (gold >= 5000 ? this.Config.TaxStarter : 0f))));
                if (rate > 0)
                {
                    int tax = (int)(gold * rate);
                    Game1.player.Money -= tax;
                    Game1.addHUDMessage(new HUDMessage($"{this.Config.TaxFrequency} Tax: -{tax}g", HUDMessage.error_type));
                    this.Data.TaxHistory.Insert(0, $"{Game1.currentSeason} {Game1.dayOfMonth}: Paid {tax}g");
                    if (this.Data.TaxHistory.Count > 10) this.Data.TaxHistory.RemoveAt(10);
                }
            }

            if (this.Config.EnablePropertyTax && Game1.dayOfMonth == 28)
            {
                int total = Game1.getFarm().buildings.Count(b => b != null) * this.Config.BuildingTaxRate;
                if (total > 0) { Game1.player.Money -= total; Game1.drawObjectDialogue($"Lewis:^'Collected {total}g in taxes for your buildings.'"); }
            }

            foreach (var key in this.Data.ActiveCrashes.Keys.ToList())
            {
                this.Data.ActiveCrashes[key]--;
                if (this.Data.ActiveCrashes[key] <= 0)
                {
                    this.Data.ActiveCrashes.Remove(key);
                    Game1.chatBox.addMessage($"RECOVERY: {GetDisplayName(key)} stabilized.", Color.Green);
                }
            }
        }

        private void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            foreach (Item item in Game1.getFarm().getShippingBin(Game1.player))
                if (item != null && item.Stack > this.Config.SaturationLimit) this.Data.ActiveCrashes[item.ItemId] = this.Config.CrashDuration;
            this.Helper.Data.WriteSaveData("market-data", this.Data);
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady || !e.Button.IsActionButton()) return;
            var obj = Game1.currentLocation.getObjectAtTile((int)e.Cursor.GrabTile.X, (int)e.Cursor.GrabTile.Y);
            if (obj != null && obj.name.Contains("TV")) {
                Response[] res = { new Response("Eco", "Report"), new Response("Ledger", "View Ledger"), new Response("TV", "Regular TV") };
                Game1.currentLocation.createQuestionDialogue("Select Channel:", res, (who, ans) => {
                    if (ans == "Eco") ShowEconomicNews();
                    else if (ans == "Ledger") ShowTaxLedger();
                    else obj.checkForAction(Game1.player);
                });
                this.Helper.Input.Suppress(e.Button);
            }
        }

        private void ShowEconomicNews()
        {
            string news = "Forecast: ";
            if (this.Data.ActiveCrashes.Any()) foreach (var crash in this.Data.ActiveCrashes) news += $"^{GetDisplayName(crash.Key)}: {crash.Value}d left. ";
            else news += "^Markets stable.";
            Game1.drawObjectDialogue(news);
        }

        private void ShowTaxLedger()
        {
            if (!this.Data.TaxHistory.Any()) { Game1.drawObjectDialogue("No recent tax records."); return; }
            string history = "--- RECENT TAX RECORDS ---";
            foreach (var record in this.Data.TaxHistory) history += $"^{record}";
            Game1.drawObjectDialogue(history);
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))
                e.Edit(asset => asset.AsDictionary<string, string>().Data["EconomicCrashLetter"] = "Market crash reported. Prices dropped.^- Lewis");
        }

        private string GetDisplayName(string id) => ItemRegistry.GetData(id)?.DisplayName ?? id;
    }

    public class ModConfig
    {
        public bool EnableWealthTax { get; set; } = true;
        public string TaxFrequency { get; set; } = "Daily";
        public float TaxStarter { get; set; } = 0.005f;
        public float TaxHumble { get; set; } = 0.015f;
        public float TaxComfort { get; set; } = 0.03f;
        public float TaxWealthy { get; set; } = 0.06f;
        public float TaxTycoon { get; set; } = 0.12f;
        public bool EnablePropertyTax { get; set; } = true;
        public int BuildingTaxRate { get; set; } = 500;
        public float LuxuryTaxRate { get; set; } = 0.10f;
        public float SubsidyBonusRate { get; set; } = 0.20f;
        public float PriceDropRate { get; set; } = 0.15f;
        public int SaturationLimit { get; set; } = 50;
        public int CrashDuration { get; set; } = 7;
        public Dictionary<string, bool> LuxuryCrops { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, bool> SubsidizedCrops { get; set; } = new Dictionary<string, bool>();
    }

    public class ModData 
    { 
        public Dictionary<string, int> ActiveCrashes { get; set; } = new Dictionary<string, int>(); 
        public List<string> TaxHistory { get; set; } = new List<string>();
    }

    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string> tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string> formatValue = null, string fieldId = null);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);
        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string> tooltip = null, string[] allowedValues = null, Func<string, string> formatAllowedValue = null, string fieldId = null);
        void AddPage(IManifest mod, string pageId, Func<string> pageTitle = null);
        void AddPageLink(IManifest mod, string pageId, Func<string> text, Func<string> tooltip = null);
    }
}