﻿using CADability.Attribute;
using CADability.GeoObject;
using System;
using System.Collections;

namespace CADability.UserInterface
{


    public class DimensionStyleSelectionPropertyException : ApplicationException
    {
        public DimensionStyleSelectionPropertyException(string msg) : base(msg) { }
    }
    /// <summary>
    /// 
    /// </summary>

    public class DimensionStyleSelectionProperty : MultipleChoiceProperty
    {
        private IGeoObject toWatch;
        private DimensionStyle[] selectableStyles;
        public delegate void DimensionStyleChangedDelegate(DimensionStyle SelectedDimensionStyle);
        public event DimensionStyleChangedDelegate DimensionStyleChangedEvent;
        private IDimensionStyle dimensionStyle;
        private DimensionStyle Find(string name)
        {
            for (int i = 0; i < selectableStyles.Length; ++i)
            {
                if (selectableStyles[i].Name == name) return selectableStyles[i];
            }
            return null;
        }
        public DimensionStyleSelectionProperty(string resourceId, DimensionStyleList list, IDimensionStyle dimensionStyle, Dimension.EDimType dimType, bool includeUndefined) : base()
        {
            base.resourceId = resourceId;
            this.dimensionStyle = dimensionStyle;
            ArrayList al = new ArrayList();
            for (int i = 0; i < list.Count; ++i)
            {
                if (dimType == Dimension.EDimType.DimAll || (((int)(list[i].Types)) & (1 << (int)dimType)) != 0)
                {
                    al.Add(list[i]);
                }
            }
            if (al.Count == 0) throw new DimensionStyleSelectionPropertyException("no appropriate DimensionStyle available");
            selectableStyles = (DimensionStyle[])al.ToArray(typeof(DimensionStyle));
            if (includeUndefined)
            {
                base.choices = new string[selectableStyles.Length + 1];
                for (int i = 0; i < selectableStyles.Length; ++i)
                {
                    base.choices[i + 1] = selectableStyles[i].Name;
                }
                string undef = StringTable.GetString("DimensionStyle.Undefined");
                // sollte es den Namen schon geben, werden solange - davor und dahintergemacht, bis es den Namen mehr gibt
                // while (Find(undef)!=null) undef = "-" + undef +"-";
                choices[0] = undef;
                if (dimensionStyle.DimensionStyle != null) selectedText = dimensionStyle.DimensionStyle.Name;
                else selectedText = undef;
            }
            else
            {
                base.choices = new string[selectableStyles.Length];
                for (int i = 0; i < selectableStyles.Length; ++i)
                {
                    base.choices[i] = selectableStyles[i].Name;
                }
                if (dimensionStyle.DimensionStyle != null) base.selectedText = dimensionStyle.DimensionStyle.Name;
            }
            toWatch = dimensionStyle as IGeoObject;
        }
        protected override void OnSelectionChanged(string selected)
        {
            base.OnSelectionChanged(selected);
            DimensionStyle sel = Find(selected);
            dimensionStyle.DimensionStyle = sel;
            if (DimensionStyleChangedEvent != null) DimensionStyleChangedEvent(sel);
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Added"/>
        /// </summary>
        /// <param name="propertyTreeView"></param>
        public override void Added(IPropertyPage propertyTreeView)
        {
            base.Added(propertyTreeView);
            if (toWatch != null) toWatch.DidChangeEvent += new ChangeDelegate(GeoObjectDidChange);
        }
        /// <summary>
        /// Overrides <see cref="IShowPropertyImpl.Removed"/>
        /// </summary>
        /// <param name="propertyTreeView">the IPropertyTreeView from which it was removed</param>
        public override void Removed(IPropertyPage propertyTreeView)
        {
            base.Removed(propertyTreeView);
            if (toWatch != null) toWatch.DidChangeEvent -= new ChangeDelegate(GeoObjectDidChange);
        }
        private void GeoObjectDidChange(IGeoObject Sender, GeoObjectChange Change)
        {
            if (Sender == toWatch && Change.OnlyAttributeChanged && propertyPage != null)
            {
                if ((Change as GeoObjectChange).MethodOrPropertyName == "DimensionStyle")
                {
                    if ((toWatch as IDimensionStyle).DimensionStyle != null) base.selectedText = (toWatch as IDimensionStyle).DimensionStyle.Name;
                    else base.selectedText = null;
                    propertyPage.Refresh(this);
                }
            }
        }
    }
}
