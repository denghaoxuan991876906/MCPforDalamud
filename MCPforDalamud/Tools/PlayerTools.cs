using System.Text.Json;
using MCPforDalamud.McpServer;
using Dalamud.Game.ClientState.Conditions;

namespace MCPforDalamud.Tools;

public static class PlayerTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition { Name = "get_player_status", Description = "获取当前角色状态：名称、等级、职业、HP/MP/GP、坐标、朝向", Handler = _ => { EnsureReady(); var lp = Service.ObjectTable.LocalPlayer!; return new { name = lp.Name.TextValue, level = lp.Level, classJob = lp.ClassJob.Value.Name.ToString(), hp = lp.CurrentHp, maxHp = lp.MaxHp, mp = lp.CurrentMp, maxMp = lp.MaxMp, gp = lp.CurrentGp, maxGp = lp.MaxGp, position = new { x = lp.Position.X, y = lp.Position.Y, z = lp.Position.Z }, rotation = lp.Rotation }; } });
        registry.Register(new ToolDefinition { Name = "get_condition", Description = "获取角色当前状态标记", Handler = _ => { EnsureReady(); var flags = new List<string>(); if (Service.Condition[ConditionFlag.InCombat]) flags.Add("战斗中"); if (Service.Condition[ConditionFlag.Mounted]) flags.Add("骑乘中"); if (Service.Condition[ConditionFlag.WatchingCutscene]) flags.Add("过场动画"); if (Service.Condition[ConditionFlag.Swimming]) flags.Add("游泳中"); if (Service.Condition[ConditionFlag.BoundByDuty]) flags.Add("副本进行中"); return new { activeFlags = flags }; } });
        registry.Register(new ToolDefinition { Name = "get_player_stats", Description = "获取角色详细属性数值", Handler = _ => { EnsureReady(); var lp = Service.ObjectTable.LocalPlayer!; return new { hp = lp.CurrentHp, maxHp = lp.MaxHp, mp = lp.CurrentMp, maxMp = lp.MaxMp, gp = lp.CurrentGp, maxGp = lp.MaxGp, cp = lp.CurrentCp, maxCp = lp.MaxCp }; } });
    }

    private static void EnsureReady() { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); }
}
