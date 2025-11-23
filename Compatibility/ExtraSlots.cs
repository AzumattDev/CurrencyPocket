using System;
using System.Collections.Generic;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace CurrencyPocket.Compatibility;

internal static class ExtraSlotsCompat
{

    // guard to prevent recursion
    private static bool _handlingChange;

    public static void FuckOff()
    {
        if (!Chainloader.PluginInfos.TryGetValue(ExtraSlotsGuid, out PluginInfo? extraSlotsInfo))
            return;

        ConfigFile cfg = extraSlotsInfo.Instance.Config;
        bool changed = false;

        // Since he cannot count and most users probably won't...strip CoinPocketUI when the dev/user “overfills” the list
        // To not be a part of the problem, I'm stripping mine out.
        changed |= AttachSanitizer(cfg, "Panels to show with 1 row", maxAllowedItems: 1);
        changed |= AttachSanitizer(cfg, "Panels to show with 2 rows", maxAllowedItems: 2);
        changed |= AttachSanitizer(cfg, "Panels to show with 3 rows", maxAllowedItems: 3);

        if (changed)
            cfg.Save();
    }


    private static bool AttachSanitizer(ConfigFile cfg, string key, int? maxAllowedItems)
    {
        if (!cfg.TryGetEntry(EsSectionName, key, out ConfigEntry<string>? entry))
            return false;

        bool changed = SanitizeEntry(entry, maxAllowedItems);

        // Hook for future changes
        entry.SettingChanged += (_, _) =>
        {
            if (_handlingChange)
                return;

            try
            {
                _handlingChange = true;
                SanitizeEntry(entry, maxAllowedItems);
            }
            finally
            {
                _handlingChange = false;
            }
        };

        return changed;
    }

    private static bool SanitizeEntry(ConfigEntry<string> entry, int? maxAllowedItems)
    {
        string oldValue = entry.Value ?? string.Empty;

        // For the 1-row case: if the list is literally just "CoinPocketUI", we allow it.
        if (maxAllowedItems == 1 && oldValue.Trim().Equals(CoinPocketUIName, StringComparison.OrdinalIgnoreCase))
            return false;

        string newValue = SanitizeCsv(oldValue, CoinPocketUIName, maxAllowedItems);

        if (newValue == oldValue)
            return false;

        entry.Value = newValue;
        return true;
    }

    private static string SanitizeCsv(string csv, string targetEntry, int? maxAllowedItems)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return string.Empty;

        string[] tokens = csv.Split(',');

        // Count non-empty entries
        int entryCount = 0;
        for (int i = 0; i < tokens.Length; ++i)
        {
            if (!string.IsNullOrWhiteSpace(tokens[i]))
                entryCount++;
        }

        // If we only strip when "too many" items and we aren't over the limit, do nothing.
        if (entryCount <= maxAllowedItems)
            return csv;

        List<string> kept = new List<string>(tokens.Length);
        bool removedAny = false;

        foreach (string raw in tokens)
        {
            string trimmed = raw.Trim();

            if (trimmed.Equals(targetEntry, StringComparison.OrdinalIgnoreCase))
            {
                removedAny = true;
                continue;
            }

            kept.Add(raw);
        }

        if (!removedAny)
            return csv;

        return kept.Count == 0 ? string.Empty : string.Join(",", kept);
    }
}