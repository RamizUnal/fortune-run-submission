using System;

namespace Vertigo.Fortune.Domain
{
    public enum ZoneKind { Bronze, Silver, Golden }

    public enum RunState { Ready, Spinning, RewardPending, Failed, CashedOut }

    /// <summary>The single source of truth for zone progression and safety.</summary>
    public static class ZoneRules
    {
        public const int SliceCount = 8;
        public const int SafeZoneInterval = 5;
        public const int SuperZoneInterval = 30;

        public static ZoneKind KindFor(int zone)
        {
            ValidateZone(zone);
            if (zone % SuperZoneInterval == 0) return ZoneKind.Golden;
            return zone % SafeZoneInterval == 0 ? ZoneKind.Silver : ZoneKind.Bronze;
        }

        public static bool IsSafe(int zone)
        {
            return KindFor(zone) != ZoneKind.Bronze;
        }

        /// <summary>Returns the next safe zone strictly after the given zone.</summary>
        public static int NextSafeZone(int zone)
        {
            ValidateZone(zone);
            return checked(zone + SafeZoneInterval - zone % SafeZoneInterval);
        }

        /// <summary>A linear baseline; content providers may supply a different growth curve.</summary>
        public static int RewardMultiplier(int zone)
        {
            ValidateZone(zone);
            return zone;
        }

        private static void ValidateZone(int zone)
        {
            if (zone < 1) throw new ArgumentOutOfRangeException(nameof(zone), "Zones start at one.");
        }
    }
}
