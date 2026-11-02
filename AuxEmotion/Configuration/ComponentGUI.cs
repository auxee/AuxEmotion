using AuxEmotion.Configuration.Data;
using AuxEmotion.Utility;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using AuxUi;
using ECommons.ImGuiMethods;
using FFXIVClientStructs.FFXIV.Common.Math;
using System;
using System.Collections.Generic;
using System.Linq;
using Num = System.Numerics;

namespace AuxEmotion
{
    public partial class AuxEmotionPlugin : IDalamudPlugin
    {
        private TriggerData newTriggerData = new();

        private CharacterData? selectedCharacter = null;
        private string searchCharacter = string.Empty;
        private string manualWhiteListName = string.Empty;
        private CharacterData? selectedCharacterB = null;
        private string searchCharacterB = string.Empty;
        private string manualBlackListName = string.Empty;
        private CharacterData copyCharacterReaction = new();

        private string searchInput = string.Empty;
        private string oldTrigger = string.Empty;
        private string newTrigger = string.Empty;

        private ReactionData newReactionData = new();

        private int beginDrag = -1;
        private int endDrag = -1;
        private Vector2 endDragPosition = new();

        private string stringRefIgnore = string.Empty;
        private int count = 3;

        private float chkSize { get; set; }
        private void InitializeGui()
        {
            configGUI.whiteList.TryGetValue(AuxEmotionConfig.EveryoneKey, out selectedCharacter);
        }

        public void DrawDragInt(string label, ref int v, int v_speed, float item_width = 120, int v_min = 0, int v_max = 0)
        {
            string[] text = label.Split(["##"], StringSplitOptions.None);
            float buttonSize = 0f;
            if (configGUI.showPlusMinus == true)
            {
                buttonSize = ((chkSize * 2f) + 1f);
            }
            ImGui.SetNextItemWidth(item_width - buttonSize);
            AuxWidgets.DragInt($"##{text[1]}", ref v, v_speed, v_min, v_max);
            if (configGUI.showPlusMinus == true)
            {
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
                {
                    ImGui.PushID($"buttons_{text[1]}");
                    ImGui.SameLine();
                    if (AuxWidgets.IconButton(FontAwesomeIcon.Plus, "plus", null, false, new Num.Vector2(chkSize)))
                    {
                        v = (v_min == 0 && v_max == 0) ? v + v_speed : Math.Min(v_max, v + v_speed);
                    }
                    ImGui.SameLine();
                    if (AuxWidgets.IconButton(FontAwesomeIcon.Minus, "minus", null, false, new Num.Vector2(chkSize)))
                    {
                        v = (v_min == 0 && v_max == 0) ? v - v_speed : Math.Max(v_min, v - v_speed);
                    }
                    ImGui.PopID();
                }
            }
            if (!string.IsNullOrEmpty(text[0]))
            {
                ImGui.SameLine();
                ImGui.Text(text[0]);
            }
        }

        public void DrawDragFloat(string label, ref float v, float v_speed, float item_width = 120, float v_min = 0, float v_max = 0)
        {
            string[] text = label.Split(["##"], StringSplitOptions.None);
            float buttonSize = 0f;
            if (configGUI.showPlusMinus == true)
            {
                buttonSize = ((chkSize * 2f) + 1f);
            }
            ImGui.SetNextItemWidth(item_width - buttonSize);
            AuxWidgets.DragFloat($"##{text[1]}", ref v, v_speed, v_min, v_max);
            if (configGUI.showPlusMinus == true)
            {
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
                {
                    ImGui.PushID($"buttons_{text[1]}");
                    ImGui.SameLine();
                    if (AuxWidgets.IconButton(FontAwesomeIcon.Plus, "plus", null, false, new Num.Vector2(chkSize)))
                    {
                        v = (v_min == 0 && v_max == 0) ? v + v_speed : Math.Min(v_max, v + v_speed);
                    }
                    ImGui.SameLine();
                    if (AuxWidgets.IconButton(FontAwesomeIcon.Minus, "minus", null, false, new Num.Vector2(chkSize)))
                    {
                        v = (v_min == 0 && v_max == 0) ? v - v_speed : Math.Max(v_min, v - v_speed);
                    }
                    ImGui.PopID();
                }
            }
            if (!string.IsNullOrEmpty(text[0]))
            {
                ImGui.SameLine();
                ImGui.Text(text[0]);
            }
        }

        public static void SetNextItemIndentation(int numberIndentation)
        {
            var indentation = ImGuiHelpers.ScaledVector2(27f * numberIndentation, 0f);
            ImGui.SetCursorPosX(indentation.X);
        }

