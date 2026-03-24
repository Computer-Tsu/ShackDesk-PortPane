using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace PortPane.Views;

/// <summary>
/// Easter egg animations — triggered by double-clicking the app logo in the About dialog.
/// Pure procedural C# drawing. Zero image assets. Zero binary size impact.
/// 5 animations chosen at random. Each runs for 5 seconds then stops automatically.
/// Total code kept under 200 lines per spec.
/// </summary>
public sealed class EasterEggControl : Canvas
{
    private enum Anim { Fireworks, Starfield, MatrixRain, PlasmaWave, Bouncing }

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private Anim         _current;
    private int          _tick;
    private readonly Random _rng = new();
    private readonly List<double[]> _particles = [];

    public EasterEggControl()
    {
        Width  = 240;
        Height = 120;
        Background = Brushes.Black;
        ClipToBounds = true;
        _timer.Tick += OnTick;
    }

    public void Trigger()
    {
        Stop();
        _current = (Anim)_rng.Next(5);
        _tick    = 0;
        _particles.Clear();
        Init();
        _timer.Start();
        // Auto-stop after 5 seconds
        _ = Task.Delay(5000).ContinueWith(_ => Dispatcher.Invoke(Stop));
    }

    private void Stop() { _timer.Stop(); Children.Clear(); }

    private void Init()
    {
        switch (_current)
        {
            case Anim.Fireworks:
                for (int i = 0; i < 40; i++)
                    _particles.Add([_rng.NextDouble() * Width, Height,
                        (_rng.NextDouble() - .5) * 4, -_rng.NextDouble() * 6 - 2,
                        _rng.NextDouble() * 360]);
                break;
            case Anim.Starfield:
                for (int i = 0; i < 80; i++)
                    _particles.Add([_rng.NextDouble() * Width, _rng.NextDouble() * Height,
                        (_rng.NextDouble() - .5) * 3, (_rng.NextDouble() - .5) * 3]);
                break;
            case Anim.MatrixRain:
                for (int col = 0; col < (int)(Width / 12); col++)
                    _particles.Add([col * 12, _rng.NextDouble() * -Height,
                        _rng.NextDouble() * 3 + 2]);
                break;
            case Anim.PlasmaWave:
            case Anim.Bouncing:
                for (int i = 0; i < 12; i++)
                    _particles.Add([_rng.NextDouble() * Width, _rng.NextDouble() * Height,
                        (_rng.NextDouble() - .5) * 5, (_rng.NextDouble() - .5) * 5,
                        _rng.NextDouble() * 8 + 4, _rng.NextDouble() * 360]);
                break;
        }
    }

    private void OnTick(object? s, EventArgs e)
    {
        Children.Clear();
        _tick++;
        switch (_current)
        {
            case Anim.Fireworks:  DrawFireworks();  break;
            case Anim.Starfield:  DrawStarfield();  break;
            case Anim.MatrixRain: DrawMatrixRain(); break;
            case Anim.PlasmaWave: DrawPlasma();     break;
            case Anim.Bouncing:   DrawBouncing();   break;
        }
    }

    private void DrawFireworks()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            double[] p = _particles[i];
            p[0] += p[2]; p[1] += p[3]; p[3] += 0.15; // gravity
            double hue = (p[4] + _tick * 2) % 360;
            var el = Dot(p[0], p[1], 3, HsvBrush(hue, 1, 1));
            Children.Add(el);
            if (p[1] > Height) { p[1] = Height; p[0] = _rng.NextDouble() * Width;
                p[2] = (_rng.NextDouble()-.5)*4; p[3] = -_rng.NextDouble()*6-2; }
        }
    }

    private void DrawStarfield()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            double[] p = _particles[i];
            double speed = 1 + _tick * 0.02;
            p[0] += p[2] * speed; p[1] += p[3] * speed;
            if (p[0] < 0) p[0] = Width; if (p[0] > Width) p[0] = 0;
            if (p[1] < 0) p[1] = Height; if (p[1] > Height) p[1] = 0;
            double brightness = Math.Clamp(speed * 0.3, 0.1, 1.0);
            Children.Add(Dot(p[0], p[1], 2, new SolidColorBrush(
                Color.FromRgb(255, 255, (byte)(200 * brightness)))));
        }
    }

    private void DrawMatrixRain()
    {
        string chars = "01アイウエオカキクケコ";
        for (int i = 0; i < _particles.Count; i++)
        {
            double[] p = _particles[i];
            p[1] += p[2];
            if (p[1] > Height + 20) p[1] = _rng.NextDouble() * -40;
            for (int row = 0; row < (int)(Height / 14) + 1; row++)
            {
                double y = p[1] - row * 14;
                if (y < 0 || y > Height) continue;
                byte g = (byte)(255 - row * 30);
                var tb = new TextBlock
                {
                    Text       = chars[_rng.Next(chars.Length)].ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(0, (byte)Math.Max(g, 0), 0)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize   = 11
                };
                SetLeft(tb, p[0]); SetTop(tb, y);
                Children.Add(tb);
            }
        }
    }

    private void DrawPlasma()
    {
        double t = _tick * 0.1;
        for (int x = 0; x < (int)Width; x += 8)
        {
            for (int y = 0; y < (int)Height; y += 8)
            {
                double v = Math.Sin(x * 0.1 + t) + Math.Sin(y * 0.1 + t)
                         + Math.Sin((x + y) * 0.1 + t);
                double hue = (v + 3) / 6 * 360;
                Children.Add(Dot(x, y, 4, HsvBrush(hue, 0.9, 0.9)));
            }
        }
    }

    private void DrawBouncing()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            double[] p = _particles[i];
            p[0] += p[2]; p[1] += p[3];
            if (p[0] < 0 || p[0] > Width)  p[2] = -p[2];
            if (p[1] < 0 || p[1] > Height) p[3] = -p[3];
            p[5] = (p[5] + 2) % 360;
            double r = p[4];
            Children.Add(Dot(p[0], p[1], r, HsvBrush(p[5], 1, 1)));
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Ellipse Dot(double x, double y, double r, Brush fill)
    {
        var el = new Ellipse { Width = r * 2, Height = r * 2, Fill = fill };
        SetLeft(el, x - r); SetTop(el, y - r);
        return el;
    }

    private static SolidColorBrush HsvBrush(double h, double s, double v)
    {
        h %= 360; if (h < 0) h += 360;
        double c = v * s, x = c * (1 - Math.Abs(h / 60 % 2 - 1)), m = v - c;
        double r, g, b;
        (r, g, b) = h switch
        {
            < 60  => (c, x, 0.0), < 120 => (x, c, 0.0), < 180 => (0.0, c, x),
            < 240 => (0.0, x, c), < 300 => (x, 0.0, c), _     => (c, 0.0, x)
        };
        return new SolidColorBrush(Color.FromRgb((byte)((r + m) * 255),
            (byte)((g + m) * 255), (byte)((b + m) * 255)));
    }
}
