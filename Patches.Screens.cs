using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace MPSkins;

public static partial class Patches
{
    static readonly StringName _hParam = new StringName("h");

    static NCreatureVisuals? _previewVisuals;
    static Node2D? _previewContainer;
    static NinePatchRect? _previewBacking;
    static CharacterModel? _previewCharModel;
    static string? _previewSkinOverride;

    static void RefreshPreview()
    {
        if (_previewVisuals != null && GodotObject.IsInstanceValid(_previewVisuals))
        {
            _previewVisuals.QueueFree();
            _previewVisuals = null;
        }

        if (_previewContainer == null || !GodotObject.IsInstanceValid(_previewContainer)) return;
        if (_previewCharModel == null)
        {
            if (_previewBacking != null) _previewBacking.Visible = false;
            return;
        }

        _previewVisuals = _previewCharModel.CreateVisuals();
        _previewContainer.AddChild(_previewVisuals);

        _previewVisuals.Position = new Vector2(500f, _previewVisuals.Bounds.Size.Y * 0.5f);

        if (_previewBacking != null)
        {
            float h = _previewVisuals.Bounds.Size.Y;
            _previewBacking.Position = new Vector2(300f, -h * 0.5f - 30f);
            _previewBacking.Size = new Vector2(400f, h + 80f);
            _previewBacking.Visible = true;
        }

        if (_previewVisuals.HasSpineAnimation && _previewVisuals.SpineBody != null)
            _previewVisuals.SpineBody.GetAnimationState().SetAnimation("idle_loop");

        string skinName = _previewSkinOverride ?? SkinManager.LocalSkinName;
        if (skinName == "Random" || _previewVisuals.SpineBody == null) return;

        if (SkinManager.IsTintSkin(skinName))
        {
            float hue = SkinManager.GetHueForSkin(skinName);
            if (hue != 0f)
                _previewVisuals.SetScaleAndHue(_previewVisuals.DefaultScale, hue);
        }
        else
        {
            string characterId = _previewCharModel.Id.Entry;
            Texture2D? texture = SkinManager.GetTextureForSkin(characterId, skinName);
            if (texture != null)
                ApplyTextureSkin(_previewVisuals.SpineBody, texture);
        }
    }

    [HarmonyPatch(typeof(NCharacterSelectScreen), "_Ready")]
    [HarmonyPostfix]
    static void CharSelectScreenReady(NCharacterSelectScreen __instance)
    {
        SkinManager.Reset();
        _previewCharModel = null;
        _previewVisuals = null;
        _previewBacking = null;

        _previewContainer = new Node2D();
        _previewContainer.Name = "MPSkins_Preview";
        _previewContainer.Position = new Vector2(960, 650);
        __instance.AddChild(_previewContainer);
        var bgNode = __instance.GetNode("AnimatedBg");
        __instance.MoveChild(_previewContainer, bgNode.GetIndex() + 1);

        _previewBacking = new NinePatchRect();
        _previewBacking.Texture = ResourceLoader.Load<Texture2D>("res://images/ui/fuzzy_nine_patch_char_select.png");
        _previewBacking.PatchMarginLeft = 42;
        _previewBacking.PatchMarginTop = 42;
        _previewBacking.PatchMarginRight = 42;
        _previewBacking.PatchMarginBottom = 42;
        _previewBacking.Modulate = new Color(0, 0, 0, 0.36f);
        _previewBacking.Visible = false;
        _previewContainer.AddChild(_previewBacking);

        var vbox = BuildSkinPickerVbox(name => SelectSkin(__instance, name));
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

        var vbox = BuildSkinPickerVbox(name => SelectSkin(__instance, name));
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
        if (LocalContext.NetId.HasValue)
            SkinManager.LocalPlayerId = LocalContext.NetId.Value;
        BroadcastLocalSkin();
    }

    [HarmonyPatch(typeof(NMapScreen), "_Ready")]
    [HarmonyPostfix]
    static void MapScreenReady(NMapScreen __instance)
    {
        var vbox = BuildSkinPickerVbox(name => SelectSkin(__instance, name), false);
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
        RefreshDropdown(__instance);
        RefreshPreview();
    }

    [HarmonyPatch(typeof(NCharacterSelectScreen), "SelectCharacter")]
    [HarmonyPostfix]
    static void OnSelectCharacter(NCharacterSelectScreen __instance, CharacterModel characterModel, NCharacterSelectButton charSelectButton)
    {
        _previewCharModel = (!charSelectButton.IsLocked && !charSelectButton.IsRandom)
            ? characterModel : null;
        HandleCharacterChanged(__instance, characterModel.Id.Entry);
        RefreshPreview();
    }

    [HarmonyPatch(typeof(NCharacterSelectScreen), "PlayerConnected")]
    [HarmonyPostfix]
    static void OnPlayerConnected(LobbyPlayer player)
    {
        if (player.id == SkinManager.LocalPlayerId) return;
        BroadcastLocalSkin();
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
}
