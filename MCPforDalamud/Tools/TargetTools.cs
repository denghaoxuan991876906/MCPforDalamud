using System.Text.Json;
using MCPforDalamud.McpServer;
using Dalamud.Game.ClientState.Objects.Types;

namespace MCPforDalamud.Tools;

public static class TargetTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition { Name = "get_target_info", Description = "获取当前目标信息", Handler = _ => { EnsureReady(); var t = Service.TargetManager.Target; if (t == null) return new { hasTarget = false }; var lp = Service.ObjectTable.LocalPlayer!; var dist = Math.Sqrt(Math.Pow(t.Position.X - lp.Position.X, 2) + Math.Pow(t.Position.Y - lp.Position.Y, 2) + Math.Pow(t.Position.Z - lp.Position.Z, 2)); var chara = t as ICharacter; return new { hasTarget = true, name = t.Name.TextValue, currentHp = chara?.CurrentHp ?? 0, maxHp = chara?.MaxHp ?? 0, distance = Math.Round(dist, 2), position = new { x = t.Position.X, y = t.Position.Y, z = t.Position.Z }, type = t.ObjectKind.ToString(), gameObjectId = t.GameObjectId }; } });
        registry.Register(new ToolDefinition { Name = "get_focus_target", Description = "获取焦点目标信息", Handler = _ => { EnsureReady(); var ft = Service.TargetManager.FocusTarget; if (ft == null) return new { hasFocusTarget = false }; return new { hasFocusTarget = true, name = ft.Name.TextValue, gameObjectId = ft.GameObjectId }; } });
        registry.Register(new ToolDefinition { Name = "get_soft_target", Description = "获取软目标信息", Handler = _ => { EnsureReady(); var st = Service.TargetManager.SoftTarget; if (st == null) return new { hasSoftTarget = false }; return new { hasSoftTarget = true, name = st.Name.TextValue, gameObjectId = st.GameObjectId }; } });
        registry.Register(new ToolDefinition { Name = "get_enemy_list", Description = "获取周围敌对目标列表", Handler = _ => { EnsureReady(); var lp = Service.ObjectTable.LocalPlayer!; var enemies = Service.ObjectTable.Select(o => { if (o == null || o == lp) return null; var chara = o as ICharacter; if (chara == null || chara.MaxHp == 0 || chara.CurrentHp >= chara.MaxHp) return null; var dist = Math.Sqrt(Math.Pow(o.Position.X - lp.Position.X, 2) + Math.Pow(o.Position.Y - lp.Position.Y, 2) + Math.Pow(o.Position.Z - lp.Position.Z, 2)); return (object)new { name = o.Name.TextValue, hp = chara.CurrentHp, maxHp = chara.MaxHp, level = chara.Level, distance = Math.Round(dist, 2) }; }).Where(x => x != null).OrderBy(x => ((dynamic)x!).distance).Take(20).ToList(); return new { count = enemies.Count, enemies }; } });
    }

    private static void EnsureReady() { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); }
}
