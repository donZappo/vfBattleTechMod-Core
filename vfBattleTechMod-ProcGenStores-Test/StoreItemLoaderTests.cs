using NUnit.Framework;
using vfBattleTechMod_Core.Helpers;
using vfBattleTechMod_Core.Utils;
using vfBattleTechMod_Core.Utils.Loggers;
using vfBattleTechMod_ProcGenStores.Mod.Features.ProcGenStoresContent;
using vfBattleTechMod_ProcGenStores.Mod.Features.ProcGenStoresContent.Logic;

namespace vfBattleTechMod_ProcGenStores_Test
{
    [TestFixture]
    public class StoreItemLoaderTests
    {
        [Test]
        public void TestStoreItemLoaderCanLoadCorrectSourceFile()
        {
            var sourceFile = TestContext.CurrentContext.TestDirectory + @"/res/xlrp-store-content.xlsx";
            var settings =
                JsonHelpers.DeserializeFile(TestContext.CurrentContext.TestDirectory + @"/res/test-settings.json");
            var procGenSettings = settings["Procedurally Generate Store Contents"]
                .ToObject<ProcGenStoreContentFeatureSettings>();
            var storeItems = StoreItemLoader.LoadStoreItemsFromExcel(sourceFile, procGenSettings.RarityBrackets,
                ProcGenStoreContentFeature.BattleTechStoreResourceTypes);
        }
    }
}