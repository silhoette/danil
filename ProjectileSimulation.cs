using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Симуляция броска тела под углом к горизонту.
/// Красный шар - движение без сопротивления воздуха (вакуум),
/// синий шар - с квадратичным сопротивлением воздуха.
/// Весь мир создаётся из кода: достаточно повесить скрипт на пустой объект.
/// </summary>
public class ProjectileSimulation : MonoBehaviour
{
    // Параметры, меняемые слайдерами
    float speed = 20f;     // начальная скорость, м/с
    float angle = 45f;     // угол броска, градусы
    float drag = 0.02f;    // коэффициент сопротивления k (a = -k*|v|*v)
    float gravity = 9.81f; // ускорение свободного падения, м/с^2

    class Body
    {
        public GameObject go;
        public LineRenderer trail;
        public Vector2 pos, vel;
        public bool flying;
        public float maxHeight, range, time;
        public List<Vector3> points = new List<Vector3>();
    }

    Body vacuum, air;
    Camera cam;

    void Start()
    {
        // Камера
        cam = Camera.main;
        if (cam == null)
        {
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.backgroundColor = new Color(0.55f, 0.75f, 0.95f);
        cam.clearFlags = CameraClearFlags.SolidColor;

        // Свет (на случай пустой сцены)
        if (FindObjectOfType<Light>() == null)
        {
            var l = new GameObject("Light").AddComponent<Light>();
            l.type = LightType.Directional;
            l.transform.rotation = Quaternion.Euler(40, -30, 0);
        }

        // Земля
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Ground";
        ground.transform.position = new Vector3(100, -0.5f, 0);
        ground.transform.localScale = new Vector3(400, 1, 4);
        ground.GetComponent<Renderer>().material.color = new Color(0.3f, 0.6f, 0.3f);

        vacuum = CreateBody("Vacuum", Color.red);
        air = CreateBody("Air", Color.blue);
        Launch();
    }

    Body CreateBody(string name, Color color)
    {
        var b = new Body();
        b.go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        b.go.name = name;
        Destroy(b.go.GetComponent<Collider>());
        b.go.GetComponent<Renderer>().material.color = color;

        var trailGo = new GameObject(name + "Trail");
        b.trail = trailGo.AddComponent<LineRenderer>();
        b.trail.material = new Material(Shader.Find("Sprites/Default"));
        b.trail.startColor = b.trail.endColor = color;
        b.trail.startWidth = b.trail.endWidth = 0.15f;
        b.trail.useWorldSpace = true;
        return b;
    }

    void Launch()
    {
        float a = angle * Mathf.Deg2Rad;
        Vector2 v0 = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
        foreach (var b in new[] { vacuum, air })
        {
            b.pos = new Vector2(0, 0.5f);
            b.vel = v0;
            b.flying = true;
            b.maxHeight = 0.5f;
            b.range = 0;
            b.time = 0;
            b.points.Clear();
            b.points.Add(b.pos);
            UpdateVisual(b);
        }
        FitCamera();
    }

    // Подгоняем камеру так, чтобы траектория в вакууме (самая длинная) влезла в кадр
    void FitCamera()
    {
        float a = angle * Mathf.Deg2Rad;
        float range = speed * speed * Mathf.Sin(2 * a) / gravity;
        float height = Mathf.Pow(speed * Mathf.Sin(a), 2) / (2 * gravity);
        float size = Mathf.Max(6f, (height + 3f) / 2f, range * 0.58f / cam.aspect);
        cam.orthographicSize = size;
        cam.transform.position = new Vector3(range / 2f, size - 1.5f, -20f);
        cam.transform.rotation = Quaternion.identity;
    }

    void FixedUpdate()
    {
        Step(vacuum, 0f);
        Step(air, drag);
    }

    void Step(Body b, float k)
    {
        if (!b.flying) return;
        float dt = Time.fixedDeltaTime;

        // a = g + (-k * |v| * v)
        Vector2 acc = new Vector2(0, -gravity) - k * b.vel.magnitude * b.vel;
        b.vel += acc * dt;   // полунеявный метод Эйлера
        b.pos += b.vel * dt;
        b.time += dt;
        b.maxHeight = Mathf.Max(b.maxHeight, b.pos.y);

        if (b.pos.y <= 0.5f)  // 0.5 - радиус шара
        {
            b.pos.y = 0.5f;
            b.flying = false;
            b.range = b.pos.x;
        }
        b.points.Add(b.pos);
        UpdateVisual(b);
    }

    void UpdateVisual(Body b)
    {
        b.go.transform.position = new Vector3(b.pos.x, b.pos.y, 0);
        b.trail.positionCount = b.points.Count;
        for (int i = 0; i < b.points.Count; i++)
            b.trail.SetPosition(i, b.points[i]);
    }

    void OnGUI()
    {
        float s = Screen.height / 540f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1));

        GUI.Box(new Rect(10, 10, 260, 210), "Параметры броска");
        speed = Slider("Скорость, м/с", speed, 5, 40, 40);
        angle = Slider("Угол, °", angle, 5, 85, 80);
        drag = Slider("Сопротивление k", drag, 0, 0.1f, 120);
        gravity = Slider("Гравитация g", gravity, 1, 25, 160);
        if (GUI.Button(new Rect(20, 185, 240, 28), "Запуск / Перезапуск")) Launch();

        GUI.Box(new Rect(10, 230, 260, 100), "Результаты");
        GUI.color = Color.red;
        GUI.Label(new Rect(20, 250, 250, 40),
            $"Вакуум: дальность {vacuum.range:F1} м\nмакс. высота {vacuum.maxHeight - 0.5f:F1} м");
        GUI.color = Color.blue;
        GUI.Label(new Rect(20, 285, 250, 40),
            $"С воздухом: дальность {air.range:F1} м\nмакс. высота {air.maxHeight - 0.5f:F1} м");
        GUI.color = Color.white;
    }

    float Slider(string label, float value, float min, float max, float y)
    {
        GUI.Label(new Rect(20, y - 2, 240, 20), $"{label}: {value:F2}");
        return GUI.HorizontalSlider(new Rect(20, y + 18, 240, 15), value, min, max);
    }
}
