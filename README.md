# 🚀 Shutdown Server App

This project is a user-friendly Windows desktop application built using C# (.NET 6) and ASP.NET Core. It provides a simple graphical interface and a built-in web server, enabling you to remotely shut down your computer from any web-enabled device, such as your smartphone or tablet.

---

## 📌 Project Overview

### 🔹 Main Features
- **Remote Shutdown:** Shut down your PC remotely via a web browser.
- **Intuitive GUI:** A modern, animated user interface built with Windows Forms.
- **System Tray Integration:** Minimizes to the system tray for seamless background operation.
- **Easy Deployment:** Provided as a self-contained executable (EXE) that doesn't require additional installations.
- **Optional PIN Security:** Add an extra layer of protection with a 4-digit PIN code to prevent unauthorized access.

### 🛠 Technologies Used
- **C# (.NET 6)**
- **Windows Forms**
- **ASP.NET Core (Minimal API)**
- **Online icons provided by Icons8**

---

## ⚠️ Important Security Notice and Risks

This application exposes an HTTP endpoint (`/shutdown`) that triggers an immediate system shutdown when accessed. To mitigate the risk of unauthorized shutdowns, the app now includes support for an **optional 4-digit PIN code**.

**Security Enhancements:**
- If a PIN is configured, shutdown requests must include the correct code.
- PIN validation is enforced both on the local application and web interface.

**Recommended Usage:**
- Use the PIN feature to prevent misuse, especially on shared or open networks.
- Do not expose the endpoint directly to the internet without further protection.
- Consider additional firewall or network-layer security if using this in sensitive environments.

---

## ❗ Disclaimer

By using this application, you acknowledge that you understand and accept the inherent risks involved. The creator of this software is not responsible or liable for any loss, damage, data corruption, or any unintended consequences resulting from the usage of this application. Use at your own risk.

---

Enjoy the convenience, but always stay secure! 🚀✨

Made with 💜 by Mehdi
