using System;
using vfBattleTechMod_Core.Mods.BaseImpl;

namespace vfBattleTechMod_ContractSpawnMorphs.Mod.Features.UnitAppearanceDateMorphs
{
    public class UnitAppearanceDateMorphFeatureSettings : ModFeatureSettingsBase
    {
        public Double TimeAccelerationFactor { get; set; } = 3;
        public bool UseTimeAccelerationFactor { get; set; } = true;
        public DateTime CompressionFactorControlDate { get; set; } =
            new DateTime(3029, 1, 1); // Helm Memory Core Recovery

        public DateTime CompressionFactorTargetDate { get; set; } =
            new DateTime(3026, 1, 1); // One year from standard game start

        public bool SetAppearanceDatesForMechsLackingSuch { get; set; } = true;
    }
}