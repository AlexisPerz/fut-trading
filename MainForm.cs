using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FutTrading;

/// <summary>
/// Main window: shows the trading interface (wwwroot/index.html) and answers its requests:
/// load/save of the local data file, and read-only page reading through the built-in browser window.
/// </summary>
public class MainForm : Form
{
    readonly WebView2 ui = new() { Dock = DockStyle.Fill };
    readonly string dataDir;
    readonly string stateFile;
    BrowserForm? browser;

    public MainForm()
    {
        Text = "FUT Trading";
        Width = 1280;
        Height = 900;
        StartPosition = FormStartPosition.CenterScreen;
        dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        stateFile = Path.Combine(dataDir, "base.json");
        Controls.Add(ui);
        Load += async (_, _) => await InitAsync();
    }

    async Task InitAsync()
    {
        try
        {
            // One browser profile for the interface and the reading tabs: logins (EA, X) and Futbin checks are kept between runs.
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(dataDir, "navigateur"));
            await ui.EnsureCoreWebView2Async(env);
            browser = new BrowserForm(env);
            browser.Show();

            var core = ui.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("app.futtrading", Path.Combine(AppContext.BaseDirectory, "wwwroot"), CoreWebView2HostResourceAccessKind.Allow);
            core.WebMessageReceived += OnMessage;
            // Links from the interface open as tabs of the built-in browser.
            core.NewWindowRequested += (_, a) => { a.Handled = true; _ = browser.OpenTabAsync(a.Uri, true); browser.ShowBrowser(); };
            core.Navigate("https://app.futtrading/index.html");
        }
        catch (WebView2RuntimeNotFoundException)
        {
            MessageBox.Show("Le composant Microsoft Edge WebView2 est introuvable. Installe-le depuis le site de Microsoft, puis relance FUT Trading.", "FUT Trading");
            Close();
        }
    }

    async void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = JsonNode.Parse(e.WebMessageAsJson)!;
        var reply = new JsonObject { ["id"] = msg["id"]!.GetValue<int>() };
        try
        {
            switch (msg["op"]!.GetValue<string>())
            {
                case "load":
                    reply["result"] = File.Exists(stateFile) ? File.ReadAllText(stateFile) : null;
                    break;
                case "save":
                    // Write to a temp file first so a crash never leaves a half-written base.
                    var tmp = stateFile + ".tmp";
                    File.WriteAllText(tmp, msg["data"]!.GetValue<string>());
                    File.Move(tmp, stateFile, true);
                    reply["result"] = true;
                    break;
                case "browser":
                    reply["result"] = await browser!.CallAsync(msg["tool"]!.GetValue<string>(), msg["input"] as JsonObject ?? new JsonObject());
                    break;
                case "showBrowser":
                    browser!.ShowBrowser();
                    reply["result"] = true;
                    break;
                default:
                    reply["error"] = "Opération inconnue";
                    break;
            }
        }
        catch (Exception ex)
        {
            reply["error"] = ex.Message;
        }
        ui.CoreWebView2.PostWebMessageAsJson(reply.ToJsonString());
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        browser?.CloseForReal();
        base.OnFormClosed(e);
    }
}
