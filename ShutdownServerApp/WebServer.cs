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
                                                "<!DOCTYPE html>"
                                                + "<html lang='en'>"
                                                + "<head>"
                                                + "  <meta charset='UTF-8'>"
                                                + "  <meta name='viewport' content='width=device-width, initial-scale=1.0'>"
                                                + "  <title>Shutdown</title>"
                                                + "  <style>"
                                                + "    body { font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; display: flex; justify-content: center; align-items: center; height: 100vh; }"
                                                + "    .container { background-color: #fff; padding: 20px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.2); text-align: center; width: 90%; max-width: 400px; }"
                                                + "    h2 { margin-bottom: 20px; color: #333; }"
                                                + "    .pin-container { display: flex; justify-content: center; }"
                                                + "    .pin-input { width: 50px; padding: 10px; margin: 5px; font-size: 18px; text-align: center; border: 1px solid #ccc; border-radius: 4px; }"
                                                + "    input[type='submit'] { padding: 10px 20px; font-size: 18px; background-color: #007BFF; color: #fff; border: none; border-radius: 4px; cursor: pointer; margin-top: 10px; }"
                                                + "    input[type='submit']:hover { background-color: #0056b3; }"
                                                + "  </style>"
                                                + "</head>"
                                                + "<body>"
                                                + "  <div class='container'>"
                                                + "    <h2>Enter your PIN</h2>"
                                                + "    <form method='get' action='/shutdown'>"
                                                + "      <div class='pin-container'>"
                                                + "        <input class='pin-input' type='password' name='d1' maxlength='1' pattern='\\d' required>"
                                                + "        <input class='pin-input' type='password' name='d2' maxlength='1' pattern='\\d' required>"
                                                + "        <input class='pin-input' type='password' name='d3' maxlength='1' pattern='\\d' required>"
                                                + "        <input class='pin-input' type='password' name='d4' maxlength='1' pattern='\\d' required>"
                                                + "      </div>"
                                                + "      <br>"
                                                + "      <input type='submit' value='Shutdown'>"
                                                + "    </form>"
                                                + "  </div>"
                                                + "</body>"
                                                + "</html>";
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
