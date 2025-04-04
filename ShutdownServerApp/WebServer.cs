using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ShutdownServerApp
{
    public class WebServer
    {
        private IHost _host;
        private CancellationTokenSource _cts;
        public string PinCode { get; set; } = "";
        public string LocalIPAddress => GetLocalIPAddress();

        public async Task StartAsync()
        {
            _cts = new CancellationTokenSource();
            var builder = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls("http://0.0.0.0:5050");
                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet(
                                "/shutdown",
                                async context =>
                                {
                                    if (!string.IsNullOrEmpty(PinCode))
                                    {
                                        var d1 = context.Request.Query["d1"].ToString();
                                        var d2 = context.Request.Query["d2"].ToString();
                                        var d3 = context.Request.Query["d3"].ToString();
                                        var d4 = context.Request.Query["d4"].ToString();
                                        string providedPin = d1 + d2 + d3 + d4;
                                        if (providedPin.Length != 4)
                                        {
                                            context.Response.ContentType = "text/html";
                                            string html =
                                                "<html><head><title>Shutdown</title></head><body>";
                                            html += "<form method='get' action='/shutdown'>";
                                            html +=
                                                "<input type='password' name='d1' maxlength='1' pattern='\\d' required>";
                                            html +=
                                                "<input type='password' name='d2' maxlength='1' pattern='\\d' required>";
                                            html +=
                                                "<input type='password' name='d3' maxlength='1' pattern='\\d' required>";
                                            html +=
                                                "<input type='password' name='d4' maxlength='1' pattern='\\d' required>";
                                            html += "<input type='submit' value='Shutdown'>";
                                            html += "</form></body></html>";
                                            await context.Response.WriteAsync(html);
                                            return;
                                        }
                                        else if (providedPin != PinCode)
                                        {
                                            context.Response.StatusCode = 403;
                                            await context.Response.WriteAsync("Invalid pin");
                                            return;
                                        }
                                    }
                                    try
                                    {
                                        Process.Start(
                                            new ProcessStartInfo("shutdown", "/s /t 1")
                                            {
                                                CreateNoWindow = true,
                                                UseShellExecute = false,
                                            }
                                        );
                                    }
                                    catch (Exception ex)
                                    {
                                        await context.Response.WriteAsync("Error: " + ex.Message);
                                        return;
                                    }
                                    await context.Response.WriteAsync("Shutdown initiated.");
                                }
                            );
                            endpoints.MapGet(
                                "/stop",
                                async context =>
                                {
                                    _cts.Cancel();
                                    await context.Response.WriteAsync("Server stopping...");
                                }
                            );
                        });
                    });
                });
            _host = builder.Build();
            _ = Task.Run(async () =>
            {
                try
                {
                    await _host.RunAsync(_cts.Token);
                }
                catch (OperationCanceledException) { }
            });
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
            if (_host != null)
            {
                try
                {
                    await _host.StopAsync();
                }
                catch { }
                _host = null;
            }
        }

        private string GetLocalIPAddress()
        {
            string localIP = "127.0.0.1";
            try
            {
                using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 80);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                {
                    localIP = endPoint.Address.ToString();
                }
            }
            catch { }
            return localIP;
        }
    }
}
