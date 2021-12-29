﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using CADability.Attribute;
using CADability.GeoObject;
using System.Drawing;
using System.Windows.Forms;
using MathNet.Numerics.LinearAlgebra.Double;


namespace CADability.Forms.OpenGL
{
    /// <summary>
    /// Implementation of <see cref="IPaintTo3D"/> via OpenGL
    /// </summary>
    public class PaintToOpenGL : IPaintTo3D
    {
        #region IPaintTo3D data
        public int debugNumTriangles = 0;
        public Thread MainThread = null;
        public List<IntPtr> ContextsToDelete = new List<IntPtr>();
        bool paintSurfaces;
        bool paintEdges;
        bool paintSurfaceEdges;
        bool selectMode;
        bool delayText;
        bool delayAll;
        bool dontRecalcTriangulation;
        bool isBitmap; // Bitmaps verhalten sich komisch in Bezug auf DisplayListen (zumindest auf meinem Rechner, G.)
        double lineWidthFactor;
        float lineWidthMin, lineWidthMax;
        bool useLineWidth;
        double precision;
        double pixelToWorld;
        int clientwidth, clientheight;
        Stack<State> stateStack = new Stack<State>();
        bool isPerspective;
        Color backgroundColor; // Die Hintergrundfarbe um sicherzustellen, dass nicht mit dieser farbe gezeichnet wird
        Color selectColor;

        OpenGlList currentList;
        IntPtr deviceContext = IntPtr.Zero, renderContext = IntPtr.Zero;
        byte accumBits = 0, colorBits = 32, depthBits = 16;
        
        IntPtr MainRenderContext = IntPtr.Zero;
        IntPtr LastRenderContext = IntPtr.Zero;

        OpenGLResourceManager resManager = new OpenGLResourceManager();


        public GeoVector ProjectionDirection { get; private set; }

        private List<Bitmap> bitmapList = null;
        internal List<Bitmap> BitmapList
        {
            get
            {
                if (bitmapList == null)
                {
                    bitmapList = new List<Bitmap>();
                    Bitmap bmp;
                    bmp = BitmapTable.GetBitmap("PointSymbols.bmp");
                    // PointSymbols.bmp and PointSymbolsB.bmp (B for bold) must have this form:
                    // 6 square pointsymbols horizontally placed with and odd number of pixels.
                    // the upper left pixel is the transparent color
                    Color clr = bmp.GetPixel(0, 0);
                    if (clr.A != 0) bmp.MakeTransparent(clr);
                    int h = bmp.Height;
                    ImageList imageList = new ImageList();
                    imageList.ImageSize = new Size(h, h);
                    imageList.Images.AddStrip(bmp); // the non-bold symbols
                    if (Settings.GlobalSettings.GetBoolValue("PointSymbolsBold", false))
                    {
                        bmp = BitmapTable.GetBitmap("PointSymbolsB.bmp"); // the bold symbols
                        clr = bmp.GetPixel(0, 0);
                        if (clr.A != 0) bmp.MakeTransparent(clr);
                        imageList.Images.AddStrip(bmp);
                    }
                    else
                    {   // again the non bold symbols
                        imageList.Images.AddStrip(bmp);
                    }
                    // full black square for selecting
                    bmp = new Bitmap(h, h);
                    for (int i = 0; i < h; i++)
                    {
                        for (int j = 0; j < h; j++)
                        {
                            bmp.SetPixel(i, j, Color.Black);
                        }
                    }
                    imageList.Images.Add(bmp);
                    for (int i = 0; i < imageList.Images.Count; ++i)
                    {
                        bitmapList.Add(imageList.Images[i] as Bitmap);
                    }
                }
                return bitmapList;
            }
        }

        public PaintToOpenGL(double precision = 1e-6)
        {
            try
            {   // hier gab es noch keinen OpenGL Aufruf, einmal CheckError nullt diesen
                CheckError(true);
            }
            catch { }
            if (MainThread == null) MainThread = Thread.CurrentThread;
            this.precision = precision;
            paintSurfaces = true;
            paintEdges = true;
            paintSurfaceEdges = true;
            currentList = null;
            selectColor = Color.Yellow;
            lineWidthFactor = 10.0;
        }

        public void SetClientSize(Size sz)
        {
            clientwidth = sz.Width;
            clientheight = sz.Height;
        }
        public void Init(IntPtr deviceContext, int width, int height, bool toBitmap)
        {
            if (deviceContext == IntPtr.Zero)
                throw new PaintToOpenGLException("Device Context is Zero");

            this.deviceContext = deviceContext;
            this.isBitmap = toBitmap;

            SetupPixelFormat(deviceContext, toBitmap);

            //Create rendering context
            renderContext = resManager.CreateContext(deviceContext);

            if (!toBitmap)
            {
                if (MainRenderContext == IntPtr.Zero)
                {
                    MainRenderContext = renderContext;
                    LastRenderContext = renderContext;
                }
                else
                {
                    bool ok = Wgl.wglShareLists(LastRenderContext, renderContext);
                    LastRenderContext = renderContext;
                    if (!ok)
                        MainRenderContext = renderContext;
                }
            }

            clientwidth = width;
            clientheight = height;

            //Make this the current context
            (this as IPaintTo3D).MakeCurrent();

            //For debugging only:
            //int dbgscentil;
            //Gl.glGetIntegerv(Gl.GL_STENCIL_BITS, out dbgscentil);
            //Gl.glGetIntegerv(Gl.GL_RED_BITS, out dbgscentil);
            //Gl.glGetIntegerv(Gl.GL_DEPTH_BITS, out dbgscentil);

            Gl.glShadeModel(Gl.GL_SMOOTH);
            CheckError();
        }

        private void SetupPixelFormat(IntPtr deviceContext, bool toBitmap)
        {
            //Setup pixel format
            Gdi.PIXELFORMATDESCRIPTOR pixelFormat = new Gdi.PIXELFORMATDESCRIPTOR();

            pixelFormat.nSize = (short)Marshal.SizeOf(pixelFormat);
            pixelFormat.nVersion = 1;

            if (toBitmap)
                pixelFormat.dwFlags = Gdi.PFD_SUPPORT_OPENGL | Gdi.PFD_DRAW_TO_BITMAP;
            else
                pixelFormat.dwFlags = Gdi.PFD_DRAW_TO_WINDOW | Gdi.PFD_SUPPORT_OPENGL | Gdi.PFD_DOUBLEBUFFER;

            pixelFormat.iPixelType = (byte)Gdi.PFD_TYPE_RGBA;
            pixelFormat.cColorBits = colorBits;
            pixelFormat.cRedBits = 0;
            pixelFormat.cRedShift = 0;
            pixelFormat.cGreenBits = 0;
            pixelFormat.cGreenShift = 0;
            pixelFormat.cBlueBits = 0;
            pixelFormat.cBlueShift = 0;
            pixelFormat.cAlphaBits = 0;
            pixelFormat.cAlphaShift = 0;
            pixelFormat.cAccumBits = accumBits;
            pixelFormat.cAccumRedBits = 0;
            pixelFormat.cAccumGreenBits = 0;
            pixelFormat.cAccumBlueBits = 0;
            pixelFormat.cAccumAlphaBits = 0;
            pixelFormat.cDepthBits = depthBits;
            pixelFormat.cStencilBits = 1; // stencilBits;
            pixelFormat.cAuxBuffers = 0;
            pixelFormat.iLayerType = (byte)Gdi.PFD_MAIN_PLANE;
            pixelFormat.bReserved = 0;
            pixelFormat.dwLayerMask = 0;
            pixelFormat.dwVisibleMask = 0;
            pixelFormat.dwDamageMask = 0;

            //Set pixel format
            int selectedFormat = Gdi.ChoosePixelFormat(deviceContext, ref pixelFormat);

            //Make sure the requested pixel format is available
            if (selectedFormat == 0)
                throw new PaintToOpenGLException("SetupPixelFormat: Unable to find a suitable pixel format");

            if (!Gdi.SetPixelFormat(deviceContext, selectedFormat, ref pixelFormat))
                throw new PaintToOpenGLException(string.Format("SetupPixelFormat: Unable to set the requested pixel format ({0})", selectedFormat));
        }

        // Mit HashCode und Equals hat es folgende Bewandnis:
        // Der HashCode und Equals darf sich nicht ändern, solange das Objekt als Key für ein Dictionary verwendet
        // wird. Deshalb geht der Trick mit paintSurfaces und paintEdges für den HashCode und Equals nicht.
        public override int GetHashCode()
        {
            return renderContext.GetHashCode();
        }
        public override bool Equals(object obj)
        {
            PaintToOpenGL other = obj as PaintToOpenGL;
            if (other == null) return false;
            return renderContext == other.renderContext;
        }

        #endregion
        #region IPaintTo3D implementation
        Dictionary<Bitmap, IPaintTo3DList> icons = new Dictionary<Bitmap, IPaintTo3DList>();
        Dictionary<Bitmap, IPaintTo3DList> bitmaps = new Dictionary<Bitmap, IPaintTo3DList>();
        Dictionary<Bitmap, uint> textures = new Dictionary<Bitmap, uint>();


