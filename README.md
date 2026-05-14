# MCP for Dalamud

让 AI 工具通过 MCP (Model Context Protocol) 实时获取 FFXIV 游戏数据、操控角色、与开发中插件交互。

## 快速开始

### 构建

```bash
cmd.exe /c "cd /d E:\MCPforDalamud && dotnet build MCPforDalamud\MCPforDalamud.csproj"
```

输出: `MCPforDalamud\bin\Debug\MCPforDalamud.dll`

### 安装

将 `MCPforDalamud.dll` 和 `MCPforDalamud.json` 放入 Dalamud 插件目录，`/xlplugins` 加载。加载后聊天栏显示 `[MCP] HTTP 服务已启动: http://127.0.0.1:{port}/mcp`。

### 配置

`/mcp` 打开设置面板。

## 工具列表 (44个)

**数据读取** (22): get_player_status, get_condition, get_player_stats, get_job_gauge, get_target_info, get_focus_target, get_soft_target, get_enemy_list, get_party_list, get_buddy_list, get_nearby_players, get_nearby_npcs, get_territory_info, get_aetheryte_list, get_fate_list, get_duty_state, get_inventory, get_equipment, get_currency, list_excel_sheets, search_excel_sheet, get_excel_row

**角色操控** (10): execute_action, use_general_action, set_target, set_focus_target, interact_with_target, dismount, cancel_cast, accept_raise, toggle_sprint, jump

**事件缓存** (3): query_events, configure_event_collection, get_event_config

**插件桥接** (4): query_push_data, register_ipc_endpoint, call_plugin_ipc, list_ipc_endpoints

**聊天** (1): send_chat

**移动** (4): automove_on, automove_off, face_target, move_to_target

## 其他插件接入

```csharp
// 推送数据到 MCP
var sub = pluginInterface.GetIpcSubscriber<string>("MCPforDalamud.PushData");
sub.InvokeFunc("{\"key\":\"mykey\",\"data\":\"some data\"}");
```

## 技术栈

.NET 10, C#, Dalamud.CN.NET.Sdk 15.0.0, API Level 15, FFXIVClientStructs, Lumina
