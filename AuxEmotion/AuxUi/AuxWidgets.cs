using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;

namespace AuxUi;

public enum TableAlign { Left, Center, Right }

public readonly record struct TitleBarButton(FontAwesomeIcon Icon, string Tooltip);

public readonly record struct FooterAction(string Label, bool Primary = false);

public static class AuxWidgets
{
    public static readonly TitleBarButton CloseButton = new(FontAwesomeIcon.Times, "Close");

    private const float HintIconWidth = 16f;
    private const float TabGap = 18f;
    private const float TabPadding = 4f;
    private const float TextButtonPaddingX = 10f;
    private const float ChipPaddingX = 7f;
    private const float ChipPaddingY = 3f;
    private const float AccentLineThickness = 2f;

    private static float Scale => AuxTheme.Scale;

    public static float FooterHeight => AuxTheme.FooterBarHeight * Scale;

    public static float BodyHeightAboveFooter => ImGui.GetContentRegionAvail().Y + AuxTheme.WindowPadding.Y - FooterHeight;

    public static int TitleBar(string title, params TitleBarButton[] buttons)
    {
        var windowPosition = ImGui.GetWindowPos();
        var windowWidth = ImGui.GetWindowWidth();
        var height = AuxTheme.TitleBarHeight * Scale;
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(windowPosition, windowPosition + new Vector2(windowWidth, height), ImGui.GetColorU32(AuxTheme.NavBg));
        drawList.AddRectFilled(windowPosition, windowPosition + new Vector2(AccentLineThickness * Scale, height), ImGui.GetColorU32(AuxTheme.Accent));

        using (AuxFonts.PushBold())
        {
            var textSize = ImGui.CalcTextSize(title);
            var textPosition = new Vector2(windowPosition.X + 12f * Scale, windowPosition.Y + MathF.Floor((height - textSize.Y) / 2f));
            drawList.AddText(textPosition, ImGui.GetColorU32(AuxTheme.NavFg), title);
        }

        var clicked = -1;
        for (var i = 0; i < buttons.Length; i++)
        {
            var left = windowPosition.X + windowWidth - height * (buttons.Length - i);
            ImGui.SetCursorScreenPos(new Vector2(left, windowPosition.Y));
            if (IconButton(buttons[i].Icon, $"titleBar{i}", buttons[i].Tooltip, false, new Vector2(height)))
            {
                clicked = i;
            }
        }

        ImGui.SetCursorPos(new Vector2(AuxTheme.WindowPadding.X, height + AuxTheme.WindowPadding.Y));
        return clicked;
    }

    public static int Tabs(string id, string[] labels, int selected)
    {
        ImGui.PushID(id);
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetFrameHeight();
        var x = start.X;

        drawList.AddLine(new Vector2(start.X, start.Y + height - 0.5f), new Vector2(start.X + available, start.Y + height - 0.5f), ImGui.GetColorU32(AuxTheme.Rule));

        for (var i = 0; i < labels.Length; i++)
        {
            var textSize = ImGui.CalcTextSize(labels[i]);
            var width = textSize.X + TabPadding * 2f * Scale;
            ImGui.SetCursorScreenPos(new Vector2(x, start.Y));
            if (ImGui.InvisibleButton($"##tab{i}", new Vector2(width, height)))
            {
                selected = i;
            }

            var hovered = ImGui.IsItemHovered();
            var color = i == selected ? AuxTheme.ContentFg : hovered ? AuxTheme.NavFg : AuxTheme.MutedFg;
            var textPosition = new Vector2(x + TabPadding * Scale, start.Y + MathF.Floor((height - textSize.Y) / 2f));
            drawList.AddText(textPosition, ImGui.GetColorU32(color), labels[i]);

            if (i == selected)
            {
                var thickness = AccentLineThickness * Scale;
                drawList.AddRectFilled(new Vector2(x, start.Y + height - thickness), new Vector2(x + width, start.Y + height), ImGui.GetColorU32(AuxTheme.Accent));
            }

            x += width + TabGap * Scale;
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(available, height));
        ImGui.PopID();
        return selected;
    }

