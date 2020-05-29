using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BattleTech;
using HBS.Collections;
using OfficeOpenXml;
using UIWidgets;
using vfBattleTechMod_Core.Mods;
using vfBattleTechMod_Core.Utils.Interfaces;
using vfBattleTechMod_Core.Utils.MetaDataHelpers;

namespace vfBattleTechMod_ProcGenStores.Mod.Features.ProcGenStoresContent.Logic
{
    public static class ProcGenStoreItemLoader
    {
        static TagSet GetTagsByType(BattleTechResourceType type, object theObject)
        {
            switch (type)
            {
                case BattleTechResourceType.AmmunitionBoxDef: return ((AmmunitionBoxDef) theObject).ComponentTags;
                case BattleTechResourceType.UpgradeDef: return ((UpgradeDef) theObject).ComponentTags;
                case BattleTechResourceType.HeatSinkDef: return ((HeatSinkDef) theObject).ComponentTags;
                case BattleTechResourceType.JumpJetDef: return ((JumpJetDef) theObject).ComponentTags;
                case BattleTechResourceType.WeaponDef: return ((WeaponDef) theObject).ComponentTags;
                case BattleTechResourceType.MechDef: return ((MechDef) theObject).MechTags;
                default:
                    throw new InvalidProgramException($"BattleTechResourceType [{type.ToString()}] unhandled.");
            }
        }
        static int GetChassisCost(BattleTechResourceType type, object theObject)
        {
            switch (type)
            {
                case BattleTechResourceType.AmmunitionBoxDef: return 0;
                case BattleTechResourceType.UpgradeDef: return 0;
                case BattleTechResourceType.HeatSinkDef: return 0;
                case BattleTechResourceType.JumpJetDef: return 0;
                case BattleTechResourceType.WeaponDef: return 0;
                case BattleTechResourceType.MechDef: return ((MechDef)theObject).Chassis.Description.Cost;
                default:
                    throw new InvalidProgramException($"BattleTechResourceType [{type.ToString()}] unhandled.");
            }
        }

        static DescriptionDef GetObjectDescriptionByType(BattleTechResourceType type, object theObject)
        {
            return type switch
            {
                BattleTechResourceType.AmmunitionBoxDef => ((AmmunitionBoxDef) theObject).Description,
                BattleTechResourceType.UpgradeDef => ((UpgradeDef) theObject).Description,
                BattleTechResourceType.HeatSinkDef => ((HeatSinkDef) theObject).Description,
                BattleTechResourceType.JumpJetDef => ((JumpJetDef) theObject).Description,
                BattleTechResourceType.WeaponDef => ((WeaponDef) theObject).Description,
                BattleTechResourceType.MechDef => ((MechDef) theObject).Description,
                _ => throw new InvalidProgramException($"BattleTechResourceType [{type.ToString()}] unhandled.")
            };
        }

        static List<object> GetObjectListByType(BattleTechResourceType type, SimGameState simGame)
        {
            return type switch
            {
                BattleTechResourceType.AmmunitionBoxDef => simGame.DataManager.AmmoBoxDefs
                    .Select(pair => (object) pair.Value).ToList(),
                BattleTechResourceType.UpgradeDef => simGame.DataManager.UpgradeDefs.Select(pair => (object) pair.Value)
                    .ToList(),
                BattleTechResourceType.HeatSinkDef => simGame.DataManager.HeatSinkDefs
                    .Select(pair => (object) pair.Value).ToList(),
                BattleTechResourceType.JumpJetDef => simGame.DataManager.JumpJetDefs.Select(pair => (object) pair.Value)
                    .ToList(),
                BattleTechResourceType.WeaponDef => simGame.DataManager.WeaponDefs.Select(pair => (object) pair.Value)
                    .ToList(),
                BattleTechResourceType.MechDef => simGame.DataManager.MechDefs.Select(pair => (object) pair.Value)
                    .ToList(),
                _ => throw new InvalidProgramException($"BattleTechResourceType [{type.ToString()}] unhandled.")
            };
        }

