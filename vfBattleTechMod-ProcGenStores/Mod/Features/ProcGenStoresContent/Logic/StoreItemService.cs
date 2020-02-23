﻿using System;
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

        public StoreItemService(string storeItemSourceFilePath,
            List<ProcGenStoreContentFeatureSettings.RarityBracket> rarityBrackets,
            List<BattleTechResourceType> storeResourceTypes, ILogger logger)
        {
            this._logger = logger;
            _rarityBrackets = rarityBrackets;
            StoreItems = StoreItemLoader.LoadStoreItemsFromExcel(storeItemSourceFilePath, rarityBrackets,
                storeResourceTypes, logger);
        }

        private List<StoreItem> StoreItems { get; set; }

        public List<StoreItem> GenerateItemsForStore(Shop.ShopType shopType, string starSystemName, string ownerName,
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

        private List<StoreItem> ProduceStoreInventoryFromPotentialItemList(Shop.ShopType shopType, string ownerName,
            DateTime currentDate, ProcGenStoreContentFeatureSettings settings,
            List<ProcGenStoreContentFeatureSettings.PlanetTagModifier> planetTagModifiers,
            List<(StoreItem StoreItem, int BracketBonus)> potentialInventoryItems)
        {
            _logger.Debug("Rolling for inventory stock...");
            var inventoryItems = new List<StoreItem>();
            var random = new Random();
            var cascadeRollOrder =
                _rarityBrackets.FirstOrDefault(bracket => bracket.Name == settings.MaxItemRarityForCascadeQualification)
                    ?.Order ?? int.MaxValue;
            potentialInventoryItems.ForEach(
                potentialItem =>
                {
                    var addedToStore = false;
                    var validRarityBrackets = settings.RarityBrackets
                        .Where(bracket =>
                            bracket.Order >= potentialItem.StoreItem.RarityBracket.Order - potentialItem.BracketBonus)
                        .ToList()
                        .OrderBy(bracket => bracket.Order).ToList();
                    _logger.Debug(
                        $"Rolling for item [{potentialItem.StoreItem.Id}], original bracket = [{potentialItem.StoreItem.RarityBracket.Name}], effective bracket = [{validRarityBrackets.First().Name}]...");

                    var effectiveQuantityBracket = potentialItem.StoreItem.RarityBracket.QuantityBracket;

                    foreach (var bracket in validRarityBrackets)
                    {
                        var chance = bracket.ChanceToAppear;
                        planetTagModifiers.ForEach(modifier => chance *= modifier.ChanceModifier);
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
                            storeItem.Quantity = effectiveQuantityBracket.LowCount == -1 ? -1 : quantityRoll;
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

                        if (!settings.CascadeRollsOnFail)
                        {
                            _logger.Debug($"CASCADE DISABLED : [{potentialItem.StoreItem.Id}] FAILED roll");
                            break;
                        }

                        if (settings.CascadeRollsOnFail && validRarityBrackets.First().Order > cascadeRollOrder)
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
            return inventoryItems;
        }

        public List<(StoreItem StoreItem, int BracketBonus)> IdentifyPotentialInventoryItems(Shop.ShopType shopType,
            string ownerName,
            List<string> planetTags,
            DateTime currentDate, ProcGenStoreContentFeatureSettings settings)
        {
            var potentialInventoryItems = new List<(StoreItem StoreItem, int BracketBonus)>();
            switch (shopType)
            {
                case Shop.ShopType.System:
                    StoreItems.ForEach(
                        item =>
                        {
                            var result = item.IsValidForAppearance(currentDate, ownerName, shopType, planetTags, settings);
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