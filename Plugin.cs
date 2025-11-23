using System.IO;
using System.Reflection;
using BepInEx.Logging;
using CurrencyPocket.Compatibility;

namespace CurrencyPocket;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class CurrencyPocketPlugin : BaseUnityPlugin
{
    internal const string ModName = "CurrencyPocket";
    internal const string ModVersion = "1.0.11";
    internal const string Author = "Azumatt";
    private const string ModGUID = $"{Author}.{ModName}";
    internal readonly Harmony _harmony = new(ModGUID);
    public static readonly ManualLogSource CurrencyPocketLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
    internal static Sprite DownloadSprite = null!;
    public static CurrencyPocketPlugin instance = null!;

    public void Awake()
    {
        instance = this;
        Assembly assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
        DownloadSprite = loadSprite("download.png");
    }

    public void Start()
    {
        RapidLoadoutsCompat.Init();
        ExtraSlots.FuckOff();
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
}