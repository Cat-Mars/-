// 喵喵.exe —— 你家猫趴在 Windows 桌面上
// 编译（Win11 自带 csc，不需要装 VS）:
//   C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /unsafe /out:喵喵.exe 喵喵.cs
// 或者双击本目录的 烤.bat
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[StructLayout(LayoutKind.Sequential)]
struct POINT { public int x, y; }
[StructLayout(LayoutKind.Sequential)]
struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

static class W {
    [DllImport("user32.dll")] public static extern IntPtr CreateWindowEx(int ex, string cls, string t, int ws, int x, int y, int w, int h, IntPtr p, IntPtr m, IntPtr i, IntPtr lp);
    [DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr RegisterClassEx(ref WNDCLASSEX wc);
    [DllImport("user32.dll")] public static extern IntPtr DefWindowProc(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetLayeredWindowAttributes(IntPtr h, uint c, byte a, uint f);
    [DllImport("user32.dll")] public static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dc, ref POINT p, ref SIZE s, IntPtr sdc, ref POINT sp, uint k, ref BLENDFUNCTION b, uint u);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr h, IntPtr ins, int x, int y, int w, int h, uint f);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int s);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern IntPtr SetWindowsHookEx(int id, IntPtr p, IntPtr m, uint t);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr o);
    [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr o);
    [DllImport("kernel32.dll")] public static extern IntPtr LoadLibrary(string n);
    [DllImport("kernel32.dll")] public static extern IntPtr GetProcAddress(IntPtr m, string n);
    public const int WS_EX_LAYERED = 0x80000, WS_EX_TOPMOST = 0x8, WS_EX_TOOLWINDOW = 0x80;
    public const int WS_POPUP = 0x80000000;
    public const uint WM_MOUSEMOVE = 0x0200, WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, WM_DESTROY = 0x0002;
    public const uint WM_APP = 0x8000, WM_APP_TICK = WM_APP + 1, WM_APP_MIAO = WM_APP + 2;
    public const uint ULW_ALPHA = 2; public const byte AC_SRC_OVER = 1, AC_SRC_ALPHA = 1;
    public const int WH_MOUSE_LL = 14;
}
[StructLayout(LayoutKind.Sequential)] struct SIZE { public int w, h; }
[StructLayout(LayoutKind.Sequential)] struct WNDCLASSEX {
    public int cbSize, style; public IntPtr lpfnWndProc, hInstance, hIcon, hCursor, hbrBackground, lpszMenuName, lpszClassName, hIconSm;
}

unsafe class CatForm {
    const int W = 200, H = 265;
    const int WM_HITTEST = 0x0084;
    const int HTCLIENT = 1, HTTRANSPARENT = -1;

    IntPtr hwnd;
    Bitmap cat;          // 猫本体（透明 PNG）
    byte* mask;          // 命中图：该像素算不算“猫身上”
    bool running = true, dragging = false, purring = false;
    int dragX, dragY, ox, oy;
    int frame;           // 0 正常 1 眯眼 2 哈欠 3 睡着
    DateTime lastPet = DateTime.MinValue, lastIdle = DateTime.Now, sleepAt = DateTime.Now.AddSeconds(6);
    Random rnd = new Random();
    Timer t;

    // 喵声：一次合成的波形，播完就停
    byte[] miao = MakeMiao();

    public CatForm() {
        using (var s = new MemoryStream(File.ReadAllBytes("喵喵.png")))
            cat = new Bitmap(Image.FromStream(s));
        mask = MakeMask(cat);
        BuildWindow();
        var area = Screen.PrimaryScreen.WorkingArea;
        ox = area.Right - W - 24; oy = area.Bottom - H - 16;
        Move(ox, oy);
        t = new Timer(Tick, null, 40, 40);
    }

    static byte[] MakeMask(Bitmap b) {
        var bmp = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        int n = bmp.Width * bmp.Height;
        byte[] m = new byte[n];
        byte* p = (byte*)bmp.Scan0;
        for (int i = 0; i < n; i++) m[i] = p[i * 4 + 3] > 60 ? (byte)1 : (byte)0;
        b.UnlockBits(bmp);
        return m;
    }

