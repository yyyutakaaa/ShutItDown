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

            // RunAsync wordt gestart op de achtergrond
            _ = Task.Run(async () =>
            {
                try
                {
                    await _host.RunAsync(_cts.Token);
                }
                catch (OperationCanceledException)
                {
                    // Verwacht bij annulering
                }
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
