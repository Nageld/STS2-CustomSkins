using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace MPSkins;

public static partial class Patches
{
    static Shader? _skinShader;
    static Shader GetSkinShader()
    {
        if (_skinShader != null) return _skinShader;
        _skinShader = new Shader();
        _skinShader.Code = @"
shader_type canvas_item;
uniform sampler2D skin_texture : hint_default_transparent;
varying vec4 modulate_color;
void vertex() { modulate_color = COLOR; }
void fragment() { COLOR = texture(skin_texture, UV) * modulate_color; }
";
        return _skinShader;
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

    static void ApplyTextureSkin(MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaSprite spineBody, Texture2D texture)
    {
        var mat = new ShaderMaterial();
        mat.Shader = GetSkinShader();
        mat.SetShaderParameter("skin_texture", texture);
        spineBody.SetNormalMaterial(mat);
    }
}
