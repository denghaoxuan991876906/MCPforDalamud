using Dalamud.Game.Inventory;
using MCPforDalamud.McpServer;

namespace MCPforDalamud.Tools;

public static class InventoryTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition { Name = "get_inventory", Description = "获取背包物品列表", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var items = new List<object>(); var types = new[] { GameInventoryType.Inventory1, GameInventoryType.Inventory2, GameInventoryType.Inventory3, GameInventoryType.Inventory4 }; foreach (var t in types) { var span = Service.GameInventory.GetInventoryItems(t); foreach (var slot in span) { if (slot.IsEmpty || slot.ItemId == 0) continue; items.Add(new { itemId = slot.ItemId, quantity = slot.Quantity, isHq = slot.IsHq, slot = slot.InventorySlot }); if (items.Count >= 50) break; } if (items.Count >= 50) break; } return new { count = items.Count, items }; } });
        registry.Register(new ToolDefinition { Name = "get_equipment", Description = "获取当前装备列表", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var equipment = new List<object>(); var span = Service.GameInventory.GetInventoryItems(GameInventoryType.EquippedItems); foreach (var slot in span) { if (slot.IsEmpty || slot.ItemId == 0) continue; equipment.Add(new { itemId = slot.ItemId, isHq = slot.IsHq, condition = slot.Condition, slot = slot.InventorySlot }); } return new { count = equipment.Count, equipment }; } });
        registry.Register(new ToolDefinition { Name = "get_currency", Description = "获取金币和各类代币", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var currencies = new List<object>(); var span = Service.GameInventory.GetInventoryItems(GameInventoryType.Currency); foreach (var slot in span) { if (slot.IsEmpty || slot.ItemId == 0) continue; currencies.Add(new { itemId = slot.ItemId, quantity = slot.Quantity }); } return new { count = currencies.Count, currencies }; } });
    }
}
