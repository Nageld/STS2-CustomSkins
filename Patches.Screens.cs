using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.CustomRun;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace MPSkins;

public static partial class Patches
{
    static readonly StringName _hParam = new StringName("h");

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
