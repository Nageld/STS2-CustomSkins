using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace MPSkins;

[HarmonyPatch]
public static class Patches
{
    static readonly StringName _hParam = new StringName("h");

    static Shader? _skinShader;
    static Shader GetSkinShader()
    {
        if (_skinShader != null) return _skinShader;
        _skinShader = new Shader();
        // Dummy shader.
        _skinShader.Code = @"
shader_type canvas_item;
uniform sampler2D skin_texture : hint_default_transparent;
varying vec4 modulate_color;
void vertex() { modulate_color = COLOR; }
void fragment() { COLOR = texture(skin_texture, UV) * modulate_color; }
";
        return _skinShader;
    }

    [HarmonyPatch(typeof(NCharacterSelectScreen), "_Ready")]
    [HarmonyPostfix]
    static void CharSelectScreenReady(NCharacterSelectScreen __instance)
    {
        SkinManager.Reset();

        var vbox = BuildSkinPickerVbox(dir =>
        {
            var selectedBtn = __instance._selectedButton;
            CycleSkin(__instance, dir, selectedBtn);
        });
        vbox.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        vbox.OffsetLeft = -310f;
        vbox.OffsetTop = -450f;
        vbox.OffsetRight = -30f;
        vbox.OffsetBottom = -360f;

        __instance.AddChild(vbox);
    }

    [HarmonyPatch(typeof(NCustomRunScreen), "_Ready")]
    [HarmonyPostfix]
    static void CustomRunScreenReady(NCustomRunScreen __instance)
    {
        var charId = __instance._lobby?.LocalPlayer.character?.Id.Entry;
        if (charId != null) SkinManager.CurrentCharacterId = charId;

        var vbox = BuildSkinPickerVbox(dir => CycleSkin(__instance, dir, null));
        vbox.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        vbox.OffsetLeft = 150f;
        vbox.OffsetRight = 400f;
        vbox.OffsetTop = -190f;
        vbox.OffsetBottom = -100f;

        __instance.AddChild(vbox);
    }

    [HarmonyPatch(typeof(NCustomRunScreen), "SelectCharacter")]
    [HarmonyPostfix]
    static void CustomRunSelectCharacter(NCustomRunScreen __instance, CharacterModel characterModel)
    {
        string characterId = characterModel.Id.Entry;
        SkinManager.CurrentCharacterId = characterId;

        var available = SkinManager.GetAvailableSkins(characterId);
        if (!available.Contains(SkinManager.LocalSkinName))
            SkinManager.LocalSkinName = "Default";

        RefreshPickerLabel(__instance);
    }

    [HarmonyPatch(typeof(NMapScreen), "_Ready")]
    [HarmonyPostfix]
    static void MapScreenReady(NMapScreen __instance)
    {
        var vbox = BuildSkinPickerVbox(dir => CycleSkin(__instance, dir, null));
        vbox.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        vbox.OffsetLeft = -310f;
        vbox.OffsetRight = -40f;
        vbox.OffsetTop = -360f;
        vbox.OffsetBottom = -430f;

        __instance.AddChild(vbox);
    }

    [HarmonyPatch(typeof(NCharacterSelectScreen), "OnSubmenuOpened")]
    [HarmonyPostfix]
    static void CharSelectOpened(NCharacterSelectScreen __instance)
    {
        ulong localId = SkinManager.LocalPlayerId;
        if (localId != 0)
        {
            string broadcastSkin = SkinManager.LocalSkinName == "Random" ? "Default" : SkinManager.LocalSkinName;
            SkinManager.SetPlayerSkinName(localId, broadcastSkin);
            SkinManager.NetService?.SendMessage(
                new SkinChangedMessage { skinName = broadcastSkin });
        }

        RefreshPickerLabel(__instance);
    }

    [HarmonyPatch(typeof(NCharacterSelectScreen), "SelectCharacter")]
    [HarmonyPostfix]
    static void OnSelectCharacter(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        string characterId = characterModel.Id.Entry;
        SkinManager.CurrentCharacterId = characterId;

        // If the current skin is a texture skin for a different character, reset to Default.
        var available = SkinManager.GetAvailableSkins(characterId);
        if (!available.Contains(SkinManager.LocalSkinName))
            SkinManager.LocalSkinName = "Default";

        RefreshPickerLabel(__instance);

        // Stubbed.
        if (__instance._selectedButton != null)
            ApplyHueToButton(__instance._selectedButton, SkinManager.LocalSkinName);
    }

    [HarmonyPatch(typeof(NCharacterSelectScreen), "PlayerConnected")]
    [HarmonyPostfix]
    static void OnPlayerConnected(LobbyPlayer player)
    {
        if (SkinManager.NetService == null) return;
        if (player.id == SkinManager.LocalPlayerId) return;

        string broadcastSkin = SkinManager.LocalSkinName == "Random" ? "Default" : SkinManager.LocalSkinName;
        SkinManager.NetService.SendMessage(
            new SkinChangedMessage { skinName = broadcastSkin });
    }

    [HarmonyPatch(typeof(StartRunLobby), MethodType.Constructor,
        new[] { typeof(GameMode), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(int) })]
    [HarmonyPostfix]
    static void LobbyConstructed(StartRunLobby __instance)
    {
        if (SkinManager.NetService != null && SkinManager.NetService != __instance.NetService)
            SkinManager.NetService.UnregisterMessageHandler<SkinChangedMessage>(HandleSkinChanged);

        SkinManager.CurrentLobby = __instance;
        SkinManager.NetService = __instance.NetService;
        SkinManager.LocalPlayerId = __instance.NetService.NetId;
        SkinManager.Reset();
        __instance.NetService.RegisterMessageHandler<SkinChangedMessage>(HandleSkinChanged);
    }

