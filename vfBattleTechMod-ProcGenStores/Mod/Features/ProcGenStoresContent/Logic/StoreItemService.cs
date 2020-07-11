using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using vfBattleTechMod_Core.Extensions;
using vfBattleTechMod_Core.Utils.Interfaces;

namespace vfBattleTechMod_ProcGenStores.Mod.Features.ProcGenStoresContent.Logic
{
    public class StoreItemService
    {
        private readonly ILogger _logger;

        private readonly List<ProcGenStoreContentFeatureSettings.RarityBracket> _rarityBrackets;
        private readonly List<BattleTechResourceType> _storeResourceTypes;
        private Dictionary<BattleTechResourceType, List<ProcGenStoreItem>> _storeItems;

        public StoreItemService(string storeItemSourceFilePath,
            List<ProcGenStoreContentFeatureSettings.RarityBracket> rarityBrackets,
            List<BattleTechResourceType> storeResourceTypes, ILogger logger)
        {
            _logger = logger;
            _rarityBrackets = rarityBrackets;
            _storeResourceTypes = storeResourceTypes;
        }

        public List<ProcGenStoreItem> GenerateItemsForStore(Shop.ShopType shopType, string starSystemName, string ownerName,
            DateTime currentDate, List<string> planetTags, List<ProcGenStoreContentFeatureSettings.PlanetTagModifier> planetTagModifiers,
            ProcGenStoreContentFeatureSettings settings)
        {
            _logger.Debug($"Generating shop inventory for [{starSystemName} - {shopType.ToString()} - {ownerName}]...");
            var potentialInventoryItems = IdentifyPotentialInventoryItems(shopType, ownerName, planetTags, currentDate, settings);
            _logger.Debug(
                $"Potential Inventory Items = {string.Join("\r\n", potentialInventoryItems.Select(item => $"[{item.StoreItem.Id}] - Bonus [{item.BracketBonus}]"))}");
            var storeInventory = ProduceStoreInventoryFromPotentialItemList(shopType, ownerName, currentDate, settings,
                planetTagModifiers, potentialInventoryItems);
            _logger.Debug(
                $"Final Inventory Items = \r\n{string.Join("\r\n", storeInventory.Select(item => $"{item.Id} @ {item.Quantity} units"))}");
            
            return storeInventory;
        }

