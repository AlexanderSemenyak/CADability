﻿using CADability.Shapes;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CADability.App
{
    public partial class ChildMdiForm : Form
    {
        public ChildMdiForm()
        {
            InitializeComponent();
            cadControl1.CreateMainMenu = true;
        }

        private void ChildMdiForm_Load(object sender, EventArgs e)
        {
            
            
            Project newProj = Project.CreateSimpleProject();            
            cadControl1.CadFrame.Project = newProj; 
        }
    }
}