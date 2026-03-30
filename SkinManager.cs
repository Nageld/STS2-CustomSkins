using System.Collections.Concurrent;
using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using Timer = System.Threading.Timer;

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

    private static string? _skinsRoot;
    private static FileSystemWatcher? _watcher;
    private static Timer? _debounceTimer;
    private static readonly ConcurrentDictionary<string, byte> _pendingReloads = new();

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
    public static void LoadSkinsFromFolder(string modDir)
    {
        _skinsRoot = Path.Combine(modDir, "skins");
        if (!Directory.Exists(_skinsRoot)) return;
        string skinsRoot = _skinsRoot;

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

    /// <summary>
    /// Watches the skins folder for PNG changes.
    /// </summary>
    public static void StartFileWatcher()
    {
        if (_skinsRoot == null || !Directory.Exists(_skinsRoot)) return;

        _debounceTimer = new Timer(_ =>
        {
            var paths = new List<string>(_pendingReloads.Keys);
            _pendingReloads.Clear();
            Callable.From(() =>
            {
                foreach (string path in paths)
                {
                    try { ProcessSkinFileChange(path); }
                    catch {  }
                }
            }).CallDeferred();
        }, null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(_skinsRoot, "*.png")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
        };
        _watcher.Changed += (_, e) => QueueReload(e.FullPath);
        _watcher.Created += (_, e) => QueueReload(e.FullPath);
        _watcher.Deleted += (_, e) => QueueReload(e.FullPath);
        _watcher.Renamed += (_, e) => { QueueReload(e.OldFullPath); QueueReload(e.FullPath); };
        _watcher.EnableRaisingEvents = true;
    }

    private static void QueueReload(string fullPath)
    {
        _pendingReloads[fullPath] = 0;
        _debounceTimer?.Change(200, Timeout.Infinite);
    }

    private static void ProcessSkinFileChange(string fullPath)
    {
        if (_skinsRoot == null) return;

        string relative = Path.GetRelativePath(_skinsRoot, fullPath);
        string[] parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length != 2) return;

        string charId = parts[0].ToLower();
        string skinName = Path.GetFileNameWithoutExtension(parts[1]);
        var key = (charId, skinName);

        if (!File.Exists(fullPath))
        {
            // Deleted
            _skinFilePaths.Remove(key);
            if (_charTextureSkins.TryGetValue(charId, out var names))
                names.Remove(skinName);
            return;
        }

        var image = Image.LoadFromFile(fullPath);
        if (image == null) return;

        if (_textureCache.TryGetValue(key, out var existing) && existing is ImageTexture imgTex)
        {
            imgTex.SetImage(image);
        }
        else
        {
            _skinFilePaths[key] = fullPath;
            if (!_charTextureSkins.TryGetValue(charId, out var names))
            {
                names = new List<string>();
                _charTextureSkins[charId] = names;
            }
            if (!names.Contains(skinName))
                names.Add(skinName);

            _textureCache[key] = ImageTexture.CreateFromImage(image);
        }
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