        public static bool IconTextFrame(uint previewIcon, string previewText, bool isSelected = false)
        {
            var frameSize = new Num.Vector2(ImGui.CalcItemWidth(), ImGui.GetFrameHeight());
            var pressed = AuxWidgets.RoundedSelectable($"##iconTextFrame_{previewIcon}_{previewText}", isSelected, frameSize);
            var min = ImGui.GetItemRectMin();
            var drawlist = ImGui.GetWindowDrawList();

            IDalamudTextureWrap? icon = null;
            if (previewIcon != 0)
            {
                try
                {
                    icon = Svc.Texture.GetFromGameIcon(previewIcon).GetWrapOrDefault();
                }
                catch { }
            }
            if (icon != null) drawlist.AddImage(icon.Handle, min, min + new Num.Vector2(frameSize.Y));

            var textSize = ImGui.CalcTextSize(previewText);
            var textColor = AuxTheme.ContentFg;
            drawlist.AddText(min + new Num.Vector2(frameSize.Y + ImGui.GetStyle().FramePadding.X, frameSize.Y / 2f - textSize.Y / 2f), ImGui.GetColorU32(textColor), previewText);
            return pressed;
        }

        private void DrawComboEmote(string ID, uint previewID, bool isExpression, ref uint outEmoteID, ref string outEmoteCommand)
        {
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
            {
                if (ThreadLoadImageHandler.TryGetIconTextureWrap(EmoteIdentifier.FetchIcon(previewID), false, out var iconPicture) && iconPicture != null)
                {
                    ImGui.Image(iconPicture.Handle, new(chkSize));
                }
                ImGui.SameLine();
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);

                IReadOnlyList<EmoteIdentifier> listEmote;
                if (isExpression)
                {
                    listEmote = EmoteIdentifier.ListExpression;
                }
                else
                {
                    listEmote = EmoteIdentifier.ListEmote;
                }
                if (AuxWidgets.BeginCombo($"##{ID}", EmoteIdentifier.FetchName(previewID), ImGuiComboFlags.HeightLargest))
                {
                    if (ImGui.IsWindowAppearing())
                    {
                        searchInput = string.Empty;
                        ImGui.SetKeyboardFocusHere();
                    }

                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    AuxWidgets.InputTextWithHint($"##{ID}SearchInput", "Search...", ref searchInput, 40);
                    if (ImGui.BeginChild($"##{ID}SearchScroll", new Vector2(ImGui.GetContentRegionAvail().X, 300)))
                    {
                        foreach (var emote in listEmote)
                        {
                            if (!string.IsNullOrWhiteSpace(searchInput))
                            {
                                if (!(emote.Name.Contains(searchInput, StringComparison.InvariantCultureIgnoreCase) || (ushort.TryParse(searchInput, out var searchShort) && searchShort == emote.EmoteID))) continue;
                            }

                            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                            if (IconTextFrame(emote.Icon, emote.Name, emote.EmoteID == previewID))
                            {
                                outEmoteID = emote.EmoteID;
                                outEmoteCommand = emote.EmoteCommand;
                                ImGui.CloseCurrentPopup();
                            }
                        }
                    }
                    ImGui.EndChild();
                    ImGui.EndCombo();
                }
            }
        }

        private static bool MouseWithin(Vector2 min, Vector2 max)
        {
            var mousePos = ImGui.GetMousePos();
            return mousePos.X >= min.X && mousePos.Y <= max.X && mousePos.Y >= min.Y && mousePos.Y <= max.Y;
        }

        private static void DrawMacroSlotEditor(string id, ref int macroBook, ref int macroSlot)
        {
            var availWidth = ImGui.GetContentRegionAvail().X;
            var half = Math.Max(58f * ImGuiHelpers.GlobalScale, (availWidth - (8f * ImGuiHelpers.GlobalScale)) / 2f);

            var comboBook = Math.Clamp(macroBook, 0, 2);
            ImGui.SetNextItemWidth(half);
            if (AuxWidgets.BeginCombo($"##{id}Book", comboBook switch
            {
                1 => "1",
                2 => "2",
                _ => "Off",
            }))
            {
                if (AuxWidgets.ComboItem("Off", comboBook == 0))
                {
                    comboBook = 0;
                }
                if (AuxWidgets.ComboItem("1", comboBook == 1))
                {
                    comboBook = 1;
                }
                if (AuxWidgets.ComboItem("2", comboBook == 2))
                {
                    comboBook = 2;
                }
                ImGui.EndCombo();
            }
            macroBook = comboBook;

            ImGui.SameLine();
            ImGui.SetNextItemWidth(half);
            if (AuxWidgets.InputInt($"S##{id}", ref macroSlot))
            {
                macroSlot = Math.Clamp(macroSlot, 0, 99);
            }
        }

        private void DrawChannelList()
        {
            ImGui.Columns(2);

            foreach (var e in (XivChatType[])Enum.GetValues(typeof(XivChatType)))
            {
                if (!configGUI.visibleChannelsDictionary.TryGetValue(e, out var visible) || !visible)
                    continue;

                var enabled = configGUI.allowedChannels.Contains(e);
                if (AuxWidgets.Checkbox($"{e}", ref enabled))
                {
                    if (enabled) configGUI.allowedChannels.Add(e);
                    else configGUI.allowedChannels.Remove(e);
                }
                ImGui.NextColumn();
            }

            ImGui.Columns(1);
        }

