using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Input;
using System.Windows.Forms;
using WW.Actions;
using WW.Cad.Drawing;
using WW.Cad.Drawing.GDI;
using WW.Cad.Model;
using WW.Cad.Model.Entities;
using WW.Math;
using WW.Drawing;
using WW.Math.Geometry;
using WW.Windows;
using WindowsFormsApp1;

namespace WindowsFormsApp1
{
    public partial class ViewControl : UserControl
    {
        private DxfModel model;
        private GDIGraphics3D gdiGraphics3D;
        private WireframeGraphicsCache graphicsCache;
        private GraphicsHelper graphicsHelper;
        private Bounds3D bounds;
        private Matrix4D from2DTransform;
        private Point mouseClickLocation;
        private bool mouseDown;
        private bool shiftPressed;
        private RenderedEntityInfo highlightedEntity;
        private ArgbColor highlightColor = ArgbColors.Magenta;
        private ArgbColor secondaryHighlightColor = ArgbColors.Cyan;

        #region zooming and panning
        private SimpleTransformationProvider3D transformationProvider;
        private SimplePanInteractor panInteractor;
        private SimpleRectZoomInteractor rectZoomInteractor;
        private SimpleZoomWheelInteractor zoomWheelInteractor;
        private IInteractorWinFormsDrawable rectZoomInteractorDrawable;
        private IInteractorWinFormsDrawable currentInteractorDrawable;
        #endregion

        public event EventHandler<EntityEventArgs> EntitySelected;

        public ViewControl()
        {
            InitializeComponent();
            //控制控件闪烁
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);//禁止擦除背景
            SetStyle(ControlStyles.DoubleBuffer, true);//双缓冲
            SetStyle(ControlStyles.UserPaint, true);//自行绘制图片
            GraphicsConfig graphicsConfig = new GraphicsConfig();
            graphicsConfig.BackColor = BackColor;
            graphicsConfig.CorrectColorForBackgroundColor = true;
            gdiGraphics3D = new GDIGraphics3D(graphicsConfig);
            gdiGraphics3D.EnableDrawablesUpdate();
            graphicsCache = new WireframeGraphicsCache(false, true);
            graphicsCache.Config = graphicsConfig;
            graphicsHelper = new GraphicsHelper(System.Drawing.Color.Blue);
            bounds = new Bounds3D();

            transformationProvider = new SimpleTransformationProvider3D();
            transformationProvider.TransformsChanged += new EventHandler(transformationProvider_TransformsChanged);
            panInteractor = new SimplePanInteractor(transformationProvider);
            rectZoomInteractor = new SimpleRectZoomInteractor(transformationProvider);
            zoomWheelInteractor = new SimpleZoomWheelInteractor(transformationProvider);
            rectZoomInteractorDrawable = new SimpleRectZoomInteractor.WinFormsDrawable(rectZoomInteractor);
        }

        public DxfModel Model
        {
            get
            {
                return model;
            }
            set
            {
                model = value;
                if (model != null)
                {
                    graphicsCache.CreateDrawables(model);
                    gdiGraphics3D.Clear();
                    graphicsCache.Draw(gdiGraphics3D.CreateGraphicsFactory());
                    gdiGraphics3D.BoundingBox(bounds, Matrix4D.Identity);
                    transformationProvider.ResetTransforms(bounds);
                    CalculateTo2DTransform();
                    Invalidate();

                }
            }
        }