    // 下滑音调 + 轻微颤，像猫叫；全部内存里算，不依赖任何音频库
    static byte[] MakeMiao() {
        int sr = 22050, len = sr * 7 / 10;          // 0.7 秒
        double f0 = 950, f1 = 520;
        double a = Math.Log(f1 / f0);
        var buf = new short[len];
        for (int i = 0; i < len; i++) {
            double tt = (double)i / len;
            double env = Math.Min(1.0, tt * 8.0) * Math.Pow(1 - tt, 2.2);
            double vib = 1 + 0.012 * Math.Sin(2 * Math.PI * 9 * tt);
            double freq = f0 * Math.Exp(a * tt) * vib;
            double sm = 0.5 * (1 - Math.Cos(2 * Math.PI * freq * i / sr));
            double noise = (rnd2() - 0.5) * 0.35 * env;
            double v = (sm + noise) * env * 0.55;
            if (v > 0.99) v = 0.99;
            buf[i] = (short)(v * short.MaxValue);
        }
        int byteLen = buf.Length * 2;
        using (var ms = new MemoryStream()) {
            var bw = new BinaryWriter(ms);
            bw.Write("RIFF".ToCharArray()); bw.Write(36 + byteLen);
            bw.Write("WAVEfmt ".ToCharArray()); bw.Write(16); bw.Write((short)1); bw.Write((short)1);
            bw.Write(sr); bw.Write(sr * 2); bw.Write((short)2); bw.Write((short)16);
            bw.Write("data".ToCharArray()); bw.Write(byteLen);
            foreach (short s in buf) bw.Write(s);
            return ms.ToArray();
        }
    }
    static readonly Random _r = new Random();
    static double rnd2() { lock (_r) return _r.NextDouble(); }

    void BuildWindow() {
        var wc = new WNDCLASSEX();
        wc.cbSize = Marshal.SizeOf<WNDCLASSEX>();
        wc.hInstance = W.LoadLibrary("user32.dll");
        wc.lpszClassName = "喵喵桌宠";
        IntPtr proc = Marshal.GetFunctionPointerForDelegate<WNDPROC>(WndProc);
        wc.lpfnWndProc = proc;
        W.RegisterClassEx(ref wc);
        hwnd = W.CreateWindowEx(
            W.WS_EX_LAYERED | W.WS_EX_TOPMOST | W.WS_EX_TOOLWINDOW,
            "喵喵桌宠", "", W.WS_POPUP, 0, 0, W, H, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        // 点击穿透由 WM_NCHITTEST 决定：只有落在猫身上才是 HTCLIENT
        W.ShowWindow(hwnd, 1);
        Paint();
    }

    void Move(int x, int y) { ox = x; oy = y; W.SetWindowPos(hwnd, new IntPtr(-1), x, y, W, H, 0x0040); }

    void Paint() {
        var src = cat;
        var dst = new Bitmap(W, H, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(dst)) {
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, new Rectangle(0, 0, W, H), new Rectangle(0, 0, src.Width, src.Height), GraphicsUnit.Pixel);
            DrawFace(g);
        }
        var bmp = dst.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        var hdc = W.CreateCompatibleDC(IntPtr.Zero);
        var old = W.SelectObject(hdc, bmp.Scan0); // 取到位图 bits
        W.SelectObject(hdc, dst.GetHbitmap());    // 用真实 hbitmap 绘制
        dst.UnlockBits(bmp);

        var size = new SIZE { w = W, h = H };
        var pt = new POINT { x = ox, y = oy };
        var spt = new POINT { x = 0, y = 0 };
        var bf = new BLENDFUNCTION { BlendOp = W.AC_SRC_OVER, SourceConstantAlpha = 255, AlphaFormat = W.AC_SRC_ALPHA };
        var hbmp = dst.GetHbitmap();
        var sdc = W.CreateCompatibleDC(IntPtr.Zero);
        W.SelectObject(sdc, hbmp);
        W.UpdateLayeredWindow(hwnd, IntPtr.Zero, ref pt, ref size, sdc, ref spt, 0, ref bf, W.ULW_ALPHA);
        W.DeleteDC(sdc); W.DeleteObject(hbmp); dst.Dispose();
    }

