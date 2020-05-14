using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BattleTech;
using Newtonsoft.Json;
using OfficeOpenXml;
using vfBattleTechMod_Core.Utils.Interfaces;

namespace vfBattleTechMod_ProcGenStores.Mod.Features.ProcGenStoresContent.Logic
{
    public static class StoreItemLoader
    {
        public static List<StoreItem> LoadStoreItemsFromExcel(string sourceExcelFilePath,
            List<ProcGenStoreContentFeatureSettings.RarityBracket> rarityBrackets,
            List<BattleTechResourceType> validStoreTypes)
        {
            vfBattleTechMod_Core.Utils.Loggers.NonSillyLogging.LogDebug($"Loading store item definitions from [{sourceExcelFilePath}]...");

            var storeItems = new List<StoreItem>();
            var package = new ExcelPackage(new FileInfo(sourceExcelFilePath));
            var book = package.Workbook;

            const string szId = "Id";
            const string szPrototypeDateFaction = "Prototype date|Faction";
            const string szProductionDateFaction = "Production Date|Faction";
            const string szExtinctionDate = "Extinction Date";
            const string szReintroDateFaction = "Reintro|Faction";
            const string szCommonDate = "Common Date";
            const string szAvailability = "Availability";
            const string szNa = "NA";
            const string szRequired = "Required PlanetTags (Any of)";
            const string szRestricted = "Restricted PlanetTags (Any of)";

            var columnHeaders = new List<string>
            {
                szId,
                szPrototypeDateFaction,
                szProductionDateFaction,
                szExtinctionDate,
                szReintroDateFaction,
                szCommonDate,
                szAvailability,
                szRequired,
                szRestricted
            };

            var columnHeaderIndex = new Dictionary<string, int>();

            foreach (var battleTechResourceType in validStoreTypes)
            {
                var sheet = book.Worksheets.FirstOrDefault(worksheet =>
                    worksheet.Name == battleTechResourceType.ToString());
                if (sheet == null)
                {
                     vfBattleTechMod_Core.Utils.Loggers.NonSillyLogging.LogDebug(
                        $"Failed to find sheet linked to BattleTechResourceType [{battleTechResourceType.ToString()}]");
                    continue;
                }

                if (sheet.Dimension.Rows <= 1)
                {
                     vfBattleTechMod_Core.Utils.Loggers.NonSillyLogging.LogDebug(
                        $"No items defined in sheet linked to BattleTechResourceType [{battleTechResourceType.ToString()}]");
                    continue;
                }

                 vfBattleTechMod_Core.Utils.Loggers.NonSillyLogging.LogDebug("Building column header index...");
                for (var colIndex = 1; colIndex <= sheet.Dimension.Columns; ++colIndex)
                {
                    var cellValue = sheet.Cells[1, colIndex].Value?.ToString();

                    if (cellValue == null)
                    {
                        continue;
                    }

                    if (columnHeaders.Contains(cellValue))
                    {
                        columnHeaderIndex[cellValue] = colIndex;
                    }
                }

                if (columnHeaderIndex.Count != columnHeaders.Count)
                {
                     vfBattleTechMod_Core.Utils.Loggers.NonSillyLogging.LogDebug(
                        "Failed to find all required column headers in store item definition file. Missing column headers are \r\n" +
                        $"{string.Join("\r\n", columnHeaders.Where(s => !columnHeaderIndex.Keys.Contains(s)))}");
                    return storeItems;
                }

                 vfBattleTechMod_Core.Utils.Loggers.NonSillyLogging.LogDebug("Column header index built.");

                StoreItem storeItem;

                (DateTime date, string faction) ExtractDateAndFaction(string value1)
                {
                    var parts = value1.Split('|');
                    var dateTime = new DateTime(Convert.ToInt32(parts[0]), 1, 1);
                    var faction1 = parts.Length > 1 ? parts[1] : null;
                    return (dateTime, faction1);
                }

                for (var rowIndex = 2; rowIndex < sheet.Dimension.Rows; ++rowIndex)
                {
                    // Shortcut check for NA availability to avoid unnecessary processing...
                    var id = sheet.Cells[rowIndex, columnHeaderIndex[szId]].Value?.ToString();
                    var availability = sheet.Cells[rowIndex, columnHeaderIndex[szAvailability]].Value?.ToString();
                    if (string.IsNullOrEmpty(availability) || availability == szNa)
                    {
                         vfBattleTechMod_Core.Utils.Loggers.NonSillyLogging.LogDebug(
                            $"Row [{rowIndex}] - Id [{id ?? "NULL"}] availability value [{availability ?? "NULL"}] is invalid. Skipping...");
                        continue;
                    }

                    storeItem = new StoreItem();
                    foreach (var columnHeader in columnHeaderIndex.Keys)
                    {
                        var value = sheet.Cells[rowIndex, columnHeaderIndex[columnHeader]].Value?.ToString();
                        if (value == szNa || string.IsNullOrEmpty(value))
                        {
                            continue;
                        }

                        switch (columnHeader)
                        {
                            case szId:
                                storeItem.Id = value;
                                break;
                            case szPrototypeDateFaction:
                            {
                                var (date, faction) = ExtractDateAndFaction(value);
                                storeItem.PrototypeDate = date;
                                storeItem.PrototypeFaction = faction;
                                break;
                            }
                            case szProductionDateFaction:
                            {
                                var (date, faction) = ExtractDateAndFaction(value);
                                storeItem.ProductionDate = date;
                                storeItem.ProductionFaction = faction;
                                break;
                            }
                            case szReintroDateFaction:
                            {
                                var (date, faction) = ExtractDateAndFaction(value);
                                storeItem.ReintroductionDate = date;
                                storeItem.ReintroductionFaction = faction;
                                break;
                            }
                            case szExtinctionDate:
                                storeItem.ExtinctionDate = new DateTime(Convert.ToInt32(value), 1, 1);
                                break;
                            case szCommonDate:
                                DateTime commonDate; 
                                if (!DateTime.TryParse(value, out commonDate))
                                {
                                    storeItem.CommonDate = new DateTime(Convert.ToInt32(value), 1, 1);
                                }
                                break;
                            case szAvailability:
                                storeItem.RarityBracket = rarityBrackets.First(bracket => bracket.Name == value);
                                break;
                            case szRequired:
                                storeItem.RequiredPlanetTags = value.Split('|').ToList();
                                break;
                            case szRestricted:
                                storeItem.RestrictedPlanetTags = value.Split('|').ToList();
                                break;
                        }
                    }

                     vfBattleTechMod_Core.Utils.Loggers.NonSillyLogging.LogDebug($"Adding store item [{JsonConvert.SerializeObject(storeItem)}]");
                    storeItem.Type = battleTechResourceType;
                    storeItems.Add(storeItem);
                }
            }

             vfBattleTechMod_Core.Utils.Loggers.NonSillyLogging.LogDebug($"Store item definitions from [{sourceExcelFilePath}] loaded. Count = [{storeItems.Count}]");

            return storeItems;
        }
    }
}