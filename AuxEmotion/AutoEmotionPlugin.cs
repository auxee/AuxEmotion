using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Diagnostics;
using System.Collections.Generic;
using ECommons;
using ECommons.DalamudServices;
using ECommons.Automation;
using Dalamud.Game.Gui.Toast;
using AuxEmotion.Hooks;
using AuxEmotion.Configuration.Data;
using AuxEmotion.Utility;
using Dalamud.Game.ClientState.Conditions;
using AuxEmotion.ChatMessages;
using System.Linq;

namespace AuxEmotion;

public partial class AuxEmotionPlugin : IDalamudPlugin
{
    public AuxEmotionConfig config { get; set; } = new();
    private AuxEmotionConfig configGUI { get; set; } = new();
    private bool isOpenConfig;
    private bool isOpenStatusWin;
    private const string MainCommand = "/auxe";

    public Queue<QueueData> messageQueue { get; init; }
    public Queue<QueueData> messageQueueResume { get; init; }
    private QueueData messageProcess = new();
    private QueueData messageProcessResume = new();
    public ReactionCache reactionCache = new();
    public Stopwatch timer { get; init; }
    public Stopwatch timerResume { get; init; }
    public EmoteReaderHooks EmoteReaderHooks { get; init; }
    public AuxEmotionPlugin(IDalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);

        LoadConfig();
        InitializeGui();
        AddHandler();

        messageQueue = new Queue<QueueData>();
        messageQueueResume = new Queue<QueueData>();
        timer = new Stopwatch();
        timerResume = new Stopwatch();

        EmoteReaderHooks = new EmoteReaderHooks();
        EmoteReaderHooks.OnEmote += OnEmote;

        Svc.Chat.ChatMessage += ChatMessage;
        Svc.Framework.Update += FrameworkUpdate;

        Svc.PluginInterface.UiBuilder.Draw += DrawConfigUI;
        Svc.PluginInterface.UiBuilder.Draw += DrawStatusWin;
        Svc.PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
        Svc.PluginInterface.UiBuilder.OpenMainUi += OpenConfig;

