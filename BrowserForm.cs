using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FutTrading;

/// <summary>
/// Built-in browser window with tabs (Futbin, X, EA web app). The interface only reads page text through it;
/// the one click it performs is Futbin's "Top Losers" tab. It never types or clicks on the EA web app.
/// Keep it open: sites such as the EA web app stop rendering when their window is minimised.
/// </summary>
public class BrowserForm : Form
{
    readonly CoreWebView2Environment env;
    readonly TabControl tabs = new() { Dock = DockStyle.Fill };
    readonly Dictionary<string, (TabPage Page, WebView2 View)> map = new();
    int seq;
    bool closing;

    public BrowserForm(CoreWebView2Environment env)
    {
        this.env = env;
        Text = "FUT Trading — Navigateur (connecte-toi ici à la web app EA et à X)";
        Width = 1280;
        Height = 860;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(60, 60);
        var bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        bar.Items.Add(new ToolStripButton("Web app EA", null, async (_, _) => await OpenTabAsync("https://www.ea.com/ea-sports-fc/ultimate-team/web-app/", true)));
        bar.Items.Add(new ToolStripButton("Futbin", null, async (_, _) => await OpenTabAsync("https://www.futbin.com/", true)));
        bar.Items.Add(new ToolStripButton("X", null, async (_, _) => await OpenTabAsync("https://x.com/FutSheriff", true)));
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add(new ToolStripButton("Fermer l'onglet", null, (_, _) => CloseSelectedTab()));
        Controls.Add(tabs);
        Controls.Add(bar);
        // Closing the window only hides it to the taskbar: the interface still needs its tabs.
        FormClosing += (_, e) => { if (!closing && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; WindowState = FormWindowState.Minimized; } };
    }

    public void ShowBrowser()
    {
        Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
    }

    public void CloseForReal() { closing = true; Close(); }

    void CloseSelectedTab()
    {
        var page = tabs.SelectedTab; if (page == null) return;
        var id = map.FirstOrDefault(kv => kv.Value.Page == page).Key;
        if (id != null) { map[id].View.Dispose(); map.Remove(id); }
        tabs.TabPages.Remove(page);
    }

    public async Task<string> OpenTabAsync(string url, bool select)
    {
        var id = "t" + (++seq);
        var view = new WebView2 { Dock = DockStyle.Fill };
        var page = new TabPage("Chargement…");
        page.Controls.Add(view);
        tabs.TabPages.Add(page);
        map[id] = (page, view);
        if (select) tabs.SelectedTab = page;
        await view.EnsureCoreWebView2Async(env);
        view.CoreWebView2.DocumentTitleChanged += (_, _) => { var t = view.CoreWebView2.DocumentTitle; page.Text = t.Length > 28 ? t[..28] + "…" : t; };
        view.CoreWebView2.NewWindowRequested += (_, a) => { a.Handled = true; _ = OpenTabAsync(a.Uri, true); };
        await NavigateAndWait(view, url);
        return id;
    }

    static async Task NavigateAndWait(WebView2 view, string url)
    {
        var done = new TaskCompletionSource<bool>();
        void Handler(object? s, CoreWebView2NavigationCompletedEventArgs e) { view.CoreWebView2.NavigationCompleted -= Handler; done.TrySetResult(e.IsSuccess); }
        view.CoreWebView2.NavigationCompleted += Handler;
        view.CoreWebView2.Navigate(url);
        await Task.WhenAny(done.Task, Task.Delay(30000));
        await Task.Delay(1500);   // let the page's scripts render its content
    }

    (string Id, WebView2 View) Resolve(JsonObject input)
    {
        var id = input["tabId"]?.GetValue<string>();
        if (id != null && map.TryGetValue(id, out var t)) return (id, t.View);
        if (id != null) throw new InvalidOperationException("Onglet introuvable : " + id);
        var sel = map.FirstOrDefault(kv => kv.Value.Page == tabs.SelectedTab);
        if (sel.Key == null) throw new InvalidOperationException("Aucun onglet ouvert");
        return (sel.Key, sel.Value.View);
    }