    void DrawFace(Graphics g) {
        var font = new Font(FontFamily.GenericSansSerif, 22, FontStyle.Bold);
        var fmt = new StringFormat { Alignment = StringAlignment.Center };
        if (purring) {
            g.DrawString("♥", font, Brushes.HotPink, new PointF(W / 2 + (float)(Math.Sin(DateTime.Now.Millisecond * 0.02) * 6), -6), fmt);
        } else if (frame == 3) {
            g.DrawString("z", font, Brushes.SkyBlue, new PointF(W - 34, 8), fmt);
            g.DrawString("z", new Font(FontFamily.GenericSansSerif, 15, FontStyle.Bold), Brushes.SkyBlue, new PointF(W - 52, 26), fmt);
        }
    }

    void Tick(object _) {
        if (!running) return;
        bool petNow = IsPetting();
        DateTime now = DateTime.Now;

        if (petNow) {
            lastPet = now; sleepAt = now.AddSeconds(4);
            purring = true;
            frame = 1;                       // 眯眼
            if (!dragging) { int cx = Cursor.Position.X, cy = Cursor.Position.Y; Move(cx - dragX, cy - dragY); }
            Paint();
            return;
        }
        purring = false;

        if ((now - lastPet).TotalSeconds > 2.5) {
            if (now > sleepAt) frame = 3;   // 睡着
            else if (rnd.NextDouble() < 0.012) frame = 2; // 打个哈欠
            else frame = 0;
        }
        Paint();
    }

    bool IsPetting() {
        POINT p; W.GetCursorPos(out p);
        int lx = p.x - ox, ly = p.y - oy;
        if (lx < 0 || ly < 0 || lx >= W || ly >= H) return false;
        int i = ly * W + lx;
        if (i < 0 || i >= W * H) return false;
        return mask[i] != 0;
    }

    IntPtr WndProc(IntPtr h, uint m, IntPtr w, IntPtr l) {
        if (m == WM_NCHITTEST) {
            // 鼠标只在猫身上才被接住，其余全部点穿桌面
            return IsPetting() ? new IntPtr(HTCLIENT) : new IntPtr(HTTRANSPARENT);
        }
        if (m == WM_MOUSEMOVE && dragging) {
            int cx = l.ToInt32() & 0xffff, cy = (l.ToInt32() >> 16) & 0xffff;
            Move(cx - dragX, cy - dragY); return IntPtr.Zero;
        }
        if (m == WM_LBUTTONDOWN) {
            dragging = true; lastPet = DateTime.Now;
            int cx = l.ToInt32() & 0xffff, cy = (l.ToInt32() >> 16) & 0xffff;
            dragX = cx - ox; dragY = cy - oy; Miao(); return IntPtr.Zero;
        }
        if (m == WM_LBUTTONUP) { dragging = false; return IntPtr.Zero; }
        if (m == WM_RBUTTONUP) { ShowMenu(); return IntPtr.Zero; }
        if (m == WM_DESTROY) { running = false; t?.Dispose(); Application.Exit(); return IntPtr.Zero; }
        return W.DefWindowProc(h, m, w, l);
    }

    void ShowMenu() {
        var menu = new ContextMenuStrip();
        menu.Items.Add("开启", null, (_, __) => { running = true; });
        menu.Items.Add("关闭", null, (_, __) => { W.ShowWindow(hwnd, 0); running = false; });
        menu.Items.Add("退出", null, (_, __) => { W.PostMessage(hwnd, W.WM_DESTROY, IntPtr.Zero, IntPtr.Zero); });
        menu.Show(Cursor.Position);
    }

    void Miao() {
        try { new Thread(() => { using (var s = new MemoryStream(miao)) new SoundPlayer(s).PlaySync(); }) { IsBackground = true }.Start(); }
        catch { }
    }

    static int Main() {
        try { Application.Run(new CatForm()); return 0; }
        catch (Exception e) { MessageBox.Show("启动失败：" + e.Message, "喵喵"); return 1; }
    }
    delegate IntPtr WNDPROC(IntPtr h, uint m, IntPtr w, IntPtr l);
}
