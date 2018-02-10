using System;
using System.IO;
using System.Windows.Forms;
using WW.Cad.Model;
using WW.Cad.Model.Entities;
using WW.Math;
using WW.Cad.IO;

namespace WindowsFormsApp1
{
    public partial class Form1 : Form
    {
        string filename;
        public Form1()
        {
            InitializeComponent();
        }
        private void viewControl_EntitySelected(object sender, EntityEventArgs e)
        {
            propertyGrid.SelectedObject = e.Entity;
        }
        private void viewControl_MouseMove(object sender, MouseEventArgs e)
        {
            Point2D point = viewControl.GetModelSpaceCoordinates(new Point2D(e.X, e.Y));
            coordinatesLabel.Text = string.Format("{0:0.000}, {1:0.000}", point.X, point.Y);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "AutoCad files (*.dwg, *.dxf)|*.dxf;*.dwg";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    filename = dialog.FileName;
                    openfilename.Text = Path.GetFileName(filename).ToString();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error occurred: " + ex.Message);
                    Environment.Exit(1);
                }
            }
            else
            {
                Environment.Exit(0);
            }

            DxfModel model;
            string extension = Path.GetExtension(filename);
            if (string.Compare(extension, ".dwg", true) == 0)
            {
                model = DwgReader.Read(filename);
            }
            else
            {
                model = DxfReader.Read(filename);
            }
            viewControl.Model = model;
        }
    }
}
