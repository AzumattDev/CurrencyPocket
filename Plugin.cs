using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using BepInEx.Logging;
using CurrencyPocket.Compatibility;
using JetBrains.Annotations;
using ServerSync;

namespace CurrencyPocket;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class CurrencyPocketPlugin : BaseUnityPlugin
{
    internal const string ModName = "CurrencyPocket";
    internal const string ModVersion = "1.0.13";
    internal const string Author = "Azumatt";
    private const string ModGUID = $"{Author}.{ModName}";
    private static string ConfigFileName = $"{ModGUID}.cfg";
    private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
    internal readonly Harmony _harmony = new(ModGUID);
    public static readonly ManualLogSource CurrencyPocketLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
    internal static Sprite DownloadSprite = null!;
    public static CurrencyPocketPlugin instance = null!;
    private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion, ModRequired = false};
    private FileSystemWatcher _watcher;
    private readonly object _reloadLock = new();
    private DateTime _lastConfigReloadTime;
    private const long RELOAD_DELAY = 10000000; // One second


    private static ConfigEntry<Toggle> _serverConfigLocked = null!;
    internal static ConfigEntry<bool> AllowValuableItems = null!;
    internal static ConfigEntry<string> AllowedValuablePrefabs = null!;

    public enum Toggle
    {
        On = 1,
        Off = 0
    }

    public void Awake()
    {
        bool saveOnSet = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;

        instance = this;

        _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On, "If on, the configuration is locked and can be changed by server admins only.");
        _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
        AllowValuableItems = Config.Bind("1 - General", "AllowValuableItems", true, "If enabled, items with a value greater than 0 can be converted to coins when dropped into the pocket.");
        AllowedValuablePrefabs = Config.Bind("1 - General", "AllowedValuablePrefabs", "", "Comma-separated list of prefab names that are allowed to be converted to coins (e.g., 'Ruby,Amber,AmberPearl'). If empty, all valuable items are allowed when AllowValuableItems is enabled.");

        Assembly assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
        SetupWatcher();

        DownloadSprite = loadSprite("download.png");

        Config.Save();
        if (saveOnSet)
        {
            Config.SaveOnConfigSet = saveOnSet;
        }
    }

    public void Start()
    {
        RapidLoadoutsCompat.Init();
        ExtraSlotsCompat.FuckOff();
    }

    private void OnDestroy()
    {
        SaveWithRespectToConfigSet();
        _watcher?.Dispose();
    }

    private void SetupWatcher()
    {
        _watcher = new FileSystemWatcher(Paths.ConfigPath, ConfigFileName);
        _watcher.Changed += ReadConfigValues;
        _watcher.Created += ReadConfigValues;
        _watcher.Renamed += ReadConfigValues;
        _watcher.IncludeSubdirectories = true;
        _watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        _watcher.EnableRaisingEvents = true;
    }

    private void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        DateTime now = DateTime.Now;
        long time = now.Ticks - _lastConfigReloadTime.Ticks;
        if (time < RELOAD_DELAY)
        {
            return;
        }

        lock (_reloadLock)
        {
            if (!File.Exists(ConfigFileFullPath))
            {
                CurrencyPocketLogger.LogWarning("Config file does not exist. Skipping reload.");
                return;
            }

            try
            {
                CurrencyPocketLogger.LogDebug("Reloading configuration...");
                SaveWithRespectToConfigSet(true);
                CurrencyPocketLogger.LogInfo("Configuration reload complete.");
            }
            catch (Exception ex)
            {
                CurrencyPocketLogger.LogError($"Error reloading configuration: {ex.Message}");
            }
        }

        _lastConfigReloadTime = now;
    }

    private void SaveWithRespectToConfigSet(bool reload = false)
    {
        bool originalSaveOnSet = Config.SaveOnConfigSet;
        Config.SaveOnConfigSet = false;
        if (reload)
            Config.Reload();
        Config.Save();
        if (originalSaveOnSet)
        {
            Config.SaveOnConfigSet = originalSaveOnSet;
        }
    }


    private static byte[] ReadEmbeddedFileBytes(string name)
    {
        using MemoryStream stream = new();
        Assembly.GetExecutingAssembly().GetManifestResourceStream(Assembly.GetExecutingAssembly().GetName().Name + "." + name)!.CopyTo(stream);
        return stream.ToArray();
    }

    private static Texture2D loadTexture(string name)
    {
        Texture2D texture = new(0, 0);
        texture.LoadImage(ReadEmbeddedFileBytes("assets." + name));

        return texture!;
    }

    internal static Sprite loadSprite(string name)
    {
        Texture2D texture = loadTexture(name);
        return texture != null ? Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero) : null!;
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription = new(description.Description + (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"), description.AcceptableValues, description.Tags);
        ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
        //var configEntry = Config.Bind(group, name, value, description);

        SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true)
    {
        return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }

    private class ConfigurationManagerAttributes
    {
        [UsedImplicitly] public int? Order = null!;
        [UsedImplicitly] public bool? Browsable = null!;
        [UsedImplicitly] public string? Category = null!;
        [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer = null!;
    }
}

public struct Constants
{
    public const string CoinPocketUIName = "CoinPocketUI";
    public const string ExtractCoinsButtonName = "ExtractCoinsButton";
    public const string ArmorName = "Armor";
    public const string WeightName = "Weight";
    public const string JewelcraftingSynergyName = "Jewelcrafting Synergy";
    public const string TrashButtonName = "Trash";
    public const string FavoritingToggleButton = "favoritingTogglingButton";
    public const string QuickStackAreaButton = "quickStackAreaButton";
    public const string RestockAreaButton = "restockAreaButton";
    public const string SortInventoryButton = "sortInventoryButton";
    internal const string CoinCountCustomData = "CoinPocket_CoinCount";
    internal const string CoinIconName = "CoinIcon";
    internal const string CoinToken = "$item_coins";
    internal const string CoinsPrefabName = "Coins";
    internal const string AcText = "ac_text";
    internal const string ArmorIconName = "armor_icon";

    // GUIDS
    internal const string RandyQuickslots = "randyknapp.mods.equipmentandquickslots";
    internal const string AzuEPIGUID = "Azumatt.AzuExtendedPlayerInventory";
    internal const string QuickStackStoreGUID = "goldenrevolver.quick_stack_store";
    internal const string JewelcraftingGUID = "org.bepinex.plugins.jewelcrafting";
    internal const string RapidLoadoutsGUID = "Azumatt.RapidLoadouts";
    internal const string ExtraSlotsGuid = "shudnal.ExtraSlots";
    internal const string EsSectionName = "Mods compatibility - Reduced inventory size";
}