        bool IPaintTo3D.PaintSurfaces
        {
            get { return paintSurfaces; }
        }
        bool IPaintTo3D.PaintEdges
        {
            get { return paintEdges; }
        }
        bool IPaintTo3D.PaintSurfaceEdges
        {
            get { return paintSurfaceEdges; }
            set { paintSurfaceEdges = value; }
        }
        bool IPaintTo3D.UseLineWidth
        {
            get { return useLineWidth; }
            set { useLineWidth = value; }
        }
        double IPaintTo3D.PixelToWorld
        {
            get
            {
                return pixelToWorld;
            }
        }
        double IPaintTo3D.Precision
        {
            get { return precision; }
            set { precision = value; }
        }
        bool IPaintTo3D.SelectMode
        {
            get { return selectMode; }
            set { selectMode = value; }
        }
        Color IPaintTo3D.SelectColor
        {
            get { return selectColor; }
            set { selectColor = value; }
        }
        bool IPaintTo3D.DelayText
        {
            get { return delayText; }
            set { delayText = value; }
        }
        bool IPaintTo3D.DelayAll
        {
            get { return delayAll; }
            set { delayAll = value; }
        }
        bool IPaintTo3D.TriangulateText
        {
            get { return true; }
        }
        bool IPaintTo3D.DontRecalcTriangulation
        {
            get
            {
                return dontRecalcTriangulation;
            }
            set
            {
                dontRecalcTriangulation = value;
            }
        }
        PaintCapabilities IPaintTo3D.Capabilities
        {
            get
            {
                return PaintCapabilities.Standard | PaintCapabilities.ZoomIndependentDisplayList;
            }
        }

        internal void Init(Control ctrl)
        {
            IntPtr deviceContext = resManager.ConnectToControl(ctrl);
            Init(deviceContext, ctrl.ClientSize.Width, ctrl.ClientSize.Height, false);

            //Find the owner windows of this control
            Form controlOwner = ctrl.FindForm();
            if (controlOwner is null)
                throw new PaintToOpenGLException("Unable to find owner of control");

            //Attach to the FormClosed Event which will occure if the owner window is destroyed
            //and OpenGL needs to be shut down for this window.
            //This event should be always synchronous as it is called from the UI Thread - unlike Disposed
            controlOwner.FormClosed += ControlOwner_FormClosed;

            Gl.glPixelStorei(Gl.GL_UNPACK_ALIGNMENT, 1);
            Gl.glPixelStorei(Gl.GL_PACK_ALIGNMENT, 1);

            CheckError();
        }

        private void ControlOwner_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (resManager != null)
            {
                resManager.Dispose();
                resManager = null;
            }
        }

        internal void Disconnect(Control ctrl)
        {
            if (ctrl != null)
            {
                if (renderContext != IntPtr.Zero && renderContext != MainRenderContext)
                {
                    renderContext = IntPtr.Zero;
                }
            }
        }

