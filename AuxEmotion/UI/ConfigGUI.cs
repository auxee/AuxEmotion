using Dalamud.Interface.Utility;
using Dalamud.Plugin;
using Dalamud.Bindings.ImGui;
using Num = System.Numerics;
using AuxEmotion.Services;
using System;
using ECommons.DalamudServices;
using AuxUi;
using Dalamud.Interface.Utility.Raii;

namespace AuxEmotion
{
    public partial class AuxEmotionPlugin : IDalamudPlugin
    {
        private static readonly string[] tabLabels = ["Settings", "Chat Reaction", "Emote Reaction"];
        private static readonly TitleBarAction[] titleActions = [new("Revert Change"), new("Save"), new("Save and Close", true)];
        private int selectedTab;

        private unsafe void DrawConfigUI()
        {
            chkSize = 23f * ImGuiHelpers.GlobalScale;
            if (!isOpenConfig)
            {
                return;
            }

#if DEBUG
            const string windowTitle = "[DEBUG] AuxEmotion";
            const string windowName = "[DEBUG] AuxEmotion Configuration Window##AuxEmotionConfigWin";
#else
            const string windowTitle = "AuxEmotion";
            const string windowName = "AuxEmotion Configuration Window##AuxEmotionConfigWin";
#endif
            using (AuxTheme.Push())
            {
                ImGui.SetNextWindowSize(new Num.Vector2(980f, 560f) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
                ImGui.SetNextWindowSizeConstraints(new Num.Vector2(520f, 320f) * ImGuiHelpers.GlobalScale, new Num.Vector2(float.MaxValue, float.MaxValue));
                ImGui.Begin(windowName, ref isOpenConfig, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);

                var titleBar = AuxWidgets.TitleBar(windowTitle, titleActions, AuxWidgets.CloseButton);
                switch (titleBar.Action)
                {
                    case 0:
                        LoadConfig();
                        InitializeGui();
                        break;
                    case 1:
                        SaveConfig();
                        break;
                    case 2:
                        SaveConfig();
                        isOpenConfig = false;
                        break;
                }
                if (titleBar.Button == 0)
                {
                    isOpenConfig = false;
                }

                selectedTab = AuxWidgets.Tabs("##MainTabs", tabLabels, selectedTab);
                ImGui.Dummy(new Num.Vector2(0f, 2f * ImGuiHelpers.GlobalScale));

                if (ImGui.BeginChild("##MainChild", new Num.Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), false, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
                {
                    switch (selectedTab)
                    {
                        case 0:
                            DrawSettingsTab();
                            break;
                        case 1:
                            DrawChatReactionTab();
                            break;
                        default:
                            DrawEmoteReactionTab();
                            break;
                    }
                }
                ImGui.EndChild();

                ImGui.End();
            }
        }

        private void DrawSettingsTab()
        {
            ImGui.BeginChild("##TabSettingsChild", new Num.Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y));

            AuxWidgets.SectionLabel("General");
            AuxWidgets.Checkbox("Enable plugin##isActived", ref configGUI.isActived);
            ImGui.SameLine();
            AuxWidgets.Hint($"Quick disable command:{Environment.NewLine}/auxe disable");

            AuxWidgets.Checkbox("Enable Chat Reaction##isChatActive", ref configGUI.isChatActive);
            ImGui.SameLine();
            AuxWidgets.Hint("No dedicated slash command for this toggle.");

            AuxWidgets.Checkbox("Enable Emote Reaction##isReactionActive", ref configGUI.isReactionActive);
            ImGui.SameLine();
            AuxWidgets.Hint("No dedicated slash command for this toggle.");

            AuxWidgets.Checkbox("Enable while in AFK or Busy state##executeReactionWhileBusyAfk", ref configGUI.executeReactionWhileBusyAfk);

            AuxWidgets.Checkbox("Hide Emote Log##isLogHide", ref configGUI.isChatLogHidden);
            ImGui.SameLine();
            AuxWidgets.Hint("When enabled the emotes used by AuxEmotion will not display the log message.");

            ImGui.NewLine();
            AuxWidgets.SectionLabel("Advanced");
            AuxWidgets.Checkbox("Resume Interrupted Loop Emote##resumeEmoteLoop", ref configGUI.resumeEmoteLoop);
            ImGui.SameLine();
            AuxWidgets.Hint("Enable this option to automatically resume the emote loop if it gets interrupted by the plugin.");

            DrawDragInt("Delay before resuming loop emote (ms)##resumeEmoteDelay", ref configGUI.resumeEmoteDelay, 100, 100 * ImGuiHelpers.GlobalScale, 0, int.MaxValue);
            ImGui.SameLine();
            AuxWidgets.Hint("Delay before resuming loop emote after the end of the executed emote.");

            AuxWidgets.Checkbox("Remove target when resuming loop emote##resumeEmoteDetarget", ref configGUI.resumeEmoteDetarget);

            AuxWidgets.Checkbox("Restore direction when resuming loop emote##resumeEmoteRestoreRotation", ref configGUI.resumeEmoteRestoreRotation);

            DrawDragInt("Emote/Player threshold before lock##maxReactionsCache", ref configGUI.maxReactionsCache, 1, 100 * ImGuiHelpers.GlobalScale, 1, 100);
            ImGui.SameLine();
            AuxWidgets.Hint($"When receiving multiple triggers of the same emote from the same target, only react to the specified amount.{Environment.NewLine}This stop the plugin from queuing too many reactions to the same event and prevents emote spamming from triggers.");

            DrawDragInt("Time before lock expires (s)##timeoutReactionsCache", ref configGUI.timeoutReactionsCache, 1, 100 * ImGuiHelpers.GlobalScale, 1, int.MaxValue);
#if DEBUG
            ImGui.Text($"Cache {reactionCache.GetTotalCacheKeys()}");
#endif

            DrawDragInt("Time for each letter before triggering emote (ms)##charDelay", ref configGUI.charDelay, 5, 100 * ImGuiHelpers.GlobalScale, 0, int.MaxValue);
            ImGui.SameLine();
            AuxWidgets.Hint($"This value represents the delay in milliseconds for each character within the message.{Environment.NewLine}The total delay for the emote will be calculated based on the position of the keyword within the message.{Environment.NewLine}The delay increases proportionally to the distance of the keyword from the start of the message.{Environment.NewLine}Minimum total delay is 500ms.");

            ImGui.NewLine();
            AuxWidgets.SectionLabel("Other/UI");
            AuxWidgets.Checkbox("Use Plus/Minus buttons for numeric inputs.##showPlusMinus", ref configGUI.showPlusMinus);

            AuxWidgets.Checkbox("Show Plugin Status Window##isActiveWinShow", ref configGUI.isStatusWinOpen);
            ImGui.SameLine();
            AuxWidgets.Hint("Enable this option to display a status window that shows whether the plugin is active and which functions are currently enabled.");

            AuxWidgets.Checkbox("Lock Plugin Status Window##isActiveWinLock", ref configGUI.isStatusWinLocked);

            ImGui.EndChild();
        }

        private void DrawChatReactionTab()
        {
            ImGui.BeginChild("##TabKeywordsChild", new Num.Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y));

            AuxWidgets.SectionLabel("Enabled channels");
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Select chat channels to enable emote execution control.");
            }

