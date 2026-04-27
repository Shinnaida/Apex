namespace Peak;

using System.Collections.Generic;
using Microsoft.Maui.Graphics;

public class MiniAreaChart : GraphicsView
{
    private static readonly double[] DefaultValues = { 0.08, 0.08, 0.11, 0.16, 0.22, 0.35 };

    public static readonly BindableProperty AreaColorProperty =
        BindableProperty.Create(nameof(AreaColor), typeof(Color), typeof(MiniAreaChart), Colors.LightBlue, propertyChanged: Redraw);

    public static readonly BindableProperty LineColorProperty =
        BindableProperty.Create(nameof(LineColor), typeof(Color), typeof(MiniAreaChart), Colors.DeepSkyBlue, propertyChanged: Redraw);

    public static readonly BindableProperty ValuesProperty =
        BindableProperty.Create(nameof(Values), typeof(IList<double>), typeof(MiniAreaChart), null, propertyChanged: Redraw);

    public Color AreaColor
    {
        get => (Color)GetValue(AreaColorProperty);
        set => SetValue(AreaColorProperty, value);
    }

    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public IList<double>? Values
    {
        get => (IList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public MiniAreaChart()
    {
        Drawable = new MiniAreaDrawable(this);
    }

    static void Redraw(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MiniAreaChart view)
        {
            view.Invalidate();
        }
    }

    private sealed class MiniAreaDrawable : IDrawable
    {
        private readonly MiniAreaChart _view;

        public MiniAreaDrawable(MiniAreaChart view)
        {
            _view = view;
        }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var values = _view.Values;
            if (values is null || values.Count < 2)
            {
                values = DefaultValues;
            }

            float width = dirtyRect.Width;
            float height = dirtyRect.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            float baselineY = height * 0.92f;
            float topY = height * 0.14f;
            float usableHeight = Math.Max(1f, baselineY - topY);
            float stepX = values.Count > 1 ? width / (values.Count - 1) : width;

            var points = new PointF[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                var clamped = Math.Clamp(values[i], 0, 1);
                float x = i * stepX;
                float y = baselineY - (float)(clamped * usableHeight);
                points[i] = new PointF(x, y);
            }

            var fillPath = new PathF();
            fillPath.MoveTo(0, baselineY);
            fillPath.LineTo(points[0]);
            for (int i = 1; i < points.Length; i++)
            {
                fillPath.LineTo(points[i]);
            }

            fillPath.LineTo(width, baselineY);
            fillPath.Close();

            canvas.SaveState();
            canvas.SetFillPaint(
                new LinearGradientPaint
                {
                    StartPoint = new PointF(0, 0),
                    EndPoint = new PointF(0, height),
                    GradientStops = new PaintGradientStop[]
                    {
                        new PaintGradientStop(0f, _view.AreaColor.WithAlpha(0.62f)),
                        new PaintGradientStop(1f, _view.AreaColor.WithAlpha(0.08f))
                    }
                },
                dirtyRect);
            canvas.FillPath(fillPath);
            canvas.RestoreState();

            canvas.StrokeColor = _view.LineColor;
            canvas.StrokeSize = 2.5f;
            for (int i = 1; i < points.Length; i++)
            {
                canvas.DrawLine(points[i - 1], points[i]);
            }

            canvas.StrokeColor = _view.LineColor.WithAlpha(0.28f);
            canvas.StrokeSize = 1.5f;
            canvas.DrawLine(0, baselineY, width, baselineY);
        }
    }
}