        public void DrawTriggerList()
        {
            if (ImGui.BeginChild("TriggerChild", new Vector2(ImGui.GetContentRegionAvail().X, Math.Max(ImGui.GetContentRegionAvail().Y, 200f * ImGuiHelpers.GlobalScale)), false))
            {
                var keywordWidth = Math.Max(100f, ImGui.GetContentRegionAvail().X - ((chkSize + 150f * 2 + 80f + 80f + 245f) * ImGuiHelpers.GlobalScale));
                if (AuxWidgets.BeginTable("HeaderTriggerTable", 7))
                {
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, chkSize * 3f + 3f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Keyword", ImGuiTableColumnFlags.WidthFixed, keywordWidth);
                    ImGui.TableSetupColumn("Emote", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Expression", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Contained", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Case Sensitive", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Priority", ImGuiTableColumnFlags.WidthFixed, ImGui.GetContentRegionAvail().X);
                    string[] infoMarkers = {
                             ""
                            ,""
                            ,""
                            ,""
                            ,""
                            ,""
                            ,"The keyword with the lowest priority will take precedence if multiple applicable triggers are found in a chat message."
                    };
                    AuxWidgets.TableHeaderRow("TriggerTable", infoMarkers, TableAlign.Left, TableAlign.Left, TableAlign.Left, TableAlign.Left, TableAlign.Center, TableAlign.Center, TableAlign.Center);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.PushID("##newTrigger");
                    using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
                    {
                        ImGui.Dummy(new Vector2((chkSize * 2f) + 1f, 0f));
                        ImGui.SameLine();
                        if (AuxWidgets.IconButton(FontAwesomeIcon.Plus, "add", null, false, new Num.Vector2(chkSize)))
                        {
                            AddNewTrigger();
                        }
                    }

                    ImGui.TableNextColumn();
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    AuxWidgets.InputText("##newTrigger", ref newTriggerData.trigger, 100);

                    ImGui.TableNextColumn();
                    DrawComboEmote("newEmote", newTriggerData.emoteID, false, ref newTriggerData.emoteID, ref newTriggerData.emoteCommand);

                    ImGui.TableNextColumn();
                    DrawComboEmote("newExpression", newTriggerData.expressionID, true, ref newTriggerData.expressionID, ref newTriggerData.expressionCommand);

                    ImGui.TableNextColumn();
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2f - chkSize / 2f);
                    AuxWidgets.Checkbox("##newIsContains", ref newTriggerData.isContains);

                    ImGui.TableNextColumn();
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2f - chkSize / 2f);
                    AuxWidgets.Checkbox("##newIsCaseSensitive", ref newTriggerData.isCaseSensitive);

                    ImGui.TableNextColumn();
                    DrawDragInt("##newPriority", ref newTriggerData.priority, 10, ImGui.GetContentRegionAvail().X);

                    ImGui.PopID();
                    ImGui.EndTable();

                    if (AuxWidgets.BeginTable("RowsTriggerTable", 7, true))
                    {
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, chkSize * 3f + 3f * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, keywordWidth);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                        ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, ImGui.GetContentRegionAvail().X);

