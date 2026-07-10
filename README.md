# MCP for Dalamud

让 AI 工具通过 MCP (Model Context Protocol) 实时获取 FFXIV 游戏数据、操控角色、与开发中插件交互。

支持 MCP Streamable HTTP JSON-RPC 请求，协议版本最高为 `2025-11-25`。端点为 `http://127.0.0.1:{port}/mcp`。

## 快速开始

### 构建

```bash
dotnet restore MCPforDalamud.slnx --locked-mode
dotnet build MCPforDalamud.slnx
```

Debug 输出：`MCPforDalamud/bin/Debug/MCPforDalamud.dll`

Release 插件包：

```bash
dotnet build MCPforDalamud/MCPforDalamud.csproj -c Release
```

输出：`MCPforDalamud/bin/Release/MCPforDalamud/latest.zip`

CI 使用 Windows 托管 runner，并从 Dalamud 官方分发地址下载最新 Hooks 后执行构建和打包校验。

### 安装

将 `MCPforDalamud.dll` 和 `MCPforDalamud.json` 放入 Dalamud 插件目录，`/xlplugins` 加载。加载后聊天栏显示 `[MCP] HTTP 服务已启动: http://127.0.0.1:{port}/mcp`。

### 配置

`/mcp` 打开设置面板。

## 工具列表（全部权限启用时 42 个）

读取、事件和 IPC Bridge 工具默认可用。角色操作、移动、聊天和插件管理默认关闭，需在 `/mcp` 设置窗口中分别启用；权限变化会自动重启本地 MCP 服务。

**数据读取** (21): get_player_status, get_condition, get_player_stats, get_target_info, get_focus_target, get_soft_target, get_enemy_list, get_party_list, get_buddy_list, get_nearby_players, get_nearby_npcs, get_territory_info, get_aetheryte_list, get_fate_list, get_duty_state, get_inventory, get_equipment, get_currency, list_excel_sheets, search_excel_sheet, get_excel_row

**角色操控** (10): execute_action, use_general_action, set_target, set_focus_target, interact_with_target, dismount, cancel_cast, accept_raise, toggle_sprint, jump

**事件缓存** (3): query_events, configure_event_collection, get_event_config

**插件桥接** (4): query_push_data, register_ipc_endpoint, call_plugin_ipc, list_ipc_endpoints

**插件管理** (1): manage_plugin

**聊天** (1): send_chat

**移动** (2): toggle_automove, face_target

## 插件管理

`manage_plugin` 通过游戏聊天系统发送 Dalamud 命令来管理插件。命令和聊天消息都走 `ProcessChatBox`，`/` 前缀自动路由。

```
manage_plugin:
  参数:
    action: "load" | "unload" | "reload" | "all"
    pluginName: string     unload/reload 时的插件 InternalName
    filePath:   string     load/reload 时 DLL 完整路径（用于 /xldev load）
```

**示例:**
- 卸载: `{"action":"unload", "pluginName":"MCPforDalamud"}`
- 加载: `{"action":"load", "filePath":"E:\\plugin\\MCPforDalamud.dll"}`
- 重载: `{"action":"reload", "pluginName":"MCPforDalamud", "filePath":"E:\\plugin\\MCPforDalamud.dll"}`
- 全部重载: `{"action":"all"}`

## 事件缓存系统

插件每帧检测游戏状态变化，生成事件写入 2000 条环形缓冲区。AI 通过 `query_events` 按需查询，无需轮询。

### query_events

```
参数:
  types: string[]      可选，筛选事件类型（见下方事件类型表），null=全部
  count: int           可选，返回条数，默认50，最大500
  since: int           可选，起始时间戳(ms)
  before: int          可选，截止时间戳(ms)
```

### 事件类型（17种）

| 类型 | 说明 | 触发 |
|------|------|------|
| `hp_change` | HP变化 | CurrentHp 与上次不同 |
| `mp_change` | MP变化 | CurrentMp 与上次不同 |
| `gp_change` | GP变化 | CurrentGp 与上次不同 |
| `player_move` | 角色移动 | 坐标变化 > 0.5m |
| `job_change` | 职业切换 | ClassJob.RowId 变化 |
| `target_change` | 目标切换 | Target GameObjectId 变化 |
| `focus_target_change` | 焦点目标切换 | FocusTarget 变化 |
| `target_hp_change` | 目标HP变化 | 当前目标HP变化 |
| `combat_damage` | 受伤事件 | 玩家HP下降 |
| `combat_start` | 战斗开始 | Condition.InCombat 变 true |
| `combat_end` | 战斗结束 | Condition.InCombat 变 false |
| `map_change` | 地图切换 | TerritoryType 变化 |
| `mount_change` | 骑乘切换 | Condition.Mounted 变化 |
| `duty_update` | 副本状态变化 | DutyState.IsDutyStarted 变化 |
| `fate_update` | FATE状态变化 | 附近FATE增删或进度变化 |
| `nearby_enemy` | 周围敌人 | 检测范围内敌对目标变化 |
| `nearby_player` | 周围玩家 | 检测范围内玩家增删 |

### configure_event_collection

动态调整采集策略，减少无关事件：

