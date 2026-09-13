using System.Collections.Generic;

namespace SideQuest.World
{
    // The §20 MVP town as data. Adding a location means adding an entry here — no logic changes.
    // Opening hours and capability flags are not in the design doc; they're flagged in STATUS.md.
    public static class MvpTownData
    {
        public const string CornerDiner = "loc_corner_diner";
        public const string GeneralStore = "loc_general_store";
        public const string AutoShop = "loc_auto_shop";
        public const string PoliceStation = "loc_police_station";
        public const string ApartmentComplex = "loc_apartment_complex";
        public const string TownSquare = "loc_town_square";

        public static List<Location> CreateLocations() => new List<Location>
        {
            new Location { Id = CornerDiner, DisplayName = "Corner Diner", Hours = OpeningHours.Between(6, 22),
                IsWorkplace = true, ServesFood = true, IsSocialVenue = true },
            new Location { Id = GeneralStore, DisplayName = "General Store", Hours = OpeningHours.Between(8, 20),
                IsWorkplace = true, ServesFood = true },
            new Location { Id = AutoShop, DisplayName = "Auto Shop", Hours = OpeningHours.Between(8, 18),
                IsWorkplace = true },
            new Location { Id = PoliceStation, DisplayName = "Police Station", Hours = OpeningHours.Always,
                IsWorkplace = true },
            new Location { Id = ApartmentComplex, DisplayName = "Apartment Complex", Hours = OpeningHours.Always,
                ServesFood = true, IsResidence = true },
            new Location { Id = TownSquare, DisplayName = "Town Square", Hours = OpeningHours.Always,
                IsSocialVenue = true },
        };

        public static LocationRegistry CreateRegistry() => new LocationRegistry(CreateLocations());
    }
}
