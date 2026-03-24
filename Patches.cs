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

    static VBoxContainer BuildSkinPickerVbox(Action<int> onCycle)
    {
        var titleLabel = new Label();
        titleLabel.Text = "Skin";
        titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
        titleLabel.AddThemeFontSizeOverride("font_size", 20);

        var leftBtn = new Godot.Button();
        leftBtn.Text = "◄";
        leftBtn.CustomMinimumSize = new Vector2(44, 44);
        leftBtn.AddThemeFontSizeOverride("font_size", 20);
        leftBtn.Pressed += () => onCycle(-1);

        var skinLabel = new Label();
        skinLabel.Name = "MPSkins_SkinLabel";
        skinLabel.Text = SkinManager.LocalSkinName;
        skinLabel.CustomMinimumSize = new Vector2(110, 0);
        skinLabel.HorizontalAlignment = HorizontalAlignment.Center;
        skinLabel.VerticalAlignment = VerticalAlignment.Center;
        skinLabel.AddThemeFontSizeOverride("font_size", 18);

        var swatch = new ColorRect();
        swatch.Name = "MPSkins_Swatch";
        swatch.CustomMinimumSize = new Vector2(24, 24);
        swatch.Color = SkinManager.GetSkinColor(SkinManager.LocalSkinName);

        var rightBtn = new Godot.Button();
        rightBtn.Text = "►";
        rightBtn.CustomMinimumSize = new Vector2(44, 44);
        rightBtn.AddThemeFontSizeOverride("font_size", 20);
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
