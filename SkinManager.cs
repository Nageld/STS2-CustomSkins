using System.Collections.Generic;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;

namespace MPSkins;

public static class SkinManager
{
    // TODO Make these customizable
    public static readonly (string Name, float Hue)[] TintSkins =
    {
        ("Default",  0.00f),
        ("Yellow",   0.12f),
        ("Green",    0.25f),
        ("Teal",     0.42f),
        ("Blue",     0.65f),
        ("Pink",     0.88f),
    };

    private static readonly Dictionary<string, List<string>> _charTextureSkins = new();
    private static readonly Dictionary<(string charId, string name), string> _skinFilePaths = new();
    private static readonly Dictionary<(string charId, string name), Texture2D> _textureCache = new();

    private static readonly Dictionary<ulong, string> _playerSkinNames = new();
    private static string _resolvedSkinName = "Default";

    public static object? CurrentLobby { get; set; }
    public static INetGameService? NetService { get; set; }
    public static ulong LocalPlayerId { get; set; }

    public static string? CurrentCharacterId { get; set; }

    public static string LocalSkinName { get; set; } = "Default";

    public static string ResolvedSkinName => _resolvedSkinName;

    public static string BroadcastSkinName =>
        LocalSkinName == "Random" ? "Default" : LocalSkinName;

    public static void ResolveLocalSkin(string? characterId)
    {
        if (LocalSkinName != "Random")
        {
            _resolvedSkinName = LocalSkinName;
            return;
        }

        var pool = GetAvailableSkins(characterId);
        pool.Remove("Random");
        _resolvedSkinName = pool.Count > 0 ? pool[(int)GD.RandRange(0, pool.Count - 1)] : "Default";
    }

    /// <summary>Scan {modDir}/skins/{characterId}/*.png and register all found skins.</summary>
    // TODO: Add support for FileSystemWatcher to detect new skins added while the game is running.
    public static void LoadSkinsFromFolder(string modDir)
    {
        string skinsRoot = Path.Combine(modDir, "skins");
        if (!Directory.Exists(skinsRoot)) return;

        foreach (string charDir in Directory.GetDirectories(skinsRoot))
        {
            string characterId = Path.GetFileName(charDir).ToLower();
            var names = new List<string>();

            foreach (string file in Directory.GetFiles(charDir, "*.png"))
            {
                string skinName = Path.GetFileNameWithoutExtension(file);
                names.Add(skinName);
                _skinFilePaths[(characterId, skinName)] = file;
            }

            if (names.Count > 0)
                _charTextureSkins[characterId] = names;
        }
    }

    public static void Reset()
    {
        _playerSkinNames.Clear();
    }

    /// <summary>All skin names available for a given character.</summary>
    public static List<string> GetAvailableSkins(string? characterId)
    {
        var list = new List<string>(TintSkins.Length);
        foreach (var (name, _) in TintSkins)
            list.Add(name);

        string? charIdLower = characterId?.ToLower();
        _charTextureSkins.TryGetValue(charIdLower ?? "", out var texNames);
        if (texNames != null)
            list.AddRange(texNames);

        list.Add("Random");
        return list;
    }

    public static bool IsTintSkin(string skinName)
    {
        foreach (var (name, _) in TintSkins)
            if (name == skinName) return true;
        return false;
    }

    public static float GetHueForSkin(string skinName)
    {
        foreach (var (name, hue) in TintSkins)
            if (name == skinName) return hue;
        return 0f;
    }

    /// <summary>Loads the replacement atlas texture for a texture skin.</summary>
    public static Texture2D? GetTextureForSkin(string characterId, string skinName)
    {
        var key = (characterId.ToLower(), skinName);
        if (_textureCache.TryGetValue(key, out var cached)) return cached;
        if (!_skinFilePaths.TryGetValue(key, out string? path)) return null;

        var image = Image.LoadFromFile(path);
        if (image == null) return null;

        var texture = ImageTexture.CreateFromImage(image);
        _textureCache[key] = texture;
        return texture;
    }

    public static void SetPlayerSkinName(ulong playerId, string skinName) =>
        _playerSkinNames[playerId] = skinName;

    public static string GetPlayerSkinName(ulong playerId) =>
        _playerSkinNames.TryGetValue(playerId, out string? name) ? name : "Default";

    public static Color GetSkinColor(string skinName)
    {
        if (skinName == "Random")
            return new Color(0.5f, 0.5f, 0.5f, 1f);
        if (IsTintSkin(skinName))
        {
            float hue = GetHueForSkin(skinName);
            return hue == 0f ? Colors.White : Color.FromHsv(hue, 0.85f, 1.0f);
        }
        return new Color(0.7f, 0.7f, 0.7f, 1f);
    }
}
