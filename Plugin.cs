using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;

namespace CurrencyPocket
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class CurrencyPocketPlugin : BaseUnityPlugin
    {
        internal const string ModName = "CurrencyPocket";
        internal const string ModVersion = "1.0.1";
        internal const string Author = "Azumatt";
        private const string ModGUID = $"{Author}.{ModName}";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource CurrencyPocketLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        internal static Sprite DownloadSprite = null!;

        public void Awake()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            DownloadSprite = loadSprite("download.png");
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
}