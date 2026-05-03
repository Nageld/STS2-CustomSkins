using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace MPSkins;

public static partial class Patches
{
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
            SkinManager.NetService.UnregisterMessageHandler<ZZ_SkinChangedMessage>(HandleSkinChanged);

        SkinManager.CurrentLobby = lobby;
        SkinManager.NetService = netService;
        SkinManager.LocalPlayerId = netService.NetId;
        SkinManager.Reset();
        netService.RegisterMessageHandler<ZZ_SkinChangedMessage>(HandleSkinChanged);
    }

    static void HandleSkinChanged(ZZ_SkinChangedMessage message, ulong senderId)
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
}
