﻿using CADability.UserInterface;
using System;
#if WEBASSEMBLY
using CADability.WebDrawing;
using Point = CADability.WebDrawing.Point;
#else
using System.Drawing;
using Point = System.Drawing.Point;
#endif

namespace CADability.Actions
{
    /// <summary>
    /// Action that constructs an angle defined by two points. It uses a <see cref="AngleProperty"/>
    /// to communicate the constructed point to the outside.
    /// </summary>

    public class ConstructAngleTwoPoints : ConstructAction, IIntermediateConstruction
    {
        private AngleProperty angleProperty;
        private GeoPointInput firstPointInput;
        private GeoPointInput secondPointInput;
        private GeoPoint firstPoint;
        private GeoPoint secondPoint;
        private bool firstPointIsValid;
        private bool secondPointIsValid;
        private bool succeeded;

        public ConstructAngleTwoPoints(AngleProperty angleProperty)
        {
            // sollte hier vielleicht eine Ebene mit reingehen? Oder immer ActiveDrawingPlane verwenden?
            this.angleProperty = angleProperty;
            firstPointIsValid = false;
            secondPointIsValid = false;
            succeeded = false;
        }
        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnSetAction ()"/>
        /// </summary>
		public override void OnSetAction()
        {
            base.TitleId = "Constr.AngleTwoPoints";

            firstPointInput = new GeoPointInput("Constr.AngleTwoPoints.FirstPoint");
            firstPointInput.DefinesBasePoint = true;
            firstPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetFirstPoint);

            secondPointInput = new GeoPointInput("Constr.AngleTwoPoints.SecondPoint");
            secondPointInput.SetGeoPointEvent += new ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetSecondPoint);

            firstPointIsValid = false;
            secondPointIsValid = false;

            base.SetInput(firstPointInput, secondPointInput);
            base.OnSetAction();
        }
        public override void OnRemoveAction()
        {
            base.OnRemoveAction();
            Frame.Project.Undo.ClearContext();
        }
        /// <summary>
        /// Implements <see cref="Action.OnActivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        public override void OnActivate(Action OldActiveAction, bool SettingAction)
        {
            foreach (IView vw in base.Frame.AllViews)
            {
                //vw.SetPaintHandler(PaintBuffer.DrawingAspect.Active,new RepaintView(OnRepaint));
            }
            base.OnActivate(OldActiveAction, SettingAction);
        }
        /// <summary>
        /// Implements <see cref="Action.OnInactivate"/>. If you override this method
        /// don't forget to call the bas implementation.
        /// </summary>
        /// <param name="NewActiveAction"><paramref name="Action.OnInactivate.NewActiveAction"/></param>
        /// <param name="RemovingAction"><paramref name="Action.OnInactivate.RemovingAction"/></param>
        public override void OnInactivate(Action NewActiveAction, bool RemovingAction)
        {
            foreach (IView vw in base.Frame.AllViews)
            {
                //vw.RemovePaintHandler(PaintBuffer.DrawingAspect.Active,new RepaintView(OnRepaint));
            }
            base.OnInactivate(NewActiveAction, RemovingAction);
        }

        /// <summary>
        /// Overrides <see cref="CADability.Actions.Action.GetID ()"/>
        /// </summary>
        /// <returns></returns>
		public override string GetID()
        {
            return "Constr.AngleTwoPoints";
        }
        private void Invalidate()
        {
            if (firstPointIsValid && secondPointIsValid)
            {
                foreach (IView vw in base.Frame.AllViews)
                {
                    PointF p1 = vw.Projection.ProjectF(firstPoint);
                    PointF p2 = vw.Projection.ProjectF(secondPoint);
                    int x = (int)(Math.Min(p1.X, p2.X) - 1);
                    int y = (int)(Math.Min(p1.Y, p2.Y) - 1);
                    int w = (int)(Math.Abs(p2.X - p1.X)) + 2;
                    int h = (int)(Math.Abs(p2.Y - p1.Y)) + 2;
                    Rectangle r = new Rectangle(x, y, w, h);
                    vw.Invalidate(PaintBuffer.DrawingAspect.Active, r);
                }
            }
        }
        private void Recalc()
        {
            if (firstPointIsValid && secondPointIsValid)
            {
                Angle a = new Angle(ActiveDrawingPlane.Project(secondPoint - firstPoint));
                using (Frame.Project.Undo.ContextFrame(this))
                {
                    angleProperty.SetAngle(a);
                }
            }
        }
        private void OnSetFirstPoint(GeoPoint p)
        {
            Invalidate(); // alter Zustand
            firstPoint = p;
            firstPointIsValid = true;
            Recalc();
            Invalidate(); // neuer Zustand
        }
        private void OnSetSecondPoint(GeoPoint p)
        {
            Invalidate();
            secondPoint = p;
            secondPointIsValid = true;
            Recalc();
            Invalidate();
        }
        #region IIntermediateConstruction Members
        IPropertyEntry IIntermediateConstruction.GetHandledProperty()
        {
            return angleProperty;
        }
        bool IIntermediateConstruction.Succeeded
        {
            get { return succeeded; }
        }
#endregion
        /// <summary>
        /// Overrides <see cref="CADability.Actions.ConstructAction.OnDone ()"/>
        /// </summary>
        public override void OnDone()
        {
            succeeded = true;
            base.OnDone();
        }

    }
}