        private void CheckError(bool dontDebug = false)
        {
            int error = Gl.glGetError();
            if (error == 0) return;

            throw new PaintToOpenGLException("Error in OpenGl (" + error.ToString("X") + ")");
        }
        void IPaintTo3D.Dispose()
        {
            if (renderContext != IntPtr.Zero && renderContext != MainRenderContext)
            {
                ContextsToDelete.Add(renderContext);
                renderContext = IntPtr.Zero;
            }
            CheckError();
        }
        void IPaintTo3D.PushState()
        {
            State s;
            s.blending = Gl.glIsEnabled(Gl.GL_BLEND) != 0;
            s.useZBuffer = Gl.glIsEnabled(Gl.GL_DEPTH_TEST) != 0;
            stateStack.Push(s);
        }
        void IPaintTo3D.PopState()
        {
            State s = stateStack.Pop();
            if (s.blending) Gl.glEnable(Gl.GL_BLEND);
            else Gl.glDisable(Gl.GL_BLEND);
            if (s.useZBuffer) Gl.glEnable(Gl.GL_DEPTH_TEST);
            else Gl.glDisable(Gl.GL_DEPTH_TEST);
        }
        void IPaintTo3D.MakeCurrent()
        {
            //if (!Wgl.wglMakeCurrent(deviceContext, renderContext))
            //{
            //    throw new PaintToOpenGLException("MakeCurrentContext: Unable to active this control's OpenGL rendering context");
            //}
            if (Wgl.wglMakeCurrent(deviceContext, renderContext))
            {
                // System.Diagnostics.Trace.WriteLine("MakeCurrent: " + deviceContext.ToInt32());
            }
            else
            {
                //System.Diagnostics.Trace.WriteLine("MakeCurrent failed: " + deviceContext.ToInt32());
            }
            // CheckError(); ist ja kein OpenGl Befehl
        }
        void IPaintTo3D.Resize(int width, int height)
        {
            if (Wgl.wglGetCurrentContext() != renderContext) (this as IPaintTo3D).MakeCurrent();
            clientwidth = width;
            clientheight = height;
            CheckError();
        }
        void IPaintTo3D.Clear(Color background)
        {
            if (Wgl.wglGetCurrentContext() != renderContext) (this as IPaintTo3D).MakeCurrent();
            backgroundColor = background;
            Gl.glViewport(0, 0, clientwidth, clientheight);
            Gl.glClearColor(background.R / 255.0f, background.G / 255.0f, background.B / 255.0f, 1.0f);
            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);
            CheckError();
        }
        void IPaintTo3D.AvoidColor(Color color)
        {
            backgroundColor = color;
        }
        void IPaintTo3D.SetProjection(Projection projection, BoundingCube boundingCube)
        {
            // System.Diagnostics.Trace.WriteLine("SetProjection: " + boundingCube.ToString());
            if (Wgl.wglGetCurrentContext() != renderContext) (this as IPaintTo3D).MakeCurrent();
            Gl.glViewport(0, 0, clientwidth, clientheight);
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glLoadIdentity();
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Gl.glLoadIdentity();
            double[,] mm;
            mm = projection.GetOpenGLProjection(0, clientwidth, 0, clientheight, boundingCube);
            double[] pmat = new double[16];
            // ACHTUNG: Matrix ist vertauscht!!!
            pmat[0] = mm[0, 0];
            pmat[1] = mm[1, 0];
            pmat[2] = mm[2, 0];
            pmat[3] = mm[3, 0];
            pmat[4] = mm[0, 1];
            pmat[5] = mm[1, 1];
            pmat[6] = mm[2, 1];
            pmat[7] = mm[3, 1];
            pmat[8] = mm[0, 2];
            pmat[9] = mm[1, 2];
            pmat[10] = mm[2, 2];
            pmat[11] = mm[3, 2];
            pmat[12] = mm[0, 3];
            pmat[13] = mm[1, 3];
            pmat[14] = mm[2, 3];
            pmat[15] = mm[3, 3];

            GeoVector v = projection.Direction;
            ProjectionDirection = projection.Direction;
            isPerspective = projection.IsPerspective;
            v = projection.InverseProjection * new GeoVector(0.5, 0.3, -1.0);
            pixelToWorld = projection.DeviceToWorldFactor;
            Gl.glEnable(Gl.GL_DEPTH_TEST); // das verdeckt oder nicht
                                           //Gl.glLightModelfv(Gl.GL_LIGHT_MODEL_AMBIENT, new float[] { 0.2f, 0.2f, 0.2f, 1.0f });
            CheckError();
            Gl.glLightModeli(Gl.GL_LIGHT_MODEL_TWO_SIDE, Gl.GL_TRUE);
            CheckError();
            Gl.glEnable(Gl.GL_LIGHTING);
            //Gl.glEnable(Gl.GL_LIGHT_MODEL_AMBIENT);
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, new float[] { (float)v.x, (float)v.y, (float)v.z, 0.0f }); // letzte Komponente 0: richtungslicht!
                                                                                                                  // Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_POSITION, new float[] { 0.1f, 0.1f, 1.0f, 0.0f }); // letzte Komponente 0: richtungslicht!
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_AMBIENT, new float[] { 0.2f, 0.2f, 0.2f, 1.0f });
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_DIFFUSE, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            Gl.glLightfv(Gl.GL_LIGHT0, Gl.GL_SPECULAR, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });

            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_POSITION, new float[] { -1.0f, 1.0f, 1.0f, 0.0f });
            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_AMBIENT, new float[] { 0.3f, 0.3f, 0.3f, 1.0f });
            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_DIFFUSE, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_SPECULAR, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            //Gl.glLightfv(Gl.GL_LIGHT1, Gl.GL_SPOT_DIRECTION, new float[] { 0.0f, 0.0f, -1.0f });
            Gl.glEnable(Gl.GL_LIGHT0);
            //Gl.glEnable(Gl.GL_LIGHT1);
            Gl.glLoadMatrixd(pmat);

            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glLoadIdentity();
            Gl.glEnable(Gl.GL_AUTO_NORMAL);
            Gl.glEnable(Gl.GL_NORMALIZE);
            Gl.glBlendFunc(Gl.GL_ONE, Gl.GL_ZERO);
            Gl.glColorMaterial(Gl.GL_FRONT_AND_BACK, Gl.GL_AMBIENT_AND_DIFFUSE);
            Gl.glEnable(Gl.GL_COLOR_MATERIAL);
            useLineWidth = projection.UseLineWidth;
            if (projection.UseLineWidth) Gl.glEnable(Gl.GL_LINE_SMOOTH);
            else Gl.glDisable(Gl.GL_LINE_SMOOTH);
            float[] lwminmax = new float[2];
            Gl.glGetFloatv(Gl.GL_LINE_WIDTH_RANGE, lwminmax);
            lineWidthMin = lwminmax[0];
            lineWidthMax = lwminmax[1];
            Gl.glLineWidth(1.0f);
            CheckError();

            // OpenGlCustomize.InvokeSetProjection(this.renderContext, this, projection, boundingCube);
            // DEBUG: (dieser test diente zum Feststellen, wieviele Listen der OpenGL Treiber zulässt
            // zuerst waren das auf meinem rechner nur ca 9000 (opengl32.dll Version 5.x), nach Installation von
            // ATI Catalyst Controlcenter machen auch 1000000 kein Problem mehr
            //IPaintTo3D p3d = this;
            //List<IPaintTo3DList> list = new List<IPaintTo3DList>();
            //for (int i = 0; i < 1000000; ++i)
            //{
            //    p3d.OpenList();
            //    p3d.Polyline(new GeoPoint[] { GeoPoint.Origin, GeoPoint.Origin + GeoVector.XAxis, GeoPoint.Origin + GeoVector.YAxis });
            //    list.Add(p3d.CloseList());
            //}
        }
        void IPaintTo3D.SetColor(Color color)
        {
            if (color.R == backgroundColor.R && color.G == backgroundColor.G && color.B == backgroundColor.B)
            {
                if (color.R + color.G + color.B < 3 * 128)
                    Gl.glColor4ub(255, 255, 255, color.A);
                else
                    Gl.glColor4ub(0, 0, 0, color.A);
            }
            else
            {
                Gl.glColor4ub(color.R, color.G, color.B, color.A);
            }
            CheckError();
        }
        void IPaintTo3D.SetLineWidth(LineWidth lineWidth)
        {
            if (!useLineWidth) return;
            if (lineWidth == null || lineWidth.Width == 0.0)
            {
                Gl.glDisable(Gl.GL_LINE_SMOOTH);
                Gl.glLineWidth(1.0f);
            }
            else
            {
                Gl.glEnable(Gl.GL_LINE_SMOOTH);
                float gllineWidth = (float)(lineWidth.Width * lineWidthFactor);
                if (gllineWidth < lineWidthMin)
                {
                    Gl.glLineWidth(lineWidthMin);
                }
                else if (gllineWidth > lineWidthMax)
                {
                    Gl.glLineWidth(lineWidthMax);
                }
                else
                {
                    Gl.glLineWidth((float)gllineWidth);
                }
            }
            CheckError();
        }
        void IPaintTo3D.SetLinePattern(LinePattern pattern)
        {
            bool badPattern = false;
            if (pattern != null && pattern.Pattern.Length > 0)
            {
                badPattern = true;
                for (int i = 0; i < pattern.Pattern.Length; i++)
                {
                    if (pattern.Pattern[i] != 0.0)
                    {
                        badPattern = false;
                        break;
                    }
                }
            }
            if (pattern == null || pattern.Pattern.Length == 0 || pattern.Pattern.Length > 8 || badPattern)
            {
                Gl.glDisable(Gl.GL_LINE_STIPPLE);
            }
            else
            {   // einfache nicht gecachete Patternberechnung
                int[] pat = new int[pattern.Pattern.Length];
                double sum = 0.0;
                for (int i = 0; i < pattern.Pattern.Length; ++i)
                {
                    sum += pattern.Pattern[i];
                }
                double f = 16.0 / sum;
                int isum = 0;
                for (int i = 0; i < pattern.Pattern.Length; ++i)
                {
                    pat[i] = (int)Math.Round(pattern.Pattern[i] * f);
                    if (pat[i] == 0) pat[i] = 1;
                    isum += pat[i];
                }
                while (isum > 16)
                {
                    for (int i = 0; i < pat.Length; ++i)
                    {
                        if (pat[i] > 2)
                        {
                            --pat[i];
                            --isum;
                            if (isum <= 16) break;
                        }
                    }
                }
                while (isum < 16)
                {
                    for (int i = 0; i < pat.Length; ++i)
                    {
                        ++pat[i];
                        ++isum;
                        if (isum >= 16) break;
                    }
                }
                int ppp = 0;
                for (int i = 0; i < pat.Length; ++i)
                {
                    for (int j = 0; j < pat[i]; ++j)
                    {
                        ppp = ppp << 1;
                        if ((i & 1) == 0) ++ppp;
                    }
                }
                Gl.glLineStipple(1, (short)ppp);
                Gl.glEnable(Gl.GL_LINE_STIPPLE);            // wird auch mit null aufgerufen
            }
            CheckError();
        }
        void IPaintTo3D.Polyline(GeoPoint[] points)
        {
            if (currentList != null) currentList.SetHasContents();
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glBegin(Gl.GL_LINE_STRIP);
            for (int i = 0; i < points.Length; ++i)
            {
                Gl.glVertex3d(points[i].x, points[i].y, points[i].z);
            }
            Gl.glEnd();
            CheckError();
        }
        void IPaintTo3D.FilledPolyline(GeoPoint[] points)
        {
            if (currentList != null) currentList.SetHasContents();
            Gl.glEnable(Gl.GL_LIGHTING);
            Gl.glBegin(Gl.GL_POLYGON);
            for (int i = 0; i < points.Length; ++i)
            {
                Gl.glVertex3d(points[i].x, points[i].y, points[i].z);
            }
            Gl.glEnd();
            CheckError();
        }
        void IPaintTo3D.Points(GeoPoint[] points, float size, PointSymbol pointSymbol)
        {
            if (pointSymbol == PointSymbol.Dot)
            {
                if (currentList != null) currentList.SetHasContents();
                Gl.glDisable(Gl.GL_LIGHTING);
                Gl.glBegin(Gl.GL_POINTS);
                for (int i = 0; i < points.Length; ++i)
                {
                    Gl.glVertex3d(points[i].x, points[i].y, points[i].z);
                }
                Gl.glEnd();
            }
            else
            {
                Bitmap bmp = null;
                if ((pointSymbol & CADability.GeoObject.PointSymbol.Select) != 0)
                {
                    bmp = BitmapList[12];
                    if (bmp != null)
                    {
                        for (int i = 0; i < points.Length; ++i) (this as IPaintTo3D).DisplayIcon(points[i], bmp);
                    }
                    return; // nur das volle Quadrat anzeigen, sonst nichts
                }

                int offset = 0;
                if ((this as IPaintTo3D).UseLineWidth) offset = 6; // so wird gesteuert dass bei nur dünn die dünnen Punkte und bei
                                                                   // mit Linienstärke ggf. die dicken Punkte angezeigt werden (Forderung PFOCAD)
                switch ((PointSymbol)((int)pointSymbol & 0x07))
                {
                    case CADability.GeoObject.PointSymbol.Empty:
                        bmp = null;
                        break;
                    case CADability.GeoObject.PointSymbol.Dot:
                        {
                            bmp = BitmapList[0 + offset];
                        }
                        break;
                    case CADability.GeoObject.PointSymbol.Plus:
                        {
                            bmp = BitmapList[1 + offset];
                        }
                        break;
                    case CADability.GeoObject.PointSymbol.Cross:
                        {
                            bmp = BitmapList[2 + offset];
                        }
                        break;
                    case CADability.GeoObject.PointSymbol.Line:
                        {
                            bmp = BitmapList[3 + offset];
                        }
                        break;
                }
                if (bmp != null)
                {
                    for (int i = 0; i < points.Length; ++i) (this as IPaintTo3D).DisplayIcon(points[i], bmp);
                }
                bmp = null;
                if ((pointSymbol & CADability.GeoObject.PointSymbol.Circle) != 0)
                {
                    bmp = BitmapList[5 + offset];
                }
                if ((pointSymbol & CADability.GeoObject.PointSymbol.Square) != 0)
                {
                    bmp = BitmapList[4 + offset];
                }
                if (bmp != null)
                {
                    for (int i = 0; i < points.Length; ++i) (this as IPaintTo3D).DisplayIcon(points[i], bmp);
                }

            }
            CheckError();
        }
        void IPaintTo3D.Triangle(GeoPoint[] vertex, GeoVector[] normals, int[] indextriples)
        {
            debugNumTriangles += indextriples.Length / 3;
            if (currentList != null) currentList.SetHasContents();
            Gl.glEnable(Gl.GL_LIGHTING);
            float[] mat_ambient = { 0.5f, 0.5f, 0.5f, 1.0f };
            float[] mat_specular = { 1.0f, 1.0f, 1.0f, 1.0f };
            float[] low_shininess = { 5.0f };
            Gl.glMaterialfv(Gl.GL_FRONT_AND_BACK, Gl.GL_AMBIENT_AND_DIFFUSE, mat_ambient);
            Gl.glMaterialfv(Gl.GL_FRONT_AND_BACK, Gl.GL_SPECULAR, mat_specular);
            Gl.glMaterialfv(Gl.GL_FRONT_AND_BACK, Gl.GL_SHININESS, low_shininess);

            //(this as IPaintTo3D).PushState();
            //Gl.glEnable(Gl.GL_BLEND);
            //Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);

            Gl.glBegin(Gl.GL_TRIANGLES);
            for (int i = 0; i < indextriples.Length; i += 3)
            {
                // die Reihenfolge der drei Punkte und die Normalenvektoren müssen zusammenpassen,
                // warum ist unklar, aber so gehts:
                GeoPoint v1 = vertex[indextriples[i]];
                GeoPoint v2 = vertex[indextriples[i + 1]];
                GeoPoint v3 = vertex[indextriples[i + 2]];
                GeoVector n0 = (v1 - v2) ^ (v3 - v2);
                GeoVector n1 = normals[indextriples[i]];
                GeoVector n2 = normals[indextriples[i + 1]];
                GeoVector n3 = normals[indextriples[i + 2]];
                if (n0 * n1 < 0)
                {
                    Gl.glNormal3d(n1.x, n1.y, n1.z);
                    Gl.glVertex3d(v1.x, v1.y, v1.z);
                    Gl.glNormal3d(n3.x, n3.y, n3.z);
                    Gl.glVertex3d(v3.x, v3.y, v3.z);
                    Gl.glNormal3d(n2.x, n2.y, n2.z);
                    Gl.glVertex3d(v2.x, v2.y, v2.z);
                }
                else
                {
                    Gl.glNormal3d(n1.x, n1.y, n1.z);
                    Gl.glVertex3d(v1.x, v1.y, v1.z);
                    Gl.glNormal3d(n2.x, n2.y, n2.z);
                    Gl.glVertex3d(v2.x, v2.y, v2.z);
                    Gl.glNormal3d(n3.x, n3.y, n3.z);
                    Gl.glVertex3d(v3.x, v3.y, v3.z);
                }
            }
            Gl.glEnd();
            //(this as IPaintTo3D).PopState();
            CheckError();
        }
        void IPaintTo3D.PrepareIcon(Bitmap icon)
        {   // Für ein Icon wird eine kleine DisplayList gemacht, in der großen stürzt es oft ab
            if (currentList != null) throw new ApplicationException("PrepareIcon called with display list open");
            if (!icons.ContainsKey(icon))
            {
                int r = (icon.Width + 7) / 8; // Anzahl der Bytes pro Zeile
                int n = r * icon.Height; // Anzahl der Bytes
                byte[] oglbitmap = new byte[n]; // stackalloc entzieht das Array der Garbage Collection
                                                // Bitmap in ein byte array umwandeln, nur alpha berücksichtigen
                for (int i = 0; i < icon.Height; ++i)
                {
                    for (int j = 0; j < icon.Width; ++j)
                    {
                        Color clr = icon.GetPixel(j, i);
                        if (clr.A != 0)
                        {
                            int index = i * r + j / 8;
                            byte toAdd = (byte)(0x80 >> (j % 8));
                            oglbitmap[index] |= toAdd;
                        }
                    }
                }
                (this as IPaintTo3D).OpenList("icon"); // zwischen open und close keine GarbageCollection sonst stimmt die Adresse von oglbitmap nicht mehr
                currentList.hasContents = true;
                Gl.glRasterPos3d(0.0, 0.0, 0.0);
                Gl.glPixelStorei(Gl.GL_PACK_SWAP_BYTES, 0);
                Gl.glPixelStorei(Gl.GL_PACK_LSB_FIRST, 0);
                Gl.glPixelStorei(Gl.GL_PACK_ROW_LENGTH, 0);
                Gl.glPixelStorei(Gl.GL_PACK_SKIP_ROWS, 0);
                Gl.glPixelStorei(Gl.GL_PACK_SKIP_PIXELS, 0);
                Gl.glPixelStorei(Gl.GL_PACK_ALIGNMENT, 1);
                Gl.glPixelStorei(Gl.GL_UNPACK_SWAP_BYTES, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_LSB_FIRST, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_ROW_LENGTH, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_SKIP_ROWS, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_SKIP_PIXELS, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_ALIGNMENT, 1);
                Gl.glBitmap(icon.Width, icon.Height, icon.Width / 2, icon.Height / 2, 0.0f, 0.0f, oglbitmap);
                // vermutlich merkt sich Gl.glBitmap nur die Adresse und CloseList holt sich dann den Inhalt
                // dazwischen darf keine GarbageCollection laufen, was hier vermutlich mit stackalloc ausgeschlossen ist
                icons[icon] = (this as IPaintTo3D).CloseList();
            }
        }
        void IPaintTo3D.PrepareBitmap(Bitmap bitmap, int xoffset, int yoffset)
        {
            if (currentList != null) throw new ApplicationException("PrepareBitmap called with display list open");
            if (!bitmaps.ContainsKey(bitmap))
            {
                int[] pixels = new int[bitmap.Width * bitmap.Height];
                for (int i = 0; i < bitmap.Height; ++i)
                {
                    for (int j = 0; j < bitmap.Width; ++j)
                    {
                        Color clr = bitmap.GetPixel(j, bitmap.Height - i - 1); // auf den Kopf stellen, Bitmap y geht nach unten
                        pixels[i * bitmap.Width + j] = (clr.R << 24) + (clr.G << 16) + (clr.B << 8) + clr.A;
                        // pixels[i * bitmap.Width + j] = (clr.R << 16) + (clr.G << 8) + clr.B;
                    }
                }
                (this as IPaintTo3D).OpenList("bitmap"); // zwischen open und close keine GarbageCollection sonst stimmt die Adresse von oglbitmap nicht mehr
                currentList.hasContents = true;
                Gl.glRasterPos3d(0.0, 0.0, 0.0);
                // ich finde keine Möglichkeit die Position auf z.B. die Mitte oder einen beliebigen Punkt des
                // Bitmaps zu setzen. Außer: In Notes zu glBitmap steht:
                // Notes
                // To set a valid raster position outside the viewport, first set a valid raster position inside the viewport, 
                // then call glBitmap with NULL as the bitmap parameter and with xmove and ymove set to the offsets of the 
                // new raster position. This technique is useful when panning an image around the viewport. 
                Gl.glBitmap(0, 0, 0, 0, -xoffset, -yoffset, null);
                Gl.glPixelStorei(Gl.GL_PACK_SWAP_BYTES, 0);
                Gl.glPixelStorei(Gl.GL_PACK_LSB_FIRST, 0);
                Gl.glPixelStorei(Gl.GL_PACK_ROW_LENGTH, 0);
                Gl.glPixelStorei(Gl.GL_PACK_SKIP_ROWS, 0);
                Gl.glPixelStorei(Gl.GL_PACK_SKIP_PIXELS, 0);
                Gl.glPixelStorei(Gl.GL_PACK_ALIGNMENT, 4);
                Gl.glPixelStorei(Gl.GL_UNPACK_SWAP_BYTES, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_LSB_FIRST, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_ROW_LENGTH, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_SKIP_ROWS, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_SKIP_PIXELS, 0);
                Gl.glPixelStorei(Gl.GL_UNPACK_ALIGNMENT, 4);
                Gl.glDrawPixels(bitmap.Width, bitmap.Height, Gl.GL_RGBA, Gl.GL_UNSIGNED_INT_8_8_8_8, pixels);
                // Gl.glDrawPixels(bitmap.Width, bitmap.Height, Gl.GL_RGB, Gl.GL_UNSIGNED_INT, pixels);
                bitmaps[bitmap] = (this as IPaintTo3D).CloseList();
                int maxTextureSize;
                Gl.glGetIntegerv(Gl.GL_MAX_TEXTURE_SIZE, out maxTextureSize);
            }
        }
        void IPaintTo3D.PrepareText(string fontName, string textString, FontStyle fontStyle)
        {
            // siehe http://www.opengl.org/resources/features/fontsurvey/ für andere Font Methoden
            // dieses hier ist zu Resourcenträchtig
            FontDisplayList fdl = resManager.GetFontDisplayList(fontName, deviceContext);
            for (int i = 0; i < textString.Length; ++i)
            {
                fdl.AssertCharacter(textString[i]);
            }
            CheckError();
        }
        int dbgNumChar;
        void IPaintTo3D.PreparePointSymbol(PointSymbol symbol)
        {
            int offset = 0;
            if ((this as IPaintTo3D).UseLineWidth) offset = 6; // so wird gesteuert dass bei nur dünn die dünnen Punkte und bei
            // mit Linienstärke ggf. die dicken Punkte angezeigt werden (Forderung PFOCAD)
            Bitmap bmp = null;
            switch ((PointSymbol)((int)symbol & 0x07))
            {
                case CADability.GeoObject.PointSymbol.Empty:
                    bmp = null;
                    break;
                case CADability.GeoObject.PointSymbol.Dot:
                    {
                        bmp = BitmapList[0 + offset];
                    }
                    break;
                case CADability.GeoObject.PointSymbol.Plus:
                    {
                        bmp = BitmapList[1 + offset];
                    }
                    break;
                case CADability.GeoObject.PointSymbol.Cross:
                    {
                        bmp = BitmapList[2 + offset];
                    }
                    break;
                case CADability.GeoObject.PointSymbol.Line:
                    {
                        bmp = BitmapList[3 + offset];
                    }
                    break;
            }
            if (bmp != null)
            {
                (this as IPaintTo3D).PrepareIcon(bmp);
            }
            bmp = null;
            if ((symbol & CADability.GeoObject.PointSymbol.Circle) != 0)
            {
                bmp = BitmapList[5 + offset];
            }
            if ((symbol & CADability.GeoObject.PointSymbol.Square) != 0)
            {
                bmp = BitmapList[4 + offset];
            }
            if ((symbol & CADability.GeoObject.PointSymbol.Select) != 0)
            {
                bmp = BitmapList[12];
            }
            if (bmp != null)
            {
                (this as IPaintTo3D).PrepareIcon(bmp);
            }
        }
        void IPaintTo3D.PrepareBitmap(Bitmap bitmap)
        {   // Mechanismus zum Entfernen aus dem Dictionary und vor allem aus OpenGL fehlt noch.
            // man bräuchte eine Art OnDispose vom Bitmap, aber das gibt es nicht...
            if (!textures.ContainsKey(bitmap))
            {
                Gl.glPixelStorei(Gl.GL_UNPACK_ALIGNMENT, 1);
                Gl.glPixelStorei(Gl.GL_PACK_ALIGNMENT, 1);
                uint texName; //  = new uint[1];
                Gl.glGenTextures(1, out texName);
                Gl.glBindTexture(Gl.GL_TEXTURE_2D, texName);
                Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_S, Gl.GL_REPEAT);
                Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_WRAP_T, Gl.GL_REPEAT);
                Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MAG_FILTER, Gl.GL_NEAREST);
                Gl.glTexParameteri(Gl.GL_TEXTURE_2D, Gl.GL_TEXTURE_MIN_FILTER, Gl.GL_NEAREST);
                // int[] pixels = new int[bitmap.Width * bitmap.Height];
