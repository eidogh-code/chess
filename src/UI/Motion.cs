using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace Chess.UI
{
    /// <summary>Пружина: плавно и с лёгким «перелётом» тянет значение к цели (expressive spring).</summary>
    sealed class Spring
    {
        public float Value, Velocity, Target;
        readonly float stiffness, damping;

        public Spring(float value, float stiffness, float damping)
        {
            Value = Target = value;
            this.stiffness = stiffness;
            this.damping = damping;
        }

        /// <summary>Быстрая пружина с небольшим перелётом — для формы и масштаба.</summary>
        public static Spring Bouncy(float value) { return new Spring(value, 700f, 22f); }

        /// <summary>Пружина без перелёта — для цвета и прозрачности.</summary>
        public static Spring Smooth(float value) { return new Spring(value, 400f, 40f); }

        public bool Settled { get { return Math.Abs(Target - Value) < 0.002f && Math.Abs(Velocity) < 0.01f; } }

        // Мелкие подшаги держат явную схему интегрирования устойчивой при жёсткой пружине.
        const float SubStep = 1f / 480f;

        public void Step(float dt)
        {
            while (dt > 0)
            {
                float h = Math.Min(dt, SubStep);
                float force = (Target - Value) * stiffness - Velocity * damping;
                Velocity += force * h;
                Value += Velocity * h;
                dt -= h;
            }
            if (Settled) { Value = Target; Velocity = 0; }
        }

        public void Snap(float v) { Value = Target = v; Velocity = 0; }
    }

    static class Easing
    {
        /// <summary>Кубическая кривая Безье (как CSS cubic-bezier).</summary>
        public static float CubicBezier(float x1, float y1, float x2, float y2, float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            float u = t;
            for (int i = 0; i < 8; i++)
            {
                float x = Bez(x1, x2, u) - t;
                float dx = BezDeriv(x1, x2, u);
                if (Math.Abs(x) < 1e-4f || Math.Abs(dx) < 1e-6f) break;
                u -= x / dx;
            }
            return Bez(y1, y2, Math.Max(0, Math.Min(1, u)));
        }

        static float Bez(float a, float b, float u) { return 3 * a * u * (1 - u) * (1 - u) + 3 * b * u * u * (1 - u) + u * u * u; }
        static float BezDeriv(float a, float b, float u) { return 3 * a * (1 - u) * (1 - u) + 6 * (b - a) * u * (1 - u) + 3 * (1 - b) * u * u; }

        // Токены движения Material 3.
        public static float EmphasizedDecelerate(float t) { return CubicBezier(0.05f, 0.7f, 0.1f, 1f, t); }
        public static float Standard(float t) { return CubicBezier(0.2f, 0f, 0f, 1f, t); }
    }

    /// <summary>Общий таймер кадров (~60 к/с), работает, только пока кто-то анимируется.</summary>
    static class FrameClock
    {
        const int FrameMs = 15;
        const float MaxDt = 0.05f;
        static readonly Timer timer = new Timer { Interval = FrameMs };
        static readonly List<Func<float, bool>> subscribers = new List<Func<float, bool>>();
        static readonly Stopwatch watch = new Stopwatch();

        static FrameClock() { timer.Tick += OnTick; }

        /// <summary>Подписывает шаг анимации; шаг возвращает false, когда закончил.</summary>
        public static void Run(Func<float, bool> step)
        {
            if (!subscribers.Contains(step)) subscribers.Add(step);
            if (!timer.Enabled) { watch.Restart(); timer.Start(); }
        }

        static void OnTick(object sender, EventArgs e)
        {
            float dt = Math.Min(MaxDt, (float)watch.Elapsed.TotalSeconds);
            watch.Restart();
            foreach (var s in subscribers.ToArray())
                if (!s(dt)) subscribers.Remove(s);
            if (subscribers.Count == 0) timer.Stop();
        }
    }
}
