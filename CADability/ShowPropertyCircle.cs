﻿using CADability.Actions;
using CADability.GeoObject;
using System.Collections;
using System.Collections.Generic;



namespace CADability.UserInterface
{
    /// <summary>
    /// Shows the properties of a circle.
    /// </summary>

    public class ShowPropertyCircle : IShowPropertyImpl, IDisplayHotSpots, ICommandHandler, IGeoObjectShowProperty
    {
        private Ellipse circle;
        private IFrame frame;
        private GeoPointProperty centerProperty;
        private LengthProperty radiusProperty;
        private LengthProperty diameterProperty;
        private LengthHotSpot[] radiusHotSpots;
        private AngleProperty startAngleProperty;
        private AngleProperty endAngleProperty;
        private GeoPointProperty startPointProperty;
        private GeoPointProperty endPointProperty;
        private LengthProperty arcLengthProperty;
        private BooleanProperty directionProperty;
        private AngleHotSpot startAngleHotSpot;
        private AngleHotSpot endAngleHotSpot;
        private IShowProperty[] subEntries;
        private IShowProperty[] attributeProperties; // Anzeigen für die Attribute (Ebene, Farbe u.s.w)

        // kommen alle weg!
        public Angle StartAngle
        {
            get { return new Angle(circle.StartParameter); }
            set { circle.StartParameter = value; }
        }
        public Angle EndAngle
        {
            get { return new Angle(circle.StartParameter + circle.SweepParameter); }
            set { circle.SweepParameter = value - (Angle)circle.StartParameter; }
        }

