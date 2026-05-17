using AuxEmotion.Configuration.Data;
using Dalamud.Configuration;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.Toast;
using Dalamud.Game.Text;
using Dalamud.Plugin;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AuxEmotion;

[Serializable]
public class AuxEmotionConfig : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool isActived = true;
    public bool isChatActive = true;
    public bool isReactionActive = true;
    public bool isChatLogHidden = false;
    public bool isStatusWinOpen = false;
    public bool isStatusWinLocked = false;
    public bool resumeEmoteLoop = false;
    public bool resumeEmoteDetarget = false;
    public bool resumeEmoteRestoreRotation = false;
    public int resumeEmoteDelay = 0;
    public bool showPlusMinus = true;
    public bool executeReactionWhileBusyAfk = true;
    public int charDelay = 0;
    public int maxReactionsCache = 2;
    public int timeoutReactionsCache = 5;
    public Dictionary<string, List<TriggerData>> triggerLists = new(StringComparer.OrdinalIgnoreCase) { };
    public List<string> triggerOrder = [];
    public List<XivChatType> allowedChannels { get; set; } = [XivChatType.Say];

    //OBSOLETE, only for old version config loading
    public readonly bool[] visibleChannels =
        {
            false,//None
            false,//None
            false,//None
            false,//None
            true,//Say
            true,//Shout
            true,//TellOutgoing
            false,//TellIncoming
            true,//Party
            true,//Alliance
            true,//Ls1
            true,//Ls2
            true,//Ls3
            true,//Ls4
            true,//Ls5
            true,//Ls6
            true,//Ls7
            true,//Ls8
            true,//FreeCompany
            true,//NoviceNetwork
            true,//CustomEmote
            false,//StandardEmote
            true,//Yell
            true,//CrossParty
            false,//PvPTeam
            true,//CrossLinkShell1
            true,//Echo
            false,//None
            false,//None
            false,//None
            false,//None
            false,//None
            false,//None
            false,//None
            true,//CrossLinkShell2
            true,//CrossLinkShell3
            true,//CrossLinkShell4
            true,//CrossLinkShell5
            true,//CrossLinkShell6
            true,//CrossLinkShell7
            true//CrossLinkShell8
        };

    public Dictionary<XivChatType, bool> visibleChannelsDictionary = CreateVisibleChannels();

    public Dictionary<string, CharacterData> whiteList = new()
    {
      { EveryoneKey, new CharacterData { name = "Everyone", worldName="Everywhere"} }
    };
    public Dictionary<string, CharacterData> blackList = [];

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public static string EveryoneKey => "Everyone@Everywhere";

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        CalculatePriorities();
        pluginInterface!.SavePluginConfig(this);
    }

    public bool TryToAddBWList(bool isWhiteList = true, bool showNotify = false)
    {
        try
        {
            if (Svc.Targets.Target is IPlayerCharacter playerCharacter)
            {
                var world = Svc.Data.GetExcelSheet<World>()?.GetRow(playerCharacter.HomeWorld.Value.RowId);
                if (world != null)
                {
                    return TryAddListEntry(
                        playerCharacter.Name.TextValue,
                        world.Value.Name.ExtractText(),
                        playerCharacter.HomeWorld.Value.RowId,
                        isWhiteList,
                        showNotify);
                }
            }
        }
        catch
        {
            Svc.Log.Error("Error on adding the target on the list.");
        }

        return false;
    }

    public bool TryToAddBWListByName(string input, bool isWhiteList = true, bool showNotify = false)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        var worldIndex = trimmed.IndexOf('@');
        if (worldIndex > 0 && worldIndex < trimmed.Length - 1)
        {
            var playerName = trimmed[..worldIndex].Trim();
            var worldName = trimmed[(worldIndex + 1)..].Trim();
            return TryAddListEntry(playerName, worldName, 0, isWhiteList, showNotify);
        }

        return TryAddListEntry(trimmed, "Everywhere", 0, isWhiteList, showNotify);
    }

    private bool TryAddListEntry(string playerName, string worldName, uint worldId, bool isWhiteList, bool showNotify)
    {
        playerName = playerName.Trim();
        worldName = worldName.Trim();
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(worldName))
        {
            return false;
        }

        var key = $"{playerName}@{worldName}";
        var targetList = isWhiteList ? whiteList : blackList;
        if (ContainsKeyIgnoreCase(targetList, key))
        {
            return false;
        }

        var characterData = new CharacterData
        {
            name = playerName,
            worldID = worldId,
            worldName = worldName,
        };

        targetList.Add(key, characterData);
        if (showNotify)
        {
            var listName = isWhiteList ? "Whitelist" : "Blacklist";
            Svc.Toasts.ShowQuest($"AuxEmotion {key} added to the {listName}.",
                new QuestToastOptions() { PlaySound = true, DisplayCheckmark = true });
        }
        return true;
    }

    private static bool ContainsKeyIgnoreCase(Dictionary<string, CharacterData> source, string key)
    {
        return source.Keys.Any(x => x.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<XivChatType, bool> CreateVisibleChannels()
    {
        var dict = Enum.GetValues<XivChatType>().ToDictionary(e => e, e => false);

        foreach (var channel in new[] {
        XivChatType.Say, XivChatType.Shout, XivChatType.TellOutgoing,
        XivChatType.Party, XivChatType.Alliance,
        XivChatType.Ls1, XivChatType.Ls2, XivChatType.Ls3, XivChatType.Ls4,
        XivChatType.Ls5, XivChatType.Ls6, XivChatType.Ls7, XivChatType.Ls8,
        XivChatType.FreeCompany, XivChatType.NoviceNetwork, XivChatType.CustomEmote,
        XivChatType.Yell, XivChatType.CrossParty, XivChatType.Echo,
        XivChatType.CrossLinkShell1, XivChatType.CrossLinkShell2, XivChatType.CrossLinkShell3,
        XivChatType.CrossLinkShell4, XivChatType.CrossLinkShell5, XivChatType.CrossLinkShell6,
        XivChatType.CrossLinkShell7, XivChatType.CrossLinkShell8
    })
            dict[channel] = true;

        return dict;
    }

    private void CalculatePriorities()
    {
        foreach (var list in triggerLists)
        {
            foreach (var trigger in list.Value)
            {
                trigger.calculatedPriority = (trigger.priority * triggerOrder.Count) + triggerOrder.IndexOf(trigger.trigger);
            }
        }
    }
}
