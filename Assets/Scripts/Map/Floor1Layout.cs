using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class Floor1Layout : MonoBehaviour
{
    [SerializeField] float wallHeight = 3.2f;
    [SerializeField] float wallThickness = 0.28f;
    [SerializeField] Material wallMaterial;
    [SerializeField] Material floorMaterial;

    Material _wallMat;
    Material _labelMat;

    struct Opening
    {
        public char side; // N S E W
        public float from;
        public float to;
        public Opening(char side, float from, float to)
        {
            this.side = side;
            this.from = from;
            this.to = to;
        }
    }

    struct Room
    {
        public string name;
        public float xMin, zMin, xMax, zMax;
        public Color floor;
        public Opening[] doors;
    }

    void OnEnable()
    {
        if (transform.childCount == 0)
            Build();
    }

    [ContextMenu("Rebuild Floor 1")]
    public void Build()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        _wallMat = wallMaterial != null ? wallMaterial : MakeMat(new Color(0.32f, 0.34f, 0.38f));
        _labelMat = MakeMat(Color.white);

        foreach (var room in Rooms())
        {
            BuildRoom(room);
        }
    }

    static Room[] Rooms()
    {
        var hall = new Color(0.18f, 0.28f, 0.36f);
        var service = new Color(0.22f, 0.22f, 0.26f);
        var living = new Color(0.16f, 0.24f, 0.34f);
        var garage = new Color(0.20f, 0.20f, 0.22f);
        var kitchen = new Color(0.24f, 0.22f, 0.20f);

        return new[]
        {
            R("쓰레기창고", 0, 16, 4, 24, service,
                new Opening('E', 18.4f, 20.4f)),
            R("사보세도", 4, 26, 12, 31, service,
                new Opening('S', 6.6f, 8.6f)),
            R("공연장", 4, 9, 24, 26, hall,
                new Opening('W', 18.4f, 20.4f),
                new Opening('N', 6.6f, 8.6f),
                new Opening('S', 6.8f, 8.8f),
                new Opening('S', 16.2f, 18.2f),
                new Opening('E', 10.6f, 14.0f),
                new Opening('E', 18.0f, 21.0f)),
            R("영로", 4, 0, 12, 9, service,
                new Opening('N', 6.8f, 8.8f),
                new Opening('E', 3.2f, 5.2f)),
            R("탈의실", 12, 0, 24, 9, service,
                new Opening('N', 16.2f, 18.2f),
                new Opening('W', 3.2f, 5.2f)),
            R("_홀연결", 24, 9, 34, 16, hall,
                new Opening('W', 10.6f, 14.0f),
                new Opening('N', 26.6f, 29.2f),
                new Opening('E', 11.2f, 14.0f)),
            R("바", 24, 16, 34, 26, living,
                new Opening('W', 18.0f, 21.0f),
                new Opening('S', 26.6f, 29.2f),
                new Opening('E', 21.0f, 23.6f),
                new Opening('N', 26.2f, 28.4f)),
            R("중앙캐비닛", 24, 26, 32, 31, service,
                new Opening('S', 26.2f, 28.4f),
                new Opening('E', 27.4f, 29.4f)),
            R("중앙복도", 34, 20, 46, 26, living,
                new Opening('W', 21.0f, 23.6f),
                new Opening('N', 36.2f, 39.0f),
                new Opening('S', 36.2f, 39.0f),
                new Opening('E', 22.0f, 24.4f)),
            R("주방", 32, 26, 46, 34, kitchen,
                new Opening('S', 36.2f, 39.0f),
                new Opening('W', 27.4f, 29.4f),
                new Opening('N', 38.0f, 41.0f)),
            R("주방입구", 37, 34, 43, 38, kitchen,
                new Opening('S', 38.0f, 41.0f)),
            R("로비", 34, 10, 44, 20, living,
                new Opening('N', 36.2f, 39.0f),
                new Opening('W', 11.2f, 14.0f),
                new Opening('E', 13.0f, 16.0f)),
            R("라운지", 44, 10, 56, 20, living,
                new Opening('W', 13.0f, 16.0f),
                new Opening('N', 48.2f, 51.0f),
                new Opening('S', 48.2f, 51.0f),
                new Opening('E', 13.2f, 16.0f)),
            R("창고", 46, 20, 56, 30, garage,
                new Opening('W', 22.0f, 24.4f),
                new Opening('S', 48.2f, 51.0f),
                new Opening('E', 22.2f, 24.6f)),
            R("음악창고", 56, 20, 68, 28, garage,
                new Opening('W', 22.2f, 24.6f),
                new Opening('S', 58.2f, 61.0f),
                new Opening('E', 21.2f, 23.6f)),
            R("_차고동편", 56, 10, 68, 20, garage,
                new Opening('W', 13.2f, 16.0f),
                new Opening('N', 58.2f, 61.0f),
                new Opening('S', 58.0f, 66.0f),
                new Opening('E', 16.2f, 19.0f)),
            R("차고저장고", 68, 16, 76, 28, garage,
                new Opening('W', 21.2f, 23.6f),
                new Opening('W', 16.2f, 19.0f)),
            R("차고", 44, 0, 68, 10, garage,
                new Opening('N', 48.2f, 51.0f),
                new Opening('N', 58.0f, 66.0f),
                new Opening('S', 57.5f, 61.0f)),
            R("사다리", 56, -4, 64, 0, garage,
                new Opening('N', 57.5f, 61.0f)),
        };
    }

    static Room R(string name, float xMin, float zMin, float xMax, float zMax, Color floor, params Opening[] doors)
    {
        return new Room
        {
            name = name,
            xMin = xMin,
            zMin = zMin,
            xMax = xMax,
            zMax = zMax,
            floor = floor,
            doors = doors
        };
    }

    void BuildRoom(Room room)
    {
        var root = new GameObject(room.name);
        root.transform.SetParent(transform, false);

        CreateFloor(root.transform, room);

        var northGaps = Gaps(room, 'N');
        var southGaps = Gaps(room, 'S');
        var eastGaps = Gaps(room, 'E');
        var westGaps = Gaps(room, 'W');

        HorizontalWall(root.transform, "North", room.zMax, room.xMin, room.xMax, northGaps);
        HorizontalWall(root.transform, "South", room.zMin, room.xMin, room.xMax, southGaps);
        VerticalWall(root.transform, "East", room.xMax, room.zMin, room.zMax, eastGaps);
        VerticalWall(root.transform, "West", room.xMin, room.zMin, room.zMax, westGaps);

        if (!room.name.StartsWith("_"))
            CreateLabel(root.transform, room);
    }

    List<(float from, float to)> Gaps(Room room, char side)
    {
        var list = new List<(float, float)>();
        if (room.doors == null) return list;
        foreach (var d in room.doors)
        {
            if (d.side == side)
                list.Add((Mathf.Min(d.from, d.to), Mathf.Max(d.from, d.to)));
        }
        list.Sort((a, b) => a.Item1.CompareTo(b.Item1));
        return list;
    }

    void HorizontalWall(Transform parent, string name, float z, float x0, float x1, List<(float from, float to)> gaps)
    {
        float start = Mathf.Min(x0, x1);
        float end = Mathf.Max(x0, x1);
        float cursor = start;
        foreach (var gap in gaps)
        {
            if (gap.to <= start || gap.from >= end) continue;
            float a = Mathf.Max(cursor, start);
            float b = Mathf.Min(gap.from, end);
            if (b - a > 0.08f)
                SpawnWall(parent, $"{name}_{a:0.0}", (a + b) * 0.5f, z, b - a, wallThickness);
            cursor = Mathf.Max(cursor, gap.to);
        }
        if (end - cursor > 0.08f)
            SpawnWall(parent, $"{name}_{cursor:0.0}", (cursor + end) * 0.5f, z, end - cursor, wallThickness);
    }

    void VerticalWall(Transform parent, string name, float x, float z0, float z1, List<(float from, float to)> gaps)
    {
        float start = Mathf.Min(z0, z1);
        float end = Mathf.Max(z0, z1);
        float cursor = start;
        foreach (var gap in gaps)
        {
            if (gap.to <= start || gap.from >= end) continue;
            float a = Mathf.Max(cursor, start);
            float b = Mathf.Min(gap.from, end);
            if (b - a > 0.08f)
                SpawnWall(parent, $"{name}_{a:0.0}", x, (a + b) * 0.5f, wallThickness, b - a);
            cursor = Mathf.Max(cursor, gap.to);
        }
        if (end - cursor > 0.08f)
            SpawnWall(parent, $"{name}_{cursor:0.0}", x, (cursor + end) * 0.5f, wallThickness, end - cursor);
    }

    void SpawnWall(Transform parent, string name, float x, float z, float sizeX, float sizeZ)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = new Vector3(x, wallHeight * 0.5f, z);
        wall.transform.localScale = new Vector3(Mathf.Max(sizeX, wallThickness), wallHeight, Mathf.Max(sizeZ, wallThickness));
        ApplyMat(wall, _wallMat);
        wall.isStatic = true;
    }

    void CreateFloor(Transform parent, Room room)
    {
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(parent, false);
        float w = room.xMax - room.xMin;
        float d = room.zMax - room.zMin;
        floor.transform.position = new Vector3((room.xMin + room.xMax) * 0.5f, 0.02f, (room.zMin + room.zMax) * 0.5f);
        floor.transform.localScale = new Vector3(w, 0.04f, d);
        ApplyMat(floor, floorMaterial != null ? floorMaterial : MakeMat(room.floor));
        floor.isStatic = true;
    }

    void CreateLabel(Transform parent, Room room)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((room.xMin + room.xMax) * 0.5f, 2.1f, (room.zMin + room.zMax) * 0.5f);
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        var tm = go.AddComponent<TextMesh>();
        tm.text = room.name;
        tm.fontSize = 32;
        tm.characterSize = 0.12f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = Color.white;
        go.AddComponent<DepthTestedText>();
    }

    static void ApplyMat(GameObject go, Material mat)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend != null && mat != null)
            rend.sharedMaterial = mat;
    }

    static Material MakeMat(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        var mat = new Material(shader);
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        return mat;
    }
}
