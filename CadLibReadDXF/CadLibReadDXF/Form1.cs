using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;

using WW.Cad.Base;
using WW.Cad.Drawing.GDI;
using WW.Cad.IO;
using WW.Cad.Model;
using WW.Math;
using WW.Cad.Drawing;
//using DevExpress.XtraTab;

namespace CadLibReadDXF
{
    public partial class Form1 : Form
    {
        private Matrix4D modelTransform = Matrix4D.Identity;
        private GDIGraphics3D gdiGraphics3D;
        private DxfModel model;//定义DxfModel对象  
        private Bounds3D bounds;
        private string filename;
        public Form1()
        {
            InitializeComponent();
        }

        private void CalculateTo2DTransform()
        {
            if (bounds != null)
            {
                Matrix4D to2DTransform = DxfUtil.GetScaleTransform(
                    bounds.Corner1,
                    bounds.Corner2,
                    bounds.Center,
                    new Point3D(0d, ClientSize.Height, 0d),
                    new Point3D(ClientSize.Width, 0d, 0d),
                    new Point3D(ClientSize.Width / 2, ClientSize.Height / 2, 0d)
                );
                gdiGraphics3D.To2DTransform = to2DTransform * modelTransform;
            }
        }
        private void OpenDxfFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "AutoCad files (*.dwg, *.dxf)|*.dxf;*.dwg";
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                filename = dialog.FileName;
            }
            if (!string.IsNullOrEmpty(filename))
            {

                //this.xtraTabPage1.BackColor = System.Drawing.Color.Black;
                try
                {
                    //通过文件扩展名判断CAD文件是dwg格式还是dxf格式  
                    string extension = Path.GetExtension(filename);
                    if (string.Compare(extension, ".dwg", true) == 0)
                    {
                        model = DwgReader.Read(filename);
                    }
                    else
                    {
                        model = DxfReader.Read(filename);
                    }
                    //将控件的标签添加上文件名  
                    this.xtraTabPage1.Text = "二维仿真(" + Path.GetFileName(filename) + ")";
                    //设置控件背景为黑色  
                    this.xtraTabPage1.BackColor = System.Drawing.Color.Black;

                    //使用GDIGraphics3D绘制CAD文件的方法  
                    //创建中间可绘制对象  
                    gdiGraphics3D = new GDIGraphics3D(GraphicsConfig.BlackBackgroundCorrectForBackColor);
                    gdiGraphics3D.CreateDrawables(model);
                    //获得bounding box  
                    bounds = new Bounds3D();
                    gdiGraphics3D.BoundingBox(bounds, modelTransform);
                    //计算GDIGraphics3D的属性To2DTransform  
                    CalculateTo2DTransform();
                    //响应控件的Paint事件，画CAD文件  

                }
                catch (Exception ex)
                {
                    MessageBox.Show("文件有错！请用AutoCad打开，通过“文件-核查”尝试修复。错误信息：" + ex.Message);
                }
            }
        }
        //xtraTabPage3控件的Paint事件，画CAD文件  
        private void xtraTabPage1_Resize(object sender, EventArgs e)
        {
            base.OnResize(e);
            CalculateTo2DTransform();
            this.xtraTabPage1.Invalidate();
        }

        private void xtraTabPage1_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            if (gdiGraphics3D != null)
            {
                gdiGraphics3D.Draw(e.Graphics, xtraTabPage1.ClientRectangle);
            }
        }
    }
}