        public static Dictionary<BattleTechResourceType, List<ProcGenStoreItem>> LoadItemsFromDataManager(
            ILogger logger, ProcGenStoreContentFeatureSettings settings,
            List<BattleTechResourceType> storeResourceTypes)
        {
            logger.Debug($"Building items lists from Data Manager...");
            var simGame = UnityGameInstance.BattleTechGame.Simulation;
            var rarityBrackets = settings.RarityBrackets;
            var storeItemsByType = new Dictionary<BattleTechResourceType, List<ProcGenStoreItem>>();
            storeResourceTypes.ForEach(type => storeItemsByType[type] = new List<ProcGenStoreItem>());
            var rarityMap = new List<(int min, int max, string bracket)>
            {
                (-1, 1, "Ubiquitous"),
                (1, 2, "VeryCommon"),
                (2, 3, "Common"),
                (3, 4, "Uncommon"),
                (4, 5, "VeryUncommon"),
                (5, 6, "Rare"),
                (6, 7, "VeryRare"),
                (7, 8, "PracticallyExtinct"),
                (8, int.MaxValue, "Extinct")
            };
            rarityMap.Reverse();

            logger.Debug($"Parsing backup canon availability data...");

            foreach (var storeResourceType in storeItemsByType.Keys)
            {
                logger.Debug($"Building object lists for [{storeResourceType.ToString()}]...");
                var rawItemsList = GetObjectListByType(storeResourceType, simGame);
                var rawItemsListSansTemplates = rawItemsList.Where(theObject =>
                    {
                        var description = GetObjectDescriptionByType(storeResourceType, theObject);
                        if (description.Id.ToLower().Contains("template"))
                        {
                            logger.Trace($"Filtering out [{description.Id}], Purchasable = [{description.Purchasable}].");
                            return false;
                        }
                        // Filter out templates and items flagged as non-purchasable...
                        return true;
                    })
                    .ToList();
                
                var itemDetails = rawItemsListSansTemplates.Select(
                    o =>
                    {
                        var description = GetObjectDescriptionByType(storeResourceType, o);
                        string id = description.Id;
                        float definedRarity = description.Rarity;
                        var mappedRarity =
                            rarityMap.First(tuple => definedRarity < tuple.max && definedRarity >= tuple.min);
                        var tagSet = GetTagsByType(storeResourceType, o);
                        var purchaseCost = description.Cost;
                        int chassiscost = 0;
                        chassiscost = GetChassisCost(storeResourceType, o);

                        var containingShopDefinitions = simGame.DataManager.Shops
                            .Select(pair => pair.Value)
                            .Where(def =>
                                def.Inventory.Select(item => item.ID).Contains(id) ||
                                def.Specials.Select(item => item.ID).Contains(id))
                            .ToList();

                        var requiredTags = containingShopDefinitions.SelectMany(def => def.RequirementTags).Distinct()
                            .ToList();
                        var exclusionTags = containingShopDefinitions.SelectMany(def => def.ExclusionTags).Distinct()
                            .ToList();

                        var invalid_tags = new List<string>() {"debug"};
                        requiredTags.RemoveAll(s => invalid_tags.Contains(s));
                        exclusionTags.RemoveAll(s => invalid_tags.Contains(s));

                        DateTime? appearanceDate = null;
                        appearanceDate = GetAppearanceDate(o, CoreMod.CoreModSingleton.MechAppearanceData, simGame.DataManager.MechDefs.Select(pair => pair.Value).ToList(), logger);

                        logger.Trace(
                            $"Adding [{storeResourceType.ToString()}] - [{description.Id}]|" +
                            $"minAppearanceDate = [{appearanceDate.ToString()}]|" +
                            $"definedRarity = [{definedRarity.ToString(CultureInfo.InvariantCulture)}, mappedRarity = [{mappedRarity.bracket}]]|" +
                            $"tagSet = [{string.Join(",", tagSet)}]|" +
                            $"requiredTags = [{string.Join(", ", requiredTags)}]|" +
                            $"exclusionTags = [{string.Join(", ", exclusionTags)}].");

                        return new ProcGenStoreItem(storeResourceType, description.Id, appearanceDate, tagSet,
                            rarityBrackets.First(bracket => bracket.Name == mappedRarity.bracket), requiredTags,
                            exclusionTags, description.Purchasable, purchaseCost, chassiscost);
                    }
                ).ToList();
                storeItemsByType[storeResourceType].AddRange(itemDetails);

                logger.Debug(
                    $"Added [{storeItemsByType[storeResourceType].Count.ToString()} items to list [{storeResourceType.ToString()}]].");
            }

            var mechsSansAppearanceDates = storeItemsByType[BattleTechResourceType.MechDef].Where(item => !item.MinAppearanceDate.HasValue).ToList();
            logger.Debug(
                $"Mechs without appearance dates (and therefore removed) = [\r\n{string.Join("\r\n", mechsSansAppearanceDates.Select(item => item.Id))}]");
            storeItemsByType[BattleTechResourceType.MechDef].RemoveAll(item => !item.MinAppearanceDate.HasValue);

            try
            {
                void DumpItemsToSheet(ExcelWorkbook excelWorkbook, string sheetName, List<string> list, List<ProcGenStoreItem> items)
                {
                    


                    var row = 2;
                    var col = 1;
                    var sheet = excelWorkbook.Worksheets.Add(sheetName);
                    list.ForEach(s => sheet.Cells[1, col++].Value = s);
                    items.ForEach(item =>
                    {
                        var requiredTagsSet = "";
                        foreach (var foo in item.RequiredTags)
                            requiredTagsSet = requiredTagsSet + "| " + foo;

                        var restrictedTagsSet = "";
                        foreach (var foo in item.RestrictedTags)
                            restrictedTagsSet = restrictedTagsSet + "| " + foo;

                        var rarityTag = "N/A";
                        if (item.TagSet.Contains("TechLevel_LowTech"))
                            rarityTag = "L";
                        if (item.TagSet.Contains("TechLevel_MidTech"))
                            rarityTag = "M";
                        if (item.TagSet.Contains("TechLevel_HighTech"))
                            rarityTag = "H";

                        string blacklisted = "";
                        if (item.TagSet.Contains("BLACKLISTED"))
                            blacklisted = "BLACKLISTED";
                        string builtin = "";
                        if (item.TagSet.Contains("BUILT-IN"))
                            builtin = "BUILT-IN";

                        sheet.Cells[row, 1].Value = item.Id;
                        sheet.Cells[row, 2].Value = item.Purchasable;
                        sheet.Cells[row, 3].Value = item.RarityBracket.Order;
                        sheet.Cells[row, 4].Value = rarityTag;
                        sheet.Cells[row, 5].Value = item.MinAppearanceDate.ToString();
                        sheet.Cells[row, 6].Value = requiredTagsSet;
                        sheet.Cells[row, 7].Value = restrictedTagsSet;
                        sheet.Cells[row, 8].Value = blacklisted;
                        sheet.Cells[row, 9].Value = builtin;
                        sheet.Cells[row, 10].Value = item.PurchaseCost;
                        sheet.Cells[row, 11].Value = item.ChassisCost;
                        row += 1;
                    });
                }

                var fileInfo = new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "DataManagerItems.xlsx"));
                if (fileInfo.Exists)
                {
                    fileInfo.Delete();
                }

