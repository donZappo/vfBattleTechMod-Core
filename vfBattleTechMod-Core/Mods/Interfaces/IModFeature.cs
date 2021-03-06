﻿using System.Collections.Generic;
using Harmony;
using vfBattleTechMod_Core.Utils.Interfaces;

namespace vfBattleTechMod_Core.Mods.Interfaces
{
    public interface IModFeature<out TModFeatureSettings>
        where TModFeatureSettings : IModFeatureSettings
    {
        bool Enabled { get; }

        string Name { get; }

        List<IModPatchDirective> PatchDirectives { get; }

        TModFeatureSettings Settings { get; }

        TModFeatureSettings DefaultSettings { get; }

        void Initialize(HarmonyInstance harmonyInstance, string settings, ILogger logger, string directory);

        void OnInitializeComplete();
    }
}