        private List<ProcGenStoreItem> ProduceStoreInventoryFromPotentialItemList(Shop.ShopType shopType, string ownerName,
            DateTime currentDate, ProcGenStoreContentFeatureSettings settings,
            List<ProcGenStoreContentFeatureSettings.PlanetTagModifier> planetTagModifiers,
            List<(ProcGenStoreItem StoreItem, int BracketBonus)> potentialInventoryItems)
        {
            _logger.Debug("Rolling for inventory stock...");
            
            var cascadeOnFail = settings.CascadeRollsOnFail;
            var maxCascadeBracket = settings.MaxItemRarityForCascadeQualification;
            var rarityBracketBonus = 0;
            var quantityBracketBonus = 0;
            
            if (shopType == Shop.ShopType.BlackMarket)
            {
                cascadeOnFail = settings.BlackMarketSettings.CascadeRollsOnFail;
                maxCascadeBracket = settings.BlackMarketSettings.MaxItemRarityForCascadeQualification;
                rarityBracketBonus = settings.BlackMarketSettings.BlackMarketRarityBracketModifier;
                quantityBracketBonus = settings.BlackMarketSettings.BlackMarketQuantityBracketModifier;
                var minRarityBracket = _rarityBrackets.First(bracket => bracket.Name == settings.BlackMarketSettings.BlackMarketMinBaseRarity);
                var maxRarityBracket = _rarityBrackets.First(bracket => bracket.Name == settings.BlackMarketSettings.BlackMarketMaxBaseRarity);
                _logger.Debug($"Trimming black market potential inventory [{potentialInventoryItems.Count} items] by rarity [{settings.BlackMarketSettings.BlackMarketMinBaseRarity}] - [{settings.BlackMarketSettings.BlackMarketMaxBaseRarity}]...");
                potentialInventoryItems.RemoveAll(tuple => tuple.StoreItem.RarityBracket.Order < minRarityBracket.Order || tuple.StoreItem.RarityBracket.Order > maxRarityBracket.Order);
                _logger.Debug($"Trimmed black market potential inventory to [{potentialInventoryItems.Count} items] by rarity [{settings.BlackMarketSettings.BlackMarketMinBaseRarity}] - [{settings.BlackMarketSettings.BlackMarketMaxBaseRarity}]...");
            }

            var inventoryItems = new List<ProcGenStoreItem>();
            var random = new Random();
            var cascadeRollOrder =
                _rarityBrackets.FirstOrDefault(bracket => bracket.Name == maxCascadeBracket)
                    ?.Order ?? int.MaxValue;
            potentialInventoryItems.ForEach(
                potentialItem =>
                {
                    var addedToStore = false;
                    var bracketModifier = 0;
                    int minimumBracket = -1;
                    if (potentialItem.StoreItem.RarityBracket.Order > 0)
                    {
                        bracketModifier = -potentialItem.BracketBonus + rarityBracketBonus;
                        minimumBracket = 1;
                    }

                    _logger.Debug($"Rolling for item [{potentialItem.StoreItem.Id}], original bracket = [{potentialItem.StoreItem.RarityBracket.Order}] + " + bracketModifier);
                    var validRarityBrackets = settings.RarityBrackets
                        .Where(bracket =>
                            bracket.Order >= Math.Max(potentialItem.StoreItem.RarityBracket.Order + bracketModifier, minimumBracket))
                        .ToList()
                        .OrderBy(bracket => bracket.Order).ToList();
                    _logger.Debug(
                        $"Rolling for item [{potentialItem.StoreItem.Id}], original bracket = [{potentialItem.StoreItem.RarityBracket.Name}], effective bracket = [{validRarityBrackets.First().Name}]...");

                    var effectiveQuantityBracket = _rarityBrackets.First(bracket => bracket.Order == potentialItem.StoreItem.RarityBracket.Order + quantityBracketBonus).QuantityBracket;

                    foreach (var bracket in validRarityBrackets)
                    {
                        var chance = bracket.ChanceToAppear;

                        if (settings.UseAdditiveForModifiers)
                        {
                            var totalModifier = planetTagModifiers.Sum(modifier => modifier.ChanceModifier >= 1 ? modifier.ChanceModifier - 1 : (1 - modifier.ChanceModifier) * -1) + 1;
                            _logger.Debug($"Additive multiplier = [{totalModifier}]");
                            chance *= totalModifier;
                        }
                        else
                        {
                            planetTagModifiers.ForEach(modifier => chance *= modifier.ChanceModifier);
                        }

                        var appearanceRoll = random.NextDouble();

                        _logger.Debug($"Default chance = [{bracket.ChanceToAppear}] for [{bracket.Name}]\r\n" +
                                      $"Planet Modifiers = [{string.Join(",", planetTagModifiers.Select(modifier => $"{modifier.Tag} - {modifier.ChanceModifier}"))}]\r\n" +
                                      $"Final Chance = [{chance}]\r\n" +
                                      $"Roll = [{appearanceRoll}]");

                        if (appearanceRoll <= chance)
                        {
                            var storeItem = potentialItem.StoreItem.Copy();
                            var quantityRoll = random.Next(effectiveQuantityBracket.LowCount,
                                effectiveQuantityBracket.HighCount + 1);
                            storeItem.Quantity = effectiveQuantityBracket.LowCount < 0 ? -1 : quantityRoll;
                            
                            if (storeItem.Quantity != -1)
                            planetTagModifiers.ForEach(modifier =>
                                storeItem.Quantity =
                                    Convert.ToInt32(Math.Round(
                                        Convert.ToDouble(storeItem.Quantity) * modifier.QuantityModifier, 0)));

                            _logger.Debug($"Rolling for quantity [{storeItem.Id}].\r\n" +
                                          $"Default range = [{effectiveQuantityBracket.LowCount} - {effectiveQuantityBracket.HighCount}] for [{effectiveQuantityBracket.Name}]\r\n" +
                                          $"Planet Modifiers = [{string.Join(",", planetTagModifiers.Select(modifier => $"{modifier.Tag} - {modifier.QuantityModifier}"))}]\r\n" +
                                          $"Unmodified Roll = [{quantityRoll}]\r\n" +
                                          $"Modified Roll = [{storeItem.Quantity}]");

                            _logger.Debug(
                                $"Adding [{storeItem.Id}] to store with quantity [{storeItem.Quantity}].{(potentialItem.StoreItem.RarityBracket.Order != bracket.Order ? "CASCADE SUCCESS" : "")}");

                            inventoryItems.Add(storeItem);
                            addedToStore = true;
                            break;
                        }

                        if (!cascadeOnFail)
                        {
                            _logger.Debug($"CASCADE DISABLED : [{potentialItem.StoreItem.Id}] FAILED roll");
                            break;
                        }

                        if (cascadeOnFail && validRarityBrackets.First().Order > cascadeRollOrder)
                        {
                            _logger.Debug(
                                $"CASCADE Enabled but max cascade [{settings.MaxItemRarityForCascadeQualification}-{cascadeRollOrder}] exceeds initial configured rarity [{validRarityBrackets.First().Name}] : [{potentialItem.StoreItem.Id}] FAILED roll");
                            break;
                        }

                        _logger.Debug(
                            $"CASCADE ENABLED : [{potentialItem.StoreItem.Id}] FAILED roll, checking next rarity bracket...");
                    }

                    _logger.Debug(
                        $"[{potentialItem.StoreItem.Id}] - [{(addedToStore ? "added to store" : "not added to store")}.]");
                });

            //Parsing the items in order to apply shop limits for items.
            var ammoBoxDefList = new List<ProcGenStoreItem>();
            var heatSinkDefList = new List<ProcGenStoreItem>();
            var jumpJetDefList = new List<ProcGenStoreItem>();
            var mechDefList = new List<ProcGenStoreItem>();
            var upgradeDefList = new List<ProcGenStoreItem>();
            var weaponDefList = new List<ProcGenStoreItem>();
            var finalInventoryItems = new List<ProcGenStoreItem>();

            foreach (var item in inventoryItems)
            {
                if (item.RarityBracket.Order == -1)
                {
                    finalInventoryItems.Add(item);
                    continue;
                }

                if (item.Type == BattleTechResourceType.AmmunitionBoxDef)
                    ammoBoxDefList.Add(item);
                if (item.Type == BattleTechResourceType.HeatSinkDef)
                    heatSinkDefList.Add(item);
                if (item.Type == BattleTechResourceType.JumpJetDef)
                    jumpJetDefList.Add(item);
                if (item.Type == BattleTechResourceType.MechDef)
                    mechDefList.Add(item);
                if (item.Type == BattleTechResourceType.UpgradeDef)
                    upgradeDefList.Add(item);
                if (item.Type == BattleTechResourceType.WeaponDef)
                    weaponDefList.Add(item);
            }
            
            ammoBoxDefList.Shuffle();
            heatSinkDefList.Shuffle();
            jumpJetDefList.Shuffle();
            mechDefList.Shuffle();
            upgradeDefList.Shuffle();
            weaponDefList.Shuffle();

            var systemTags = UnityGameInstance.BattleTechGame.Simulation.CurSystem.Tags;

            int totalModifier = 0;
            foreach (var tag in systemTags)
            {
                if (settings.StoreSizeModifiers.ContainsKey(tag))
                    totalModifier += settings.StoreSizeModifiers[tag];
            }

            int ammoCount = Math.Max(0, settings.AmmoStoreLimit + totalModifier);
            for (int j = 0; j < Math.Min(ammoCount, ammoBoxDefList.Count()); j++)
                finalInventoryItems.Add(ammoBoxDefList[j]);

            int heatSinkCount = Math.Max(0, settings.HeatSinkStoreLimit + totalModifier);
            for (int j = 0; j < Math.Min(heatSinkCount, heatSinkDefList.Count()); j++)
                finalInventoryItems.Add(heatSinkDefList[j]);

            int jumpJetCount = Math.Max(0, settings.JumpJetStoreLimit + totalModifier);
            for (int j = 0; j < Math.Min(jumpJetCount, jumpJetDefList.Count()); j++)
                finalInventoryItems.Add(jumpJetDefList[j]);

            int mechDefCount = Math.Max(0, settings.MechStoreLimit + totalModifier);
            for (int j = 0; j < Math.Min(mechDefCount, mechDefList.Count()); j++)
                finalInventoryItems.Add(mechDefList[j]);

            int upgradeDefCount = Math.Max(0, settings.UpgradeStoreLimit + totalModifier);
            for (int j = 0; j < Math.Min(upgradeDefCount, upgradeDefList.Count()); j++)
                finalInventoryItems.Add(upgradeDefList[j]);

            int weaponDefCount = Math.Max(0, settings.WeaponStoreLimit + totalModifier);
            for (int j = 0; j < Math.Min(weaponDefCount, weaponDefList.Count()); j++)
                finalInventoryItems.Add(weaponDefList[j]);

            return finalInventoryItems;
        }

        public List<(ProcGenStoreItem StoreItem, int BracketBonus)> IdentifyPotentialInventoryItems(Shop.ShopType shopType,
            string ownerName,
            List<string> planetTags,
            DateTime currentDate, ProcGenStoreContentFeatureSettings settings)
        {

            if (_storeItems == null)
            {
                try
                {
                    _storeItems =
                        ProcGenStoreItemLoader.LoadItemsFromDataManager(_logger, settings, _storeResourceTypes);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Exception building items list from Data Manager.", ex);
                }
            }
            
            var potentialInventoryItems = new List<(ProcGenStoreItem StoreItem, int BracketBonus)>();
            switch (shopType)
            {
                case Shop.ShopType.System:
                case Shop.ShopType.BlackMarket:
                    _storeItems.Values.SelectMany(list => list).ToList().ForEach(
                        item =>
                        {
                            var result = item.IsValidForAppearance(currentDate, ownerName, shopType, planetTags, item.TagSet, settings);
                            _logger.Debug($"[{item.Id}] - [{result.ToString()}]");
                            if (result.result)
                            {
                                potentialInventoryItems.Add((item, result.bracketBonus));
                            }
                        });
                    break;
            }

            return potentialInventoryItems;
        }
    }
}