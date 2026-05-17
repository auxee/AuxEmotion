using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using AuxEmotion.Configuration.Data;
using System;
using System.Linq;
using AuxEmotion.Utility;
using Lumina.Excel.Sheets;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Collections.Generic;

namespace AuxEmotion
{
    public partial class AuxEmotionPlugin : IDalamudPlugin
    {
        private bool isReactionActive()
        {
            if (!config.isActived) return false;
            if (!config.isReactionActive) return false;
            return true;
        }
        private bool CanExecuteReaction()
        {
            if (!CharacterUtility.IsCharacterAvailable(config)) return false;
            return true;
        }
        private void OnEmote(IPlayerCharacter playerCharacter, ushort emoteId)
        {
            if (!playerCharacter.IsValid()) return;

            if (!isReactionActive()) return;
            if (!CanExecuteReaction()) return;

            var playerName = playerCharacter.Name.TextValue;
            var homeWorldID = playerCharacter.HomeWorld.RowId;
            var pos = playerCharacter.Position;

            Svc.Log.Debug($"OnEmote > Player {playerName} - Emote {emoteId} - X: {pos.X} - Z: {pos.Z}");
#if DEBUG
            if (true)
#else
            if (playerName != ECommons.GameHelpers.Player.Name)
#endif
            {
                var world = Svc.Data.GetExcelSheet<World>()?.GetRow(homeWorldID);
                if (world == null) return;

                var worldName = world.Value.Name.ExtractText();
                var key = playerName + "@" + worldName;
                if (IsBlacklisted(playerName, worldName)) return;

                IOrderedEnumerable<ReactionData>? reactions = null;
                var characterData = FindWhiteListCharacter(playerName, worldName);
                if (characterData != null && characterData.isEnabled) //Character in whitelist
                {
                    reactions = characterData.reactionList.Where(r => r.receivedEmoteID == emoteId).OrderBy(r => r.priority);
                }
                if (characterData == null || reactions == null || !reactions.Any()) //Character not in whitelist or not reactions for that emote
                {
                    config.whiteList.TryGetValue(AuxEmotionConfig.EveryoneKey, out characterData);
                    if (characterData != null && characterData.isEnabled)
                    {
                        reactions = characterData.reactionList.Where(r => r.receivedEmoteID == emoteId).OrderBy(r => r.priority);
                    }
                }
                if (reactions == null || !reactions.Any()) return;
                if (reactionCache.CanPerformAction(key, emoteId))
                {
                    foreach (var reaction in reactions)
                    {

                        if (reaction.yalms > 0f)
                        {
                            float distance = CharacterUtility.CalculateDistanceFromCharacter(pos);
                            Svc.Log.Debug($"Distance: {distance}");
                            if (distance > reaction.yalms) continue;
                        }

                        try
                        {
                            var queueData = new QueueData();
                            queueData.emote = reaction.responseEmoteCommand;
                            queueData.expression = reaction.responseExpressionCommand;
                            var macroCommands = ReadMacroSlotCommands(reaction.responseMacroBook, reaction.responseMacroSlot);
                            if (macroCommands.Count > 0)
                            {
                                queueData.command = macroCommands[0];
                            }
                            queueData.SetDelay(reaction.responseDelay);
                            queueData.targetBack = reaction.targetBack;
                            if (!playerCharacter.IsValid()) return;
                            queueData.playerCharacter = playerCharacter;

                            if (!string.IsNullOrWhiteSpace(queueData.emote) ||
                                !string.IsNullOrWhiteSpace(queueData.expression) ||
                                !string.IsNullOrWhiteSpace(queueData.command))
                            {
                                messageQueue.Enqueue(queueData);
                            }

                            if (macroCommands.Count > 1)
                            {
                                const int macroSpacing = 600;
                                for (var macroIndex = 1; macroIndex < macroCommands.Count; macroIndex++)
                                {
                                    var macroQueue = new QueueData
                                    {
                                        command = macroCommands[macroIndex],
                                        targetBack = false,
                                        playerCharacter = playerCharacter,
                                    };
                                    macroQueue.SetDelay(reaction.responseDelay + (macroSpacing * macroIndex));
                                    messageQueue.Enqueue(macroQueue);
                                }
                            }
                            reactionCache.RecordAction(key, emoteId);
                        }
                        catch (Exception e)
                        {
                            Svc.Log.Error("Error while queuing message: {}", e.Message);
                        }
                    }
                }
            }
        }

        private bool IsBlacklisted(string playerName, string worldName)
        {
            var exactKey = $"{playerName}@{worldName}";
            if (config.blackList.ContainsKey(exactKey))
            {
                return true;
            }

            var everywhereKey = $"{playerName}@Everywhere";
            if (config.blackList.ContainsKey(everywhereKey))
            {
                return true;
            }

            return config.blackList.Values.Any(x => x.name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
        }

        private CharacterData? FindWhiteListCharacter(string playerName, string worldName)
        {
            var exactKey = $"{playerName}@{worldName}";
            if (config.whiteList.TryGetValue(exactKey, out var exact))
            {
                return exact;
            }

            var everywhereKey = $"{playerName}@Everywhere";
            if (config.whiteList.TryGetValue(everywhereKey, out var everywhere))
            {
                return everywhere;
            }

            return config.whiteList.Values.FirstOrDefault(x => x.name.Equals(playerName, StringComparison.OrdinalIgnoreCase));
        }

        private unsafe static List<string> ReadMacroSlotCommands(int macroBook, int macroSlot)
        {
            var commands = new List<string>();
            if (macroBook <= 0)
            {
                return commands;
            }

            var macroModule = RaptureMacroModule.Instance();
            if (macroModule is null)
            {
                return commands;
            }

            var bookIndex = Math.Clamp(macroBook - 1, 0, 1);
            var macro = macroModule->GetMacro((uint)bookIndex, (uint)Math.Clamp(macroSlot, 0, 99));
            if (macro is null || !macro->IsNotEmpty())
            {
                return commands;
            }

            foreach (ref var line in macro->Lines)
            {
                var command = NormalizeCommand(line.ToString());
                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }

                if (command.StartsWith("/wait", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                commands.Add(command);
            }

            return commands;
        }

        private static string NormalizeCommand(string command)
        {
            var normalized = command?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return string.Empty;
            }

            return normalized.StartsWith('/') ? normalized : $"/{normalized}";
        }
    }
}