                var excelPackage = new ExcelPackage(fileInfo);
                var book = excelPackage.Workbook;
                var columns = new List<string>()
                {
                    "Id", "Purchasable", "Rarity", "T. Level", "Appearance Date", "Req. Tags", "Ex. Tags, Blacklisted, Built-In, Purchase Cost, Chassis Cost"
                };

                storeItemsByType.Keys.ForEach(type =>
                {
                    var items = storeItemsByType[type];
                    var sheetName = type.ToString();
                    DumpItemsToSheet(book, sheetName, columns, items);
                });

                DumpItemsToSheet(book, "Mechs w/o date", columns, mechsSansAppearanceDates);
                excelPackage.Save();
            }
            catch (Exception ex)
            {
                logger.Error($"Exception logging data manager items.", ex);
            }


            return storeItemsByType;
        }

        private static DateTime? GetAppearanceDate(object o, List<MechModel> mechAppearanceData, List<MechDef> mechDefs,
            ILogger logger)
        {
            DateTime? appearanceDate = null;
            if (o is MechDef mechDef)
            {
                var mechModelEntry = mechAppearanceData.FirstOrDefault(model =>
                    model.Name.Trim('"') == mechDef.Description.UIName);
                if (mechModelEntry != null)
                {
                    appearanceDate = new DateTime(mechModelEntry.Year, 1, 1);
                }

                appearanceDate = mechDef.MinAppearanceDate ?? appearanceDate;
            }
            else
            {
                if (ProcGenStoreContentFeatureSettings.EquipmentAppearanceByMechs)
                {
                    if (o is MechComponentDef component)
                    {
                        var id = component.Description.Id;
                        logger.Trace($"Attempting to mine first appearance of [{id}] on any mech...");
                        var hostingMechs = mechDefs.Where(def =>
                            {
                                logger.Trace($"Evaluating mech [{def.Description.Id}]...");
                                if (!def.MinAppearanceDate.HasValue)
                                {
                                    logger.Trace($"Mech [{def.Description.Id}] has no appearance date, skipping...");
                                    return false;
                                }
                                return def.Inventory.Any(inventoryRef =>
                                {
                                    return inventoryRef.ComponentDefID == id;
                                });
                            }).OrderBy(def => def.MinAppearanceDate);
                        var earliestMech = hostingMechs.FirstOrDefault();
                        appearanceDate = earliestMech?.MinAppearanceDate;
                        logger.Trace($"Component [{id}] first appears [{appearanceDate.ToString()}] on mech [{earliestMech?.Description?.Id ?? "N/A"}].");
                    }
                }
                else
                {
                    appearanceDate = ProcGenStoreContentFeatureSettings.ItemAppearanceDate;
                }
            }

            return appearanceDate;
        }
    }
}