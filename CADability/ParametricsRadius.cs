﻿using CADability.Actions;
using CADability.GeoObject;
using CADability.UserInterface;
using System;
using System.Collections.Generic;
using System.Text;

namespace CADability
{
    class ParametricsRadius : ConstructAction
    {
        private Face faceWithRadius;
        private IFrame frame;
        private bool diameter;
        private Shell shell;
        private LengthInput radiusInput;
        private LengthInput diameterInput;
        private bool validResult;
        public ParametricsRadius(Face faceWithRadius, IFrame frame)
        {
            this.faceWithRadius = faceWithRadius;
            this.frame = frame;
            shell = faceWithRadius.Owner as Shell;
        }

        public override string GetID()
        {
            return "MenuId.Parametrics.Cylinder";
        }

        public override bool OnCommand(string MenuId)
        {
            switch (MenuId)
            {
                case "MenuId.Parametrics.Cylinder.Radius":
                    diameter = false;
                    frame.SetAction(this); // this is the way this action comes to life
                    return true;
                case "MenuId.Parametrics.Cylinder.Diameter":
                    diameter = true;
                    frame.SetAction(this); // this is the way this action comes to life
                    return true;
            }
            return false;
        }

        public override void OnSelected(string MenuId, bool selected)
        {

        }

        public override bool OnUpdateCommand(string MenuId, CommandState CommandState)
        {
            switch (MenuId)
            {
                case "MenuId.Parametrics.Cylinder.Radius":
                    return true;
                case "MenuId.Parametrics.Cylinder.Diameter":
                    return true;
            }
            return false;
        }

        public override void OnSetAction()
        {
            base.TitleId = "Constr.Parametrics.Cylinder.Radius";
            base.ActiveObject = shell.Clone();
            shell.Layer.Transparency = 128;


            radiusInput = new LengthInput("Parametrics.Cylinder.Radius");
            radiusInput.GetLengthEvent += RadiusInput_GetLength;
            radiusInput.SetLengthEvent += RadiusInput_SetLength;
            radiusInput.Optional = diameter;

            diameterInput = new LengthInput("Parametrics.Cylinder.Diameter");
            diameterInput.GetLengthEvent += DiameterInput_GetLength;
            diameterInput.SetLengthEvent += DiameterInput_SetLength;
            diameterInput.Optional = !diameter;

            base.SetInput(radiusInput, diameterInput);
            base.OnSetAction();

            validResult = false;
        }

        private bool RadiusInput_SetLength(double length)
        {
            validResult = false;
            if (shell!=null)
            {
                Parametrics pm = new Parametrics(shell);
                if (pm.ModifyRadius(faceWithRadius, length))
                {
                    Shell sh = pm.Result(out HashSet<Face> involvedFaces);
                    if (sh != null)
                    {
                        ActiveObject = sh;
                        validResult = true;
                        return true;
                    }
                    else
                    {
                        ActiveObject = shell.Clone();
                        return false;
                    }
                }
            }
            return false;
        }

        private double RadiusInput_GetLength()
        {
            if (faceWithRadius.Surface is CylindricalSurface cyl) return cyl.RadiusX; // we only have round cylinders here
            else return 0.0;
        }
        private bool DiameterInput_SetLength(double length)
        {
            return RadiusInput_SetLength(length / 2.0);
        }

        private double DiameterInput_GetLength()
        {
            return 2.0 * RadiusInput_GetLength();
        }

        public override void OnDone()
        {
            if (validResult && ActiveObject != null)
            {
                Solid sld = shell.Owner as Solid;
                if (sld != null)
                {   // the shell was part of a Solid
                    IGeoObjectOwner owner = sld.Owner; // Model or Block
                    owner.Remove(sld);
                    Solid replacement = Solid.MakeSolid(ActiveObject as Shell);
                    owner.Add(replacement);
                }
                else
                {
                    IGeoObjectOwner owner = shell.Owner;
                    owner.Remove(shell);
                    owner.Add(ActiveObject);
                }
            }
            ActiveObject = null;
            base.OnDone();
        }
    }
}
