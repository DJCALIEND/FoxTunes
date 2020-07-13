﻿using System.Collections.Generic;

namespace FoxTunes
{
    public static class PlaylistBehaviourConfiguration
    {
        public const string SECTION = "11FAE8A9-8DF4-4DD5-B0C7-DFFCBABDC04A";

        public const string ORDER_ELEMENT = "E666183E-486E-45B4-A7CB-CE225AB89A1F";

        public const string ORDER_DEFAULT_OPTION = "AAAA3B37-7DD2-43BF-9917-8A11158E8DC3";

        public const string ORDER_SHUFFLE_TRACKS = "BBBB239D-7B39-40AD-B74D-B165358BE9DD";

        public const string ORDER_SHUFFLE_ALBUMS = "CCCC7619-11AB-45E6-99F5-23A513DEDFE3";

        public const string ORDER_SHUFFLE_ARTISTS = "DDDD150A-BE60-47EA-8C05-57219126BF5A";

        public static IEnumerable<ConfigurationSection> GetConfigurationSections()
        {
            yield return new ConfigurationSection(SECTION, "Playlist")
                .WithElement(
                    new SelectionConfigurationElement(ORDER_ELEMENT, "Order").WithOptions(GetOrderOptions())
            );
        }

        private static IEnumerable<SelectionConfigurationOption> GetOrderOptions()
        {
            yield return new SelectionConfigurationOption(ORDER_DEFAULT_OPTION, "Default");
            yield return new SelectionConfigurationOption(ORDER_SHUFFLE_TRACKS, "Shuffle (Tracks)");
            yield return new SelectionConfigurationOption(ORDER_SHUFFLE_ALBUMS, "Shuffle (Albums)");
            yield return new SelectionConfigurationOption(ORDER_SHUFFLE_ARTISTS, "Shuffle (Artists)");
        }
    }
}