        public ShowPropertyCircle(Ellipse circle, IFrame frame)
        {
            this.circle = circle;
            this.frame = frame;
            centerProperty = new GeoPointProperty("Circle.Center", frame, true);
            centerProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetCenter);
            centerProperty.SetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.SetGeoPointDelegate(OnSetCenter);
            centerProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyCenterWithMouse);
            centerProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);

            radiusProperty = new LengthProperty("Circle.Radius", frame, true);
            radiusProperty.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnGetRadius);
            radiusProperty.SetLengthEvent += new CADability.UserInterface.LengthProperty.SetLengthDelegate(OnSetRadius);
            radiusProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyRadiusWithMouse);
            radiusProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);
            radiusHotSpots = new LengthHotSpot[4];
            for (int i = 0; i < 4; ++i)
            {
                radiusHotSpots[i] = new LengthHotSpot(radiusProperty);
                switch (i)
                {
                    case 0:
                        radiusHotSpots[i].Position = circle.Center + circle.MajorAxis;
                        break;
                    case 1:
                        radiusHotSpots[i].Position = circle.Center - circle.MajorAxis;
                        break;
                    case 2:
                        radiusHotSpots[i].Position = circle.Center + circle.MinorAxis;
                        break;
                    case 3:
                        radiusHotSpots[i].Position = circle.Center - circle.MinorAxis;
                        break;
                }
            }

            diameterProperty = new LengthProperty("Circle.Diameter", frame, true);
            diameterProperty.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnGetDiameter);
            diameterProperty.SetLengthEvent += new CADability.UserInterface.LengthProperty.SetLengthDelegate(OnSetDiameter);
            diameterProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyRadiusWithMouse);
            diameterProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);

            if (circle.IsArc)
            {
                startAngleProperty = new AngleProperty("Arc.StartAngle", frame, true);
                startAngleProperty.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnGetStartAngle);
                startAngleProperty.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnSetStartAngle);
                startAngleProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyStartAngleWithMouse);
                startAngleProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);
                startAngleHotSpot = new AngleHotSpot(startAngleProperty);
                startAngleHotSpot.Position = circle.StartPoint;

                endAngleProperty = new AngleProperty("Arc.EndAngle", frame, true);
                endAngleProperty.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnGetEndAngle);
                endAngleProperty.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnSetEndAngle);
                endAngleProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyEndAngleWithMouse);
                endAngleProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);
                endAngleHotSpot = new AngleHotSpot(endAngleProperty);
                endAngleHotSpot.Position = circle.EndPoint;
                base.resourceId = "CircleArc.Object";

                directionProperty = new BooleanProperty("Arc.Direction", "Arc.Direction.Values");
                directionProperty.BooleanValue = circle.SweepParameter > 0.0;
                directionProperty.GetBooleanEvent += new CADability.UserInterface.BooleanProperty.GetBooleanDelegate(OnGetDirection);
                directionProperty.SetBooleanEvent += new CADability.UserInterface.BooleanProperty.SetBooleanDelegate(OnSetDirection);
                // hat keinen Hotspot
                startPointProperty = new GeoPointProperty("Arc.StartPoint", frame, false);
                startPointProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetStartPoint);
                startPointProperty.ReadOnly = true;
                startPointProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);
                endPointProperty = new GeoPointProperty("Arc.EndPoint", frame, false);
                endPointProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetEndPoint);
                endPointProperty.ReadOnly = true;
                endPointProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);
            }
            else
            {
                if (Settings.GlobalSettings.GetBoolValue("CircleShowStartPointProperty", false))
                {
                    startAngleProperty = new AngleProperty("Arc.StartAngle", frame, true);
                    startAngleProperty.GetAngleEvent += new AngleProperty.GetAngleDelegate(OnGetStartAngle);
                    startAngleProperty.SetAngleEvent += new AngleProperty.SetAngleDelegate(OnSetStartAngle);
                    startAngleProperty.ModifyWithMouseEvent += new ModifyWithMouseDelegate(ModifyStartAngleWithMouse);
                    startAngleProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);
                    startAngleHotSpot = new AngleHotSpot(startAngleProperty);
                    startAngleHotSpot.Position = circle.StartPoint;

                    directionProperty = new BooleanProperty("Arc.Direction", "Arc.Direction.Values");
                    directionProperty.BooleanValue = circle.SweepParameter > 0.0;
                    directionProperty.GetBooleanEvent += new CADability.UserInterface.BooleanProperty.GetBooleanDelegate(OnGetDirection);
                    directionProperty.SetBooleanEvent += new CADability.UserInterface.BooleanProperty.SetBooleanDelegate(OnSetDirection);
                    // hat keinen Hotspot
                    startPointProperty = new GeoPointProperty("Arc.StartPoint", frame, false);
                    startPointProperty.GetGeoPointEvent += new CADability.UserInterface.GeoPointProperty.GetGeoPointDelegate(OnGetStartPoint);
                    startPointProperty.ReadOnly = true;
                    startPointProperty.StateChangedEvent += new StateChangedDelegate(OnStateChanged);
                }
                base.resourceId = "Circle.Object";
            }
            arcLengthProperty = new LengthProperty("Circle.ArcLength", frame, true);
            arcLengthProperty.GetLengthEvent += new CADability.UserInterface.LengthProperty.GetLengthDelegate(OnGetArcLength);
            arcLengthProperty.ReadOnly = true;
            attributeProperties = circle.GetAttributeProperties(frame);
        }
        private void OnGeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            centerProperty.GeoPointChanged();
            radiusProperty.LengthChanged();
            diameterProperty.LengthChanged();
            if (radiusHotSpots != null)
            {
                radiusHotSpots[0].Position = circle.Center + circle.MajorAxis;
                radiusHotSpots[1].Position = circle.Center - circle.MajorAxis;
                radiusHotSpots[2].Position = circle.Center + circle.MinorAxis;
                radiusHotSpots[3].Position = circle.Center - circle.MinorAxis;
                if (HotspotChangedEvent != null)
                {
                    HotspotChangedEvent(radiusHotSpots[0], HotspotChangeMode.Moved);
                    HotspotChangedEvent(radiusHotSpots[1], HotspotChangeMode.Moved);
                    HotspotChangedEvent(radiusHotSpots[2], HotspotChangeMode.Moved);
                    HotspotChangedEvent(radiusHotSpots[3], HotspotChangeMode.Moved);
                }
            }
            if (startAngleProperty != null)
            {
                startAngleProperty.AngleChanged();
                if (endAngleProperty != null) endAngleProperty.AngleChanged();
                startPointProperty.Refresh();
                if (endAngleProperty != null) endPointProperty.Refresh();
                startAngleHotSpot.Position = circle.StartPoint;
                if (endAngleProperty != null) endAngleHotSpot.Position = circle.EndPoint;
                if (HotspotChangedEvent != null)
                {
                    HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Moved);
                    if (endAngleProperty != null) HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Moved);
                }
            }
            if (arcLengthProperty != null)
            {
                arcLengthProperty.Refresh();
            }
            if (directionProperty != null)
            {
                directionProperty.Refresh();
            }

        }

        #region IShowPropertyImpl Overrides

        public override ShowPropertyEntryType EntryType { get { return ShowPropertyEntryType.GroupTitle; } }

        public override int SubEntriesCount
        {
            get
            {
                return SubEntries.Length;
            }
        }

        public override IShowProperty[] SubEntries
        {
            get
            {
                if (subEntries == null)
                {
                    ArrayList prop = new ArrayList();
                    prop.Add(centerProperty);
                    prop.Add(radiusProperty);
                    prop.Add(diameterProperty);
                    centerProperty.GeoPointChanged();
                    radiusProperty.LengthChanged();
                    diameterProperty.LengthChanged();
                    if (startAngleProperty != null)
                    {
                        prop.Add(startAngleProperty);
                        startAngleProperty.AngleChanged();
                        prop.Add(startPointProperty);
                        if (endAngleProperty != null)
                        {
                            prop.Add(endAngleProperty);
                            endAngleProperty.AngleChanged();
                            prop.Add(endPointProperty);
                        }
                        prop.Add(directionProperty);
                    }
                    prop.Add(arcLengthProperty);
                    IShowProperty[] mainProps = (IShowProperty[])prop.ToArray(typeof(IShowProperty));
                    subEntries = IShowPropertyImpl.Concat(mainProps, attributeProperties);
                }
                return subEntries;
            }
        }

        public override void Opened(bool IsOpen)
        {
            if (HotspotChangedEvent != null)
            {
                if (IsOpen)
                {
                    if (startAngleHotSpot != null) HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Visible);
                    if (endAngleHotSpot != null) HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Visible);
                    HotspotChangedEvent(centerProperty, HotspotChangeMode.Visible);
                    for (int i = 0; i < 4; ++i)
                        HotspotChangedEvent(radiusHotSpots[i], HotspotChangeMode.Visible);
                }
                else
                {
                    if (startAngleHotSpot != null) HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Invisible);
                    if (endAngleHotSpot != null) HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Invisible);
                    HotspotChangedEvent(centerProperty, HotspotChangeMode.Invisible);
                    for (int i = 0; i < 4; ++i)
                        HotspotChangedEvent(radiusHotSpots[i], HotspotChangeMode.Invisible);
                }
            }
            base.Opened(IsOpen);
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Removed"/>
        /// </summary>
        /// <param name="propertyTreeView">the IPropertyTreeView from which it was removed</param>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            this.circle.DidChangeEvent -= new ChangeDelegate(OnGeoObjectDidChange);
            circle.UserData.UserDataAddedEvent -= new UserData.UserDataAddedDelegate(OnUserDataAdded);
            circle.UserData.UserDataRemovedEvent -= new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Removed(propertyTreeView);
        }
        public override void Added(IPropertyPage propertyTreeView)
        {
            this.circle.DidChangeEvent += new ChangeDelegate(OnGeoObjectDidChange);
            circle.UserData.UserDataAddedEvent += new UserData.UserDataAddedDelegate(OnUserDataAdded);
            circle.UserData.UserDataRemovedEvent += new UserData.UserDataRemovedDelegate(OnUserDataAdded);
            base.Added(propertyTreeView);
        }
        void OnUserDataAdded(string name, object value)
        {
            this.subEntries = null;
            attributeProperties = circle.GetAttributeProperties(frame);
            propertyTreeView.Refresh(this);
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.LabelType"/>
        /// </summary>
        public override ShowPropertyLabelFlags LabelType
        {
            get
            {
                return ShowPropertyLabelFlags.Selectable | ShowPropertyLabelFlags.ContextMenu | ShowPropertyLabelFlags.ContextMenu;
            }
        }

        public override MenuWithHandler[] ContextMenu
        {
            get
            {
                List<MenuWithHandler> items = new List<MenuWithHandler>(MenuResource.LoadMenuDefinition("MenuId.Object.Ellipse", false, this));
                // if (CreateContextMenueEvent != null) CreateContextMenueEvent(this, cm);
                circle.GetAdditionalContextMenue(this, Frame, items);
                return items.ToArray();
            }
        }



#endregion

#region IDisplayHotSpots Members

        public event CADability.HotspotChangedDelegate HotspotChangedEvent;

        public void ReloadProperties()
        {
            // TODO:  Add ShowPropertyCircle.ReloadProperties implementation
        }

#endregion

        private GeoPoint OnGetEndPoint(GeoPointProperty sender)
        {
            return circle.EndPoint;
        }
        private GeoPoint OnGetStartPoint(GeoPointProperty sender)
        {
            return circle.StartPoint;
        }
        private GeoPoint OnGetCenter(GeoPointProperty sender)
        {
            return circle.Center;
        }
        private void OnSetCenter(GeoPointProperty sender, GeoPoint p)
        {
            circle.Center = p;
        }
        private void ModifyCenterWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralGeoPointAction gpa = new GeneralGeoPointAction(centerProperty, circle);
            frame.SetAction(gpa);
        }
        private double OnGetRadius(LengthProperty sender)
        {
            return circle.Radius;
        }
        private double OnGetDiameter(LengthProperty sender)
        {
            return circle.Radius * 2.0;
        }
        private double OnGetArcLength(LengthProperty sender)
        {
            return circle.Length;
        }
        private void OnSetRadius(LengthProperty sender, double l)
        {
            circle.Radius = l;
        }
        private void OnSetDiameter(LengthProperty sender, double l)
        {
            circle.Radius = l / 2.0;
        }
        private void ModifyRadiusWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralLengthAction gla = new GeneralLengthAction(radiusProperty, circle.Center, circle);
            frame.SetAction(gla);
        }
        private Angle OnGetStartAngle()
        {
            return new Angle(circle.StartParameter);
        }
        private void OnSetStartAngle(Angle a)
        {
            circle.StartParameter = a.Radian;
        }
        private void ModifyStartAngleWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralAngleAction gaa = new GeneralAngleAction(startAngleProperty, circle.Plane);
            frame.SetAction(gaa);
        }
        private Angle OnGetEndAngle()
        {
            return new Angle(circle.StartParameter + circle.SweepParameter);
        }
        private void OnSetEndAngle(Angle a)
        {
            SweepAngle sa = new SweepAngle(new Angle(circle.StartParameter), a, circle.SweepParameter > 0.0);
            circle.SweepParameter = sa.Radian;
        }
        private bool OnGetDirection()
        {
            return circle.SweepParameter > 0.0;
        }
        private void OnSetDirection(bool val)
        {
            if (val)
            {
                if (circle.SweepParameter < 0.0) (circle as ICurve).Reverse();
            }
            else
            {
                if (circle.SweepParameter > 0.0) (circle as ICurve).Reverse();
            }
        }
        private void ModifyEndAngleWithMouse(IPropertyEntry sender, bool StartModifying)
        {
            GeneralAngleAction gaa = new GeneralAngleAction(endAngleProperty, circle.Plane);
            frame.SetAction(gaa);
        }
        private void OnStateChanged(IShowProperty sender, StateChangedArgs args)
        {
            if (HotspotChangedEvent != null)
            {
                if (args.EventState == StateChangedArgs.State.Selected || args.EventState == StateChangedArgs.State.SubEntrySelected)
                {
                    if (sender == startPointProperty)
                    {
                        HotspotChangedEvent(startPointProperty, HotspotChangeMode.Selected);
                    }
                    else if (sender == endPointProperty)
                    {
                        HotspotChangedEvent(endPointProperty, HotspotChangeMode.Selected);
                    }
                    if (sender == startAngleProperty)
                    {
                        HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Selected);
                    }
                    else if (sender == endAngleProperty)
                    {
                        HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Selected);
                    }
                    else if (sender == centerProperty)
                    {
                        HotspotChangedEvent(centerProperty, HotspotChangeMode.Selected);
                    }
                    else if (sender == radiusProperty)
                    {
                        for (int i = 0; i < radiusHotSpots.Length; i++)
                        {
                            HotspotChangedEvent(radiusHotSpots[i], HotspotChangeMode.Selected);
                        }
                    }
                }
                else if (args.EventState == StateChangedArgs.State.UnSelected)
                {
                    if (sender == startPointProperty)
                    {
                        HotspotChangedEvent(startPointProperty, HotspotChangeMode.Deselected);
                    }
                    else if (sender == endPointProperty)
                    {
                        HotspotChangedEvent(endPointProperty, HotspotChangeMode.Deselected);
                    }
                    if (sender == startAngleProperty)
                    {
                        HotspotChangedEvent(startAngleHotSpot, HotspotChangeMode.Deselected);
                    }
                    else if (sender == endAngleProperty)
                    {
                        HotspotChangedEvent(endAngleHotSpot, HotspotChangeMode.Deselected);
                    }
                    else if (sender == centerProperty)
                    {
                        HotspotChangedEvent(centerProperty, HotspotChangeMode.Deselected);
                    }
                    else if (sender == radiusProperty)
                    {
                        for (int i = 0; i < radiusHotSpots.Length; i++)
                        {
                            HotspotChangedEvent(radiusHotSpots[i], HotspotChangeMode.Deselected);
                        }
                    }
                }
            }
        }
