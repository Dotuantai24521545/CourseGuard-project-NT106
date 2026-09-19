using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace CourseGuard.Frontend.UserControls.Shared
{
    public partial class UC_Dashboard : UserControl
    {
        public UC_Dashboard()
        {
            InitializeComponent();
        }

        public virtual void LoadData()
        {
            // Base implementation can be empty or log
        }
    }
}
