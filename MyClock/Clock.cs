using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace MyClock
{
	public partial class Clock : Form
	{
		//
		//  to get per-pixel alpha, adapting MS sample code from:
		//	https://code.msdn.microsoft.com/windowsapps/CSWinFormLayeredWindow-23cdc375/sourcecode?fileId=21775&pathId=551136934
		//

		#region Native Methods and Structures

		const Int32 WS_EX_LAYERED = 0x80000;
		const Int32 WS_EX_TOPMOST = 0x00000008;
		const Int32 HTCAPTION = 0x02;
		const Int32 WM_NCHITTEST = 0x84;
		const Int32 WM_LBUTTONUP = 0x0202;
		const Int32 WM_RBUTTONUP = 0x0205;
		const Int32 WM_NCLBUTTONUP = 0x00A2;
		const Int32 WM_NCRBUTTONUP = 0x00A5;
		const Int32 ULW_ALPHA = 0x02;
		const byte AC_SRC_OVER = 0x00;
		const byte AC_SRC_ALPHA = 0x01;

		[StructLayout(LayoutKind.Sequential)]
		struct NativePoint
		{
			public Int32 x;
			public Int32 y;

			public NativePoint(Int32 x, Int32 y) { this.x = x; this.y = y; }
		}

		[StructLayout(LayoutKind.Sequential)]
		struct NativeSize
		{
			public Int32 cx;
			public Int32 cy;

			public NativeSize(Int32 cx, Int32 cy) { this.cx = cx; this.cy = cy; }
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct ARGB
		{
			public byte Blue;
			public byte Green;
			public byte Red;
			public byte Alpha;
		}

		[StructLayout(LayoutKind.Sequential, Pack = 1)]
		struct BLENDFUNCTION
		{
			public byte BlendOp;
			public byte BlendFlags;
			public byte SourceConstantAlpha;
			public byte AlphaFormat;
		}

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst,
			ref NativePoint pptDst, ref NativeSize psize, IntPtr hdcSrc, ref NativePoint pprSrc,
			Int32 crKey, ref BLENDFUNCTION pblend, Int32 dwFlags);

		[DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern IntPtr CreateCompatibleDC(IntPtr hDC);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern IntPtr GetDC(IntPtr hWnd);

		[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

		[DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

		[DllImport("gdi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool DeleteObject(IntPtr hObject);

		#endregion

		public Clock() {
			InitializeComponent();
            todayToolStripMenuItem.Text = DateTime.Now.ToString("ddd MMM d, yyyy");

            Restore();
			//  someday we may want to have the clock style selectable...
			dot = new Bitmap(MyClock.Properties.Resources.trad_dot);
			clock = new Bitmap(MyClock.Properties.Resources.trad);
			hourHand = new Bitmap(MyClock.Properties.Resources.trad_h);
			minuteHand = new Bitmap(MyClock.Properties.Resources.trad_m);
			this.Size = clock.Size;
			ShowTime();
		}

		private void exitToolStripMenuItem_Click(object sender, EventArgs e) {
			this.Close();
		}

		private void Clock_MouseClick(object sender, MouseEventArgs e) {
			if (e.Button != MouseButtons.Right) return;
			alwaysOnTopToolStripMenuItem.Checked = this.TopMost;
			contextMenuStrip1.Show(this, e.Location);
		}

		Image dot;
		Image clock;
		Image hourHand;
		Image minuteHand;

		const int slop = 2;

		void ShowTime() {
			Point origin = new Point(0, 0);
			int hour = DateTime.Now.Hour;
			int minute = DateTime.Now.Minute;
			//  tetsing:
//			minute = DateTime.Now.Second;
			Point cc = new Point(clock.Size.Width / 2, clock.Size.Height / 2);
			//  hour & minute hands have same dimensions:
			Point center = new Point(hourHand.Size.Width / 2, hourHand.Size.Height / 2);
			Point handsOrigin = new Point(cc.X - center.X - 2 * slop, 0);
//			Rectangle testRect = new Rectangle(origin, (Size)center);
			Bitmap canvas = new Bitmap(this.Size.Width, this.Size.Height, PixelFormat.Format32bppArgb);
			using (Graphics g = Graphics.FromImage(canvas)) {
				Brush background = new SolidBrush(Color.FromArgb(0, 0, 0, 0));
				Rectangle fullBitmap = new Rectangle(origin, this.Size);
				g.FillRectangle(background, fullBitmap);
				g.DrawImage(clock, origin);
				float hourAngle = 360f * hour / 12;
				//  ... and adjust for minutes:
				hourAngle += (360f / 12) * minute / 60;
				g.ResetTransform();
				g.TranslateTransform(cc.X - center.X + slop, center.Y);
				g.RotateTransform(hourAngle);
				g.TranslateTransform(-center.X, -center.Y);
				g.DrawImage(hourHand, origin);
				float minuteAngle = 360f * minute / 60;
				g.ResetTransform();
				g.TranslateTransform(cc.X - center.X + slop, center.Y);
				g.RotateTransform(minuteAngle);
				g.TranslateTransform(-center.X, -center.Y);
				g.DrawImage(minuteHand, origin);
				g.ResetTransform();
				g.DrawImage(dot, handsOrigin);
				SelectBitmap(canvas);
			}
		}

		/// <summary> 
		///  
		/// </summary> 
		/// <param name="bitmap"></param> 
		public void SelectBitmap(Bitmap bitmap) {
			SelectBitmap(bitmap, 255);
		}


		/// <summary> 
		///  
		/// </summary> 
		/// <param name="bitmap"> 
		///  
		/// </param> 
		/// <param name="opacity"> 
		/// Specifies an alpha transparency value to be used on the entire source  
		/// bitmap. The SourceConstantAlpha value is combined with any per-pixel  
		/// alpha values in the source bitmap. The value ranges from 0 to 255. If  
		/// you set SourceConstantAlpha to 0, it is assumed that your image is  
		/// transparent. When you only want to use per-pixel alpha values, set  
		/// the SourceConstantAlpha value to 255 (opaque). 
		/// </param> 
		public void SelectBitmap(Bitmap bitmap, int opacity) {
			// Does this bitmap contain an alpha channel? 
			if (bitmap.PixelFormat != PixelFormat.Format32bppArgb) {
				throw new ApplicationException("The bitmap must be 32bpp with alpha-channel.");
			}

			// Get device contexts 
			IntPtr screenDc = GetDC(IntPtr.Zero);
			IntPtr memDc = CreateCompatibleDC(screenDc);
			IntPtr hBitmap = IntPtr.Zero;
			IntPtr hOldBitmap = IntPtr.Zero;

			try {
				// Get handle to the new bitmap and select it into the current  
				// device context. 
				hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
				hOldBitmap = SelectObject(memDc, hBitmap);

				// Set parameters for layered window update. 
				NativeSize newSize = new NativeSize(bitmap.Width, bitmap.Height);
				NativePoint sourceLocation = new NativePoint(0, 0);
				NativePoint newLocation = new NativePoint(this.Left, this.Top);
				BLENDFUNCTION blend = new BLENDFUNCTION();
				blend.BlendOp = AC_SRC_OVER;
				blend.BlendFlags = 0;
				blend.SourceConstantAlpha = (byte)opacity;
				blend.AlphaFormat = AC_SRC_ALPHA;

				// Update the window. 
				UpdateLayeredWindow(
					this.Handle,     // Handle to the layered window 
					screenDc,        // Handle to the screen DC 
					ref newLocation, // New screen position of the layered window 
					ref newSize,     // New size of the layered window 
					memDc,           // Handle to the layered window surface DC 
					ref sourceLocation, // Location of the layer in the DC 
					0,               // Color key of the layered window 
					ref blend,       // Transparency of the layered window 
					ULW_ALPHA        // Use blend as the blend function 
					);
			}
			finally {
				// Release device context. 
				ReleaseDC(IntPtr.Zero, screenDc);
				if (hBitmap != IntPtr.Zero) {
					SelectObject(memDc, hOldBitmap);
					DeleteObject(hBitmap);
				}
				DeleteDC(memDc);
			}
		} 
 
		private void timer1_Tick(object sender, EventArgs e) {
			ShowTime();
		}

		private void Clock_Paint(object sender, PaintEventArgs e) {
			ShowTime();
		}

		/// <summary> 
		/// Let Windows drag this window for us (thinks its hitting the title  
		/// bar of the window) 
		/// </summary> 
		/// <param name="message"></param> 
		protected override void WndProc(ref Message message) {
			if (message.Msg == WM_NCHITTEST) {
				// Tell Windows that the user is on the title bar (caption) 
				message.Result = (IntPtr)HTCAPTION;
			} else if (message.Msg == WM_LBUTTONUP || message.Msg == WM_RBUTTONUP) {
				//  since we're always saying we're in HTCAPTION this never happens, so:
			} else if (message.Msg == WM_NCLBUTTONUP || message.Msg == WM_NCRBUTTONUP) {
				int x = (int) message.LParam & 0xffff;
				int y = ((int) message.LParam >> 16) & 0xffff;
				//  must treat these as shorts to accomodate possible negative coords:
				Point p = PointToClient(new Point((short) x, (short) y));
				MouseEventArgs e = new MouseEventArgs(MouseButtons.Right, 1, p.X, p.Y, 0);
				Clock_MouseClick(null, e);
			} else {
				base.WndProc(ref message);
			}
		}

		protected override CreateParams CreateParams {
			get {
				// Add the layered extended style (WS_EX_LAYERED) to this window. 
				CreateParams createParams = base.CreateParams;
				createParams.ExStyle |= WS_EX_LAYERED;
				return createParams;
			}
		}

		private void alwaysOnTopToolStripMenuItem_Click(object sender, EventArgs e) {
			bool topmost = this.TopMost;
			this.TopMost = !topmost;
		}

		string registryPath = @"Software\YagyuSoft\Clock";

		private void Clock_FormClosing(object sender, FormClosingEventArgs e) {
			RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath);
			key.SetValue("TopMost", this.TopMost ? 1 : 0);
			key.SetValue("Left", this.Left);
			key.SetValue("Top", this.Top);
		}

		private void Restore() {
			RegistryKey key = Registry.CurrentUser.CreateSubKey(registryPath);
			if (key == null) {
				return;
			}
			this.TopMost = (int) key.GetValue("TopMost", this.TopMost ? 1 : 0) != 0;
			this.Left = (int) key.GetValue("Left", this.Left);
			this.Top = (int) key.GetValue("Top", this.Top);
		} 
 
	}
}
