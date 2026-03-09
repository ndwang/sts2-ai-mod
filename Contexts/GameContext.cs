using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Sts2Agent.Contexts;

public enum ContextType
{
    Map,
    HandSelection,
    CardSelection,
    Rewards,
    Combat,
    Event,
    RestSite,
    Shop,
    Treasure,
    Unknown
}

public class ContextInfo
{
    public ContextType Type { get; init; }
    public RunState RunState { get; init; } = null!;

    // Map
    public List<MegaCrit.Sts2.Core.Map.MapPoint>? AvailableMapNodes { get; init; }

    // Hand selection
    public NPlayerHand? Hand { get; init; }

    // Card selection overlay
    public object? OverlayScreen { get; init; }
    public Node? OverlayNode { get; init; }
    public List<NCardHolder>? CardHolders { get; init; }
    public bool IsGridScreen { get; init; }

    // Rewards
    public NRewardsScreen? RewardsScreen { get; init; }

    // Combat
    public CombatState? CombatState { get; init; }

    // Rooms
    public EventRoom? EventRoom { get; init; }
    public RestSiteRoom? RestSiteRoom { get; init; }
    public MerchantRoom? MerchantRoom { get; init; }
    public MerchantInventory? ShopInventory { get; init; }
    public bool ShopIsOpen { get; init; }
    public List<MerchantEntry>? ShopItems { get; init; }
}

public static class GameContext
{
    public static ContextInfo? Resolve()
    {
        var runState = RunManager.Instance?.DebugOnlyGetState();
        if (runState == null) return null;

        // 1. Map screen
        if (NMapScreen.Instance is { IsOpen: true })
        {
            return new ContextInfo
            {
                Type = ContextType.Map,
                RunState = runState,
                AvailableMapNodes = GetAvailableMapNodes(runState)
            };
        }

        // 2. Overlay stack (rewards, card selection)
        var overlayScreen = NOverlayStack.Instance?.Peek();
        if (overlayScreen is Node overlayNode)
        {
            // Card selection overlay?
            var cardHolders = UiHelper.FindAll<NCardHolder>(overlayNode)
                .OrderBy(h => h.GlobalPosition.Y)
                .ThenBy(h => h.GlobalPosition.X)
                .ToList();
            if (cardHolders.Count > 0)
            {
                return new ContextInfo
                {
                    Type = ContextType.CardSelection,
                    RunState = runState,
                    OverlayScreen = overlayScreen,
                    OverlayNode = overlayNode,
                    CardHolders = cardHolders,
                    IsGridScreen = overlayScreen is NCardGridSelectionScreen
                };
            }

            // Rewards overlay?
            if (overlayScreen is NRewardsScreen rewardsScreen)
            {
                return new ContextInfo
                {
                    Type = ContextType.Rewards,
                    RunState = runState,
                    RewardsScreen = rewardsScreen,
                    OverlayNode = overlayNode
                };
            }
        }

        // 3. Hand card selection (discard/exhaust prompts during combat)
        var hand = NPlayerHand.Instance;
        if (hand != null && hand.IsInCardSelection)
        {
            return new ContextInfo
            {
                Type = ContextType.HandSelection,
                RunState = runState,
                Hand = hand
            };
        }

        // 4. Active combat
        var cm = CombatManager.Instance;
        if (cm != null && cm.IsInProgress && !cm.IsOverOrEnding)
        {
            return new ContextInfo
            {
                Type = ContextType.Combat,
                RunState = runState,
                CombatState = cm.DebugOnlyGetState()
            };
        }

        // 5. Room-based contexts
        var room = runState.CurrentRoom;

        if (room is EventRoom eventRoom)
        {
            return new ContextInfo
            {
                Type = ContextType.Event,
                RunState = runState,
                EventRoom = eventRoom
            };
        }

        if (room is RestSiteRoom restRoom)
        {
            return new ContextInfo
            {
                Type = ContextType.RestSite,
                RunState = runState,
                RestSiteRoom = restRoom,
                AvailableMapNodes = GetAvailableMapNodes(runState)
            };
        }

        if (room is MerchantRoom shopRoom)
        {
            var inv = shopRoom.Inventory;
            var nRoom = MegaCrit.Sts2.Core.Nodes.Rooms.NMerchantRoom.Instance;
            bool isOpen = nRoom?.Inventory?.IsOpen == true;

            return new ContextInfo
            {
                Type = ContextType.Shop,
                RunState = runState,
                MerchantRoom = shopRoom,
                ShopInventory = inv,
                ShopIsOpen = isOpen,
                ShopItems = isOpen && inv != null ? BuildShopItems(inv) : null
            };
        }

        if (room is TreasureRoom)
        {
            return new ContextInfo
            {
                Type = ContextType.Treasure,
                RunState = runState
            };
        }

        return new ContextInfo
        {
            Type = ContextType.Unknown,
            RunState = runState
        };
    }

    private static List<MegaCrit.Sts2.Core.Map.MapPoint> GetAvailableMapNodes(RunState runState)
    {
        try
        {
            var map = runState.Map;
            var visited = runState.VisitedMapCoords;
            if (visited.Count == 0)
                return map.GetPointsInRow(0).ToList();

            var lastCoord = visited[visited.Count - 1];
            return map.GetPoint(lastCoord)?.Children.ToList()
                   ?? new List<MegaCrit.Sts2.Core.Map.MapPoint>();
        }
        catch
        {
            return new List<MegaCrit.Sts2.Core.Map.MapPoint>();
        }
    }

    public static List<MerchantEntry> BuildShopItems(MerchantInventory inv)
    {
        var entries = new List<MerchantEntry>();
        foreach (var e in inv.CharacterCardEntries.Concat(inv.ColorlessCardEntries))
            if (e.IsStocked) entries.Add(e);
        foreach (var e in inv.RelicEntries)
            if (e.IsStocked) entries.Add(e);
        foreach (var e in inv.PotionEntries)
            if (e.IsStocked) entries.Add(e);
        try { if (inv.CardRemovalEntry?.IsStocked == true) entries.Add(inv.CardRemovalEntry); } catch { }
        return entries;
    }
}
