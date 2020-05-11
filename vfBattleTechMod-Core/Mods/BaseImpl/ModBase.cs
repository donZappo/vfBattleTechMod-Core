using System.Collections.Generic;
using Harmony;
using HBS.Logging;
using vfBattleTechMod_Core.Helpers;
using vfBattleTechMod_Core.Mods.Interfaces;
using vfBattleTechMod_Core.Utils;
using vfBattleTechMod_Core.Utils.Interfaces;
using vfBattleTechMod_Core.Utils.Loggers;

namespace vfBattleTechMod_Core.Mods.BaseImpl
{
    public abstract class ModBase<TModSettings> : IBattleTechMod<TModSettings> where TModSettings : IModSettings, new()
    {
        protected ModBase(
            HarmonyInstance harmonyInstance,
            string directory,
            string settings,
            string name,
            List<IModFeature<IModFeatureSettings>> modFeatures)
        {
            Name = name;
            Directory = directory;
            ModFeatures = modFeatures;
            InitialiseLogging(directory);
            Logger.Debug($"Booting up [{Name}] at[{Directory}], with settings \r\n{settings}]...");
            Logger.Debug($"Default settings = [{GenerateDefaultModSettings()}]");
            Initialize(harmonyInstance, settings);
        }

        public string Directory { get; }

        public ILogger Logger { get; private set; }

        public List<IModFeature<IModFeatureSettings>> ModFeatures { get; }

        public string Name { get; }

        public string GenerateDefaultModSettings()
        {
            Logger.Debug("Generating default settings...");
            var settingsList = new List<string>();
            settingsList.Add(new TModSettings().Serialize());
            ModFeatures.ForEach(feature => settingsList.Add(feature.DefaultSettings.Serialize()));
            return settingsList.Join();
        }

        public TModSettings ModSettings { get; private set; }

        private ILog GetHbsLogger()
        {
            var hbsLogger = HBS.Logging.Logger.GetLogger(Name, LogLevel.Debug);
            HBS.Logging.Logger.SetLoggerLevel(Name, LogLevel.Debug);
            return hbsLogger;
        }

        private void InitialiseLogging(string directory)
        {
            //Logger = new log4NetLogger(Name);
            Logger = new HbsLogger(GetHbsLogger(), Directory, Name);
            //Logger.Debug($"Initialized logging for [{Name}]");
        }

        private void Initialize(HarmonyInstance harmonyInstance, string settings)
        {
            Logger.Debug("Initializing Features...");
            ModSettings = JsonHelpers.Deserialize<TModSettings>(settings) ?? new TModSettings();
            Logger.LogLevel = ModSettings.LogLevel;
            var jsonSettings = JsonHelpers.Deserialize(settings);
            ModFeatures.ForEach(
                feature => feature.Initialize(
                    harmonyInstance,
                    jsonSettings?[feature.Name]?.ToString(),
                    Logger,
                    Directory));
            Logger.Debug("Feature initialization complete.");
        }
    }
}