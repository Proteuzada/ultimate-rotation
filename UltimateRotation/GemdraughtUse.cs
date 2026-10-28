using FFXIVClientStructs.FFXIV.Client.Game;

namespace UltimateRotation;

/// <summary>
/// Drinks the highest-grade Gemdraught of the requested stat that is in the inventory.
/// HQ is preferred. This uses ActionType.Item and never targets another player.
/// </summary>
internal static class GemdraughtUse
{
    private static readonly ushort[] Strength = [49234, 45995, 44162, 44157];
    private static readonly ushort[] Dexterity = [49235, 45996, 44163, 44158];
    private static readonly ushort[] Intelligence = [49237, 45998, 44165, 44160];
    private static readonly ushort[] Mind = [49238, 45999, 44166, 44161];

    public static unsafe ActionAttempt Use(string stat)
    {
        try
        {
            if (!TryFind(Ids(stat), out var useId))
                return ActionAttempt.Impossible;

            var player = Plugin.Objects.LocalPlayer;
            var manager = ActionManager.Instance();
            if (player == null || manager == null || player.GameObjectId == 0)
                return ActionAttempt.Blocked;

            var target = player.GameObjectId;
            if (manager->ActionQueued)
                return ActionAttempt.Blocked;

            if (manager->GetActionStatus(ActionType.Item, useId, target) != 0)
                return ActionAttempt.Blocked;

            return manager->UseAction(ActionType.Item, useId, target)
                ? ActionAttempt.Used
                : ActionAttempt.Blocked;
        }
        catch
        {
            return ActionAttempt.Impossible;
        }
    }

    private static ushort[] Ids(string stat) => stat switch
    {
        Gemdraughts.Dexterity => Dexterity,
        Gemdraughts.Intelligence => Intelligence,
        Gemdraughts.Mind => Mind,
        _ => Strength,
    };

    private static unsafe bool TryFind(ushort[] itemIds, out uint useId)
    {
        useId = 0;
        var manager = InventoryManager.Instance();
        if (manager == null)
            return false;

        foreach (var itemId in itemIds)
        {
            var quality = FindQuality(manager, itemId);
            if (quality is null)
                continue;

            useId = quality.Value ? itemId + 1_000_000u : itemId;
            return true;
        }

        return false;
    }

    private static unsafe bool? FindQuality(InventoryManager* manager, ushort itemId)
    {
        bool? normal = null;
        foreach (var type in new[] { InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4 })
        {
            var bag = manager->GetInventoryContainer(type);
            if (bag == null)
                continue;

            for (var slot = 0; slot < bag->Size; slot++)
            {
                var item = bag->GetInventorySlot(slot);
                if (item == null || item->ItemId != itemId)
                    continue;

                if (item->IsHighQuality())
                    return true;

                normal = false;
            }
        }

        return normal;
    }
}