    static void HandleSkinChanged(SkinChangedMessage message, ulong senderId)
    {
        SkinManager.SetPlayerSkinName(senderId, message.skinName);
    }

    [HarmonyPatch(typeof(StartRunLobby), "CleanUp")]
    [HarmonyPrefix]
    static void LobbyCleanUp(StartRunLobby __instance)
    {
        if (SkinManager.CurrentLobby == __instance)
            SkinManager.CurrentLobby = null;
    }

    [HarmonyPatch(typeof(NCharacterSelectButton), "Select")]
    [HarmonyPostfix]
    static void ButtonSelected(NCharacterSelectButton __instance)
    {
        ApplyHueToButton(__instance, SkinManager.LocalSkinName);
    }

    [HarmonyPatch(typeof(NCharacterSelectButton), "Deselect")]
    [HarmonyPostfix]
    static void ButtonDeselected(NCharacterSelectButton __instance)
    {
        __instance._hsv?.SetShaderParameter(_hParam, 0f);
    }

    [HarmonyPatch(typeof(NCharacterSelectButton), "RefreshPlayerIcons")]
    [HarmonyPostfix]
    static void UpdatePlayerIconColors(NCharacterSelectButton __instance)
    {
        var playerIconContainer = __instance._playerIconContainer;
        var remoteSelectedPlayers = __instance._remoteSelectedPlayers;

        if (playerIconContainer == null || remoteSelectedPlayers == null) return;

        var delegateRef = __instance._delegate;
        if (delegateRef == null || delegateRef.Lobby.NetService.Type == NetGameType.Singleplayer) return;

        bool isSelected = __instance._isSelected;

        int iconIndex = 0;

        if (isSelected && iconIndex < playerIconContainer.GetChildCount())
        {
            playerIconContainer.GetChild<Control>(iconIndex).Modulate =
                SkinManager.GetSkinColor(SkinManager.LocalSkinName);
            iconIndex++;
        }

        foreach (ulong playerId in remoteSelectedPlayers)
        {
            if (iconIndex >= playerIconContainer.GetChildCount()) break;
            playerIconContainer.GetChild<Control>(iconIndex).Modulate =
                SkinManager.GetSkinColor(SkinManager.GetPlayerSkinName(playerId));
            iconIndex++;
        }
    }

    [HarmonyPatch(typeof(NCreature), "_Ready")]
    [HarmonyPostfix]
    static void CreatureReady(NCreature __instance)
    {
        if (!__instance.Entity.IsPlayer) return;
        if (__instance.Visuals.SpineBody == null) return;

        ulong netId = __instance.Entity.Player.NetId;
        ulong localId = SkinManager.LocalPlayerId;
        bool isLocal = localId == 0 || netId == localId;
        string characterId = __instance.Entity.Player.Character.Id.Entry;
        if (isLocal)
        {
            SkinManager.ResolveLocalSkin(characterId);
            SkinManager.LocalSkinName = SkinManager.ResolvedSkinName;
        }
        string skinName = isLocal ? SkinManager.ResolvedSkinName : SkinManager.GetPlayerSkinName(netId);

        if (SkinManager.IsTintSkin(skinName))
        {
            float hue = SkinManager.GetHueForSkin(skinName);
            if (hue != 0f)
                __instance.Visuals.SetScaleAndHue(__instance.Visuals.DefaultScale, hue);
        }
        else
        {
            Texture2D? texture = SkinManager.GetTextureForSkin(characterId, skinName);
            if (texture != null)
                ApplyTextureSkin(__instance.Visuals.SpineBody, texture);
        }
    }

    static void CycleSkin(Node labelParent, int direction, NCharacterSelectButton? selectedBtn)
    {
        string characterId = SkinManager.CurrentCharacterId ?? "";
        var available = SkinManager.GetAvailableSkins(characterId);

        int currentIdx = available.IndexOf(SkinManager.LocalSkinName);
        if (currentIdx < 0) currentIdx = 0;
        int newIdx = ((currentIdx + direction) % available.Count + available.Count) % available.Count;
        SkinManager.LocalSkinName = available[newIdx];

        RefreshPickerLabel(labelParent);

        if (selectedBtn != null)
            ApplyHueToButton(selectedBtn, SkinManager.LocalSkinName);

        // "Random" is resolved at run start; broadcast Default as a placeholder until then.
        string broadcastSkin = SkinManager.LocalSkinName == "Random" ? "Default" : SkinManager.LocalSkinName;
        ulong localId = SkinManager.LocalPlayerId;
        if (localId != 0)
            SkinManager.SetPlayerSkinName(localId, broadcastSkin);

        SkinManager.NetService?.SendMessage(
            new SkinChangedMessage { skinName = broadcastSkin });
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

    static void ApplyHueToButton(NCharacterSelectButton button, string skinName)
    {
        // Unsure if I want to keep this
        // if (!SkinManager.IsTintSkin(skinName)) return;
        // button._hsv?.SetShaderParameter(_hParam, SkinManager.GetHueForSkin(skinName));
    }

    static void ApplyTextureSkin(MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaSprite spineBody, Texture2D texture)
    {
        var mat = new ShaderMaterial();
        mat.Shader = GetSkinShader();
        mat.SetShaderParameter("skin_texture", texture);
        spineBody.SetNormalMaterial(mat);
    }
}
