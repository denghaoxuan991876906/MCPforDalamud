using Lumina.Excel.Sheets;
using MCPforDalamud.McpServer;

namespace MCPforDalamud.Tools;

public static class MapTools
{
    public static void Register(ToolRegistry registry)
    {
        registry.Register(new ToolDefinition { Name = "get_territory_info", Description = "获取当前地图/区域信息", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var tt = Service.ClientState.TerritoryType; var sheet = Service.DataManager.GetExcelSheet<TerritoryType>(); var row = sheet?.GetRow(tt); return new { territoryId = tt, mapId = Service.ClientState.MapId, territoryName = row?.PlaceName.Value.Name.ToString() ?? "", regionName = row?.PlaceNameRegion.Value.Name.ToString() ?? "" }; } });
        registry.Register(new ToolDefinition { Name = "get_aetheryte_list", Description = "获取已解锁的传送点列表", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var list = new List<object>(); int count = 0; foreach (var a in Service.AetheryteList) { if (a == null || count >= 50) continue; list.Add(new { name = a.AetheryteData.Value.PlaceName.Value.Name.ToString(), territoryId = a.AetheryteData.Value.Territory.Value.RowId }); count++; } return new { count = list.Count, aetherytes = list }; } });
        registry.Register(new ToolDefinition { Name = "get_fate_list", Description = "获取当前地图活跃FATE列表", Handler = _ => { if (!Service.IsReady) throw new InvalidOperationException("游戏角色未登录"); var fates = new List<object>(); foreach (var f in Service.FateTable) { if (f == null) continue; fates.Add(new { name = f.Name.TextValue, fateId = f.FateId, progress = f.Progress, position = new { x = f.Position.X, y = f.Position.Y, z = f.Position.Z } }); } return new { count = fates.Count, fates }; } });
        registry.Register(new ToolDefinition { Name = "get_duty_state", Description = "获取副本状态", Handler = _ => { var state = Service.DutyState; return new { isDutyStarted = state.IsDutyStarted, status = state.IsDutyStarted ? "副本进行中" : "空闲" }; } });
    }
}
