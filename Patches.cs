using Godot;
using HarmonyLib;

namespace MPSkins;

[HarmonyPatch]
public static partial class Patches
{
    static void BroadcastLocalSkin()
    {
        ulong localId = SkinManager.LocalPlayerId;
        if (localId == 0) return;

        string skinName = SkinManager.BroadcastSkinName;
        SkinManager.SetPlayerSkinName(localId, skinName);
        SkinManager.NetService?.SendMessage(
            new SkinChangedMessage { skinName = skinName, playerId = localId });
    }

    static void HandleCharacterChanged(Node labelParent, string characterId)
    {
        SkinManager.CurrentCharacterId = characterId;
        if (!SkinManager.GetAvailableSkins(characterId).Contains(SkinManager.LocalSkinName))
            SkinManager.LocalSkinName = "Default";
        RefreshPickerLabel(labelParent);
    }

    static void CycleSkin(Node labelParent, int direction)
    {
        string characterId = SkinManager.CurrentCharacterId ?? "";
        var available = SkinManager.GetAvailableSkins(characterId);

        int currentIdx = available.IndexOf(SkinManager.LocalSkinName);
        if (currentIdx < 0) currentIdx = 0;
        int newIdx = ((currentIdx + direction) % available.Count + available.Count) % available.Count;
        SkinManager.LocalSkinName = available[newIdx];

        RefreshPickerLabel(labelParent);
        BroadcastLocalSkin();
    }

    static void RefreshPickerLabel(Node parent)
    {
        if (parent.FindChild("MPSkins_SkinLabel", owned: false) is Label label)
            label.Text = SkinManager.LocalSkinName;

        if (parent.FindChild("MPSkins_Swatch", owned: false) is ColorRect swatch)
            swatch.Color = SkinManager.GetSkinColor(SkinManager.LocalSkinName);
    }

    static Font? _gameFont;
    static Font GetGameFont()
    {
        _gameFont ??= ResourceLoader.Load<Font>("res://themes/kreon_bold_shared.tres",
            null, ResourceLoader.CacheMode.Reuse);
        return _gameFont;
    }

    static void ApplyLabelStyle(Label label, int fontSize)
    {
        var font = GetGameFont();
        if (font != null) label.AddThemeFontOverride("font", font);
        label.AddThemeFontSizeOverride("font_size", fontSize);
        label.AddThemeConstantOverride("outline_size", 8);
        label.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.4f));
    }

    static VBoxContainer BuildSkinPickerVbox(Action<int> onCycle)
    {
        var font = GetGameFont();

        var titleLabel = new Label();
        titleLabel.Text = "Skin";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        ApplyLabelStyle(titleLabel, 20);

        var leftBtn = new Godot.Button();
        leftBtn.Text = "◄";
        leftBtn.CustomMinimumSize = new Vector2(44, 44);
        leftBtn.AddThemeFontSizeOverride("font_size", 20);
        if (font != null) leftBtn.AddThemeFontOverride("font", font);
        leftBtn.Pressed += () => onCycle(-1);

        var skinLabel = new Label();
        skinLabel.Name = "MPSkins_SkinLabel";
        skinLabel.Text = SkinManager.LocalSkinName;
        skinLabel.CustomMinimumSize = new Vector2(110, 0);
        skinLabel.HorizontalAlignment = HorizontalAlignment.Center;
        skinLabel.VerticalAlignment = VerticalAlignment.Center;
        ApplyLabelStyle(skinLabel, 18);

        var swatch = new ColorRect();
        swatch.Name = "MPSkins_Swatch";
        swatch.CustomMinimumSize = new Vector2(24, 24);
        swatch.Color = SkinManager.GetSkinColor(SkinManager.LocalSkinName);

        var rightBtn = new Godot.Button();
        rightBtn.Text = "►";
        rightBtn.CustomMinimumSize = new Vector2(44, 44);
        rightBtn.AddThemeFontSizeOverride("font_size", 20);
        if (font != null) rightBtn.AddThemeFontOverride("font", font);
        rightBtn.Pressed += () => onCycle(1);

        var hbox = new HBoxContainer();
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddThemeConstantOverride("separation", 4);
        hbox.AddChild(leftBtn);
        hbox.AddChild(swatch);
        hbox.AddChild(skinLabel);
        hbox.AddChild(rightBtn);

        var vbox = new VBoxContainer();
        vbox.Name = "MPSkins_SkinPicker";
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(titleLabel);
        vbox.AddChild(hbox);

        return vbox;
    }
}
