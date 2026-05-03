using Godot;
using HarmonyLib;

namespace MPSkins;

[HarmonyPatch]
public static partial class Patches
{
    static readonly Color DropdownBgColor = new(0.172549f, 0.262745f, 0.309804f, 1f);
    static readonly Color DropdownTextColor = new(0.937255f, 0.784314f, 0.317647f, 1f);
    static readonly Color ItemBgColor = new(0.0705882f, 0.129412f, 0.160784f, 1f);
    static readonly Color ItemTextColor = new(1f, 0.964706f, 0.886275f, 1f);
    static readonly Color ItemHoverColor = new(0.172549f, 0.345098f, 0.439216f, 1f);
    static readonly StyleBoxFlat ItemHoverStyle = new() { BgColor = ItemHoverColor };
    static readonly StyleBoxFlat ItemNormalStyle = new() { BgColor = Colors.Transparent };
    const float DropdownWidth = 200;
    const int MaxVisibleItems = 6;
    const float ItemHeight = 44f;

    static void BroadcastLocalSkin()
    {
        ulong localId = SkinManager.LocalPlayerId;
        if (localId == 0) return;

        string skinName = SkinManager.BroadcastSkinName;
        SkinManager.SetPlayerSkinName(localId, skinName);
        SkinManager.NetService?.SendMessage(
            new ZZ_SkinChangedMessage { skinName = skinName, playerId = localId });
    }

    static void SelectSkin(Node parent, string skinName)
    {
        SkinManager.LocalSkinName = skinName;
        RefreshDropdown(parent);
        BroadcastLocalSkin();
        RefreshPreview();
    }

    static void HandleCharacterChanged(Node parent, string characterId)
    {
        SkinManager.CurrentCharacterId = characterId;
        if (!SkinManager.GetAvailableSkins(characterId).Contains(SkinManager.LocalSkinName))
            SkinManager.LocalSkinName = "Default";
        RefreshDropdown(parent);
    }

    static void RefreshDropdown(Node parent)
    {
        if (parent.FindChild("MPSkins_ItemContainer", owned: false) is not VBoxContainer itemContainer) return;
        if (parent.FindChild("MPSkins_PanelWrapper", owned: false) is not Control panelWrapper) return;
        if (parent.FindChild("MPSkins_Dismisser", owned: false) is not Button dismisser) return;

        if (parent.FindChild("MPSkins_DropdownBtn", owned: false) is Button btn)
        {
            bool dropUp = (bool)panelWrapper.GetMeta("drop_up", true);
            btn.Text = SkinManager.LocalSkinName + (dropUp ? "  ▲" : "  ▼");
        }

        PopulateDropdownItems(panelWrapper, dismisser, itemContainer, name => SelectSkin(parent, name));
    }

    static void PopulateDropdownItems(Control panelWrapper, Button dismisser, VBoxContainer itemContainer, Action<string> onSelect)
    {
        foreach (var child in itemContainer.GetChildren())
            child.QueueFree();

        string characterId = SkinManager.CurrentCharacterId ?? "";
        var skins = SkinManager.GetAvailableSkins(characterId);

        PositionDropdownPanel(panelWrapper, skins.Count);

        foreach (string skinName in skins)
            itemContainer.AddChild(BuildDropdownItem(skinName, panelWrapper, dismisser, onSelect));
    }

    static void PositionDropdownPanel(Control panelWrapper, int itemCount)
    {
        int visibleCount = Math.Min(itemCount, MaxVisibleItems);
        float panelHeight = visibleCount * ItemHeight;
        bool dropUp = (bool)panelWrapper.GetMeta("drop_up", true);
        if (dropUp)
        {
            panelWrapper.OffsetTop = -panelHeight;
            panelWrapper.OffsetBottom = 0;
        }
        else
        {
            panelWrapper.OffsetTop = ItemHeight;
            panelWrapper.OffsetBottom = ItemHeight + panelHeight;
        }
    }