                        string deleteTrigger = string.Empty;
                        bool isModified = false;
                        var i = 0;
                        var windowMax = ImGui.GetWindowPos() + ImGui.GetWindowSize();
                        foreach (var key in configGUI.triggerOrder)
                        {
                            if (configGUI.triggerLists.TryGetValue(key, out var triggerList))
                            {
                                var triggerData = triggerList.First(t => t.trigger == key);
                                var trigger = key;
                                if (triggerData != null)
                                {
                                    ImGui.PushID($"trigger_{key}");

                                    ImGui.TableNextRow();
                                    ImGui.TableNextColumn();

                                    using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
                                    {
                                        AuxWidgets.IconButton(FontAwesomeIcon.ArrowsUpDown, "drag", null, false, new Num.Vector2(chkSize));
                                        if (beginDrag == -1 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                                        {
                                            beginDrag = i;
                                            endDrag = i;
                                            endDragPosition = ImGui.GetItemRectMin();
                                        }

                                        if (beginDrag >= 0 && MouseWithin(ImGui.GetItemRectMin(), new Vector2(windowMax.X, ImGui.GetItemRectMax().Y)))
                                        {
                                            endDrag = i;
                                            endDragPosition = ImGui.GetItemRectMin();
                                        }

                                        ImGui.SameLine();
                                        if (AuxWidgets.IconButton(triggerData.isLocked ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen, "lock", null, false, new Num.Vector2(chkSize)))
                                        {
                                            if (triggerData.isLocked == false || ImGui.GetIO().KeyShift)
                                                triggerData.isLocked = !triggerData.isLocked;
                                        }
                                        if (ImGui.IsItemHovered())
                                        {
                                            if (triggerData.isLocked && ImGui.GetIO().KeyShift)
                                            {
                                                ImGui.SetTooltip("Unlock Entry.");
                                            }
                                            else if (triggerData.isLocked)
                                            {
                                                ImGui.SetTooltip("Hold SHIFT to unlock.");
                                            }
                                            else
                                            {
                                                ImGui.SetTooltip("Lock Entry.");
                                            }
                                        }
                                        if (triggerData.isLocked) ImGui.BeginDisabled(triggerData.isLocked);

                                        ImGui.SameLine();
                                        if (AuxWidgets.IconButton(FontAwesomeIcon.Trash, "delete", null, true, new Num.Vector2(chkSize)) && ImGui.GetIO().KeyShift)
                                        {
                                            deleteTrigger = triggerData.trigger;
                                        }
                                        if (ImGui.IsItemHovered() && !ImGui.GetIO().KeyShift)
                                        {
                                            ImGui.SetTooltip("Hold SHIFT to delete.");
                                        }
                                    }

                                    ImGui.TableNextColumn();
                                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                                    if (AuxWidgets.InputText("##trigger", ref trigger, 100))
                                    {
                                        newTrigger = trigger;
                                        if (string.IsNullOrEmpty(oldTrigger))
                                        {
                                            oldTrigger = triggerData.trigger;
                                        }
                                    }
                                    if (ImGui.IsItemDeactivatedAfterEdit())
                                    {
                                        isModified = true;
                                    }

                                    ImGui.TableNextColumn();
                                    DrawComboEmote("emote", triggerData.emoteID, false, ref triggerData.emoteID, ref triggerData.emoteCommand);

                                    ImGui.TableNextColumn();
                                    DrawComboEmote("expression", triggerData.expressionID, true, ref triggerData.expressionID, ref triggerData.expressionCommand);

                                    ImGui.TableNextColumn();
                                    ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2f - chkSize / 2f - 4f);
                                    AuxWidgets.Checkbox("##isContains", ref triggerData.isContains);

                                    ImGui.TableNextColumn();
                                    ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2f - chkSize / 2f - 4f);
                                    AuxWidgets.Checkbox("##isCaseSensitive", ref triggerData.isCaseSensitive);

                                    ImGui.TableNextColumn();
                                    DrawDragInt("##Priority", ref triggerData.priority, 10, ImGui.GetContentRegionAvail().X);

                                    if (triggerData.isLocked) ImGui.EndDisabled();

                                    ImGui.PopID();
                                }
                            }
                            else
                            {
                                Svc.Log.Debug($"Key {key}");
                            }
                            i++;
                        }

                        if (isModified)
                        {
                            newTrigger = newTrigger.Trim();
                            ModifyTrigger();
                            newTrigger = string.Empty;
                            oldTrigger = string.Empty;
                        }

                        if (!string.IsNullOrEmpty(deleteTrigger))
                        {
                            if (configGUI.triggerLists.TryGetValue(deleteTrigger, out var value))
                            {
                                TriggerData? toRemove = value.Find(v => v.trigger == deleteTrigger);
                                if (toRemove != null)
                                {
                                    value.Remove(toRemove);
                                    if (value.Count == 0) configGUI.triggerLists.Remove(deleteTrigger);

                                    int index = configGUI.triggerOrder.IndexOf(deleteTrigger);
                                    if (index != -1)
                                    {
                                        configGUI.triggerOrder.RemoveAt(index);
                                    }
                                }
                            }
                            deleteTrigger = string.Empty;
                        }

                        ImGui.EndTable();

                        if (beginDrag >= 0)
                        {
                            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
                            {
                                if (endDrag != beginDrag)
                                {
                                    var move = configGUI.triggerOrder[beginDrag];
                                    configGUI.triggerOrder.RemoveAt(beginDrag);
                                    configGUI.triggerOrder.Insert(endDrag, move);
                                }

                                beginDrag = -1;
                                endDrag = -1;
                            }
                            else
                            {
                                var dl = ImGui.GetWindowDrawList();
                                dl.AddLine(endDragPosition, endDragPosition + new Vector2(ImGui.GetWindowContentRegionMax().X, 0), ImGui.GetColorU32(ImGuiCol.DragDropTarget), 2 * ImGuiHelpers.GlobalScale);
                            }
                        }
                    }
                }
            }
            ImGui.EndChild();
        }

        private void AddNewTrigger()
        {
            newTriggerData.trigger = newTriggerData.trigger.Trim();
            if (!string.IsNullOrEmpty(newTriggerData.trigger))
            {
                if (configGUI.triggerLists.TryGetValue(newTriggerData.trigger, out var value))
                {
                    if (value.Exists(v => v.trigger == newTriggerData.trigger))
                    {
                        Svc.Toasts.ShowError($"Trigger '{newTriggerData.trigger}' already exists.");
                        return;
                    }
                    else
                    {
                        value.Add(newTriggerData);
                        configGUI.triggerLists[newTriggerData.trigger] = value;
                    }
                }
                else
                {
                    configGUI.triggerLists.Add(newTriggerData.trigger.ToLower(), new List<TriggerData> { newTriggerData });
                }
                configGUI.triggerOrder.Add(newTriggerData.trigger);
                newTriggerData = new TriggerData();
            }
        }

        private void ModifyTrigger()
        {
            if (string.IsNullOrEmpty(newTrigger)) return;

            configGUI.triggerLists.TryGetValue(oldTrigger, out var value);
            if (value == null) return;

            TriggerData editedTriggerData = new TriggerData();
            editedTriggerData.CopyValue(value.First(v => v.trigger == oldTrigger));
            editedTriggerData.trigger = newTrigger;

            if (configGUI.triggerLists.TryGetValue(newTrigger, out var triggerArray))
            {
                bool isExists = false;

                if (newTrigger.Equals(oldTrigger, StringComparison.CurrentCultureIgnoreCase))
                {
                    if (triggerArray.Any(v => v.trigger == newTrigger))
                    {
                        isExists = true;
                        Svc.Log.Debug("Matched more than once for {0}", newTrigger);
                    }
                }
                else
                {
                    if (triggerArray.Exists(v => v.trigger == newTrigger))
                    {
                        isExists = true;
                        Svc.Log.Debug("Trigger {0} exists", newTrigger);
                    }
                }

                if (isExists)
                {
                    Svc.Toasts.ShowError($"Trigger '{newTrigger}' already exists.");
                    return;
                }
                else
                {
                    triggerArray.Add(editedTriggerData);
                    configGUI.triggerLists[editedTriggerData.trigger] = triggerArray;
                }
            }
            else
            {
                configGUI.triggerLists.Add(editedTriggerData.trigger.ToLower(), new List<TriggerData> { editedTriggerData });
            }

            editedTriggerData = value.First(v => v.trigger == oldTrigger);
            value.Remove(editedTriggerData);
            if (value.Count == 0)
            {
                configGUI.triggerLists.Remove(oldTrigger);
            }
            int index = configGUI.triggerOrder.IndexOf(oldTrigger);
            if (index != -1)
            {
                configGUI.triggerOrder[index] = newTrigger;
            }
        }

        private float ListFooterReserve() => chkSize + (ImGui.GetStyle().ItemSpacing.Y * 2f) + 1f;

        public void DrawWhiteList()
        {
            AuxWidgets.SectionLabel("Whitelist", $"Reactions configured for characters in the whitelist will take priority over Everyone reactions.{Environment.NewLine}If no specific reaction is set for a whitelisted character, the Everyone reaction will be used instead.{Environment.NewLine}You can quickly add a person to the whitelist using:{Environment.NewLine}/auxe a <name>{Environment.NewLine}/auxe add <name>");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            AuxWidgets.InputTextWithHint("##SearchWhiteList", "Search...", ref searchCharacter, 255);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - chkSize - ImGui.GetStyle().ItemSpacing.X);
            AuxWidgets.InputTextWithHint("##ManualWhiteListName", "Name or Name@World", ref manualWhiteListName, 255);
            ImGui.SameLine();
            if (AuxWidgets.IconButton(FontAwesomeIcon.Plus, "addManualWhiteList", null, false, new Num.Vector2(chkSize)))
            {
                if (configGUI.TryToAddBWListByName(manualWhiteListName, true))
                {
                    manualWhiteListName = string.Empty;
                    InitializeGui();
                }
            }
            ImGui.Separator();

            ImGui.BeginChild("whitelistChildFrame", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - ListFooterReserve()), false, ImGuiWindowFlags.NoBackground);
            foreach (var player in configGUI.whiteList)
            {
                if (!string.IsNullOrEmpty(searchCharacter))
                {
                    if (!player.Key.Contains(searchCharacter, StringComparison.CurrentCultureIgnoreCase)) continue;
                }
                using (ImRaii.PushColor(ImGuiCol.Text, player.Value.isEnabled ? AuxTheme.ContentFg : AuxTheme.MutedFg))
                {
                    AuxWidgets.Checkbox($"##{player.Key}Enabled", ref player.Value.isEnabled);
                    ImGui.SameLine();
                    if (AuxWidgets.RoundedSelectable($"{player.Key}##w{player.Key}", selectedCharacter == player.Value, new Num.Vector2(0f, ImGui.GetFrameHeight()), 8f))
                    {
                        selectedCharacter = player.Value;
                    }
                }
            }
            ImGui.EndChild();

            ImGui.Separator();

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
            {
                if (AuxWidgets.IconButton(FontAwesomeIcon.PersonCirclePlus, "addWhitelistTarget", "Add targeted player to the whitelist.", false, new Num.Vector2(chkSize)))
                {
                    configGUI.TryToAddBWList(true);
                }
                ImGui.SameLine();
                if (AuxWidgets.IconButton(FontAwesomeIcon.Trash, "removeWhitelist", $"Hold SHIFT to remove the selected player from the white list.{Environment.NewLine}{AuxEmotionConfig.EveryoneKey} can not be removed.", true, new Num.Vector2(chkSize)) && ImGui.GetIO().KeyShift)
                {
                    if (selectedCharacter != null)
                    {
                        if (selectedCharacter.GetKey() != AuxEmotionConfig.EveryoneKey)
                        {
                            configGUI.whiteList.Remove(selectedCharacter.GetKey());
                            configGUI.whiteList.TryGetValue(AuxEmotionConfig.EveryoneKey, out selectedCharacter);
                        }
                    }
                }
            }
        }

        public void DrawBlackList()
        {
            AuxWidgets.SectionLabel("Blacklist", "No reactions will be executed for characters added to the blacklist.");
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            AuxWidgets.InputTextWithHint("##SearchWhiteList", "Search...", ref searchCharacterB, 255);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - chkSize - ImGui.GetStyle().ItemSpacing.X);
            AuxWidgets.InputTextWithHint("##ManualBlackListName", "Name or Name@World", ref manualBlackListName, 255);
            ImGui.SameLine();
            if (AuxWidgets.IconButton(FontAwesomeIcon.Plus, "addManualBlackList", null, false, new Num.Vector2(chkSize)))
            {
                if (configGUI.TryToAddBWListByName(manualBlackListName, false))
                {
                    manualBlackListName = string.Empty;
                }
            }
            ImGui.Separator();

            ImGui.BeginChild("blacklistChildFrame", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - ListFooterReserve()), false, ImGuiWindowFlags.NoBackground);
            foreach (var player in configGUI.blackList)
            {
                if (!string.IsNullOrEmpty(searchCharacterB))
                {
                    if (!player.Key.Contains(searchCharacterB, StringComparison.CurrentCultureIgnoreCase)) continue;
                }
                if (AuxWidgets.RoundedSelectable($"{player.Key}##b{player.Key}", selectedCharacterB == player.Value, new Num.Vector2(0f, ImGui.GetFrameHeight()), 8f))
                {
                    selectedCharacterB = player.Value;
                }
            }
            ImGui.EndChild();

            ImGui.Separator();

            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
            {
                if (AuxWidgets.IconButton(FontAwesomeIcon.PersonCirclePlus, "addBlacklistTarget", "Add targeted player to the blacklist.", false, new Num.Vector2(chkSize)))
                {
                    configGUI.TryToAddBWList(false);
                }
                ImGui.SameLine();
                if (AuxWidgets.IconButton(FontAwesomeIcon.Trash, "removeBlacklist", "Hold SHIFT to remove the selected player from the black list.", true, new Num.Vector2(chkSize)) && ImGui.GetIO().KeyShift)
                {
                    if (selectedCharacterB != null)
                    {
                        configGUI.blackList.Remove(selectedCharacterB.GetKey());
                        selectedCharacterB = null;
                    }
                }
            }
        }

        public void DrawReactionList()
        {
            if (ImGui.BeginChild("ReactionChildFrame", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y), false) && selectedCharacter != null)
            {
                var text = copyCharacterReaction.name == string.Empty ? "Copy" : $"Copied {copyCharacterReaction.name} reactions";
                if (AuxWidgets.TextButton($"{text}##Copy"))
                {
                    copyCharacterReaction.CopyValue(selectedCharacter);
                }
                ImGui.SameLine();
                text = copyCharacterReaction.name == string.Empty ? "Paste" : $"Paste rections into {selectedCharacter.name}";
                if (AuxWidgets.TextButton($"{text}##Paste"))
                {
                    if (copyCharacterReaction != null)
                        selectedCharacter.CopyReactionList(copyCharacterReaction.reactionList);
                    copyCharacterReaction = new();
                }

                if (AuxWidgets.BeginTable("HeaderReactionTable", 9))
                {
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, chkSize * 3f + 3f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Received Emote", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Response Emote", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Response Expression", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Response Macro", ImGuiTableColumnFlags.WidthFixed, 170f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Distance (yalms)", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Response Delay (ms)", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Target Back", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("Priority", ImGuiTableColumnFlags.WidthFixed, ImGui.GetContentRegionAvail().X);
                    string[] infoMarkers = {
                             ""
                            ,""
                            ,""
                            ,""
                            ,"Book: Off disables macro response, 1 uses Individual macros, 2 uses Shared macros. Slot: macro number 0-99."
                            ,"Sets the distance (in yalms) at which you'll respond to incoming emotes.\r\n0 means unlimited range.\r\nNote: This setting is highly affected by server latency and may not always be accurate."
                            ,""
                            ,""
                            ,"The reaction with the lowest priority will take precedence if multiple applicable reaction are found."
                    };
                    AuxWidgets.TableHeaderRow("ReactionTable", infoMarkers, TableAlign.Left, TableAlign.Left, TableAlign.Left, TableAlign.Left,
                        TableAlign.Left, TableAlign.Left, TableAlign.Center, TableAlign.Center, TableAlign.Center);

                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
                    {
                        ImGui.Dummy(new Vector2((chkSize * 2f) + 1f, 0f));
                        ImGui.SameLine();
                        if (AuxWidgets.IconButton(FontAwesomeIcon.Plus, "addReaction", null, false, new Num.Vector2(chkSize)))
                        {
                            AddNewReaction();
                        }
                    }

                    ImGui.TableNextColumn();
                    DrawComboEmote("newReceived", newReactionData.receivedEmoteID, false, ref newReactionData.receivedEmoteID, ref stringRefIgnore);

                    ImGui.TableNextColumn();
                    DrawComboEmote("newResponseEmote", newReactionData.responseEmoteID, false, ref newReactionData.responseEmoteID, ref newReactionData.responseEmoteCommand);

                    ImGui.TableNextColumn();
                    DrawComboEmote("newResponseExpression", newReactionData.responseExpressionID, true, ref newReactionData.responseExpressionID, ref newReactionData.responseExpressionCommand);

                    ImGui.TableNextColumn();
                    DrawMacroSlotEditor("newResponseMacro", ref newReactionData.responseMacroBook, ref newReactionData.responseMacroSlot);

                    ImGui.TableNextColumn();
                    DrawDragFloat("##newYalms", ref newReactionData.yalms, 0.500f, ImGui.GetContentRegionAvail().X, 0, 500);

                    ImGui.TableNextColumn();
                    DrawDragInt("##newDelay", ref newReactionData.responseDelay, 100, ImGui.GetContentRegionAvail().X);

                    ImGui.TableNextColumn();
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2f - chkSize / 2f);
                    AuxWidgets.Checkbox("##newTargetBack", ref newReactionData.targetBack);

                    ImGui.TableNextColumn();
                    DrawDragInt("##newReceivedPriority", ref newReactionData.priority, 10, ImGui.GetContentRegionAvail().X);

                    ImGui.EndTable();
                }

                if (selectedCharacter.reactionList.Count == 0)
                {
                    if (selectedCharacter.GetKey() == AuxEmotionConfig.EveryoneKey)
                        ImGui.Text($"No reactions are configured for {selectedCharacter.name}.{Environment.NewLine}You will not respond to any emotes.");
                    else
                        ImGui.Text($"No reactions are configured for {selectedCharacter.name}.{Environment.NewLine}Reactions set for Everyone will be used if available.");
                }

                if (AuxWidgets.BeginTable("RowsReceivedTable", 9, true))
                {
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, chkSize * 3f + 3f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, (chkSize + 150f) * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 170f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 120f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 80f * ImGuiHelpers.GlobalScale);
                    ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, ImGui.GetContentRegionAvail().X);

                    int deleteIndex = -1;
                    int i = 0;

                    var windowMax = ImGui.GetWindowPos() + ImGui.GetWindowSize();
                    foreach (var reactionData in selectedCharacter.reactionList)
                    {
                        ImGui.PushID($"reaction_{i}");

                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.One * ImGuiHelpers.GlobalScale))
                        {
                            AuxWidgets.IconButton(FontAwesomeIcon.ArrowsUpDown, "drag", null, false, new Num.Vector2(chkSize));
                            if (beginDrag == -1 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                            {
                                beginDrag = i;
                                endDrag = i;
                                endDragPosition = ImGui.GetItemRectMin();
                            }

                            if (beginDrag >= 0 && MouseWithin(ImGui.GetItemRectMin(), new Vector2(windowMax.X, ImGui.GetItemRectMax().Y)))
                            {
                                endDrag = i;
                                endDragPosition = ImGui.GetItemRectMin();
                            }

                            ImGui.SameLine();
                            if (AuxWidgets.IconButton(reactionData.isLocked ? FontAwesomeIcon.Lock : FontAwesomeIcon.LockOpen, "lock", null, false, new Num.Vector2(chkSize)))
                            {
                                if (reactionData.isLocked == false || ImGui.GetIO().KeyShift)
                                    reactionData.isLocked = !reactionData.isLocked;
                            }
                            if (ImGui.IsItemHovered())
                            {
                                if (reactionData.isLocked && ImGui.GetIO().KeyShift)
                                {
                                    ImGui.SetTooltip("Unlock Entry.");
                                }
                                else if (reactionData.isLocked)
                                {
                                    ImGui.SetTooltip("Hold SHIFT to unlock.");
                                }
                                else
                                {
                                    ImGui.SetTooltip("Lock Entry.");
                                }
                            }
                            if (reactionData.isLocked) ImGui.BeginDisabled(reactionData.isLocked);

                            ImGui.SameLine();
                            if (AuxWidgets.IconButton(FontAwesomeIcon.Trash, "delete", null, true, new Num.Vector2(chkSize)) && ImGui.GetIO().KeyShift)
                            {
                                deleteIndex = i;
                            }
                            if (ImGui.IsItemHovered() && !ImGui.GetIO().KeyShift)
                            {
                                ImGui.SetTooltip("Hold SHIFT to delete.");
                            }
                        }

                        ImGui.TableNextColumn();
                        DrawComboEmote("rowReceived", reactionData.receivedEmoteID, false, ref reactionData.receivedEmoteID, ref stringRefIgnore);

                        ImGui.TableNextColumn();
                        DrawComboEmote("rowResponseEmote", reactionData.responseEmoteID, false, ref reactionData.responseEmoteID, ref reactionData.responseEmoteCommand);

                        ImGui.TableNextColumn();
                        DrawComboEmote("rowResponseExpression", reactionData.responseExpressionID, true, ref reactionData.responseExpressionID, ref reactionData.responseExpressionCommand);

                        ImGui.TableNextColumn();
                        DrawMacroSlotEditor("rowResponseMacro", ref reactionData.responseMacroBook, ref reactionData.responseMacroSlot);

                        ImGui.TableNextColumn();
                        DrawDragFloat("##rowYalms", ref reactionData.yalms, 0.500f, ImGui.GetContentRegionAvail().X, 0, 500);

                        ImGui.TableNextColumn();
                        DrawDragInt("##rowDelay", ref reactionData.responseDelay, 100, ImGui.GetContentRegionAvail().X);

                        ImGui.TableNextColumn();
                        ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2f - chkSize / 2f - 4f);
                        AuxWidgets.Checkbox("##rowTargetBack", ref reactionData.targetBack);

                        ImGui.TableNextColumn();
                        DrawDragInt("##rowReceivedPriority", ref reactionData.priority, 10, ImGui.GetContentRegionAvail().X);

                        if (reactionData.isLocked) ImGui.EndDisabled();
                        ImGui.PopID();
                        i++;
                    }
                    if (deleteIndex > -1)
                    {
                        selectedCharacter.reactionList.RemoveAt(deleteIndex);
                    }

                    ImGui.EndTable();
                    if (beginDrag >= 0)
                    {
                        if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
                        {
                            if (endDrag != beginDrag && selectedCharacter != null)
                            {
                                var move = selectedCharacter.reactionList[beginDrag];
                                selectedCharacter.reactionList.RemoveAt(beginDrag);
                                selectedCharacter.reactionList.Insert(endDrag, move);
                            }
                            beginDrag = -1;
                            endDrag = -1;
                        }
                        else
                        {
                            var dl = ImGui.GetWindowDrawList();
                            dl.AddLine(endDragPosition, endDragPosition + new Vector2(ImGui.GetWindowContentRegionMax().X, 0), ImGui.GetColorU32(ImGuiCol.DragDropTarget), 2f * ImGuiHelpers.GlobalScale);
                        }
                    }
                }
            }
            ImGui.EndChild();
        }

        private void AddNewReaction()
        {
            if (selectedCharacter != null)
            {
                if (newReactionData.receivedEmoteID > 0)
                {
                    selectedCharacter.reactionList.Add(newReactionData);
                    newReactionData = new ReactionData();
                }
            }
        }

        private void DrawStatusWin()
        {
            if (isOpenStatusWin && config.isActived)
            {
                var Flags = ImGuiWindowFlags.NoDecoration |
                            ImGuiWindowFlags.AlwaysAutoResize |
                            ImGuiWindowFlags.NoFocusOnAppearing |
                            ImGuiWindowFlags.NoNav;

                if (config.isStatusWinLocked)
                {
                    Flags |= ImGuiWindowFlags.NoMove;
                }
                using (AuxTheme.Push())
                using (ImRaii.PushColor(ImGuiCol.WindowBg, AuxTheme.NavBg))
                using (ImRaii.PushStyle(ImGuiStyleVar.WindowRounding, AuxTheme.WindowRounding * ImGuiHelpers.GlobalScale))
                using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Num.Vector2(8f, 4f) * ImGuiHelpers.GlobalScale))
                using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Num.Vector2(4f, 0f) * ImGuiHelpers.GlobalScale))
                {
                    ImGui.Begin("StatusWindow", Flags);
                    try
                    {
                        AuxWidgets.NavLabel("AuxEmotion");
                        ImGui.SameLine();
                        AuxWidgets.StatusChip("Chat", config.isChatActive, "Chat Reactions are enabled.");
                        ImGui.SameLine();
                        AuxWidgets.StatusChip("Emote", config.isReactionActive, "Emote Reactions are enabled.");
                    }
                    catch { }
                    ImGui.End();
                }
            }
        }
    }
}
