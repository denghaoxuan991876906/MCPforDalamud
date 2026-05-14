using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace MCPforDalamud.McpServer;

public class McpHttpServer : IDisposable
{
    private readonly ToolRegistry _toolRegistry;
    private HttpListener? _listener;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _listenThread;

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
        _listener.Prefixes.Add(string.Format("http://127.0.0.1:{0}/", Port));
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
                ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
            }
            catch (HttpListenerException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    private async void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

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

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
            {
                body = await reader.ReadToEndAsync();
            }

            var jsonResult = ProcessRequest(body);

            response.ContentType = "application/json; charset=utf-8";
            response.StatusCode = 200;

            var buffer = Encoding.UTF8.GetBytes(jsonResult);
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
            response.OutputStream.Close();
        }
        catch
        {
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch { }
        }
    }

    internal string ProcessRequest(string body)
    {
        JsonRpcRequest? jsonRpcRequest;
        try
        {
            jsonRpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(body, JsonOptions);
        }
        catch (JsonException)
        {
            return BuildErrorResponse(null, JsonRpcErrorCodes.ParseError, "请求体不是有效的 JSON");
        }

        if (jsonRpcRequest == null)
        {
            return BuildErrorResponse(null, JsonRpcErrorCodes.ParseError, "请求体为空");
        }

        if (jsonRpcRequest.Method != "tools/list" && jsonRpcRequest.Method != "tools/call")
        {
            return BuildErrorResponse(jsonRpcRequest.Id, JsonRpcErrorCodes.MethodNotFound,
                string.Format("未知方法: {0}", jsonRpcRequest.Method));
        }

        try
        {
            if (jsonRpcRequest.Method == "tools/list")
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
                return BuildSuccessResponse(jsonRpcRequest.Id, result);
            }
            else
            {
                if (jsonRpcRequest.Params == null)
                {
                    return BuildErrorResponse(jsonRpcRequest.Id, JsonRpcErrorCodes.InvalidParams,
                        "缺少 params 参数");
                }

                var toolName = jsonRpcRequest.Params.Value.GetProperty("name").GetString();
                JsonElement? arguments = null;

                if (jsonRpcRequest.Params.Value.TryGetProperty("arguments", out var args))
                {
                    arguments = args;
                }

                if (string.IsNullOrEmpty(toolName))
                {
                    return BuildErrorResponse(jsonRpcRequest.Id, JsonRpcErrorCodes.InvalidParams,
                        "未指定工具名称");
                }

                var toolResult = _toolRegistry.CallTool(toolName, arguments);
                var resultElement = JsonSerializer.SerializeToElement(toolResult, JsonOptions);

                return BuildSuccessResponse(jsonRpcRequest.Id, resultElement);
            }
        }
        catch (InvalidOperationException ex)
        {
            return BuildErrorResponse(jsonRpcRequest.Id, JsonRpcErrorCodes.MethodNotFound, ex.Message);
        }
        catch (Exception ex)
        {
            return BuildErrorResponse(jsonRpcRequest.Id, JsonRpcErrorCodes.InternalError, ex.Message);
        }
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
        try { _listener?.Stop(); } catch { }
        _listenThread?.Join(TimeSpan.FromSeconds(3));
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        (_listener as IDisposable)?.Dispose();
    }
}
