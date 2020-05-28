using System;
using System.Collections.Generic;
using System.Linq;
using BattleTech;
using HBS.Collections;

namespace vfBattleTechMod_ProcGenStores.Mod.Features.ProcGenStoresContent.Logic
{
    public class ProcGenStoreItem
    {
        public ProcGenStoreItem(BattleTechResourceType type, string id, DateTime? appearanceDate, TagSet tagSet,
            ProcGenStoreContentFeatureSettings.RarityBracket rarityBracket, List<string> requiredTags,
            List<string> restrictedTags, bool descriptionPurchasable)
        {
            Type = type;
            Id = id;
            MinAppearanceDate = appearanceDate;
            TagSet = tagSet;
            RarityBracket = rarityBracket;
            RequiredTags = requiredTags;
            RestrictedTags = restrictedTags;
            Purchasable = descriptionPurchasable;
        }

        public BattleTechResourceType Type { get; set; }
        public string Id { get; set; }
        public TagSet TagSet { get; set; }
        public ProcGenStoreContentFeatureSettings.RarityBracket RarityBracket { get; set; }

        public List<string> RequiredTags { get; set; }

        public List<string> RestrictedTags { get; set; }
        public DateTime? MinAppearanceDate { get; set; }
        public int Quantity { get; set; }
        public bool Purchasable { get; set; }

        public (bool result, int bracketBonus) IsValidForAppearance(DateTime currentDate, string ownerValueName,
            Shop.ShopType shopType,
            List<string> planetTags,
            TagSet itemTags,
            ProcGenStoreContentFeatureSettings settings)
        {
            var sim = UnityGameInstance.BattleTechGame.Simulation;
            // Check tags...
            if (RequiredTags.Any())
            {
                // Check at least one required tag is present...
                // Unless we're populating a black market, and they're configured to circumvent required restrictions...
                if (RequiredTags.Any() && !planetTags.Any(s => RequiredTags.Contains(s)) &&
                    !(shopType == Shop.ShopType.BlackMarket &&
                      settings.BlackMarketSettings.CircumventRequiredPlanetTags))
                {
                    return (false, 0);
                }
            }

            if (RestrictedTags.Any() && planetTags.Any(s => RestrictedTags.Contains(s)) &&
                !(shopType == Shop.ShopType.BlackMarket && settings.BlackMarketSettings.CircumventRestrictedPlanetTags))
            {
                return (false, 0);
            }

            if (MinAppearanceDate.HasValue && MinAppearanceDate > currentDate)
            {
                return (false, 0);
            }

            if (!Purchasable)
            {
                return (false, 0);
            }

            if (itemTags.Contains("TechLevel_LowTech"))
            {
                var days = (double)sim.CurrentDate.Subtract(sim.GetCampaignStartDate()).Days;
                var totalDays = (double)sim.Constants.CareerMode.GameLength;
                int rarityBonus = Math.Min((int) (7 * Math.Round(days / totalDays)), 7);
                return (true, rarityBonus);
            }

            if (itemTags.Contains("TechLevel_MidTech"))
            {
                var days = (double)sim.CurrentDate.Subtract(sim.GetCampaignStartDate()).Days;
                var totalDays = (double)sim.Constants.CareerMode.GameLength;
                int rarityBonus = Math.Min((int)(6 * Math.Round(days / totalDays)), 6);
                return (true, rarityBonus);
            }

            if (itemTags.Contains("TechLevel_HighTech"))
            {
                var days = (double)sim.CurrentDate.Subtract(sim.GetCampaignStartDate()).Days;
                var totalDays = (double)sim.Constants.CareerMode.GameLength;
                int rarityBonus = Math.Min((int)(5 * Math.Round(days / totalDays)), 5);
                return (true, rarityBonus);
            }

            return (true, 0);
        }
    }
}