        Svc.ClientState.Login += ClientState_Login;
        Svc.ClientState.Logout += ClientState_Logout;
    }

    private void LoadConfig()
    {
        configGUI = Svc.PluginInterface.GetPluginConfig() as AuxEmotionConfig ?? new AuxEmotionConfig();
        configGUI.Initialize(Svc.PluginInterface);
        InitializeConfig();

    }
    private void InitializeConfig()
    {
        config = Svc.PluginInterface.GetPluginConfig() as AuxEmotionConfig ?? new AuxEmotionConfig();
        config.Initialize(Svc.PluginInterface);
        reactionCache = new ReactionCache(config.maxReactionsCache, config.timeoutReactionsCache);
    }

    private void FrameworkUpdate(IFramework framework)
    {
        if (config != null)
        {
            try
            {
                if (messageQueue.Any() || timer.IsRunning)
                {
                    if (!timer.IsRunning)
                    {
                        messageProcess = messageQueue.Dequeue();
                        timer.Start();
                    }
                    if (timer.ElapsedMilliseconds > messageProcess.delay)
                    {
                        if (!string.IsNullOrEmpty(messageProcess.command))
                        {
                            messageProcess.TargetBack();
                            var command = messageProcess.GetCommand();
                            Chat.ExecuteCommand(command);
                            Svc.Log.Debug($"Command: {command}");
                            messageProcess.command = string.Empty;
                            return;
                        }
                        if (!string.IsNullOrEmpty(messageProcess.expression))
                        {
                            messageProcess.TargetBack();
                            var expression = messageProcess.GetExpression(config.isChatLogHidden);
                            Chat.ExecuteCommand(expression);
                            Svc.Log.Debug($"Expressione: {expression}");
                            messageProcess.expression = string.Empty;
                            messageProcess.SetDelay(messageProcess.delay + messageProcess.minDelay);
                            return;
                        }
                        if (!string.IsNullOrEmpty(messageProcess.emote))
                        {
                            if (config.resumeEmoteLoop)
                            {
                                if (Svc.Condition.Any(ConditionFlag.Emoting))
                                {
                                    var currentEmote = CharacterUtility.GetCurrentEmote(true);
                                    if (currentEmote != null)
                                    {
                                        if (currentEmote.EmoteCommand != messageProcess.emote)
                                        {
                                            var queueData = new QueueData() { emote = currentEmote.EmoteCommand };
                                            queueData.SetDelay(config.resumeEmoteDelay);
                                            if (Svc.Objects.LocalPlayer != null)
                                            {
                                                queueData.rotation = Svc.Objects.LocalPlayer.Rotation;
                                            }
                                            messageQueueResume.Enqueue(queueData);
                                            Svc.Log.Debug($"Enqueue resume Emote: {queueData.emote} with delay {queueData.delay}ms");
                                        }
                                    }
                                }
                            }
                            messageProcess.TargetBack();
                            var emote = messageProcess.GetEmote(config.isChatLogHidden);
                            Chat.ExecuteCommand(emote);
                            Svc.Log.Debug($"Emote: {emote}");
                            messageProcess.emote = string.Empty;
                            return;
                        }
                        timer.Reset();
                    }
                }
                else if (timerResume.IsRunning)
                {
                    var currentEmote = CharacterUtility.GetCurrentEmote();
                    if (currentEmote != null && currentEmote.EmoteID == 0)
                    {
                        if (!string.IsNullOrEmpty(messageProcessResume.emote))
                        {
                            if (config.resumeEmoteDetarget) Svc.Targets.Target = null;
                            var emote = messageProcessResume.GetEmote(config.isChatLogHidden);
                            Chat.ExecuteCommand(emote);
                            if (config.resumeEmoteRestoreRotation)
                            {
                                CharacterUtility.SetRotation(messageProcessResume.rotation);
                            }
                            Svc.Log.Debug($"Resume Emote: {emote}");
                        }
                        timerResume.Reset();
                    }
                }
                else if (messageQueueResume.Any())
                {
                    messageProcessResume = messageQueueResume.Dequeue();
                    timerResume.Start();
                }
            }
            catch (Exception e)
            {
                Svc.Log.Error($"[Chat Manager]: Failed to process Framework Update! ${e}: ${e.Message}");
            }
            reactionCache.CleanExpiredActions();
        }
    }

    public void Dispose()
    {
        Svc.Chat.ChatMessage -= ChatMessage;
        Svc.Framework.Update -= FrameworkUpdate;
        Svc.PluginInterface.UiBuilder.Draw -= DrawConfigUI;
        Svc.PluginInterface.UiBuilder.Draw -= DrawStatusWin;
        Svc.PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        Svc.ClientState.Login -= ClientState_Login;
        Svc.ClientState.Logout -= ClientState_Logout;
        EmoteReaderHooks.OnEmote -= OnEmote;
        EmoteReaderHooks.Dispose();
        RemoveHandler();
        ECommonsMain.Dispose();
    }

    private void AddHandler()
    {
        Svc.Commands.AddHandler(MainCommand, new CommandInfo(OnCommands)
        {
            HelpMessage = """
                Opens the plugin configuration window.
                /auxe a <name> | add <name> → Adds a character name to the whitelist.
                /auxe disable → Disables the plugin.
                """
        });
    }

    private void RemoveHandler()
    {
        Svc.Commands.RemoveHandler(MainCommand);
    }

    private void OnCommands(string command, string args)
    {
        var trimmed = args.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            OpenConfig();
            return;
        }

        var splitIndex = trimmed.IndexOf(' ');
        var action = splitIndex >= 0 ? trimmed[..splitIndex].Trim() : trimmed;
        var actionArgs = splitIndex >= 0 ? trimmed[(splitIndex + 1)..].Trim() : string.Empty;

        if (action.EqualsIgnoreCaseAny("a", "add"))
        {
            var added = !string.IsNullOrWhiteSpace(actionArgs)
                ? config.TryToAddBWListByName(actionArgs, true, true)
                : config.TryToAddBWList(true, true);

            if (added)
            {
                configGUI = config;
                InitializeGui();
                config.Save();
            }
            else
            {
                Svc.Toasts.ShowError("Could not add whitelist entry. Provide a name or target a character.");
            }
        }
        else if (action.EqualsIgnoreCaseAny("disable", "d"))
        {
            ClearQueue();
            config.isActived = false;
            configGUI.isActived = false;
            Svc.Toasts.ShowQuest("AuxEmotion Disabled.",
                new QuestToastOptions() { PlaySound = true, DisplayCheckmark = true });
            config.Save();
        }
        else
        {
            OpenConfig();
        }
    }

    private void ClientState_Login()
    {
        isOpenStatusWin = config.isStatusWinOpen;
    }

    private void ClientState_Logout(int type, int code)
    {
        isOpenStatusWin = false;
    }

    private void OpenConfig() => isOpenConfig = true;

    private void ClearQueue()
    {
        messageQueue.Clear();
        messageProcess.Clear();
        messageQueueResume.Clear();
        messageProcessResume.Clear();
    }
}
