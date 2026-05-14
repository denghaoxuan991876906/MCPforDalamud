using MCPforDalamud.McpServer;
using Dalamud.Game.ClientState.Objects.Types;

namespace MCPforDalamud.Tools;

public static class PartyTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition { Name = "get_party_list", Description = "获取小队成员列表", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var list = new List<object>(); foreach (var m in Service.PartyList) { if (m == null) continue; list.Add(new { name = m.Name.TextValue, classJob = m.ClassJob.Value.Name.ToString(), level = m.Level, currentHp = m.CurrentHP, maxHp = m.MaxHP, currentMp = m.CurrentMP, maxMp = m.MaxMP, position = new { x = m.Position.X, y = m.Position.Y, z = m.Position.Z } }); } return new { count = list.Count, members = list }; } });
        registry.Register(new ToolDefinition { Name = "get_buddy_list", Description = "获取搭档/陆行鸟状态", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var buddies = new List<object>(); foreach (var b in Service.BuddyList) { if (b == null) continue; var chara = b.GameObject as ICharacter; buddies.Add(new { name = b.GameObject?.Name.TextValue ?? "", currentHp = b.CurrentHP, maxHp = b.MaxHP, dataId = b.DataID, level = chara?.Level ?? 0 }); } return new { count = buddies.Count, buddies }; } });
    }
}