```
参数 config 字段:
  playerStats: string[]     采集的属性 ["hp","mp","gp","job","position"]，默认全开
  targetStats: string[]     目标属性 ["hp","type","targetChange"]，默认全开
  objectRange: int          环境检测范围(米)，默认30，0=关闭
  objectTypes: string[]     环境对象类型，目前支持 ["enemy"]
  nearbyPlayerRange: int    周围玩家检测范围(米)，默认0(关闭)
  combatEvents: string[]    战斗事件 ["damage","startEnd"]，默认全部启用
  systemEvents: string[]    系统事件 ["duty","fate"]，默认全部启用
  throttleMs: int           同类事件最小间隔(ms)，默认500
```

### get_event_config

返回当前采集配置和缓冲区状态（bufferSize, maxBufferSize）。

## 插件桥接系统

让 MCP 插件与开发中的其他插件双向通信。

### 其他插件推送数据到 MCP

```csharp
// 通过 IPC 推送键值对数据到 MCP 的 2000 条环形缓存
var sub = pluginInterface.GetIpcSubscriber<string, object?>("MCPforDalamud.PushData");

// 数据格式: {"key": "分类键", "data": "任意JSON数据"}
sub.InvokeFunc("{\"key\":\"myPlugin.status\",\"data\":\"{\\\"hp\\\":12345}\"}");
```

插件启动时自动注册 `MCPforDalamud.PushData` IPC 接口（`Func<string, object?>` 签名），其他插件通过 `GetIpcSubscriber` 获取并调用即可推送数据。

### AI 查询推送数据

```
query_push_data:
  参数:
    key: string    可选，筛选指定 key
    count: int     可选，默认50
  返回:
    entries: [{key, data, timestamp, id}]
    total: int
```

### AI 调用其他插件 IPC

三步工作流：

**1. 注册已知端点**
```
register_ipc_endpoint:
  参数:
    pluginName: "BossModReborn"     插件 InternalName
    methodName: "Presets.GetActive"  IPC方法名
    signature:   "Func_string"      签名模板（如 Func_string, Func_bool, Action_bool, Func_int_string）
    description: "获取当前Preset"   功能描述
```

**2. 查询已注册端点**
```
list_ipc_endpoints:
  参数:
    pluginName: string   可选筛选
  返回:
    [{pluginName, methodName, signature, description}]
```

**3. 调用端点**
```
call_plugin_ipc:
  参数:
    pluginName: "BossModReborn"
    methodName: "Presets.GetActive"
    arguments:  {"value": true} 可选；单参数签名统一使用 value
  返回:
    success: boolean
    result:  string             调用返回值
```

### 签名模板说明

| 模板 | 含义 |
|------|------|
| `Func_bool` | 无参数，返回 bool |
| `Func_string` | 无参数，返回 string |
| `Func_int_string` | 入参 int，返回 string |
| `Action_bool` | 入参 bool，无返回 |
| `Func_bool_string` | 入参 bool，返回 string |

注册的端点会保存到插件配置。`Func_bool` 和 `Func_string` 不接受参数；其余单参数签名通过 `arguments.value` 传值，例如 `{"arguments":{"value":42}}`。

## 典型用例

### AI 自动化测试插件新功能

开发插件时，在关键节点向 MCP 推送数据，AI 结合游戏状态自动验证功能是否正常。

**被测插件代码：**
```csharp
// 第一步：推送测试开始标记
var ipc = pluginInterface.GetIpcSubscriber<string, object?>("MCPforDalamud.PushData");
ipc.InvokeFunc("{\"key\":\"myPlugin.test.autoDismount\",\"data\":\"{\\\"phase\\\":\\\"start\\\",\\\"expected\\\":\\\"dismount_on_aggro\\\"}\"}");

// 第二步：执行功能逻辑（如检测到进战自动下马）
var wasMounted = Condition[ConditionFlag.Mounted];
AutoDismountFeature.Execute(); // 被测功能

// 第三步：推送结果
ipc.InvokeFunc($"{{\"key\":\"myPlugin.test.autoDismount\",\"data\":\"{{ \\\"phase\\\":\\\"result\\\", \\\"wasMounted\\\":{wasMounted.ToString().ToLower()}, \\\"executedAt\\\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()} }}\"}}");
```

**AI 验证流程：**
```
1. AI 调用 query_push_data(key="myPlugin.test.autoDismount")  → 读到 start + result
2. AI 调用 query_events(types=["combat_start","mount_change"]) → 读到战斗开始 + 骑乘变化
3. AI 调用 get_condition → 确认当前不在骑乘状态
4. AI 对比: wasMounted=true, mount_change 事件存在, 当前未骑乘 → 功能正常 ✓
```

### AI 交互式调试

AI 操控角色并观察事件反馈，验证插件响应：

```
1. AI: set_target(name="木人") + execute_action(2241)    → 开始攻击
2. AI: query_events(types=["combat_start","combat_damage"]) → 确认进入战斗、造成伤害
3. AI: 等待 3 秒
4. AI: query_events(since=上一步时间) → 检查期间事件是否符合预期
5. AI: get_target_info → 确认木人HP变化
```

## 其他插件接入

```csharp
var sub = pluginInterface.GetIpcSubscriber<string, object?>("MCPforDalamud.PushData");
sub.InvokeFunc("{\"key\":\"mykey\",\"data\":\"some data\"}");
```

## 技术栈

.NET 10, C#, Dalamud.CN.NET.Sdk 15.0.0, API Level 15, FFXIVClientStructs, Lumina