        public Point2D GetModelSpaceCoordinates(Point2D screenSpaceCoordinates)
        {
            return from2DTransform.TransformTo2D(screenSpaceCoordinates);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics gr = e.Graphics;
            gdiGraphics3D.Draw(e.Graphics, ClientRectangle);
            if (currentInteractorDrawable != null)
            {
                InteractionContext context =
                    new InteractionContext(
                        GetClientRectangle2D(),
                        transformationProvider.CompleteTransform,
                        true,
                        new ArgbColor(BackColor.ToArgb())
                        );
                currentInteractorDrawable.Draw(e, graphicsHelper, context);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            // Be careful to check that the control was already initialized, 
            // in some cases the InitializeComponent call triggers an OnResize call.
            if (transformationProvider != null)
            {
                CalculateTo2DTransform();
                Invalidate();
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            mouseClickLocation = e.Location;
            mouseDown = true;
            shiftPressed = ModifierKeys == Keys.Shift;
            if (shiftPressed)
            {
                rectZoomInteractor.Activate();
                rectZoomInteractor.ProcessMouseButtonDown(new CanonicalMouseEventArgs(e),GetInteractionContext());
                currentInteractorDrawable = rectZoomInteractorDrawable;
            }
            else
            {
                panInteractor.Activate();
                panInteractor.ProcessMouseButtonDown(new CanonicalMouseEventArgs(e), GetInteractionContext());
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (mouseDown == true)
            {
                if (shiftPressed)
                {
                    rectZoomInteractor.ProcessMouseMove(new CanonicalMouseEventArgs(e), GetInteractionContext());
                }
                else
                {
                    panInteractor.ProcessMouseMove(new CanonicalMouseEventArgs(e), GetInteractionContext());
                }
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            mouseDown = false;

            //使用shift键放大缩小
            if (shiftPressed)
            {
                rectZoomInteractor.ProcessMouseButtonUp(new CanonicalMouseEventArgs(e), GetInteractionContext());
                rectZoomInteractor.Deactivate();
                Invalidate();
            }
            else
            {
                panInteractor.Deactivate();
                // Select entity at mouse location if mouse didn't move
                // and show entity in property grid.

            if(mouseClickLocation == e.Location)
                {
                    Point2D referencePoint = new Point2D(e.X, e.Y);
                    double distance;
                    IList<RenderedEntityInfo> closestEntities =
                        EntitySelector.GetClosestEntities(
                            model,
                            GraphicsConfig.BlackBackgroundCorrectForBackColor,
                            gdiGraphics3D.To2DTransform,
                            referencePoint,
                            out distance
                            );
                    if (highlightedEntity != null)
                    {
                        IList<IWireframeDrawable> drawables = graphicsCache.GetDrawables(highlightedEntity);
                        IWireframeGraphicsFactory graphicsFactory = null;
                        gdiGraphics3D.UpdateDrawables(
                            highlightedEntity,
                            () => {
                                foreach (IWireframeDrawable drawable in drawables)
                                {
                                    drawable.Draw(graphicsFactory);
                                }
                            },
                            o => graphicsFactory = o
                        );
                        Invalidate();
                        highlightedEntity = null;
                    }
                    if (closestEntities.Count > 0)
                    {
                        // Chose the last entity as it is drawn last, so will be on top.
                        highlightedEntity = closestEntities[closestEntities.Count - 1];
                        IList<IWireframeDrawable> drawables = graphicsCache.GetDrawables(highlightedEntity);
                        WireframeGraphicsFactoryColorChanger graphicsFactoryColorChanger = null;
                        gdiGraphics3D.UpdateDrawables(
                            highlightedEntity,
                            () => {
                                foreach (IWireframeDrawable drawable in drawables)
                                {
                                    drawable.Draw(graphicsFactoryColorChanger);
                                }
                            },
                            o => graphicsFactoryColorChanger = new WireframeGraphicsFactoryColorChanger(o, ColorChanger)
                        );
                        Invalidate();
                        DxfEntity entity = highlightedEntity.Entity;
                        OnEntitySelected(new EntityEventArgs(entity));
                    }
                }
            }
            currentInteractorDrawable = null;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);

            zoomWheelInteractor.Activate();
            zoomWheelInteractor.ProcessMouseWheel(new CanonicalMouseEventArgs(e), GetInteractionContext());
            zoomWheelInteractor.Deactivate();

            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            Key key;
            ModifierKeys modifierKeys;
            InputUtil.GetWindowsKey(keyData, out key, out modifierKeys);
            IInteractor interactor = new ShowPositionInteractor();
            Point p = PointToClient(MousePosition);
            bool handled = interactor.ProcessKeyDown(
                new CanonicalMouseEventArgs(new MouseEventArgs(MouseButtons.None, 0, p.X, p.Y, 0)),
                key,
                modifierKeys,
                GetInteractionContext()
            );
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected virtual void OnEntitySelected(EntityEventArgs e)
        {
            if (EntitySelected != null)
            {
                EntitySelected(this, e);
            }
        }

        private Matrix4D CalculateTo2DTransform()
        {
            transformationProvider.ViewWindow = GetClientRectangle2D();
            Matrix4D to2DTransform = Matrix4D.Identity;
            if (model != null && bounds != null)
            {
                to2DTransform = transformationProvider.CompleteTransform;
            }
            gdiGraphics3D.To2DTransform = to2DTransform;
            from2DTransform = gdiGraphics3D.To2DTransform.GetInverse();
            return to2DTransform;
        }

        private Rectangle2D GetClientRectangle2D() => new Rectangle2D(
                ClientRectangle.Left,
                ClientRectangle.Top,
                ClientRectangle.Right,
                ClientRectangle.Bottom
                );
        private void transformationProvider_TransformsChanged(object sender, EventArgs e)
        {
            CalculateTo2DTransform();
            Invalidate();
        }
        private InteractionContext GetInteractionContext()
        {
            return new InteractionContext(
                new Rectangle2D(
                ClientRectangle.Left,
                ClientRectangle.Top,
                ClientRectangle.Right,
                ClientRectangle.Bottom),
                transformationProvider.CompleteTransform,
                true,
                BackColor
                );
        }

        private  ArgbColor ColorChanger(ArgbColor argbColor)
        {
            ArgbColor result = highlightColor;
            if (argbColor == result)
            {
                result = secondaryHighlightColor;
            }
            return result;
        }
    }
}
