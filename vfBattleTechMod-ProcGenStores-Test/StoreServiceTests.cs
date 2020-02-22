﻿using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using vfBattleTechMod_Core.Helpers;
using vfBattleTechMod_Core.Utils;
using vfBattleTechMod_Core.Utils.Interfaces;
using vfBattleTechMod_ProcGenStores.Mod.Features.ProcGenStoresContent;
using vfBattleTechMod_ProcGenStores.Mod.Features.ProcGenStoresContent.Logic;

namespace vfBattleTechMod_ProcGenStores_Test
{
    [TestFixture]
    public class StoreServiceTests
    {
        private string sourceFile;
        private JObject settings;
        private ProcGenStoreContentFeatureSettings procGenSettings;
        private readonly ILogger logger = new NullLogger();

        [OneTimeSetUp]
        public void Init()
        {
            sourceFile = TestContext.CurrentContext.TestDirectory + @"/res/test-xlrp-store-content.xlsx";
            settings = JsonHelpers.DeserializeFile(
                TestContext.CurrentContext.TestDirectory + @"/res/test-settings.json");
            procGenSettings = settings["Procedurally Generate Store Contents"]
                .ToObject<ProcGenStoreContentFeatureSettings>();
        }

        [Test]
        public void TestStoreItemPotentialsCorrectlyExcludeFutureTechForEarlyDate()
        {
            var storeItemTypes = new List<BattleTechResourceType> {BattleTechResourceType.HeatSinkDef};
            var date = new DateTime(3025, 1, 1);
            var storeItemService =
                new StoreItemService(sourceFile, procGenSettings.RarityBrackets, storeItemTypes, logger);
            var potentialInventory =
                storeItemService.IdentifyPotentialInventoryItems(Shop.ShopType.System, "vengefire", date,
                    procGenSettings);
            Assert.IsFalse(potentialInventory.Any(item => item.StoreItem.Id == "emod_engineslots_compact_center"));
        }

        [Test]
        public void TestStoreItemPotentialsCorrectlyExcludesNaTech()
        {
            var storeItemTypes = new List<BattleTechResourceType> {BattleTechResourceType.HeatSinkDef};
            var date = new DateTime(3100, 1, 1);
            var storeItemService =
                new StoreItemService(sourceFile, procGenSettings.RarityBrackets, storeItemTypes, logger);
            var potentialInventory =
                storeItemService.IdentifyPotentialInventoryItems(Shop.ShopType.System, "TH", date, procGenSettings);
            Assert.IsFalse(potentialInventory.Any(item => item.StoreItem.Id == "HeatSink_Template"));
        }

        [Test]
        public void TestStoreItemPotentialsCorrectlyExcludesPrototypeTechForLateDateAndFaction()
        {
            var storeItemTypes = new List<BattleTechResourceType> {BattleTechResourceType.HeatSinkDef};
            var date = new DateTime(3036, 1, 1);
            var storeItemService =
                new StoreItemService(sourceFile, procGenSettings.RarityBrackets, storeItemTypes, logger);
            var potentialInventory =
                storeItemService.IdentifyPotentialInventoryItems(Shop.ShopType.System, "TH", date, procGenSettings);
            Assert.IsFalse(potentialInventory.Any(item => item.StoreItem.Id == "emod_engineslots_xl_center"));
        }

        [Test]
        public void TestStoreItemPotentialsCorrectlyIncludesFutureTechForLateDate()
        {
            var storeItemTypes = new List<BattleTechResourceType> {BattleTechResourceType.HeatSinkDef};
            var date = new DateTime(3100, 1, 1);
            var storeItemService =
                new StoreItemService(sourceFile, procGenSettings.RarityBrackets, storeItemTypes, logger);
            var potentialInventory =
                storeItemService.IdentifyPotentialInventoryItems(Shop.ShopType.System, "vengefire", date,
                    procGenSettings);
            Assert.IsTrue(potentialInventory.Any(item => item.StoreItem.Id == "emod_engineslots_compact_center"));
        }

        [Test]
        public void TestStoreItemPotentialsCorrectlyIncludesPrototypeTechForLateDateAndFaction()
        {
            var storeItemTypes = new List<BattleTechResourceType> {BattleTechResourceType.HeatSinkDef};
            var date = new DateTime(3036, 1, 1);
            var storeItemService =
                new StoreItemService(sourceFile, procGenSettings.RarityBrackets, storeItemTypes, logger);
            var potentialInventory =
                storeItemService.IdentifyPotentialInventoryItems(Shop.ShopType.System, "LC", date, procGenSettings);
            Assert.IsTrue(potentialInventory.Any(item => item.StoreItem.Id == "emod_engineslots_xl_center"));
        }

        [Test]
        public void TestStoreItemServiceBasicProcessing()
        {
            var storeItemTypes = ProcGenStoreContentFeature.BattleTechStoreResourceTypes;
            var date = new DateTime(3025, 1, 1);
            var storeItemService =
                new StoreItemService(sourceFile, procGenSettings.RarityBrackets, storeItemTypes, logger);
            var planetTags = new List<string> {"planet_pop_large"};
            var planetModifiers = procGenSettings.PlanetTagModifiers
                .Where(modifier => planetTags.Contains(modifier.Tag)).ToList();
            var storeInventory = storeItemService.GenerateItemsForStore(Shop.ShopType.System, "Planet Vengeance",
                "vengefire", date, planetModifiers, procGenSettings);
        }
    }
}