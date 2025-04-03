import threading
import os
import socket
import requests
import io
from flask import Flask, request
import customtkinter as ctk
import pystray
from PIL import Image

app = Flask(__name__)


@app.route("/shutdown")
def shutdown():
    os.system("shutdown /s /t 1")
    return "Shutdown initiated."


@app.route("/stop")
def stop_server_route():
    shutdown_func = request.environ.get("werkzeug.server.shutdown")
    if shutdown_func:
        shutdown_func()
        return "Server stopping..."
    return "Server shutdown not available."


def run_flask():
    app.run(host="0.0.0.0", port=5050, use_reloader=False)


def get_local_ip():
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        s.connect(("8.8.8.8", 80))
        ip = s.getsockname()[0]
    except Exception:
        ip = "127.0.0.1"
    finally:
        s.close()
    return ip


server_thread = None


def start_server():
    global server_thread
    if server_thread is None or not server_thread.is_alive():
        server_thread = threading.Thread(target=run_flask, daemon=True)
        server_thread.start()
        update_status("running")


def stop_server():
    try:
        requests.get("http://127.0.0.1:5050/stop")
    except Exception as e:
        print("Error stopping server:", e)
    update_status("stopped")


def update_status(status):
    if status == "running":
        status_label.configure(text="🟢 Server is running")
        ip = get_local_ip()
        link_label.configure(text=f"http://{ip}:5050/shutdown")
        toggle_btn.configure(text="Stop Server", command=stop_server)
    else:
        status_label.configure(text="🔴 Server is stopped")
        link_label.configure(text="N/A")
        toggle_btn.configure(text="Start Server", command=start_server)


tray_icon = None


def on_open(icon, item):
    icon.stop()
    app_ui.after(0, app_ui.deiconify)


def on_exit(icon, item):
    icon.stop()
    app_ui.after(0, app_ui.destroy)


def load_icon_from_url(url):
    try:
        response = requests.get(url)
        response.raise_for_status()
        image_data = response.content
        image = Image.open(io.BytesIO(image_data))
        return image
    except Exception as e:
        print("Error loading icon from URL:", e)
        return None


def create_tray_icon():
    global tray_icon
    icon_url = "https://img.icons8.com/fluency/64/000000/shutdown.png"
    image = load_icon_from_url(icon_url)
    if image is None:
        return
    menu = pystray.Menu(
        pystray.MenuItem("Open", on_open), pystray.MenuItem("Exit", on_exit)
    )
    tray_icon = pystray.Icon("Shutdown", image, "Shutdown", menu)
    tray_icon.run()


def on_minimize(event):
    if app_ui.state() == "iconic":
        app_ui.withdraw()
        threading.Thread(target=create_tray_icon, daemon=True).start()


ctk.set_appearance_mode("dark")
ctk.set_default_color_theme("blue")
app_ui = ctk.CTk()
app_ui.geometry("450x300")
app_ui.title("Shutdown Server")
frame = ctk.CTkFrame(app_ui, corner_radius=15)
frame.pack(padx=20, pady=20, fill="both", expand=True)
title_label = ctk.CTkLabel(
    frame, text="💻 Shutdown Server", font=ctk.CTkFont(size=20, weight="bold")
)
title_label.pack(pady=(10, 5))
status_label = ctk.CTkLabel(
    frame, text="🔴 Server is stopped", font=ctk.CTkFont(size=16)
)
status_label.pack(pady=5)
toggle_btn = ctk.CTkButton(frame, text="Start Server", command=start_server)
toggle_btn.pack(pady=10)
link_label = ctk.CTkLabel(frame, text="N/A", font=ctk.CTkFont(size=12))
link_label.pack(pady=10)
app_ui.bind("<Unmap>", on_minimize)
app_ui.mainloop()
