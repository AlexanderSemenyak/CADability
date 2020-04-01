﻿namespace CADability.Actions
{
    /// <summary>
    /// 
    /// </summary>
    internal class ConstrDrawingPlaneOffset : ConstructAction
    {
        private Plane basePlane;
        private Projection projection;
        private GeoPoint throughPoint;
        public ConstrDrawingPlaneOffset(Plane basePlane, Projection projection)
        {
            this.basePlane = basePlane;
            this.projection = projection;
            throughPoint = basePlane.Location;
            base.ChangeTabInControlCenter = true;
        }
        public override void OnSetAction()
        {
            base.TitleId = "DrawingPlane.Constr.Offset";
            GeoPointInput geoPointInput = new GeoPointInput("DrawingPlane.Offset");
            geoPointInput.GetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.GetGeoPointDelegate(OnGetThroughPoint);
            geoPointInput.SetGeoPointEvent += new CADability.Actions.ConstructAction.GeoPointInput.SetGeoPointDelegate(OnSetThroughPoint);
            base.SetInput(geoPointInput);
            base.OnSetAction();
        }

        public override string GetID()
        {
            return "Construct.DrawingPlane.Offset";
        }

        public override void OnDone()
        {
            basePlane.Location = throughPoint;
            projection.DrawingPlane = basePlane;
            base.OnDone();
        }

        private GeoPoint OnGetThroughPoint()
        {
            return throughPoint;
        }

        private void OnSetThroughPoint(GeoPoint p)
        {
            throughPoint = p;
        }
    }
}