            DrawChannelList();

            ImGui.NewLine();

            var text = "Clear Keywords list";
            var clearButtonWidth = AuxWidgets.TextButtonWidth(text);
            AuxWidgets.SectionLabel("Keywords list", null, clearButtonWidth + ImGui.GetStyle().ItemSpacing.X);
            ImGui.SameLine(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - clearButtonWidth);
            if (AuxWidgets.TextButton(text))
            {
                if (ImGui.GetIO().KeyShift)
                {
                    count--;
                    if (count == 0)
                    {
                        configGUI.triggerLists.Clear();
                        configGUI.triggerOrder.Clear();
                        count = 3;
                    }
                }
            }
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip($"Hold SHIFT and CLICK {count} times to clear the whole list.");
            }
            else
            {
                count = 3;
            }

            DrawTriggerList();

            ImGui.EndChild();
        }

        private void DrawEmoteReactionTab()
        {
            ImGui.BeginChild("##BWLists", new Num.Vector2(260f * ImGuiHelpers.GlobalScale, ImGui.GetContentRegionAvail().Y));
            using (ImRaii.PushColor(ImGuiCol.ChildBg, AuxTheme.RowAlt))
            {
                ImGui.BeginChild("##BoxWhitelist", new Num.Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y / 2), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                DrawWhiteList();
                ImGui.EndChild();

                ImGui.BeginChild("##BoxBlacklist", new Num.Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
                DrawBlackList();
                ImGui.EndChild();
            }
            ImGui.EndChild();

            ImGui.SameLine();
            DrawReactionList();
        }

        private void SaveConfig()
        {
            config = configGUI;
            config.Save();
            InitializeConfig();
            if (Svc.ClientState.IsLoggedIn)
            {
                isOpenStatusWin = config.isStatusWinOpen;
            }
            else
            {
                isOpenStatusWin = false;
            }
        }
    }
}
