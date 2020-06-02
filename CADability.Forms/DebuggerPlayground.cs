﻿using CADability.Curve2D;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace CADability.Forms
{
    /// <summary>
    /// This class is not part of CADability OpenSource. I use it to write testcode for debugging
    /// </summary>
    internal class DebuggerPlayground : ICommandHandler
    {
        private IFrame frame;
        public DebuggerPlayground(IFrame frame)
        {
            this.frame = frame;
        }
        public static MenuWithHandler[] Connect(IFrame frame, MenuWithHandler[] mainMenu)
        {
            MenuWithHandler newItem = new MenuWithHandler();
            newItem.ID = "DebuggerPlayground.Debug";
            newItem.Text = "Debug";
            newItem.Target = new DebuggerPlayground(frame);
            List<MenuWithHandler> tmp = new List<MenuWithHandler>(mainMenu);
            tmp.Add(newItem);
            return tmp.ToArray();
        }

        public bool OnCommand(string MenuId)
        {
            if (MenuId == "DebuggerPlayground.Debug")
            {

                TestCollision();
                return true;
            }
            return false;
        }

        public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            return false;
        }

        private void SomeTestCode()
        {
            //Project.ReadFromFile(@"C:\Zeichnungen\DxfDwg\N5-E4-01.DXF", "dxf");
            Project pr1 = Project.ReadFromFile(@"C:\Zeichnungen\DxfDwg\V38 04 28 PWCS 50012.dxf", "dxf");
            bool skip = true;
            string lastSkipFile = "NurbsTest.dxf";
            foreach (string file in Directory.EnumerateFiles(@"C:\Zeichnungen\DxfDwg", "*.dxf"))
            {
                if (skip)
                {
                    if (file.EndsWith(lastSkipFile)) skip = false;
                    continue;
                }
                try
                {
                    System.Diagnostics.Trace.WriteLine("importing: " + file);
                    Project pr = Project.ReadFromFile(file, "dxf");
                    if (pr != null)
                    {
                        if (pr.GetModelCount() > 1)
                        {
                            System.Diagnostics.Trace.WriteLine(file + ": " + pr.GetModel(0).Count.ToString() + ", " + pr.GetModel(1).Count.ToString());
                        }
                        else
                        {
                            System.Diagnostics.Trace.WriteLine(file + ": " + pr.GetModel(0).Count.ToString());
                        }
                    }
                    else
                    {
                        System.Diagnostics.Trace.WriteLine("could not import: " + file);
                    }
                }
                catch (Exception ex)
                {
                    if (ex.InnerException != null) System.Diagnostics.Trace.WriteLine(file + ": " + ex.InnerException.Message);
                    else System.Diagnostics.Trace.WriteLine(file + ": " + ex.Message);
                }
            }
        }

        void TestFaceBoundingCube()
        {
            Face face = frame.Project.GetActiveModel()[0] as Face;
            Solid sld = frame.Project.GetActiveModel()[1] as Solid;
            if (sld != null && face != null)
            {
                bool ok = face.HitBoundingCube(sld.GetBoundingCube());
            }
        }

        private void ExportDxf()
        {
            DXF.Export exp = new DXF.Export(netDxf.Header.DxfVersion.AutoCad2000);
            exp.WriteToFile(frame.Project, @"C:\Zeichnungen\DxfDwg\testNetDxfOut.dxf");

        }
        void TestCollision()
        {
            if (frame.SelectedObjects.Count>2)
            {
                List<Solid> tools = new List<Solid>();
                Solid body = null;
                for (int i = 0; i < frame.SelectedObjects.Count; i++)
                {
                    if (frame.SelectedObjects[i] is Solid sld)
                    {
                        if (sld.Name != null && sld.Name == "1583424708") body = sld;
                        else tools.Add(sld);
                    }
                }
                if (body != null && tools.Count > 0)
                {
                    int tc0 = System.Environment.TickCount;
                    for (int i = 0; i < tools.Count; i++)
                    {
                        CollisionDetection cd = new CollisionDetection(tools[i].Shells[0], body.Shells[0]);
                        bool collision = cd.GetResult(1e-6, false, out GeoPoint cp, out GeoObjectList collidingFaces, true);
                    }
                    int tc1 = System.Environment.TickCount;
                    int dt = tc1 - tc0;
                    System.Diagnostics.Trace.WriteLine("CollisionDetection: " + dt.ToString());
                }
            }
        }

    }
}
