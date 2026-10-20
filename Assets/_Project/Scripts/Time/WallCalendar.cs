using System.Collections.Generic;
using GrandmasHouse.Data;
using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // The paper calendar on the kitchen wall. It shows this month's page:
    // the month's name on a colored band, a box for each day, the days gone by
    // crossed off and today ringed in red. It turns the page as the days pass.
    // The months and their lengths are in Data/CalendarSettings.
    public class WallCalendar : MonoBehaviour
    {
        [SerializeField] DayClock clock;
        [SerializeField] CalendarSettings settings;
        [Tooltip("The page's size on the wall, in meters.")]
        [SerializeField] Vector2 pageSize = new Vector2(0.42f, 0.56f);
        [SerializeField] int columns = 6;
        [SerializeField] Material paper;
        [SerializeField] Material ink;
        [SerializeField] Material red;
        [SerializeField] Material band;
        [SerializeField] Font font;

        public int Month { get; private set; } = -1;
        public int DayOfMonth { get; private set; }
        public int Year { get; private set; }
        public string MonthName => Month >= 0 ? settings.months[Month].name : "";

        Transform page;
        Renderer bandRenderer;
        TextMesh title;
        readonly List<Transform> crosses = new List<Transform>();
        Transform ring;
        int shownMonth = -1;
        int shownDay = -1;
        MaterialPropertyBlock block;

        void Start()
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            block = new MaterialPropertyBlock();
            Show();
        }

        // A new day however it came: sleeping, a loaded save, catching up to the host.
        void Update()
        {
            if (clock.Day != shownDay) Show();
        }

        // Brings the page up to today.
        public void Show()
        {
            if (block == null) return;
            shownDay = clock.Day;
            (int month, int dayOfMonth, int year) = settings.DateOf(clock.Day);
            Month = month;
            DayOfMonth = dayOfMonth;
            Year = year;
            int days = Mathf.Max(1, settings.months[month].days);
            if (month != shownMonth || page == null)
            {
                BuildPage(days);
                shownMonth = month;
            }
            for (int i = 0; i < crosses.Count; i++) crosses[i].gameObject.SetActive(i + 1 < dayOfMonth);
            ring.localPosition = CellCenter(dayOfMonth - 1, days) + Vector3.back * 0.002f;
        }

        // ---- The page ----

        float Header => pageSize.y * 0.2f;
        int Rows(int days) => Mathf.CeilToInt(days / (float)columns);
        Vector2 Cell(int days) => new Vector2(pageSize.x * 0.92f / columns, (pageSize.y - Header) * 0.94f / Rows(days));

        // Local position of a day's box, from the page's top left; the page faces -z.
        Vector3 CellCenter(int index, int days)
        {
            Vector2 cell = Cell(days);
            float left = -pageSize.x * 0.46f;
            float top = pageSize.y / 2f - Header - (pageSize.y - Header) * 0.03f;
            int col = index % columns, row = index / columns;
            return new Vector3(left + cell.x * (col + 0.5f), top - cell.y * (row + 0.5f), 0f);
        }

        void BuildPage(int days)
        {
            if (page != null)
            {
                // Torn off: gone from sight now, cleaned up at the end of the frame.
                page.gameObject.SetActive(false);
                Destroy(page.gameObject);
            }
            crosses.Clear();
            page = new GameObject("Page").transform;
            page.SetParent(transform, false);

            Quad("Paper", page, Vector3.zero, pageSize, paper, 0f);
            bandRenderer = Quad("Band", page, new Vector3(0f, pageSize.y / 2f - Header / 2f, -0.001f), new Vector2(pageSize.x, Header), band, 0f);
            bandRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", settings.months[Month].color);
            bandRenderer.SetPropertyBlock(block);
            title = Text("Month", page, new Vector3(0f, pageSize.y / 2f - Header / 2f, -0.002f), settings.months[Month].name, Header * 0.55f, Color.white);

            Vector2 cell = Cell(days);
            for (int i = 0; i < days; i++)
            {
                Vector3 center = CellCenter(i, days);
                // Box outline: four thin lines.
                float t = 0.0015f;
                Quad("Line", page, center + new Vector3(0f, cell.y / 2f, -0.001f), new Vector2(cell.x, t), ink, 0f);
                Quad("Line", page, center + new Vector3(0f, -cell.y / 2f, -0.001f), new Vector2(cell.x, t), ink, 0f);
                Quad("Line", page, center + new Vector3(cell.x / 2f, 0f, -0.001f), new Vector2(t, cell.y), ink, 0f);
                Quad("Line", page, center + new Vector3(-cell.x / 2f, 0f, -0.001f), new Vector2(t, cell.y), ink, 0f);
                Text("Day", page, center + new Vector3(cell.x * 0.22f, cell.y * 0.22f, -0.0015f), (i + 1).ToString(), cell.y * 0.36f, Color.black);
                // Crossed off in pencil once it's gone by.
                var cross = new GameObject("Cross").transform;
                cross.SetParent(page, false);
                cross.localPosition = center + Vector3.back * 0.002f;
                float diagonal = Mathf.Sqrt(cell.x * cell.x + cell.y * cell.y) * 0.8f;
                float angle = Mathf.Atan2(cell.y, cell.x) * Mathf.Rad2Deg;
                Quad("Stroke", cross, Vector3.zero, new Vector2(diagonal, 0.002f), ink, angle);
                Quad("Stroke", cross, Vector3.zero, new Vector2(diagonal, 0.002f), ink, -angle);
                crosses.Add(cross);
            }

            // Today, ringed in red.
            ring = new GameObject("Today").transform;
            ring.SetParent(page, false);
            float r = 0.004f;
            Quad("Ring", ring, new Vector3(0f, cell.y * 0.42f, 0f), new Vector2(cell.x * 0.86f, r), red, 0f);
            Quad("Ring", ring, new Vector3(0f, -cell.y * 0.42f, 0f), new Vector2(cell.x * 0.86f, r), red, 0f);
            Quad("Ring", ring, new Vector3(cell.x * 0.42f, 0f, 0f), new Vector2(r, cell.y * 0.86f), red, 0f);
            Quad("Ring", ring, new Vector3(-cell.x * 0.42f, 0f, 0f), new Vector2(r, cell.y * 0.86f), red, 0f);
        }

        static Renderer Quad(string name, Transform parent, Vector3 position, Vector2 size, Material material, float angle)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.transform.localPosition = position;
            // Quads face -z already, which is how the page faces.
            quad.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            quad.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return renderer;
        }

        TextMesh Text(string name, Transform parent, Vector3 position, string words, float height, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var text = go.AddComponent<TextMesh>();
            text.font = font;
            text.text = words;
            text.fontSize = 64;
            // A line of 64-point text is about 6.4 units tall at character size 1.
            text.characterSize = height / 6.4f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = font.material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return text;
        }
    }
}
