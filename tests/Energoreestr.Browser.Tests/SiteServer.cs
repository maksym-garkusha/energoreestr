using System.Net;
using System.Net.Sockets;

namespace Energoreestr.Browser.Tests;

/// <summary>
/// Віддає теку site так, як її віддає GitHub Pages: під /energoreestr/, щоб перевірити
/// і відносні посилання між сторінками. Рахує кожен запит, який до нього дійшов.
/// </summary>
public sealed class SiteServer : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly string root;
    private readonly List<string> requests = [];

    public SiteServer(string root)
    {
        this.root = root;
        int port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}/energoreestr/";
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        _ = Task.Run(ServeAsync);
    }

    public string BaseUrl { get; }

    public IReadOnlyList<string> Requests
    {
        get { lock (requests) return [.. requests]; }
    }

    private async Task ServeAsync()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (HttpListenerException)
            {
                return;   // сервер зупинено
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            await RespondAsync(context);
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        string path = context.Request.Url!.AbsolutePath;
        lock (requests) requests.Add(path);

        string? file = Resolve(path);
        if (file is null)
        {
            context.Response.StatusCode = 404;
            context.Response.Close();
            return;
        }
        byte[] body = await File.ReadAllBytesAsync(file);
        context.Response.ContentType = Path.GetExtension(file) == ".json" ? "application/json" : "text/html; charset=utf-8";
        context.Response.Headers["Cache-Control"] = "no-store";
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    private string? Resolve(string path)
    {
        const string prefix = "/energoreestr/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal)) return null;
        string relative = path[prefix.Length..];
        if (relative.Length == 0 || relative.EndsWith('/')) relative += "index.html";
        string full = Path.GetFullPath(Path.Combine(root, relative));
        return full.StartsWith(root, StringComparison.Ordinal) && File.Exists(full) ? full : null;
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public void Dispose()
    {
        listener.Stop();
        listener.Close();
    }
}
