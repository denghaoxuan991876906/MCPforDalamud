using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Numerics;

namespace MCPforDalamud.UI;

public class ConfigWindow : Window
{
    public ConfigWindow() : base("MCP for Dalamud 设置###MCPforDalamudConfig")
    {
        Size = new Vector2(560, 500);
        SizeCondition = ImGuiCond.FirstUseEver;
        RespectCloseHotkey = true;
    }

    public override void Draw()
    {
        var plugin = Plugin.Instance;
        if (plugin == null) { ImGui.Text("插件未初始化"); return; }
        var config = plugin.Config;

        ImGui.TextColored(new Vector4(0.3f, 1f, 0.6f, 1f), "MCP 服务状态");
        ImGui.Separator();

        bool isRunning = plugin.IsServerRunning;
        ImGui.TextColored(isRunning ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f),
            isRunning ? string.Format("服务运行中 - http://127.0.0.1:{0}/mcp", plugin.ServerPort) : "服务未运行");
        if (!string.IsNullOrEmpty(plugin.LastServerError))
            ImGui.TextWrapped(string.Format("启动错误: {0}", plugin.LastServerError));
        ImGui.Text(string.Format("已注册工具: {0} 个", plugin.ToolCount));

        ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.3f, 1f, 0.6f, 1f), "端口设置");
        ImGui.Separator();

        var port = config.Port;
        ImGui.SetNextItemWidth(120f);
        if (ImGui.InputInt("HTTP 端口", ref port, 0, 0))
        {
            if (port < 0) port = 0;
            if (port > 65535) port = 65535;
            config.Port = port;
            config.Save();
        }
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "(0=自动分配)");
        ImGui.Spacing();

        if (ImGui.Button("重启 HTTP 服务", new Vector2(160, 0)))
        {
            if (isRunning) { plugin.StopServer(); plugin.StartServer(); }
        }
        ImGui.SameLine();
        if (ImGui.Button("停止 HTTP 服务", new Vector2(160, 0))) { plugin.StopServer(); }
        ImGui.SameLine();
        if (ImGui.Button("启动 HTTP 服务", new Vector2(160, 0))) { plugin.StartServer(); }

        ImGui.Spacing(); ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.3f, 1f, 0.6f, 1f), "危险工具权限");
        ImGui.Separator();
        var permissionsChanged = false;
        var allowActions = config.AllowActions;
        if (ImGui.Checkbox("角色操作", ref allowActions)) { config.AllowActions = allowActions; permissionsChanged = true; }
        var allowMovement = config.AllowMovement;
        if (ImGui.Checkbox("角色移动", ref allowMovement)) { config.AllowMovement = allowMovement; permissionsChanged = true; }
        var allowChat = config.AllowChat;
        if (ImGui.Checkbox("发送聊天", ref allowChat)) { config.AllowChat = allowChat; permissionsChanged = true; }
        var allowPluginManagement = config.AllowPluginManagement;
        if (ImGui.Checkbox("插件管理", ref allowPluginManagement)) { config.AllowPluginManagement = allowPluginManagement; permissionsChanged = true; }
        if (permissionsChanged)
        {
            config.Save();
            plugin.StopServer();
            plugin.StartServer();
        }

        ImGui.Spacing(); ImGui.Spacing();
        ImGui.TextColored(new Vector4(0.3f, 1f, 0.6f, 1f), "工具类别");
        ImGui.Separator();
        ImGui.Text(string.Format("共计 {0} 个工具:", plugin.ToolCount));
        ImGui.BulletText("数据读取: get_player_status, get_target_info, get_party_list, get_inventory ...");
        ImGui.BulletText("角色操控: execute_action, set_target, jump, dismount, cancel_cast ...");
        ImGui.BulletText("事件缓存: query_events, configure_event_collection, get_event_config");
        ImGui.BulletText("插件桥接: query_push_data, register_ipc_endpoint, call_plugin_ipc ...");
        ImGui.BulletText("聊天: send_chat");
        ImGui.BulletText("移动: toggle_automove, face_target");

        ImGui.Spacing();
        ImGui.Separator();
        if (ImGui.Button("重置所有设置", new Vector2(160, 0)))
        {
            config.Port = 0;
            config.AllowActions = false;
            config.AllowMovement = false;
            config.AllowChat = false;
            config.AllowPluginManagement = false;
            config.Save();
        }
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1f), "MCP for Dalamud v0.2.0 by 嗨呀");
    }
}