    static Button BuildDropdownItem(string skinName, Control panelWrapper, Button dismisser, Action<string> onSelect)
    {
        var btn = MakeButton(skinName, DropdownWidth, ItemHeight, ItemTextColor);
        btn.AddThemeStyleboxOverride("hover", ItemHoverStyle);
        btn.AddThemeStyleboxOverride("normal", ItemNormalStyle);
        btn.AddThemeStyleboxOverride("pressed", ItemHoverStyle);

        btn.Pressed += () => { CloseDropdown(panelWrapper, dismisser); onSelect(skinName); };

        if (_previewContainer != null && GodotObject.IsInstanceValid(_previewContainer))
        {
            btn.MouseEntered += () => { _previewSkinOverride = skinName; RefreshPreview(); };
            btn.MouseExited += () => { _previewSkinOverride = null; RefreshPreview(); };
        }

        return btn;
    }

    static void CloseDropdown(Control panelWrapper, Button dismisser)
    {
        panelWrapper.Visible = false;
        dismisser.Visible = false;

        if (_previewSkinOverride != null)
        {
            _previewSkinOverride = null;
            RefreshPreview();
        }
    }

    static Button MakeButton(string text, float width, float height, Color color)
    {
        var btn = new Button
        {
            CustomMinimumSize = new Vector2(width, height),
            Text = text,
            Flat = true,
        };
        btn.AddThemeColorOverride("font_color", color);
        btn.AddThemeColorOverride("font_hover_color", color);
        btn.AddThemeColorOverride("font_pressed_color", color);
        btn.AddThemeFontSizeOverride("font_size", 22);
        var font = GetGameFont();
        if (font != null) btn.AddThemeFontOverride("font", font);
        return btn;
    }

    static Font? _gameFont;
    static Font GetGameFont()
    {
        _gameFont ??= ResourceLoader.Load<Font>("res://themes/kreon_bold_shared.tres");
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

    static VBoxContainer BuildSkinPickerVbox(Action<string> onSelect, bool dropUp = true)
    {
        var titleLabel = new Label
        {
            Text = "Skin",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        ApplyLabelStyle(titleLabel, 20);

        var btnWrapper = new Control
        {
            CustomMinimumSize = new Vector2(DropdownWidth, ItemHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
        };

        var btnBg = new ColorRect
        {
            Color = DropdownBgColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        btnBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        btnWrapper.AddChild(btnBg);

        string arrow = dropUp ? "  ▲" : "  ▼";
        var dropdownBtn = MakeButton(SkinManager.LocalSkinName + arrow, DropdownWidth, ItemHeight, DropdownTextColor);
        dropdownBtn.Name = "MPSkins_DropdownBtn";
        dropdownBtn.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        btnWrapper.AddChild(dropdownBtn);

        // idk how else to do a dropdown so here's a big invisible button until i see if making it in godot is better
        var dismisser = new Button
        {
            Name = "MPSkins_Dismisser",
            Flat = true,
            Visible = false,
            AnchorLeft = 0.5f, AnchorRight = 0.5f,
            AnchorTop = 0.5f, AnchorBottom = 0.5f,
            OffsetLeft = -2000, OffsetRight = 2000,
            OffsetTop = -2000, OffsetBottom = 2000,
        };
        btnWrapper.AddChild(dismisser);

        var panelWrapper = new Control
        {
            Name = "MPSkins_PanelWrapper",
            Visible = false,
            OffsetLeft = 0,
            OffsetRight = DropdownWidth,
        };

        var panelBg = new ColorRect
        {
            Color = ItemBgColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        panelBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        panelWrapper.AddChild(panelBg);

        var scroll = new ScrollContainer
        {
            Name = "MPSkins_DropdownPanel",
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        scroll.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        panelWrapper.AddChild(scroll);

        var itemContainer = new VBoxContainer
        {
            Name = "MPSkins_ItemContainer",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        itemContainer.AddThemeConstantOverride("separation", 0);
        scroll.AddChild(itemContainer);

        panelWrapper.SetMeta("drop_up", dropUp);
        PopulateDropdownItems(panelWrapper, dismisser, itemContainer, onSelect);
        btnWrapper.AddChild(panelWrapper);

        dropdownBtn.Pressed += () =>
        {
            bool opening = !panelWrapper.Visible;
            panelWrapper.Visible = opening;
            dismisser.Visible = opening;
        };
        dismisser.Pressed += () => CloseDropdown(panelWrapper, dismisser);

        var vbox = new VBoxContainer { Name = "MPSkins_SkinPicker" };
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(titleLabel);
        vbox.AddChild(btnWrapper);
        return vbox;
    }
}
