using System.Runtime.InteropServices;
using System.Text;

namespace XMouse
{
 public class MyApplicationContext : ApplicationContext
 {
 private readonly NotifyIcon trayIcon;
 private readonly ContextMenuStrip contextMenu;

 // Tracking fields
 private readonly ToolStripMenuItem trackToggleItem;
 private readonly ToolStripMenuItem resetItem;
 private readonly object distanceLock = new object();
 private double totalDistancePixels =0.0;
 private POINT lastPoint;
 private bool hasLastPoint = false;

 // Low-level mouse hook
 private IntPtr hookId = IntPtr.Zero;
 private LowLevelMouseProc? hookCallback;

 // UI form for live display
 private DistanceForm? distanceForm;

 // Persistence
 private readonly string settingsFilePath;

 public MyApplicationContext()
 {
 // Determine storage path
 var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
 var dir = Path.Combine(appData, "XMouse");
 Directory.CreateDirectory(dir);
 settingsFilePath = Path.Combine(dir, "settings.ini");

 // Create context menu so we can dispose/unsubscribe cleanly
 contextMenu = new ContextMenuStrip
 {
 Items =
 {
 new ToolStripMenuItem("Open", null, Open),
 (trackToggleItem = new ToolStripMenuItem("Track Mouse Distance", null, ToggleTracking) { CheckOnClick = true }),
 (resetItem = new ToolStripMenuItem("Reset Distance", null, ResetDistance) { Enabled = false }),
 new ToolStripSeparator(),
 new ToolStripMenuItem("Exit", null, Exit)
 }
 };

 // Load persisted settings (after menu items exist so we can apply values)
 LoadSettings();

 // Initialize Tray Icon
 trayIcon = new NotifyIcon()
 {
 Icon = SystemIcons.Application,
 ContextMenuStrip = contextMenu,
 Text = "XMouse - running",
 Visible = true
 };

 // UX: double-click opens the app (if any forms exist)
 trayIcon.DoubleClick += TrayIcon_DoubleClick;

 // Update tooltip with persisted distance
 UpdateTooltipWithDistance();
 }

 private void TrayIcon_DoubleClick(object? sender, EventArgs e)
 {
 Open(sender, e);
 }

 private void Open(object? sender, EventArgs e)
 {
 // If there is an open form, bring the first one to front; otherwise show the distance window
 if (Application.OpenForms.Count >0)
 {
 var form = Application.OpenForms[0];
 if (form.WindowState == FormWindowState.Minimized)
 form.WindowState = FormWindowState.Normal;

 // Bring to foreground
 form.Invoke((Action)(() =>
 {
 form.BringToFront();
 form.Activate();
 }));
 }

 // Show or create the distance window
 if (distanceForm == null || distanceForm.IsDisposed)
 {
 distanceForm = new DistanceForm();
 distanceForm.SetTotal(GetTotalDistance());
 }

 if (!distanceForm.Visible)
 {
 distanceForm.Show();
 }
 else
 {
 distanceForm.BringToFront();
 }
 }

 void Exit(object? sender, EventArgs e)
 {
 // Hide tray icon, otherwise it will remain shown until user mouses over it
 trayIcon.Visible = false;

 // Save distance and exit
 SaveDistance();

 // This will trigger disposal when application exits
 Application.Exit();
 }

 private void ToggleTracking(object? sender, EventArgs e)
 {
 if (trackToggleItem.Checked)
 {
 StartTracking();
 }
 else
 {
 StopTracking();
 }
 }

 private void ResetDistance(object? sender, EventArgs e)
 {
 lock (distanceLock)
 {
 totalDistancePixels =0.0;
 }

 hasLastPoint = false;
 resetItem.Enabled = false;
 SaveDistance();

 // Update UI
 trayIcon.ShowBalloonTip(1000, "XMouse", "Distance reset.", ToolTipIcon.Info);
 UpdateTooltipWithDistance();
 NotifyDistanceForm();
 }

 private void StartTracking()
 {
 // Initialize last point to current cursor position
 if (GetCursorPos(out POINT p))
 {
 lastPoint = p;
 hasLastPoint = true;
 }

 hookCallback = HookCallback;
 hookId = SetHook(hookCallback);

 resetItem.Enabled = true;
 trayIcon.ShowBalloonTip(1000, "XMouse", "Mouse distance tracking enabled.", ToolTipIcon.Info);
 UpdateTooltipWithDistance();
 }

 private void StopTracking()
 {
 Unhook();
 trayIcon.ShowBalloonTip(1000, "XMouse", "Mouse distance tracking disabled.", ToolTipIcon.Info);
 UpdateTooltipWithDistance();
 }

 private void UpdateTooltipWithDistance()
 {
 lock (distanceLock)
 {
 // NotifyIcon.Text max length is limited (~63 characters). Keep concise.
 trayIcon.Text = "XMouse - Distance: " + FormatDistance(totalDistancePixels);
 }
 }

 private static string FormatDistance(double pixels)
 {
 // For now show pixels; could convert to inches/cm using screen DPI if desired
 return Math.Round(pixels).ToString() + " px";
 }

 private double GetTotalDistance()
 {
 lock (distanceLock)
 {
 return totalDistancePixels;
 }
 }

