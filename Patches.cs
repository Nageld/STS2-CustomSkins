using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

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

        var vbox = BuildSkinPickerVbox(dir => CycleSkin(__instance, dir));
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

        var vbox = BuildSkinPickerVbox(dir => CycleSkin(__instance, dir));
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
        HandleCharacterChanged(__instance, characterModel.Id.Entry);
    }

    [HarmonyPatch(typeof(NMapScreen), "Initialize")]
    [HarmonyPostfix]
    static void MapScreenInitialize(RunState runState)
    {
        SkinManager.CurrentCharacterId = LocalContext.GetMe(runState).Character.Id.Entry;
        BroadcastLocalSkin();
    }

    [HarmonyPatch(typeof(NMapScreen), "_Ready")]
    [HarmonyPostfix]
    static void MapScreenReady(NMapScreen __instance)
    {
        var vbox = BuildSkinPickerVbox(dir => CycleSkin(__instance, dir));
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
        BroadcastLocalSkin();
        RefreshPickerLabel(__instance);
    }

    [HarmonyPatch(typeof(NCharacterSelectScreen), "SelectCharacter")]
    [HarmonyPostfix]
    static void OnSelectCharacter(NCharacterSelectScreen __instance, CharacterModel characterModel)
    {
        HandleCharacterChanged(__instance, characterModel.Id.Entry);
    }

    [HarmonyPatch(typeof(NCharacterSelectScreen), "PlayerConnected")]
    [HarmonyPostfix]
    static void OnPlayerConnected(LobbyPlayer player)
    {
        if (player.id == SkinManager.LocalPlayerId) return;
        BroadcastLocalSkin();
    }

    [HarmonyPatch(typeof(StartRunLobby), MethodType.Constructor,
        new[] { typeof(GameMode), typeof(INetGameService), typeof(IStartRunLobbyListener), typeof(int) })]
    [HarmonyPostfix]
    static void LobbyConstructed(StartRunLobby __instance)
    {
        SetupNetService(__instance.NetService, __instance);
    }

    [HarmonyPatch(typeof(LoadRunLobby), MethodType.Constructor,
        new[] { typeof(INetGameService), typeof(ILoadRunLobbyListener), typeof(SerializableRun) })]
    [HarmonyPostfix]
    static void LoadRunLobbyConstructed(LoadRunLobby __instance)
    {
        SetupNetService(__instance.NetService, __instance);
    }

    static void SetupNetService(INetGameService netService, object lobby)
    {
        if (SkinManager.NetService != null && SkinManager.NetService != netService)
            SkinManager.NetService.UnregisterMessageHandler<SkinChangedMessage>(HandleSkinChanged);

        SkinManager.CurrentLobby = lobby;
        SkinManager.NetService = netService;
        SkinManager.LocalPlayerId = netService.NetId;
        SkinManager.Reset();
        netService.RegisterMessageHandler<SkinChangedMessage>(HandleSkinChanged);
    }

    static void HandleSkinChanged(SkinChangedMessage message, ulong senderId)
    {
        SkinManager.SetPlayerSkinName(message.playerId, message.skinName);
    }

    [HarmonyPatch(typeof(StartRunLobby), "CleanUp")]
    [HarmonyPrefix]
    static void LobbyCleanUp(StartRunLobby __instance)
    {
        if (SkinManager.CurrentLobby == __instance)
            SkinManager.CurrentLobby = null;
    }

    [HarmonyPatch(typeof(LoadRunLobby), "CleanUp")]
    [HarmonyPrefix]
    static void LoadRunLobbyCleanUp(LoadRunLobby __instance)
    {
        if (SkinManager.CurrentLobby == __instance)
            SkinManager.CurrentLobby = null;
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

        int iconIndex = 0;

        if (__instance._isSelected && iconIndex < playerIconContainer.GetChildCount())
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
        bool isLocal = netId == localId;
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

    static void ApplyTextureSkin(MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaSprite spineBody, Texture2D texture)
    {
        var mat = new ShaderMaterial();
        mat.Shader = GetSkinShader();
        mat.SetShaderParameter("skin_texture", texture);
        spineBody.SetNormalMaterial(mat);
    }
}