#if DEBUG
                //System.Diagnostics.Trace.WriteLine("Texture: " + texName.ToString());
#endif
                byte[] pixels;
                bool alpha = Settings.GlobalSettings.GetBoolValue("OpenGLAlpha", true);
                if (alpha)
                {
                    pixels = new byte[bitmap.Width * bitmap.Height * 4];
                    for (int i = 0; i < bitmap.Height; ++i)
                    {
                        for (int j = 0; j < bitmap.Width; ++j)
                        {
                            Color clr = bitmap.GetPixel(j, bitmap.Height - i - 1); // auf den Kopf stellen, Bitmap y geht nach unten
                                                                                   // pixels[i * bitmap.Width + j] = (clr.R << 24) + (clr.G << 16) + (clr.B << 8) + clr.A;
                            pixels[(i * bitmap.Width + j) * 4 + 0] = clr.R;
                            pixels[(i * bitmap.Width + j) * 4 + 1] = clr.G;
                            pixels[(i * bitmap.Width + j) * 4 + 2] = clr.B;
                            pixels[(i * bitmap.Width + j) * 4 + 3] = clr.A;
                        }
                    }
                    Gl.glEnable(Gl.GL_ALPHA_TEST); // damit der Alpha Kanal berücksichtigt wird
                    Gl.glAlphaFunc(Gl.GL_GREATER, 0.5f); // Sollte eigentlich immer so eingestellt sein
                                                         // Gl.glTexImage2D(Gl.GL_TEXTURE_2D, 0, Gl.GL_RGBA, bitmap.Width, bitmap.Height, 0, Gl.GL_RGBA, Gl.GL_UNSIGNED_INT_8_8_8_8, pixels);
                    Gl.glTexImage2D(Gl.GL_TEXTURE_2D, 0, Gl.GL_RGBA, bitmap.Width, bitmap.Height, 0, Gl.GL_RGBA, Gl.GL_UNSIGNED_BYTE, pixels);
                }
                else
                {

                    pixels = new byte[bitmap.Width * bitmap.Height * 3];
                    for (int i = 0; i < bitmap.Height; ++i)
                    {
                        for (int j = 0; j < bitmap.Width; ++j)
                        {
                            Color clr = bitmap.GetPixel(j, bitmap.Height - i - 1); // auf den Kopf stellen, Bitmap y geht nach unten
                            pixels[(i * bitmap.Width + j) * 3 + 0] = clr.R;
                            pixels[(i * bitmap.Width + j) * 3 + 1] = clr.G;
                            pixels[(i * bitmap.Width + j) * 3 + 2] = clr.B;
                        }
                    }
                    Gl.glTexImage2D(Gl.GL_TEXTURE_2D, 0, Gl.GL_RGB, bitmap.Width, bitmap.Height, 0, Gl.GL_RGB, Gl.GL_UNSIGNED_BYTE, pixels);
                }
                textures[bitmap] = texName;
                CheckError();
            }
        }
        void IPaintTo3D.RectangularBitmap(Bitmap bitmap, GeoPoint location, GeoVector directionWidth, GeoVector directionHeight)
        {
            uint texName;
            if (textures.TryGetValue(bitmap, out texName))
            {
                if (currentList != null) currentList.SetHasContents();
                // Gl.glEnable(Gl.GL_LIGHTING);
                Gl.glEnable(Gl.GL_TEXTURE_2D);
                Gl.glTexEnvf(Gl.GL_TEXTURE_ENV, Gl.GL_TEXTURE_ENV_MODE, Gl.GL_REPLACE);
                Gl.glColor4ub(255, 255, 255, 255);
                // Gl.glHint(Gl.GL_PERSPECTIVE_CORRECTION_HINT, Gl.GL_NICEST);
                Gl.glBindTexture(Gl.GL_TEXTURE_2D, texName);
                bool blend = Gl.glIsEnabled(Gl.GL_BLEND) != 0;
                Gl.glEnable(Gl.GL_BLEND); // damit der Alpha Kanal berücksichtigt wird
                Gl.glEnable(Gl.GL_ALPHA_TEST); // damit der Alpha Kanal berücksichtigt wird
                Gl.glAlphaFunc(Gl.GL_GREATER, 0.5f); // Sollte eigentlich immer so eingestellt sein
                GeoPoint p0 = location;
                GeoPoint p1 = location + directionWidth;
                GeoPoint p2 = location + directionWidth + directionHeight;
                GeoPoint p3 = location + directionHeight;
                Gl.glBegin(Gl.GL_QUADS);
                Gl.glTexCoord2d(0.0, 0.0); Gl.glVertex3d(p0.x, p0.y, p0.z);
                Gl.glTexCoord2d(0.0, 1.0); Gl.glVertex3d(p3.x, p3.y, p3.z);
                Gl.glTexCoord2d(1.0, 1.0); Gl.glVertex3d(p2.x, p2.y, p2.z);
                Gl.glTexCoord2d(1.0, 0.0); Gl.glVertex3d(p1.x, p1.y, p1.z);
                Gl.glEnd();
                Gl.glDisable(Gl.GL_TEXTURE_2D);
                Gl.glDisable(Gl.GL_ALPHA_TEST); // eingeführt wg. Hintergrund in PFOCad
                if (!blend) Gl.glDisable(Gl.GL_BLEND); // wieder zurückstellen
                CheckError();
            }
        }
        void IPaintTo3D.Text(GeoVector lineDirection, GeoVector glyphDirection, GeoPoint location, string fontName, string textString, FontStyle fontStyle, Text.AlignMode alignment, Text.LineAlignMode lineAlignment)
        {
            if (currentList != null) currentList.SetHasContents();
            if (textString.Length == 0) return;
            GeoVector normal = lineDirection ^ glyphDirection;
            FontDisplayList fdl = resManager.GetFontDisplayList(fontName, deviceContext);
            if (alignment != GeoObject.Text.AlignMode.Baseline || lineAlignment != GeoObject.Text.LineAlignMode.Left)
            {
                // hier location modifizieren gemäß alignment
                float dx = 0.0f;
                float dy = 0.0f;
                float yoffset = 0.0f;
                // es schein so zu sein: blackbox ist die Größe des Zeichens, also nur der Geometrie, unabhängig von der Lage
                // gm.gmfptGlyphOrigin.Y is die Oberkante, wenn also blackbox>gm.gmfptGlyphOrigin.Y, dann geht es unter die Linie
                // platziert wird immer auf der Basislinie. Wieviel maximal unter der Basislinie und über der Basislinie
                // werden kann, steht nirgends geschrieben, das gibt es nur für die einzelnen Zeichen
                // wenn man also unten oder oben platzieren will, dann ist man auf Schätzungen angewiesen
                // der Zeilenabstand scheint 1 zu sein, als machen wir hier grob: +0.2, wenn Bottom, -0.7 wenn Top
                // die Mitte wäre dann -0.25, man könnte aber auch bei der Mitte die echte Mitte des Strings nehmen
                for (int i = 0; i < textString.Length; ++i)
                {
                    CharacterDisplayInfo cdl;
                    if (fdl.TryGetValue(textString[i], out cdl))
                    {
                        Gdi.GLYPHMETRICSFLOAT gm = cdl.Glyphmetrics;
                        dx += gm.gmfBlackBoxX;
                        dy += gm.gmfBlackBoxY;
                        yoffset = gm.gmfptGlyphOrigin.Y;
                    }
                }
                dy /= textString.Length;
                switch (alignment)
                {   // der Text kommt wenn unverändert angegeben an der Baseline
                    case GeoObject.Text.AlignMode.Baseline: break;
                    case GeoObject.Text.AlignMode.Bottom:
                        location = location + 0.2 * glyphDirection;
                        break;
                    case GeoObject.Text.AlignMode.Center:
                        location = location - 0.25 * glyphDirection;
                        break;
                    case GeoObject.Text.AlignMode.Top:
                        location = location - 0.7 * glyphDirection;
                        break;
                }
                switch (lineAlignment)
                {
                    case GeoObject.Text.LineAlignMode.Left: break;
                    case GeoObject.Text.LineAlignMode.Center:
                        location = location - (dx / 2) * lineDirection;
                        break;
                    case GeoObject.Text.LineAlignMode.Right:
                        location = location - (dx) * lineDirection;
                        break;
                }
            }
            Gl.glMatrixMode(Gl.GL_MODELVIEW); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            // ACHTUNG: Matrix ist vertauscht!!!
            double[] pmat = new double[16];
            pmat[0] = lineDirection.x;
            pmat[1] = lineDirection.y;
            pmat[2] = lineDirection.z;
            pmat[3] = 0;
            pmat[4] = glyphDirection.x;
            pmat[5] = glyphDirection.y;
            pmat[6] = glyphDirection.z;
            pmat[7] = 0;
            pmat[8] = normal.x;
            pmat[9] = normal.y;
            pmat[10] = normal.z;
            pmat[11] = 0;
            pmat[12] = location.x;
            pmat[13] = location.y;
            pmat[14] = location.z;
            pmat[15] = 1;
            Gl.glLoadMatrixd(pmat);
            for (int i = 0; i < textString.Length; ++i)
            {
                CharacterDisplayInfo cdl;
                if (fdl.TryGetValue(textString[i], out cdl))
                {
                    Gl.glCallList(cdl.Displaylist.ListNumber);
                    ++dbgNumChar;
                }
            }

            Gl.glPopMatrix();
            CheckError();
        }
        void IPaintTo3D.Nurbs(GeoPoint[] poles, double[] weights, double[] knots, int degree)
        {   // sollte nicht verwendet werden
            //if (currentList != null) currentList.SetHasContents();
            //Gl.glDisable(Gl.GL_LIGHTING);
            //Glu.gluBeginCurve(nurbsRenderer);
            //float[] fknots = new float[knots.Length];
            //for (int i = 0; i < knots.Length; ++i)
            //{
            //    fknots[i] = (float)knots[i];
            //}
            //float[] controlpoints = new float[poles.Length * 4];
            //    for (int i = 0; i < poles.Length; ++i)
            //    {
            //        controlpoints[i * 4 + 0] = (float)(poles[i].x * weights[i]);
            //        controlpoints[i * 4 + 1] = (float)(poles[i].y * weights[i]);
            //        controlpoints[i * 4 + 2] = (float)(poles[i].z * weights[i]);
            //        controlpoints[i * 4 + 3] = (float)weights[i];
            //    }
            //Glu.gluNurbsCurve(nurbsRenderer, fknots.Length, fknots, 4, controlpoints, degree + 1, Gl.GL_MAP1_VERTEX_4);
            //Glu.gluEndCurve(nurbsRenderer);
            //CheckError();
        }
        void IPaintTo3D.Line2D(int sx, int sy, int ex, int ey)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            // die Windows-Koordinaten gehen von links unten nach rechts oben...
            Glu.gluOrtho2D(0, clientwidth, clientheight, 0);
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glDisable(Gl.GL_LINE_SMOOTH);
            Gl.glLineWidth(1.0f);
            Gl.glBegin(Gl.GL_LINE_STRIP);
            Gl.glVertex2i(sx, sy);
            Gl.glVertex2i(ex, ey);
            Gl.glEnd();
            Gl.glPopMatrix();
            if (useLineWidth) Gl.glEnable(Gl.GL_LINE_SMOOTH);
            CheckError();
        }
        void IPaintTo3D.FillRect2D(PointF p1, PointF p2)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            // die Windows-Koordinaten gehen von links unten nach rechts oben...
            Glu.gluOrtho2D(0, clientwidth, clientheight, 0);
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glBegin(Gl.GL_POLYGON);
            Gl.glVertex2f(p1.X, p1.Y);
            Gl.glVertex2f(p1.X, p2.Y);
            Gl.glVertex2f(p2.X, p2.Y);
            Gl.glVertex2f(p2.X, p1.Y);
            Gl.glEnd();
            Gl.glPopMatrix();
            CheckError();
        }
        void IPaintTo3D.Point2D(int x, int y)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            // die Windows-Koordinaten gehen von links unten nach rechts oben...
            Glu.gluOrtho2D(0, clientwidth, clientheight, 0);
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glDisable(Gl.GL_LINE_SMOOTH);
            Gl.glBegin(Gl.GL_POINTS);
            Gl.glVertex2i(x, y);
            Gl.glEnd();
            Gl.glPopMatrix();
            if (useLineWidth) Gl.glEnable(Gl.GL_LINE_SMOOTH);
            CheckError();
        }
        void IPaintTo3D.Line2D(PointF p1, PointF p2)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION); // ModelView Matrix ist und bleibt immer Identität
            Gl.glPushMatrix();
            Gl.glLoadIdentity();
            // die Windows-Koordinaten gehen von links unten nach rechts oben...
            Glu.gluOrtho2D(0, clientwidth, clientheight, 0);
            Gl.glDisable(Gl.GL_LIGHTING);
            Gl.glDisable(Gl.GL_LINE_SMOOTH);
            Gl.glLineWidth(1.0f);
            Gl.glBegin(Gl.GL_LINE_STRIP);
            Gl.glVertex2f(p1.X, p1.Y);
            Gl.glVertex2f(p2.X, p2.Y);
            Gl.glEnd();
            Gl.glPopMatrix();
            if (useLineWidth) Gl.glEnable(Gl.GL_LINE_SMOOTH);
            CheckError();
        }
        void IPaintTo3D.DisplayIcon(GeoPoint p, Bitmap icon)
        {
            if (currentList != null) currentList.SetHasContents();
            if (icons.ContainsKey(icon))
            {
                Gl.glMatrixMode(Gl.GL_MODELVIEW); // ModelView Matrix ist und bleibt immer Identität (nein! bei BlockRef nicht!)
                Gl.glPushMatrix();
                // ACHTUNG: Matrix ist vertauscht!!!
                double[] pmat = new double[16];
                pmat[0] = 1.0;
                pmat[1] = 0.0;
                pmat[2] = 0.0;
                pmat[3] = 0;
                pmat[4] = 0.0;
                pmat[5] = 1.0;
                pmat[6] = 0.0;
                pmat[7] = 0;
                pmat[8] = 0.0;
                pmat[9] = 0.0;
                pmat[10] = 1.0;
                pmat[11] = 0;
                pmat[12] = p.x;
                pmat[13] = p.y;
                pmat[14] = p.z;
                pmat[15] = 1;
                // Gl.glDisable(Gl.GL_LINE_SMOOTH);
                Gl.glDisable(Gl.GL_LIGHTING);
                // Gl.glDisable(Gl.GL_BLEND);
                // Gl.glLoadMatrixd(pmat); // damit ging der Punkt über das Clipboard nicht, denn das zu verschiebende Objekt ist ein BlockRef
                // und der verändert die Matrix, es war also nicht die Eiheitsmatrix, wie oben geschrieben
                Gl.glMultMatrixd(pmat);
                Gl.glCallList((icons[icon] as OpenGlList).ListNumber);
                Gl.glPopMatrix();
            }
            CheckError();
        }
        void IPaintTo3D.DisplayBitmap(GeoPoint p, Bitmap bitmap)
        {
            if (currentList != null) currentList.SetHasContents();
            IPaintTo3DList list;
            if (bitmaps.TryGetValue(bitmap, out list))
            {
                Gl.glMatrixMode(Gl.GL_MODELVIEW); // ModelView Matrix ist und bleibt immer Identität
                Gl.glPushMatrix();
                // ACHTUNG: Matrix ist vertauscht!!!
                double[] pmat = new double[16];
                pmat[0] = 1.0;
                pmat[1] = 0.0;
                pmat[2] = 0.0;
                pmat[3] = 0;
                pmat[4] = 0.0;
                pmat[5] = 1.0;
                pmat[6] = 0.0;
                pmat[7] = 0;
                pmat[8] = 0.0;
                pmat[9] = 0.0;
                pmat[10] = 1.0;
                pmat[11] = 0;
                pmat[12] = p.x;
                pmat[13] = p.y;
                pmat[14] = p.z;
                pmat[15] = 1;
                // Gl.glDisable(Gl.GL_LINE_SMOOTH);
                Gl.glDisable(Gl.GL_LIGHTING);
                bool blend = Gl.glIsEnabled(Gl.GL_BLEND) != 0;
                Gl.glEnable(Gl.GL_BLEND); // damit der Alpha Kanal berücksichtigt wird
                Gl.glEnable(Gl.GL_ALPHA_TEST); // damit der Alpha Kanal berücksichtigt wird
                Gl.glAlphaFunc(Gl.GL_GREATER, 0.5f); // Sollte eigentlich immer so eingestellt sein
                Gl.glLoadMatrixd(pmat);
                Gl.glCallList((list as OpenGlList).ListNumber);
                if (!blend) Gl.glDisable(Gl.GL_BLEND); // wieder zurückstellen
                Gl.glPopMatrix();
            }
            CheckError();
        }
        void IPaintTo3D.List(IPaintTo3DList paintThisList)
        {
            if (paintThisList == null) return;
            if (currentList != null) currentList.SetHasContents();
            if ((paintThisList as OpenGlList).ListNumber != 0) Gl.glCallList((paintThisList as OpenGlList).ListNumber);
            //System.Diagnostics.Trace.WriteLine("display list: " + (paintThisList as OpenGlList).ListNumber.ToString());
            CheckError();
        }
        private void PaintListWithOffset(IPaintTo3DList paintThisList, int offsetX, int offsetY)
        {
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            // leider macht die glTranslated funktion ein Matrix in der zuerst die verschiebung und dann die 
            // alte projektion stattfinden
            double[] current = new double[16];
            Gl.glGetDoublev(Gl.GL_PROJECTION_MATRIX, current);
            double[] trans = new double[16];
            trans[0] = 1.0;
            trans[1] = 0.0;
            trans[2] = 0.0;
            trans[3] = 0.0;
            trans[4] = 0.0;
            trans[5] = 1.0;
            trans[6] = 0.0;
            trans[7] = 0.0;
            trans[8] = 0.0;
            trans[9] = 0.0;
            trans[10] = 1.0;
            trans[11] = 0.0;
            trans[12] = (double)offsetX / (double)clientwidth; // da die Ausgabe sich auf einen Bereich von 0 bis 1 bezieht
            trans[13] = (double)offsetY / (double)clientheight;
            trans[14] = 0.0;
            trans[15] = 1.0;
            Gl.glPushMatrix();
            Gl.glLoadMatrixd(trans); // dies ist die gewünschte reihenfolge
            Gl.glMultMatrixd(current);
            // Gl.glTranslated(offsetX * pixelToWorld, offsetY * pixelToWorld, 0.0);
            Gl.glCallList((paintThisList as OpenGlList).ListNumber);
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Gl.glPopMatrix();
            CheckError();
        }
        void IPaintTo3D.SelectedList(IPaintTo3DList paintThisList, int wobbleRadius)
        {
            if (paintThisList == null) return;
            if (wobbleRadius <= 0)
            {
                (this as IPaintTo3D).PushState();
                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glLoadIdentity();
                Gl.glTranslated(-2 * precision * ProjectionDirection.x, -2 * precision * ProjectionDirection.y, -2 * precision * ProjectionDirection.z);

                //Gl.glEnable(Gl.GL_TEXTURE_2D); 
                if (wobbleRadius == -1) Gl.glClear(Gl.GL_DEPTH_BUFFER_BIT); // Select findet über den Objekten statt, alte ZBuffer Inhalte werden gelöscht
                Gl.glEnable(Gl.GL_DEPTH_TEST);

                Gl.glEnable(Gl.GL_BLEND); // damit der Alpha Kanal berücksichtigt wird
                Gl.glEnable(Gl.GL_ALPHA_TEST); // damit der Alpha Kanal berücksichtigt wird
                                               // Gl.glTexCoord2d(0.0, 0.0);
                Gl.glColor4ub(selectColor.R, selectColor.G, selectColor.B, selectColor.A); // transparent mit Farbe geht hier nicht
                Gl.glCallList((paintThisList as OpenGlList).ListNumber);

                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glLoadIdentity();

                //Gl.glDisable(Gl.GL_TEXTURE_2D);
                // Gl.glEnable(Gl.GL_DEPTH_TEST);
                (this as IPaintTo3D).PopState();
                CheckError();
            }
            else
            {
                Gl.glDisable(Gl.GL_DEPTH_TEST);
                Gl.glClearStencil(0x0);
                Gl.glEnable(Gl.GL_STENCIL_TEST);
                Gl.glClear(Gl.GL_STENCIL_BUFFER_BIT);
                Gl.glStencilFunc(Gl.GL_ALWAYS, 0x1, 0x1);
                Gl.glStencilOp(Gl.GL_REPLACE, Gl.GL_REPLACE, Gl.GL_REPLACE);
                Gl.glColorMask(false, false, false, false);
                Gl.glCallList((paintThisList as OpenGlList).ListNumber);

                Gl.glStencilFunc(Gl.GL_NOTEQUAL, 0x1, 0x1);
                Gl.glStencilOp(Gl.GL_KEEP, Gl.GL_KEEP, Gl.GL_KEEP);
                Gl.glColor4ub(selectColor.R, selectColor.G, selectColor.B, selectColor.A);
                Gl.glColorMask(true, true, true, true);

                int a = wobbleRadius, b = wobbleRadius;
                int a2 = a * a, b2 = b * b, fa2 = 4 * a2;
                int x, y, sigma;
                for (x = 0, y = b, sigma = 2 * b2 + a2 * (1 - 2 * b); b2 * x <= a2 * y; x++)
                {
                    PaintListWithOffset(paintThisList, +x, +y);
                    PaintListWithOffset(paintThisList, -x, +y);
                    PaintListWithOffset(paintThisList, +x, -y);
                    PaintListWithOffset(paintThisList, -x, -y);
                    if (sigma >= 0)
                    {
                        sigma += fa2 * (1 - y);
                        y--;
                    }
                    sigma += b2 * (4 * x + 6);
                }
                a2 = a * a;
                b2 = b * b;
                int fb2 = 4 * b2;
                for (x = a, y = 0, sigma = 2 * a2 + b2 * (1 - 2 * a); a2 * y <= b2 * x; y++)
                {
                    PaintListWithOffset(paintThisList, +x, +y);
                    PaintListWithOffset(paintThisList, -x, +y);
                    PaintListWithOffset(paintThisList, +x, -y);
                    PaintListWithOffset(paintThisList, -x, -y);
                    if (sigma >= 0)
                    {
                        sigma += fb2 * (1 - x);
                        x--;
                    }
                    sigma += a2 * (4 * y + 6);
                }
                Gl.glDisable(Gl.GL_STENCIL_TEST);
                Gl.glEnable(Gl.GL_DEPTH_TEST);
                CheckError();
            }
        }
        //void IPaintTo3D.SelectedList(IPaintTo3DList paintThisList, int wobbleRadius)
        //{
        //    if (paintThisList == null) return;
        //    // zeichnet die Liste zuerst in den Stencil Buffer, was danach als verbotene Fläche verwendet wird
        //    // Dann wird die gleiche Liste mit der Selectfarbe in den normalen RGB Buffer gezeichnet, wobei das
        //    // Objekt selbst durch den Stencil Buffer nicht überzeichnet werden kann
        //    Gl.glDisable(Gl.GL_DEPTH_TEST);
        //    Gl.glClearStencil(0x0);
        //    Gl.glEnable(Gl.GL_STENCIL_TEST); 
        //    Gl.glClear(Gl.GL_STENCIL_BUFFER_BIT);
        //    Gl.glStencilFunc(Gl.GL_ALWAYS, 0x1, 0x1);
        //    Gl.glStencilOp(Gl.GL_REPLACE, Gl.GL_REPLACE, Gl.GL_REPLACE);
        //    Gl.glColorMask(false,false,false,false);
        //    Gl.glCallList((paintThisList as OpenGlList).ListNumber);

        //    Gl.glStencilFunc(Gl.GL_NOTEQUAL, 0x1, 0x1);
        //    Gl.glStencilOp(Gl.GL_KEEP, Gl.GL_KEEP, Gl.GL_KEEP);
        //    Gl.glColor4ub(selectColor.R, selectColor.G, selectColor.B, selectColor.A);
        //    Gl.glColorMask(true, true, true, true);

        //    int a = wobbleRadius, b = wobbleRadius;
        //    int a2 = a * a, b2 = b * b, fa2 = 4 * a2;
        //    int x, y, sigma;
        //    for (x = 0, y = b, sigma = 2 * b2 + a2 * (1 - 2 * b); b2 * x <= a2 * y; x++)
        //    {
        //        PaintListWithOffset(paintThisList, +x, +y);
        //        PaintListWithOffset(paintThisList, -x, +y);
        //        PaintListWithOffset(paintThisList, +x, -y);
        //        PaintListWithOffset(paintThisList, -x, -y);
        //        if (sigma >= 0)
        //        {
        //            sigma += fa2 * (1 - y);
        //            y--;
        //        }
        //        sigma += b2 * (4 * x + 6);
        //    }
        //    a2 = a * a;
        //    b2 = b * b;
        //    int fb2 = 4 * b2;
        //    for (x = a, y = 0, sigma = 2 * a2 + b2 * (1 - 2 * a); a2 * y <= b2 * x; y++)
        //    {
        //        PaintListWithOffset(paintThisList, +x, +y);
        //        PaintListWithOffset(paintThisList, -x, +y);
        //        PaintListWithOffset(paintThisList, +x, -y);
        //        PaintListWithOffset(paintThisList, -x, -y);
        //        if (sigma >= 0)
        //        {
        //            sigma += fb2 * (1 - x);
        //            x--;
        //        }
        //        sigma += a2 * (4 * y + 6);
        //    }
        //    Gl.glDisable(Gl.GL_STENCIL_TEST);
        //    Gl.glEnable(Gl.GL_DEPTH_TEST);
        //    CheckError();
        //}
        List<OpenGlList> listMaster = new List<OpenGlList>();


        void IPaintTo3D.OpenList(string name)
        {
            resManager.OpenList(name);            
        }

        IPaintTo3DList IPaintTo3D.CloseList()
        {
            return resManager.CloseList();            
        }

        IPaintTo3DList IPaintTo3D.MakeList(List<IPaintTo3DList> sublists)
        {
            return resManager.MakeList(sublists);
        }
        void IPaintTo3D.OpenPath()
        {
            throw new NotSupportedException("OpenGL does not support paths");
        }
        void IPaintTo3D.ClosePath(Color color)
        {
            throw new NotSupportedException("OpenGL does not support paths");
        }
        void IPaintTo3D.CloseFigure()
        {
            throw new NotSupportedException("OpenGL does not support paths");
        }
        void IPaintTo3D.Arc(GeoPoint center, GeoVector majorAxis, GeoVector minorAxis, double startParameter, double sweepParameter)
        {
            throw new NotSupportedException("OpenGL does not support arcs");
        }
        void IPaintTo3D.FreeUnusedLists()
        {
            //currentList.FreeLists();
        }
        void IPaintTo3D.UseZBuffer(bool use)
        {
            if (use)
            {
                Gl.glEnable(Gl.GL_DEPTH_TEST);
            }
            else
            {
                Gl.glDisable(Gl.GL_DEPTH_TEST);
            }
            CheckError();
        }
        void IPaintTo3D.FinishPaint()
        {
            try
            {
                Gl.glFlush();
                Gl.glFinish();
                Gdi.SwapBuffersFast(deviceContext);
            }
            catch (Exception e)
            {   // stürzt manchmal auf Eckhards Rechner ab
                if (e is ThreadAbortException) throw;
            }
            CheckError();
            //Wgl.wglMakeCurrent(IntPtr.Zero, IntPtr.Zero);
            //CheckError();
        }

        IDisposable IPaintTo3D.FacesBehindEdgesOffset => new MoveFacesBehindEdgesOffset(this);

        bool IPaintTo3D.IsBitmap => isBitmap;

        void IPaintTo3D.PaintFaces(PaintTo3D.PaintMode paintMode)
        {
            if (paintMode == PaintTo3D.PaintMode.FacesOnly)
            {   // Faces um die Genauigkeit nach hinten verschieben
                if (isPerspective)
                {
                    Gl.glMatrixMode(Gl.GL_PROJECTION);
                    double[] pmat = new double[16];
                    Gl.glGetDoublev(Gl.GL_PROJECTION_MATRIX, pmat);
                    double[,] mat = new double[4, 4];
                    mat[0, 0] = pmat[0];
                    mat[1, 0] = pmat[1];
                    mat[2, 0] = pmat[2];
                    mat[3, 0] = pmat[3];
                    mat[0, 1] = pmat[4];
                    mat[1, 1] = pmat[5];
                    mat[2, 1] = pmat[6];
                    mat[3, 1] = pmat[7];
                    mat[0, 2] = pmat[8];
                    mat[1, 2] = pmat[9];
                    mat[2, 2] = pmat[10];
                    mat[3, 2] = pmat[11];
                    mat[0, 3] = pmat[12];
                    mat[1, 3] = pmat[13];
                    mat[2, 3] = pmat[14];
                    mat[3, 3] = pmat[15];
                    Matrix m0 = DenseMatrix.OfArray(mat);
                    double det = m0.Determinant();
                    Matrix m1 = (Matrix)m0.Inverse();
                    Matrix trans = DenseMatrix.OfArray(new double[,] {
                    { 1, 0, 0, 0 },
                    { 0, 1, 0, 0 },
                    { 0, 0, 1, 0.001 }, // verschiebung in z un 1/1000
                    { 0, 0, 0, 1 } });
                    Matrix comp = (Matrix)(m1 * trans * m0);
                    pmat[0] = comp[0, 0];
                    pmat[1] = comp[1, 0];
                    pmat[2] = comp[2, 0];
                    pmat[3] = comp[3, 0];
                    pmat[4] = comp[0, 1];
                    pmat[5] = comp[1, 1];
                    pmat[6] = comp[2, 1];
                    pmat[7] = comp[3, 1];
                    pmat[8] = comp[0, 2];
                    pmat[9] = comp[1, 2];
                    pmat[10] = comp[2, 2];
                    pmat[11] = comp[3, 2];
                    pmat[12] = comp[0, 3];
                    pmat[13] = comp[1, 3];
                    pmat[14] = comp[2, 3];
                    pmat[15] = comp[3, 3];

                    Gl.glMatrixMode(Gl.GL_MODELVIEW);
                    Gl.glLoadMatrixd(pmat);
                }
                else
                {
                    Gl.glMatrixMode(Gl.GL_MODELVIEW);
                    Gl.glLoadIdentity();
                    Gl.glTranslated(precision * ProjectionDirection.x, precision * ProjectionDirection.y, precision * ProjectionDirection.z);
                }
                paintSurfaces = true;
                paintEdges = false;
            }
            else if (paintMode == PaintTo3D.PaintMode.CurvesOnly)
            {   // alles andere genau am Ort darstellen
                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glLoadIdentity();
                paintSurfaces = false;
                paintEdges = true;
            }
            else
            {
                Gl.glMatrixMode(Gl.GL_MODELVIEW);
                Gl.glLoadIdentity();
                paintSurfaces = true;
                paintEdges = true;
            }
            CheckError();
        }
        void IPaintTo3D.Blending(bool on)
        {
            if (on)
            {
                Gl.glEnable(Gl.GL_BLEND);
                Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);
            }
            else
            {
                Gl.glDisable(Gl.GL_BLEND);
            }
        }
        void IPaintTo3D.PushMultModOp(ModOp mm)
        {
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glPushMatrix();
            double[] pmat = new double[16];
            // ACHTUNG: Matrix ist vertauscht!!!
            pmat[0] = mm[0, 0];
            pmat[1] = mm[1, 0];
            pmat[2] = mm[2, 0];
            pmat[3] = 0.0;
            pmat[4] = mm[0, 1];
            pmat[5] = mm[1, 1];
            pmat[6] = mm[2, 1];
            pmat[7] = 0.0;
            pmat[8] = mm[0, 2];
            pmat[9] = mm[1, 2];
            pmat[10] = mm[2, 2];
            pmat[11] = 0.0;
            pmat[12] = mm[0, 3];
            pmat[13] = mm[1, 3];
            pmat[14] = mm[2, 3];
            pmat[15] = 1.0;
            Gl.glMultMatrixd(pmat);
        }
        void IPaintTo3D.PopModOp()
        {
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glPopMatrix();
        }
        void IPaintTo3D.SetClip(Rectangle clipRectangle)
        {
            if (clipRectangle.IsEmpty)
            {
                Gl.glClearStencil(0x0);
                Gl.glClear(Gl.GL_STENCIL_BUFFER_BIT);
                Gl.glDisable(Gl.GL_STENCIL_TEST);
                Gl.glStencilFunc(Gl.GL_ALWAYS, 0x1, 0x1);
            }
            else
            {
                (this as IPaintTo3D).PushState(); // GL_DEPTH_TEST und GL_BLEND beibehalten
                Gl.glDisable(Gl.GL_DEPTH_TEST);
                Gl.glDisable(Gl.GL_BLEND);
                Gl.glClearStencil(0x0);
                Gl.glEnable(Gl.GL_STENCIL_TEST);
                Gl.glClear(Gl.GL_STENCIL_BUFFER_BIT);
                Gl.glStencilFunc(Gl.GL_ALWAYS, 0x1, 0x1);
                Gl.glStencilOp(Gl.GL_REPLACE, Gl.GL_REPLACE, Gl.GL_REPLACE);
                Gl.glColorMask(false, false, false, false);

                (this as IPaintTo3D).FillRect2D(new PointF((float)clipRectangle.Left, (float)clipRectangle.Bottom), new PointF((float)clipRectangle.Right, (float)clipRectangle.Top));

                Gl.glStencilFunc(Gl.GL_EQUAL, 0x1, 0x1);
                Gl.glStencilOp(Gl.GL_KEEP, Gl.GL_KEEP, Gl.GL_KEEP);
                Gl.glColorMask(true, true, true, true);
                (this as IPaintTo3D).PopState();
            }
        }
        #endregion
    }
}