#region ICommandHandler Members
        public bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    (circle as ICurve).Reverse();
                    return true;
                case "MenuId.CurveSplit":
                    frame.SetAction(new ConstrSplitCurve(circle));
                    return true;
                case "MenuId.Approximate":
                    if (frame.ActiveAction is SelectObjectsAction && (frame.GetIntSetting("Approximate.Mode", 0) == 0))
                    {
                        Curves.Approximate(frame, circle);
                    }
                    return true;
                case "MenuId.Aequidist":
                    frame.SetAction(new ConstructAequidist(circle));
                    return true;
            }
            return false;
        }

        public bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Reverse":
                    CommandState.Enabled = (circle.IsArc);
                    return true;
                case "MenuId.CurveSplit":
                    return true;
                case "MenuId.Approximate":
                    CommandState.Enabled = (frame.ActiveAction is SelectObjectsAction) && (frame.GetIntSetting("Approximate.Mode", 0) == 0);
                    return true;
            }
            return false;
        }

#endregion
#region IGeoObjectShowProperty Members
        public event CADability.GeoObject.CreateContextMenueDelegate CreateContextMenueEvent;
        IGeoObject IGeoObjectShowProperty.GetGeoObject()
        {
            return circle;
        }
        string IGeoObjectShowProperty.GetContextMenuId()
        {
            return "MenuId.Object.Ellipse";
        }
#endregion
    }

}