 private void NotifyDistanceForm()
 {
 if (distanceForm == null || distanceForm.IsDisposed)
 return;

 try
 {
 var total = GetTotalDistance();
 if (distanceForm.InvokeRequired)
 {
 distanceForm.BeginInvoke((Action)(() => distanceForm.SetTotal(total)));
 }
 else
 {
 distanceForm.SetTotal(total);
 }
 }
 catch
 {
 // ignore if UI is shutting down
 }
 }

 // Hook management
 private IntPtr SetHook(LowLevelMouseProc proc)
 {
 using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
 using (var curModule = curProcess.MainModule!)
 {
 return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName),0);
 }
 }

 private void Unhook()
 {
 if (hookId != IntPtr.Zero)
 {
 UnhookWindowsHookEx(hookId);
 hookId = IntPtr.Zero;
 }

 hookCallback = null;
 hasLastPoint = false;
 }

 private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
 {
 const int WM_MOUSEMOVE =0x0200;

 if (nCode >=0 && wParam.ToInt64() == WM_MOUSEMOVE)
 {
 MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
 var pt = hookStruct.pt;

 if (hasLastPoint)
 {
 double dx = pt.x - lastPoint.x;
 double dy = pt.y - lastPoint.y;
 double dist = Math.Sqrt(dx * dx + dy * dy);

 lock (distanceLock)
 {
 totalDistancePixels += dist;
 }

 // Update last point
 lastPoint = pt;
 }
 else
 {
 lastPoint = pt;
 hasLastPoint = true;
 }

 // Update UI (throttling could be added later)
 try
 {
 UpdateTooltipWithDistance();
 NotifyDistanceForm();
 }
 catch
 {
 // ignore invoke errors if shutting down
 }
 }

 return CallNextHookEx(IntPtr.Zero, nCode, wParam, lParam);
 }

 protected override void Dispose(bool disposing)
 {
 if (disposing)
 {
 // Unsubscribe events
 if (trayIcon != null)
 {
 trayIcon.DoubleClick -= TrayIcon_DoubleClick;
 }

 // Unhook and dispose owned disposable resources
 Unhook();
 contextMenu?.Dispose();

 // Save distance
 SaveDistance();

 // Dispose and close form
 if (distanceForm != null && !distanceForm.IsDisposed)
 {
 try { distanceForm.Close(); } catch { }
 try { distanceForm.Dispose(); } catch { }
 }

 trayIcon?.Dispose();
 }

 base.Dispose(disposing);
 }

 private void SaveDistance()
 {
 try
 {
 var total = GetTotalDistance();
 // Write to INI
 WritePrivateProfileString("XMouse", "TotalDistance", total.ToString(System.Globalization.CultureInfo.InvariantCulture), settingsFilePath);
 WritePrivateProfileString("XMouse", "TrackingEnabled", trackToggleItem.Checked ? "1" : "0", settingsFilePath);
 }
 catch
 {
 // ignore persistence errors
 }
 }

 private void LoadSettings()
 {
 try
 {
 var sb = new StringBuilder(256);
 // Read total distance
 GetPrivateProfileString("XMouse", "TotalDistance", "0", sb, sb.Capacity, settingsFilePath);
 if (double.TryParse(sb.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v))
 {
 lock (distanceLock)
 {
 totalDistancePixels = v;
 }
 }

 // Read tracking enabled
 sb.Clear();
 GetPrivateProfileString("XMouse", "TrackingEnabled", "0", sb, sb.Capacity, settingsFilePath);
 var trackingEnabled = sb.ToString() == "1";

 // Apply to UI controls
 if (trackToggleItem != null)
 {
 trackToggleItem.Checked = trackingEnabled;
 if (trackingEnabled)
 {
 // start tracking if preference enabled
 StartTracking();
 }
 }

 if (resetItem != null)
 {
 resetItem.Enabled = totalDistancePixels >0.0;
 }
 }
 catch
 {
 // ignore
 }
 }

 #region Native interop
 private const int WH_MOUSE_LL =14;

 private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

 [StructLayout(LayoutKind.Sequential)]
 private struct POINT
 {
 public int x;
 public int y;
 }

 [StructLayout(LayoutKind.Sequential)]
 private struct MSLLHOOKSTRUCT
 {
 public POINT pt;
 public uint mouseData;
 public uint flags;
 public uint time;
 public IntPtr dwExtraInfo;
 }

 [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
 private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

 [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
 [return: MarshalAs(UnmanagedType.Bool)]
 private static extern bool UnhookWindowsHookEx(IntPtr hhk);

 [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
 private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

 [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
 private static extern IntPtr GetModuleHandle(string lpModuleName);

 [DllImport("user32.dll")]
 [return: MarshalAs(UnmanagedType.Bool)]
 private static extern bool GetCursorPos(out POINT lpPoint);

 // INI helpers (Windows)
 [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
 [return: MarshalAs(UnmanagedType.Bool)]
 private static extern bool WritePrivateProfileString(string section, string key, string value, string filePath);

 [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
 private static extern int GetPrivateProfileString(string section, string key, string defaultValue, StringBuilder retVal, int size, string filePath);
 #endregion
 }
}
