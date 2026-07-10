using System.Numerics;
using MCPforDalamud.McpServer;

namespace MCPforDalamud.Tools;

public static class MovementTools
{
    public static void Register(ToolRegistry registry)
    {
        if (Plugin.Instance?.Config.AllowMovement != true) return;
        registry.Register(new ToolDefinition { Name = "toggle_automove", Description = "切换自动前进状态", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); unsafe { var am = GetActionManager(); var success = am->UseAction(FFXIVClientStructs.FFXIV.Client.Game.ActionType.GeneralAction, 13, 0xE0000000, 0, 0, 0, null); return new { success }; } } });
        registry.Register(new ToolDefinition { Name = "face_target", Description = "面向当前目标", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var t = Service.TargetManager.Target; if (t == null) return new { error = "没有当前目标" }; unsafe { var am = GetActionManager(); var pos = t.Position; am->AutoFaceTargetPosition(&pos, 0); return new { success = true, targetName = t.Name.TextValue }; } } });
    }

    private static unsafe FFXIVClientStructs.FFXIV.Client.Game.ActionManager* GetActionManager()
    {
        var manager = FFXIVClientStructs.FFXIV.Client.Game.ActionManager.Instance();
        return manager != null ? manager : throw new InvalidOperationException("ActionManager 不可用");
    }
}
