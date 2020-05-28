using System;
using vfBattleTechMod_Core.Utils.Interfaces;

namespace vfBattleTechMod_Core.Helpers
{
    public static class AppearanceUtils
    {
        public static double CalculateAppearanceDateFactor(DateTime gameStartDate, DateTime factorControlDate, DateTime factorTargetDate, ILogger logger)
        {
            logger.Debug($"Calculating appearance date factor...");
            var uncompressedDaysDifference = Convert.ToDouble(factorControlDate.Subtract(gameStartDate).Days + 1);
            logger.Debug($"Uncompressed days difference = [{uncompressedDaysDifference}]...");
            var compressedDaysDifference = Convert.ToDouble(gameStartDate.Subtract(factorTargetDate).Days + 1);
            logger.Debug($"Compressed days difference = [{compressedDaysDifference}]...");
            var compressionPercentage = uncompressedDaysDifference / compressedDaysDifference;
            logger.Debug($"returning Compression percentage = [{compressionPercentage}]...");
            return compressionPercentage;
        }

        public static DateTime CalculateCompressedAppearanceDate(DateTime gameStartDate, DateTime appearanceDate,
            double compressionFactor, bool useTimeAccFactor, double timeAccelerationFactor, ILogger logger)
        {
            logger.Trace($"Calculating compressed appearance date for game start date = [{gameStartDate}], appearance date = [{appearanceDate}], using factor = [{compressionFactor}]...");
            if (appearanceDate <= gameStartDate)
            {
                logger.Trace($"Appearance date [{appearanceDate}] < Game Start Date = [{gameStartDate}], return raw appearance date.");
                return appearanceDate;
            }

            if (useTimeAccFactor)
                compressionFactor = 1 / timeAccelerationFactor;

            var actualDaysUntilAppearance = appearanceDate.Subtract(gameStartDate).Days;
            logger.Debug($"Actual days until appearance = [{actualDaysUntilAppearance}]...");
            var compressedDaysUntilAppearance = actualDaysUntilAppearance * compressionFactor;
            logger.Debug($"Compressed days until appearance = [{compressedDaysUntilAppearance}]...");
            var compressedAppearanceDate = gameStartDate.AddDays(compressedDaysUntilAppearance);
            logger.Debug($"Returning Compressed appearance date = [{compressedAppearanceDate}].");
            return compressedAppearanceDate;
        }
    }
}