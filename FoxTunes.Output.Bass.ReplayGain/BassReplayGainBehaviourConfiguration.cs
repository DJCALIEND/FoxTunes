﻿using System.Collections.Generic;

namespace FoxTunes
{
    public static class BassReplayGainBehaviourConfiguration
    {
        public const string ENABLED = "AAAA1379-60E3-426D-9CF0-61F11343A627";

        public const string ON_DEMAND = "AABB8343-832C-4B99-A34C-8D9475D56722";

        public const string MODE = "BBBB9149-D485-45F3-A505-750774C06D0D";

        public const string MODE_ALBUM = "AAAAF2BB-3753-48A2-A73F-EA46EAA0E91E";

        public const string MODE_TRACK = "BBBBEEB5-A4EC-45A0-8717-E7E8F1EF457E";

        public static IEnumerable<ConfigurationSection> GetConfigurationSections()
        {
            var section = new ConfigurationSection(BassOutputConfiguration.SECTION, "Output")
                .WithElement(new BooleanConfigurationElement(ENABLED, "Enabled", path: "Replay Gain").WithValue(false))
                .WithElement(new SelectionConfigurationElement(MODE, "Mode", path: "Replay Gain").WithOptions(GetModeOptions()).DependsOn(BassOutputConfiguration.SECTION, ENABLED))
                .WithElement(new BooleanConfigurationElement(ON_DEMAND, "On Demand", path: "Replay Gain").WithValue(false).DependsOn(BassOutputConfiguration.SECTION, ENABLED)
            );
            yield return section;
        }

        private static IEnumerable<SelectionConfigurationOption> GetModeOptions()
        {
            yield return new SelectionConfigurationOption(MODE_ALBUM, "Prefer Album").Default();
            yield return new SelectionConfigurationOption(MODE_TRACK, "Prefer Track");
        }

        public static ReplayGainMode GetMode(SelectionConfigurationOption option)
        {
            switch (option.Id)
            {
                default:
                case MODE_ALBUM:
                    return ReplayGainMode.Album;
                case MODE_TRACK:
                    return ReplayGainMode.Track;
            }
        }
    }

    public enum ReplayGainMode : byte
    {
        None,
        Album,
        Track
    }
}
