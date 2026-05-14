using MCPforDalamud.McpServer;

namespace MCPforDalamud.Tools;

public static class ObjectTableTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition { Name = "get_nearby_players", Description = "获取周围其他玩家列表", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var lp = Service.ObjectTable.LocalPlayer!; var players = Service.ObjectTable.Where(o => o != null && (int)o.ObjectKind == 1 && o.GameObjectId != lp.GameObjectId).Take(20).Select(o => new { name = o.Name.TextValue, gameObjectId = o.GameObjectId, distance = Math.Round(Math.Sqrt(Math.Pow(o.Position.X - lp.Position.X, 2) + Math.Pow(o.Position.Y - lp.Position.Y, 2) + Math.Pow(o.Position.Z - lp.Position.Z, 2)), 2) }).ToList(); return new { count = players.Count, players }; } });
        registry.Register(new ToolDefinition { Name = "get_nearby_npcs", Description = "获取周围NPC列表", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var lp = Service.ObjectTable.LocalPlayer!; var npcs = Service.ObjectTable.Where(o => o != null && (int)o.ObjectKind == 4).Take(30).Select(o => new { name = o.Name.TextValue, gameObjectId = o.GameObjectId, distance = Math.Round(Math.Sqrt(Math.Pow(o.Position.X - lp.Position.X, 2) + Math.Pow(o.Position.Y - lp.Position.Y, 2) + Math.Pow(o.Position.Z - lp.Position.Z, 2)), 2) }).ToList(); return new { count = npcs.Count, npcs }; } });
    }
}
