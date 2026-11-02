using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;

namespace AuxUi;

public static class AuxTheme
{
    public static readonly Vector4 NavBg = Rgba(31, 31, 31, 0.90f);
    public static readonly Vector4 NavFg = Hex(0xDDDDDD);
    public static readonly Vector4 ContentBg = Rgba(0, 0, 0, 0.72f);
    public static readonly Vector4 ContentFg = Hex(0xEEEEEE);
    public static readonly Vector4 MutedFg = Hex(0xDDDDDD, 0.5f);
    public static readonly Vector4 CellBg = Rgba(255, 255, 255, 0.10f);
    public static readonly Vector4 RowAlt = Rgba(255, 255, 255, 0.04f);
    public static readonly Vector4 Accent = Hex(0x26C6DA);
    public static readonly Vector4 AccentDeep = Hex(0x0097A7);
    public static readonly Vector4 AccentDim = Hex(0x006064);
    public static readonly Vector4 HoverTint = Rgba(127, 127, 127, 0.25f);
    public static readonly Vector4 ActiveTint = Rgba(127, 127, 127, 0.40f);
    public static readonly Vector4 Rule = Rgba(127, 127, 127, 0.50f);
    public static readonly Vector4 DropdownBg = Rgba(31, 31, 31, 0.95f);
    public static readonly Vector4 Danger = Hex(0xEF5350);
    public static readonly Vector4 Transparent = new(0f, 0f, 0f, 0f);

    public const float TitleBarHeight = 28f;
    public const float FooterBarHeight = 32f;
    public const float AccentMarkWidth = 2f;
    public const float UnderlineThickness = 1f;
    public const float SmallFontRatio = 0.8f;
    public const float ScrollbarWidth = 4f;

    public static float Scale => ImGuiHelpers.GlobalScale;

    public static Vector2 WindowPadding => new Vector2(8f, 8f) * Scale;
    public static Vector2 FramePadding => new Vector2(6f, 3f) * Scale;
    public static Vector2 ItemSpacing => new Vector2(6f, 4f) * Scale;
    public static Vector2 CellPadding => new Vector2(6f, 2f) * Scale;

    public static IDisposable Push() => new ThemeScope();

    public static Vector4 Rgba(int red, int green, int blue, float alpha) => new(red / 255f, green / 255f, blue / 255f, alpha);

    public static Vector4 Hex(uint rgb, float alpha = 1f) => new(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

    public static Vector4 WithAlpha(Vector4 color, float alpha) => new(color.X, color.Y, color.Z, alpha);

    private sealed class ThemeScope : IDisposable
    {
        private readonly IDisposable bodyFont;
        private readonly ImRaii.ColorDisposable colors = new();
        private readonly ImRaii.StyleDisposable styles = new();

        public ThemeScope()
        {
            bodyFont = AuxFonts.PushBody();
            PushColors();
            PushStyles();
        }

        public void Dispose()
        {
            styles.Dispose();
            colors.Dispose();
            bodyFont.Dispose();
        }

        private void PushColors()
        {
            colors
                .Push(ImGuiCol.Text, ContentFg)
                .Push(ImGuiCol.TextDisabled, MutedFg)
                .Push(ImGuiCol.TextSelectedBg, WithAlpha(Accent, 0.35f))
                .Push(ImGuiCol.WindowBg, ContentBg)
                .Push(ImGuiCol.ChildBg, Transparent)
                .Push(ImGuiCol.PopupBg, DropdownBg)
                .Push(ImGuiCol.Border, Rule)
                .Push(ImGuiCol.BorderShadow, Transparent)
                .Push(ImGuiCol.FrameBg, CellBg)
                .Push(ImGuiCol.FrameBgHovered, HoverTint)
                .Push(ImGuiCol.FrameBgActive, ActiveTint)
                .Push(ImGuiCol.TitleBg, NavBg)
                .Push(ImGuiCol.TitleBgActive, NavBg)
                .Push(ImGuiCol.TitleBgCollapsed, NavBg)
                .Push(ImGuiCol.MenuBarBg, NavBg)
                .Push(ImGuiCol.Button, Transparent)
                .Push(ImGuiCol.ButtonHovered, HoverTint)
                .Push(ImGuiCol.ButtonActive, ActiveTint)
                .Push(ImGuiCol.Header, CellBg)
                .Push(ImGuiCol.HeaderHovered, HoverTint)
                .Push(ImGuiCol.HeaderActive, ActiveTint)
                .Push(ImGuiCol.CheckMark, Accent)
                .Push(ImGuiCol.SliderGrab, Accent)
                .Push(ImGuiCol.SliderGrabActive, AccentDeep)
                .Push(ImGuiCol.Separator, Rule)
                .Push(ImGuiCol.SeparatorHovered, Accent)
                .Push(ImGuiCol.SeparatorActive, AccentDeep)
                .Push(ImGuiCol.ResizeGrip, Transparent)
                .Push(ImGuiCol.ResizeGripHovered, Accent)
                .Push(ImGuiCol.ResizeGripActive, AccentDeep)
                .Push(ImGuiCol.Tab, Transparent)
                .Push(ImGuiCol.TabHovered, HoverTint)
                .Push(ImGuiCol.TabActive, Transparent)
                .Push(ImGuiCol.TabUnfocused, Transparent)
                .Push(ImGuiCol.TabUnfocusedActive, Transparent)
                .Push(ImGuiCol.TableHeaderBg, Transparent)
                .Push(ImGuiCol.TableBorderStrong, Rule)
                .Push(ImGuiCol.TableBorderLight, Rule)
                .Push(ImGuiCol.TableRowBg, Transparent)
                .Push(ImGuiCol.TableRowBgAlt, RowAlt)
                .Push(ImGuiCol.ScrollbarBg, Transparent)
                .Push(ImGuiCol.ScrollbarGrab, Rule)
                .Push(ImGuiCol.ScrollbarGrabHovered, Accent)
                .Push(ImGuiCol.ScrollbarGrabActive, AccentDeep)
                .Push(ImGuiCol.DragDropTarget, Accent)
                .Push(ImGuiCol.NavHighlight, Accent);
        }

        private void PushStyles()
        {
            var scale = Scale;
            styles
                .Push(ImGuiStyleVar.WindowRounding, 0f)
                .Push(ImGuiStyleVar.ChildRounding, 0f)
                .Push(ImGuiStyleVar.FrameRounding, 0f)
                .Push(ImGuiStyleVar.PopupRounding, 0f)
                .Push(ImGuiStyleVar.ScrollbarRounding, 0f)
                .Push(ImGuiStyleVar.GrabRounding, 0f)
                .Push(ImGuiStyleVar.TabRounding, 0f)
                .Push(ImGuiStyleVar.WindowBorderSize, 0f)
                .Push(ImGuiStyleVar.ChildBorderSize, 0f)
                .Push(ImGuiStyleVar.FrameBorderSize, 0f)
                .Push(ImGuiStyleVar.PopupBorderSize, 1f)
                .Push(ImGuiStyleVar.ScrollbarSize, ScrollbarWidth * scale)
                .Push(ImGuiStyleVar.WindowPadding, WindowPadding)
                .Push(ImGuiStyleVar.FramePadding, FramePadding)
                .Push(ImGuiStyleVar.ItemSpacing, ItemSpacing)
                .Push(ImGuiStyleVar.CellPadding, CellPadding);
        }
    }
}
