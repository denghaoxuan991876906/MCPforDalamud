using System.Numerics;
using MCPforDalamud.McpServer;

namespace MCPforDalamud.Tools;

public static class MovementTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition { Name = "automove_on", Description = "开启自动前进", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); unsafe { var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance(); am->UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction, 13, 0xE0000000, 0, 0, 0, null); return new { success = true }; } } });
        registry.Register(new ToolDefinition { Name = "automove_off", Description = "关闭自动前进", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); unsafe { var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance(); am->UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction, 13, 0xE0000000, 0, 0, 0, null); return new { success = true }; } } });
        registry.Register(new ToolDefinition { Name = "face_target", Description = "面向当前目标", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var t = Service.TargetManager.Target; if (t == null) return new { error = "没有当前目标" }; unsafe { var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance(); var pos = t.Position; am->AutoFaceTargetPosition(&pos, 0); return new { success = true, targetName = t.Name.TextValue }; } } });
        registry.Register(new ToolDefinition { Name = "move_to_target", Description = "面向目标并自动前进", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var t = Service.TargetManager.Target; if (t == null) return new { error = "没有当前目标" }; unsafe { var am = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance(); var pos = t.Position; am->AutoFaceTargetPosition(&pos, 0); am->UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction, 13, 0xE0000000, 0, 0, 0, null); return new { success = true, targetName = t.Name.TextValue }; } } });
    }
}
