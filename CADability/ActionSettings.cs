﻿using CADability.UserInterface;
using System;
using System.Runtime.Serialization;

namespace CADability
{
    /// <summary>
    /// 
    /// </summary>
    [Serializable()]
    internal class ActionSettings : CADability.Settings
    {
        public ActionSettings()
        {
            this.Initialize();
        }
        internal void Initialize()
        {
            this.resourceId = "Action.Settings";
            this.myName = "Action";

            if (!this.ContainsSetting("RepeatConstruct"))
            {
                BooleanProperty repeatConstruct = new BooleanProperty("Setting.Action.RepeatConstruct", "Setting.Action.RepeatConstruct.Values", "RepeatConstruct");
                repeatConstruct.BooleanValue = false;
                AddSetting("RepeatConstruct", repeatConstruct);
            }

            if (!this.ContainsSetting("PopProperties"))
            {

                BooleanProperty popProperties = new BooleanProperty("Setting.Action.PopProperties", "YesNo.Values", "PopProperties");
                popProperties.BooleanValue = true;
                AddSetting("PopProperties", popProperties);
            }
            if (!this.ContainsSetting("KeepStyle"))
            {

                BooleanProperty popProperties = new BooleanProperty("Setting.Action.KeepStyle", "YesNo.Values", "KeepStyle");
                popProperties.BooleanValue = true;
                AddSetting("KeepStyle", popProperties);
            }
        }
        /// <summary>
        /// Constructor required by deserialization
        /// </summary>
        /// <param name="info">SerializationInfo</param>
        /// <param name="context">StreamingContext</param>
        protected ActionSettings(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