    static async Task<JsonNode?> Eval(WebView2 view, string js)
    {
        var raw = await view.CoreWebView2.ExecuteScriptAsync(js);
        var s = JsonSerializer.Deserialize<string>(raw);
        return s == null ? null : JsonNode.Parse(s);
    }

    /// <summary>Same tools and text formats as the Claude app browser, so the interface's page readers work unchanged.</summary>
    public async Task<string> CallAsync(string tool, JsonObject input)
    {
        switch (tool)
        {
            case "preview_start":
            {
                var id = await OpenTabAsync(input["url"]!.GetValue<string>(), false);
                return JsonSerializer.Serialize(new { tabId = id, navOk = true });
            }
            case "navigate":
            {
                var (id, view) = Resolve(input);
                var url = input["url"]!.GetValue<string>();
                await NavigateAndWait(view, url);
                return $"navigated to {url}\n\nTab Context:\n- Executed on tabId: {id}";
            }
            case "get_page_text":
            {
                var (id, view) = Resolve(input);
                var max = input["max_chars"]?.GetValue<int>() ?? 50000;
                var o = await Eval(view, "JSON.stringify((()=>{const m=document.querySelector('main')||document.querySelector('article')||document.body;return {t:document.title,u:location.href,s:m.tagName.toLowerCase(),x:m.innerText||''};})())");
                var text = o?["x"]?.GetValue<string>() ?? "";
                if (text.Length > max) text = text[..max];
                return $"Title: {o?["t"]}\nURL: {o?["u"]}\nSource element: <{o?["s"]}>\n---\n{text}\n\nTab Context:\n- Executed on tabId: {id}";
            }
            case "find":
            {
                var (_, view) = Resolve(input);
                var q = JsonSerializer.Serialize(input["query"]!.GetValue<string>());
                var o = await Eval(view, $$"""
                    JSON.stringify((()=>{const q={{q}}.toLowerCase();let n=window.__futref||0;const out=[];
                      for(const el of document.querySelectorAll('a,button,[role=tab],[role=button]')){
                        const t=(el.innerText||el.textContent||'').trim().replace(/\s+/g,' ');
                        if(!t||t.length>120||!t.toLowerCase().includes(q)) continue;
                        if(!el.dataset.futref){el.dataset.futref='ref_'+(++n);}
                        const role=el.tagName==='A'?'link':'button';
                        const href=el.getAttribute('href');
                        out.push('- '+role+' "'+t+'" ['+el.dataset.futref+']'+(href?' href="'+href+'"':''));
                        if(out.length>=40) break;}
                      window.__futref=n;return {r:out.join('\n')};})())
                    """);
                var r = o?["r"]?.GetValue<string>() ?? "";
                return r.Length > 0 ? r : $"No matches for \"{input["query"]}\".";
            }
            case "computer":
            {
                var (_, view) = Resolve(input);
                if (input["action"]?.GetValue<string>() != "left_click") throw new InvalidOperationException("Action non prise en charge");
                var reference = JsonSerializer.Serialize(input["ref"]!.GetValue<string>());
                // Only Futbin pages may be clicked: never act on the EA web app.
                if (!view.CoreWebView2.Source.Contains("futbin.com")) throw new InvalidOperationException("Clic refusé hors de Futbin");
                var o = await Eval(view, $"JSON.stringify((()=>{{const el=document.querySelector('[data-futref='+JSON.stringify({reference})+']');if(!el)return {{ok:false}};el.click();return {{ok:true}};}})())");
                if (o?["ok"]?.GetValue<bool>() != true) throw new InvalidOperationException("Élément introuvable");
                await Task.Delay(800);
                return "clicked";
            }
            default:
                throw new InvalidOperationException("Outil inconnu : " + tool);
        }
    }
}
