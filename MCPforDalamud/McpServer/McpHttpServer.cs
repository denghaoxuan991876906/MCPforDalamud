using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MCPforDalamud.McpServer;

public class McpHttpServer : IDisposable
{
    private const int MaxRequestBodyBytes = 1024 * 1024;
    private readonly ToolRegistry _toolRegistry;
    private HttpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _listenThread;
    private readonly string _host;
    private readonly SemaphoreSlim _requestSlots = new(8, 8);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public int Port { get; private set; }

    public McpHttpServer(ToolRegistry toolRegistry, string host = "127.0.0.1", int port = 0)
    {
        _toolRegistry = toolRegistry;
        _host = host;

        if (port == 0)
        {
            port = FindFreePort();
        }

        Port = port;
    }

    private static int FindFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    public void Start()
    {
        _listener = new HttpListener();
        _listener.Prefixes.Add(string.Format("http://{0}:{1}/", _host, Port));
        _listener.Start();

        _listenThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "MCP-HTTP-Server"
        };
        _listenThread.Start();
    }

    private void ListenLoop()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var context = _listener!.GetContext();
                _ = HandleRequestAsync(context);
            }
            catch (HttpListenerException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "MCP HTTP listener stopped unexpectedly");
                break;
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        var slotAcquired = false;

        try
        {
            if (request.Url?.AbsolutePath != "/mcp")
            {
                response.StatusCode = 404;
                response.Close();
                return;
            }

            if (request.HttpMethod != "POST")
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            if (request.ContentLength64 > MaxRequestBodyBytes)
            {
                response.StatusCode = 413;
                response.Close();
                return;
            }

            await _requestSlots.WaitAsync(_cts.Token);
            slotAcquired = true;
            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                var readBuffer = new char[8192];
                var builder = new StringBuilder();
                var bodyBytes = 0;
                int read;
                while ((read = await reader.ReadAsync(readBuffer, _cts.Token)) > 0)
                {
                    builder.Append(readBuffer, 0, read);
                    bodyBytes += Encoding.UTF8.GetByteCount(readBuffer.AsSpan(0, read));
                    if (bodyBytes > MaxRequestBodyBytes)
                    {
                        response.StatusCode = 413;
                        response.Close();
                        return;
                    }
                }
                body = builder.ToString();
            }

            var jsonResult = ProcessRequest(body);

            if (jsonResult == null)
            {
                response.StatusCode = 202;
                response.Close();
                return;
            }

            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = 200;

            var buffer = Encoding.UTF8.GetBytes(jsonResult);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
        }
        catch (OperationCanceledException) when (_cts.IsCancellationRequested)
        {
            try { response.Close(); } catch { }
        }
        catch (Exception ex)
        {
            Service.PluginLog.Error(ex, "MCP HTTP request failed");
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch { }
        }
        finally { if (slotAcquired) _requestSlots.Release(); }
    }

    internal string? ProcessRequest(string body)
    {
        JsonElement root;
        try
        {
            root = JsonDocument.Parse(body).RootElement.Clone();
        }
        catch (JsonException)
        {
            return BuildErrorResponse(null, JsonRpcErrorCodes.ParseError, "请求体不是有效的 JSON");
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return BuildErrorResponse(null, JsonRpcErrorCodes.InvalidRequest, "请求必须是 JSON 对象");
        }

        var id = root.TryGetProperty("id", out var idValue) ? idValue.Clone() : (JsonElement?)null;
        var isNotification = !id.HasValue;
        if (!root.TryGetProperty("jsonrpc", out var version) || version.GetString() != "2.0" ||
            !root.TryGetProperty("method", out var methodValue) || methodValue.ValueKind != JsonValueKind.String)
        {
            return isNotification ? null : BuildErrorResponse(id, JsonRpcErrorCodes.InvalidRequest, "无效的 JSON-RPC 2.0 请求");
        }

        var method = methodValue.GetString()!;
        JsonElement? parameters = root.TryGetProperty("params", out var paramsValue) ? paramsValue.Clone() : null;

        if (isNotification)
        {
            if (method == "notifications/initialized" || method == "notifications/cancelled") return null;
            return null;
        }

        try
        {
            if (method == "initialize")
            {
                return BuildSuccessResponse(id, BuildInitializeResult(parameters));
            }
            if (method == "ping")
            {
                return BuildSuccessResponse(id, JsonSerializer.SerializeToElement(new { }));
            }
            if (method == "tools/list")
            {
                var tools = _toolRegistry.ListTools();
                var toolList = tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    inputSchema = t.InputSchema ?? JsonSerializer.SerializeToElement(new
                    {
                        type = "object",
                        properties = new Dictionary<string, object>()
                    })
                }).ToArray();

                var result = JsonSerializer.SerializeToElement(new { tools = toolList }, JsonOptions);
                return BuildSuccessResponse(id, result);
            }
            if (method == "tools/call")
            {
                if (parameters is not { ValueKind: JsonValueKind.Object })
                {
                    return BuildErrorResponse(id, JsonRpcErrorCodes.InvalidParams,
                        "缺少 params 参数");
                }

                if (!parameters.Value.TryGetProperty("name", out var nameValue) || nameValue.ValueKind != JsonValueKind.String)
                    return BuildErrorResponse(id, JsonRpcErrorCodes.InvalidParams, "未指定有效的工具名称");
                var toolName = nameValue.GetString();
                JsonElement? arguments = null;

                if (parameters.Value.TryGetProperty("arguments", out var args))
                {
                    if (args.ValueKind != JsonValueKind.Object)
                        return BuildErrorResponse(id, JsonRpcErrorCodes.InvalidParams, "arguments 必须是对象");
                    arguments = args;
                }

                if (string.IsNullOrEmpty(toolName))
                {
                    return BuildErrorResponse(id, JsonRpcErrorCodes.InvalidParams,
                        "未指定工具名称");
                }

                var toolResult = _toolRegistry.CallTool(toolName, arguments);
                var structured = JsonSerializer.SerializeToElement(toolResult, JsonOptions);
                var text = JsonSerializer.Serialize(toolResult, JsonOptions);
                var resultElement = JsonSerializer.SerializeToElement(new
                {
                    content = new[] { new { type = "text", text } },
                    structuredContent = structured,
                    isError = false
                }, JsonOptions);
                return BuildSuccessResponse(id, resultElement);
            }
            return BuildErrorResponse(id, JsonRpcErrorCodes.MethodNotFound, string.Format("未知方法: {0}", method));
        }
        catch (ToolNotFoundException ex)
        {
            return BuildErrorResponse(id, JsonRpcErrorCodes.MethodNotFound, ex.Message);
        }
        catch (Exception ex)
        {
            var resultElement = JsonSerializer.SerializeToElement(new
            {
                content = new[] { new { type = "text", text = ex.Message } },
                isError = true
            }, JsonOptions);
            return method == "tools/call"
                ? BuildSuccessResponse(id, resultElement)
                : BuildErrorResponse(id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
    }

    private static JsonElement BuildInitializeResult(JsonElement? parameters)
    {
        var requested = parameters is { ValueKind: JsonValueKind.Object } &&
                        parameters.Value.TryGetProperty("protocolVersion", out var protocol)
            ? protocol.GetString()
            : null;
        var selected = requested != null && McpProtocol.SupportedVersions.Contains(requested)
            ? requested
            : McpProtocol.LatestVersion;
        return JsonSerializer.SerializeToElement(new
        {
            protocolVersion = selected,
            capabilities = new { tools = new { listChanged = false } },
            serverInfo = new { name = "MCPforDalamud", version = "0.2.0" },
            instructions = "Provides FFXIV game state and explicitly enabled game-control tools through Dalamud."
        }, JsonOptions);
    }

    private static string BuildSuccessResponse(JsonElement? id, JsonElement result)
    {
        var response = new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Id = id,
            Result = result
        };
        return JsonSerializer.Serialize(response, JsonOptions);
    }

    private static string BuildErrorResponse(JsonElement? id, int code, string message)
    {
        var response = new JsonRpcResponse
        {
            Jsonrpc = "2.0",
            Id = id,
            Error = new JsonRpcError
            {
                Code = code,
                Message = message
            }
        };
        return JsonSerializer.Serialize(response, JsonOptions);
    }

    public void Stop()
    {
        _cts.Cancel();
        try { _listener?.Close(); } catch { }
        _listenThread?.Join(TimeSpan.FromSeconds(3));
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
    }
}
