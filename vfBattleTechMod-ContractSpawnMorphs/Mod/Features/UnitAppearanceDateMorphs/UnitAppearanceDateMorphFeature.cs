using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using Harmony;
using vfBattleTechMod_ContractSpawnMorphs.Mod.Features.UnitSpawnMorph;
using vfBattleTechMod_Core.Helpers;
using vfBattleTechMod_Core.Mods;
using vfBattleTechMod_Core.Mods.BaseImpl;
using vfBattleTechMod_Core.Mods.Interfaces;

namespace vfBattleTechMod_ContractSpawnMorphs.Mod.Features.UnitAppearanceDateMorphs
{
    public class UnitAppearanceDateMorphFeature : ModFeatureBase<UnitAppearanceDateMorphFeatureSettings>
    {
        private new static UnitAppearanceDateMorphFeature Myself;

        public UnitAppearanceDateMorphFeature()
            : base(GetPatchDirectives)
        {
            Myself = this;
        }

        public static List<IModPatchDirective> GetPatchDirectives =>
            new List<IModPatchDirective>
            {
                new ModPatchDirective(
                    AccessTools.Method(typeof(SimGameState), "_OnDefsLoadComplete"),
                    null,
                    typeof(UnitAppearanceDateMorphFeature).GetMethod("SimGameState__OnDefsLoadComplete_Postfix"),
                    null,
                    Harmony.Priority.High)
            };

        public override string Name => "UnitAppearanceDateMorphFeature";

        public static void SimGameState__OnDefsLoadComplete_Postfix(SimGameState __instance)
        {
            if (UnitAppearanceDateMorphFeature.AppearanceDatesAdjusted == true)
            {
                Logger.Debug($"Appearance dates have already been adjusted.");
                return;
            }

            if (Myself.Settings.SetAppearanceDatesForMechsLackingSuch)
            {
                Logger.Debug($"Setting appearance dates for mechs lacking...");
                var mechAppearanceData = CoreMod.CoreModSingleton.MechAppearanceData;
                var mechsSansAppearanceDates =
                    __instance.DataManager.MechDefs
                        .Where(pair => !pair.Value.MinAppearanceDate.HasValue)
                        .Select(pair => pair.Value);
                
                mechsSansAppearanceDates
                    //.AsParallel()
                    //.ForAll
                    .ToList()
                    .ForEach
                    (mechDef =>
                {
                    Logger.Trace($"Mech [{mechDef.Description.UIName}] lacks an appearance date, attempting to set it...");
                    var mechModelEntry = mechAppearanceData.FirstOrDefault(model =>
                        model.Name.Trim('"') == mechDef.Description.UIName);
                    DateTime? appearanceDate = null; 
                    if (mechModelEntry != null)
                    {
                        appearanceDate = new DateTime(mechModelEntry.Year, 1, 1);
                    }

                    appearanceDate = mechDef.MinAppearanceDate ?? appearanceDate;
                    Logger.Trace($"Setting appearance date for [{mechDef.Description.UIName}] to [{appearanceDate}].");
                    var traverse = Traverse.Create(mechDef).Property("MinAppearanceDate");
                    traverse.SetValue(appearanceDate);
                });
            }
            
            
            Logger.Debug("Dynamically adjusting appearance dates...");
            var appearanceFactor = AppearanceUtils.CalculateAppearanceDateFactor(__instance.GetCampaignStartDate(),
                Myself.Settings.CompressionFactorControlDate,
                Myself.Settings.CompressionFactorTargetDate, Logger);
            
            __instance.DataManager.MechDefs
                .Where(pair => pair.Value.MinAppearanceDate.HasValue)
                .Select(pair => pair.Value)
                .ToList()
                .ForEach
                //.AsParallel()
                //.ForAll
                (mechDef =>
                {
                    Logger.Trace($"Calculating new appearance date for [{mechDef.Description.Id}]...");
                    var appearanceDate = mechDef.MinAppearanceDate;
                    var newAppearanceDate =
                        AppearanceUtils.CalculateCompressedAppearanceDate(__instance.GetCampaignStartDate(),
                            appearanceDate.Value, appearanceFactor, Myself.Settings.UseTimeAccelerationFactor, Myself.Settings.TimeAccelerationFactor, Logger);
                    Logger.Trace($"Setting appearance date for [{mechDef.Description.Id}] to [{newAppearanceDate}] from [{appearanceDate}]...");
                    // mechDef.MinAppearanceDate = newAppearanceDate;
                    var traverse = Traverse.Create(mechDef).Property("MinAppearanceDate");
                    traverse.SetValue(newAppearanceDate);
                });

            UnitAppearanceDateMorphFeature.AppearanceDatesAdjusted = true;
        }

        public static bool AppearanceDatesAdjusted { get; set; } = false;

        protected override bool ValidateSettings()
        {
            return true;
        }

        public override void OnInitializeComplete()
        {
        }
    }
}