    public static void SectionLabel(string label, string? hint = null, float reserveRight = 0f)
    {
        var rightEdge = ImGui.GetCursorScreenPos().X + ImGui.GetContentRegionAvail().X - reserveRight;
        using (AuxFonts.PushSmallBold())
        {
            ImGui.TextColored(AuxTheme.Accent, label.ToUpperInvariant());
        }

        var labelMin = ImGui.GetItemRectMin();
        var labelMax = ImGui.GetItemRectMax();
        var lineStart = labelMax.X;
        if (hint != null)
        {
            ImGui.SameLine();
            Hint(hint);
            lineStart = ImGui.GetItemRectMax().X;
        }

        lineStart += 8f * Scale;
        var y = MathF.Floor((labelMin.Y + labelMax.Y) / 2f) + 0.5f;
        if (rightEdge > lineStart)
        {
            ImGui.GetWindowDrawList().AddLine(new Vector2(lineStart, y), new Vector2(rightEdge, y), ImGui.GetColorU32(AuxTheme.Rule));
        }
    }

    public static float TextButtonWidth(string label) => ImGui.CalcTextSize(label).X + TextButtonPaddingX * 2f * Scale;

    public static bool TextButton(string label, bool primary = false)
    {
        using var colors = ImRaii.PushColor(ImGuiCol.Text, primary ? AuxTheme.Accent : AuxTheme.NavFg)
            .Push(ImGuiCol.Button, AuxTheme.Transparent)
            .Push(ImGuiCol.ButtonHovered, AuxTheme.HoverTint)
            .Push(ImGuiCol.ButtonActive, AuxTheme.ActiveTint);
        using var padding = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(TextButtonPaddingX * Scale, AuxTheme.FramePadding.Y));
        return ImGui.Button(label);
    }

    public static bool IconButton(FontAwesomeIcon icon, string id, string? tooltip = null, bool danger = false, Vector2? size = null)
    {
        var buttonSize = size ?? new Vector2(ImGui.GetFrameHeight());
        var glyph = ((char)icon).ToString();
        bool pressed;
        bool hovered;

        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (ImRaii.PushColor(ImGuiCol.Button, AuxTheme.Transparent)
            .Push(ImGuiCol.ButtonHovered, AuxTheme.HoverTint)
            .Push(ImGuiCol.ButtonActive, AuxTheme.ActiveTint))
        {
            pressed = ImGui.Button($"##{id}", buttonSize);
            hovered = ImGui.IsItemHovered();
            var min = ImGui.GetItemRectMin();
            var textSize = ImGui.CalcTextSize(glyph);
            var color = hovered ? (danger ? AuxTheme.Danger : AuxTheme.ContentFg) : AuxTheme.NavFg;
            var position = new Vector2(MathF.Floor(min.X + (buttonSize.X - textSize.X) / 2f), MathF.Floor(min.Y + (buttonSize.Y - textSize.Y) / 2f));
            ImGui.GetWindowDrawList().AddText(position, ImGui.GetColorU32(color), glyph);
        }

        if (hovered && tooltip != null)
        {
            ImGui.SetTooltip(tooltip);
        }

        return pressed;
    }

    public static bool Checkbox(string label, ref bool value)
    {
        using var colors = ImRaii.PushColor(ImGuiCol.FrameBg, AuxTheme.CellBg)
            .Push(ImGuiCol.FrameBgHovered, AuxTheme.HoverTint)
            .Push(ImGuiCol.FrameBgActive, AuxTheme.ActiveTint)
            .Push(ImGuiCol.CheckMark, AuxTheme.Accent);
        return ImGui.Checkbox(label, ref value);
    }

    public static void Hint(string text)
    {
        var glyph = ((char)FontAwesomeIcon.QuestionCircle).ToString();
        bool hovered;

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var size = ImGui.CalcTextSize(glyph);
            var position = ImGui.GetCursorScreenPos();
            hovered = ImGui.IsMouseHoveringRect(position, position + size);
            ImGui.TextColored(hovered ? AuxTheme.NavFg : AuxTheme.MutedFg, glyph);
        }

        if (ImGui.IsItemHovered())
        {
            using (ImRaii.Tooltip())
            {
                ImGui.TextUnformatted(text);
            }
        }
    }

    public static bool InputText(string label, ref string value, int maxLength)
    {
        using var frame = PushInputFrame();
        var changed = ImGui.InputText(label, ref value, maxLength);
        DrawItemUnderline();
        return changed;
    }

    public static bool InputTextWithHint(string label, string hint, ref string value, int maxLength)
    {
        using var frame = PushInputFrame();
        var changed = ImGui.InputTextWithHint(label, hint, ref value, maxLength);
        DrawItemUnderline();
        return changed;
    }

    public static bool InputInt(string label, ref int value)
    {
        using var frame = PushInputFrame();
        var changed = ImGui.InputInt(label, ref value);
        DrawItemUnderline();
        return changed;
    }

    public static bool DragInt(string label, ref int value, float speed, int min, int max)
    {
        using var frame = PushInputFrame();
        var changed = ImGui.DragInt(label, ref value, speed, min, max);
        DrawItemUnderline();
        return changed;
    }

    public static bool DragFloat(string label, ref float value, float speed, float min, float max)
    {
        using var frame = PushInputFrame();
        var changed = ImGui.DragFloat(label, ref value, speed, min, max);
        DrawItemUnderline();
        return changed;
    }

    public static bool BeginCombo(string label, string preview, ImGuiComboFlags flags = ImGuiComboFlags.None)
    {
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(ImGui.CalcItemWidth(), ImGui.GetFrameHeight());
        bool open;
        using (PushInputFrame())
        {
            open = ImGui.BeginCombo(label, preview, flags);
        }

        DrawUnderline(drawList, min, max, open || ImGui.IsItemActive());
        return open;
    }

    public static bool ComboItem(string label, bool selected)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, AuxTheme.Accent, selected);
        return ImGui.Selectable(label, selected);
    }

    public static bool BeginTable(string id, int columns, bool scrollingRows = false)
    {
        var flags = scrollingRows ? ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg : ImGuiTableFlags.None;
        return ImGui.BeginTable(id, columns, flags);
    }

    public static void TableHeaderRow(string id, string[] hints, params TableAlign[] aligns)
    {
        ImGui.TableNextRow();
        var drawList = ImGui.GetWindowDrawList();
        var columns = ImGui.TableGetColumnCount();
        var lineLeft = 0f;
        var lineRight = 0f;
        var lineY = 0f;

        for (var i = 0; i < columns; i++)
        {
            ImGui.TableNextColumn();
            var label = ImGui.TableGetColumnName(i).ToUpperInvariant();
            var align = aligns.Length <= i ? TableAlign.Left : aligns[i];
            var cellStartX = ImGui.GetCursorPosX();
            var cellAvailable = ImGui.GetContentRegionAvail().X;
            var cellScreen = ImGui.GetCursorScreenPos();

            if (i == 0)
            {
                lineLeft = cellScreen.X - AuxTheme.CellPadding.X;
            }

            if (i == columns - 1)
            {
                lineRight = cellScreen.X + cellAvailable + AuxTheme.CellPadding.X;
            }

            ImGui.PushID($"{id}Header_{i}");
            using (AuxFonts.PushSmallBold())
            using (ImRaii.PushColor(ImGuiCol.Text, AuxTheme.MutedFg))
            {
                var textWidth = ImGui.CalcTextSize(label).X;
                switch (align)
                {
                    case TableAlign.Center:
                        ImGui.SetCursorPosX(cellStartX + Math.Max(0f, (cellAvailable - textWidth) / 2f));
                        break;
                    case TableAlign.Right:
                        ImGui.SetCursorPosX(cellStartX + Math.Max(0f, cellAvailable - textWidth));
                        break;
                }

                ImGui.TextUnformatted(label);
            }

            lineY = Math.Max(lineY, ImGui.GetItemRectMax().Y + AuxTheme.CellPadding.Y);

            if (i < hints.Length && hints[i].Length > 0)
            {
                ImGui.SameLine(cellStartX + cellAvailable - HintIconWidth * Scale);
                Hint(hints[i]);
            }

            ImGui.PopID();
        }

        drawList.PushClipRect(new Vector2(lineLeft, lineY - 1f), new Vector2(lineRight, lineY + 1f), false);
        drawList.AddLine(new Vector2(lineLeft, lineY - 0.5f), new Vector2(lineRight, lineY - 0.5f), ImGui.GetColorU32(AuxTheme.Rule));
        drawList.PopClipRect();
    }

    public static void Gauge(float fraction, Vector4 color, float width = 0f, float height = 0f)
    {
        var min = ImGui.GetCursorScreenPos();
        var size = new Vector2(width > 0f ? width : ImGui.GetContentRegionAvail().X, height > 0f ? height : ImGui.GetFrameHeight());
        Gauge(min, min + size, fraction, color);
    }

    public static void Gauge(Vector2 min, Vector2 max, float fraction, Vector4 color)
    {
        var (fillMin, fillMax) = AuxGauge.FillRect(min, max, fraction);
        if (fillMax.X > fillMin.X)
        {
            ImGui.GetWindowDrawList().AddRectFilled(fillMin, fillMax, ImGui.GetColorU32(color));
        }
    }

    public static int FooterBar(params FooterAction[] actions)
    {
        var windowPosition = ImGui.GetWindowPos();
        var windowSize = ImGui.GetWindowSize();
        var height = FooterHeight;
        var min = new Vector2(windowPosition.X, windowPosition.Y + windowSize.Y - height);
        var max = windowPosition + windowSize;
        var drawList = ImGui.GetWindowDrawList();

        drawList.AddRectFilled(min, max, ImGui.GetColorU32(AuxTheme.NavBg));
        drawList.AddLine(min, new Vector2(max.X, min.Y), ImGui.GetColorU32(AuxTheme.Rule));

        var spacing = AuxTheme.ItemSpacing.X;
        var totalWidth = 0f;
        foreach (var action in actions)
        {
            totalWidth += TextButtonWidth(action.Label) + spacing;
        }

        totalWidth -= spacing;
        var x = max.X - AuxTheme.WindowPadding.X - totalWidth;
        var y = min.Y + MathF.Floor((height - ImGui.GetFrameHeight()) / 2f);
        var clicked = -1;

        for (var i = 0; i < actions.Length; i++)
        {
            ImGui.SetCursorScreenPos(new Vector2(x, y));
            if (TextButton(actions[i].Label, actions[i].Primary))
            {
                clicked = i;
            }

            x += TextButtonWidth(actions[i].Label) + spacing;
        }

        return clicked;
    }

    public static void NavLabel(string text)
    {
        using (AuxFonts.PushSmallBold())
        {
            var upper = text.ToUpperInvariant();
            var textSize = ImGui.CalcTextSize(upper);
            var padding = new Vector2(ChipPaddingX, ChipPaddingY) * Scale;
            var position = ImGui.GetCursorScreenPos();
            ImGui.Dummy(textSize + padding * 2f);
            ImGui.GetWindowDrawList().AddText(position + padding, ImGui.GetColorU32(AuxTheme.NavFg), upper);
        }
    }

    public static void StatusChip(string label, bool enabled, string? tooltip = null)
    {
        using (AuxFonts.PushSmallBold())
        {
            var text = label.ToUpperInvariant();
            var textSize = ImGui.CalcTextSize(text);
            var padding = new Vector2(ChipPaddingX, ChipPaddingY) * Scale;
            var position = ImGui.GetCursorScreenPos();
            ImGui.Dummy(textSize + padding * 2f);
            var hovered = ImGui.IsItemHovered();

            var drawList = ImGui.GetWindowDrawList();
            drawList.AddText(position + padding, ImGui.GetColorU32(enabled ? AuxTheme.Accent : AuxTheme.MutedFg), text);
            if (enabled)
            {
                var bottom = position.Y + textSize.Y + padding.Y * 2f;
                var thickness = AccentLineThickness * Scale;
                drawList.AddRectFilled(new Vector2(position.X, bottom - thickness), new Vector2(position.X + textSize.X + padding.X * 2f, bottom), ImGui.GetColorU32(AuxTheme.Accent));
            }

            if (enabled && hovered && tooltip != null)
            {
                ImGui.SetTooltip(tooltip);
            }
        }
    }

    private static ImRaii.ColorDisposable PushInputFrame() => ImRaii.PushColor(ImGuiCol.FrameBg, AuxTheme.Transparent)
        .Push(ImGuiCol.FrameBgActive, AuxTheme.Transparent);

    private static void DrawItemUnderline()
    {
        DrawUnderline(ImGui.GetWindowDrawList(), ImGui.GetItemRectMin(), ImGui.GetItemRectMax(), ImGui.IsItemActive() || ImGui.IsItemFocused());
    }

    private static void DrawUnderline(ImDrawListPtr drawList, Vector2 min, Vector2 max, bool lit)
    {
        var y = max.Y - 0.5f;
        drawList.AddLine(new Vector2(min.X, y), new Vector2(max.X, y), ImGui.GetColorU32(lit ? AuxTheme.Accent : AuxTheme.AccentDim), AuxTheme.UnderlineThickness);
    }
}
