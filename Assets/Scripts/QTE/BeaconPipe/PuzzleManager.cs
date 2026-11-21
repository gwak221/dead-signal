
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 비콘 파이프 연결 퍼즐 (팀 '비콘 qte' 패키지 → DEAD SIGNAL 용 개조).
/// 자체 입력(E)과 자체 팝업 토글은 없다: Beacon → BeaconPipeQte 가 BuildNewPuzzle() 로 새 판을 만들고,
/// 파이프가 끝까지 연결되면 Solved 이벤트를 한 번 발생시킨다. 결과 처리(배터리 소비/진행도)는 Beacon 이 담당한다.
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    /// <summary>파이프가 끝점까지 연결됐을 때(판당 한 번).</summary>
    public event System.Action Solved;
    public bool IsSolved => solved;

    [Header("Puzzle Settings")]
    public PipeTile[] allPipes;
    public GameObject endOff;
    public GameObject endOn;
    public Vector2Int startCell = new Vector2Int(0, 0);
    public Vector2Int endCell = new Vector2Int(4, 4);
    public int startFeedDirection = 1;

    [Header("Difficulty")]
    public int blockedCellCount = 3;
    public int lockedPathCount = 2;
    public int lockedDecoyCount = 2;

    static readonly float[] ColX = { -299f, -148f, 1f, 150f, 299f };
    static readonly float[] RowY = { 305f, 153f, 1f, -151f, -303f };
    static readonly Vector2Int[] Deltas =
    {
        new Vector2Int(0, -1),
        new Vector2Int(1, 0),
        new Vector2Int(0, 1),
        new Vector2Int(-1, 0)
    };

    const int Size = 5;

    readonly List<PipeTile> spawned = new List<PipeTile>();
    readonly List<Vector2Int> solutionPath = new List<Vector2Int>();
    readonly Dictionary<Vector2Int, int> solutionMask = new Dictionary<Vector2Int, int>();

    PipeTile straightTemplate;
    PipeTile cornerTemplate;
    PipeTile teeTemplate;
    PipeTile crossTemplate;
    RectTransform board;
    Sprite blockedSprite;
    bool solved;
    bool[,] blocked;
    bool templatesCached;
    readonly List<GameObject> blockedObjects = new List<GameObject>();

    void Awake()
    {
        CacheTemplates();
    }

    /// <summary>이전 판을 지우고 새 퍼즐을 만든다 (비콘 QTE 시작 시 호출).</summary>
    public void BuildNewPuzzle()
    {
        CacheTemplates();
        foreach (PipeTile pipe in spawned)
            if (pipe != null) Destroy(pipe.gameObject);
        spawned.Clear();
        foreach (GameObject go in blockedObjects)
            if (go != null) Destroy(go);
        blockedObjects.Clear();
        solutionPath.Clear();
        solutionMask.Clear();
        allPipes = null;
        solved = false;

        BuildBoard();
        CheckConnection();
        solved = false;   // 시작 직후 우연히 이어져 있어도 클리어 처리하지 않는다 (BuildBoard 가 막지만 이중 안전)
    }

    void CacheTemplates()
    {
        if (templatesCached) return;
        templatesCached = true;
        PipeTile[] existing = GetComponentsInChildren<PipeTile>(true);
        foreach (PipeTile pipe in existing)
        {
            if (board == null)
                board = pipe.transform.parent as RectTransform;

            switch (pipe.NormalizedType())
            {
                case "2way":
                    if (straightTemplate == null)
                        straightTemplate = pipe;
                    break;
                case "2way_cha":
                    if (cornerTemplate == null)
                        cornerTemplate = pipe;
                    break;
                case "3way":
                    if (teeTemplate == null)
                        teeTemplate = pipe;
                    break;
                case "4way":
                    if (crossTemplate == null)
                        crossTemplate = pipe;
                    break;
            }
        }

        foreach (PipeTile pipe in existing)
            pipe.gameObject.SetActive(false);
    }

    void BuildBoard()
    {
        if (board == null || straightTemplate == null || cornerTemplate == null || teeTemplate == null || crossTemplate == null)
        {
            Debug.LogError("파이프 원본을 찾지 못했습니다.");
            return;
        }

        blocked = new bool[Size, Size];
        blocked[startCell.x, startCell.y] = true;
        blocked[endCell.x, endCell.y] = true;

        var entry = startCell + Deltas[startFeedDirection];
        var candidates = new List<Vector2Int>();
        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var cell = new Vector2Int(x, y);
                if (cell == startCell || cell == endCell || cell == entry)
                    continue;
                candidates.Add(cell);
            }
        }

        Shuffle(candidates);
        int blocksLeft = blockedCellCount;
        foreach (Vector2Int cell in candidates)
        {
            if (blocksLeft <= 0)
                break;
            if (!CanStillReachEnd(cell))
                continue;

            blocked[cell.x, cell.y] = true;
            SpawnBlocked(cell);
            blocksLeft--;
        }

        if (!TryBuildPath(solutionPath))
        {
            Debug.LogError("이어지는 길을 만들지 못했습니다.");
            return;
        }

        var pathSet = new HashSet<Vector2Int>(solutionPath);
        var pathPipes = new List<PipeTile>();
        var decoys = new List<PipeTile>();
        solutionMask.Clear();

        for (int i = 0; i < solutionPath.Count; i++)
        {
            Vector2Int cell = solutionPath[i];
            Vector2Int previous = i == 0 ? startCell : solutionPath[i - 1];
            Vector2Int next = i == solutionPath.Count - 1 ? endCell : solutionPath[i + 1];
            int required = (1 << DirectionTo(cell, previous)) | (1 << DirectionTo(cell, next));
            solutionMask[cell] = required;

            PipeTile template = ChoosePathTemplate(required);
            int solvedTurns = FindTurns(template, required, true);
            PipeTile pipe = SpawnPipe(template, cell, solvedTurns * 90, false);
            pathPipes.Add(pipe);
        }

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var cell = new Vector2Int(x, y);
                if (blocked[x, y] || pathSet.Contains(cell))
                    continue;

                PipeTile template = RandomTemplate();
                PipeTile pipe = SpawnPipe(template, cell, Random.Range(0, 4) * 90, false);
                decoys.Add(pipe);
            }
        }

        LockSome(pathPipes, lockedPathCount, true);
        LockSome(decoys, lockedDecoyCount, false);

        foreach (PipeTile pipe in pathPipes)
        {
            if (pipe.rotationLocked)
                continue;

            int required = solutionMask[new Vector2Int(pipe.gridX, pipe.gridY)];
            int wrongTurns = FindTurns(pipe, required, false);
            if (wrongTurns >= 0)
                pipe.SetRotationImmediate(wrongTurns * 90);
        }

        allPipes = spawned.ToArray();
        for (int attempt = 0; attempt < 12 && ReachesEnd(); attempt++)
        {
            PipeTile pipe = pathPipes[Random.Range(0, pathPipes.Count)];
            if (pipe.rotationLocked)
                continue;

            int required = solutionMask[new Vector2Int(pipe.gridX, pipe.gridY)];
            int wrongTurns = FindTurns(pipe, required, false);
            if (wrongTurns >= 0)
                pipe.SetRotationImmediate(wrongTurns * 90);
        }

        for (int attempt = 0; attempt < decoys.Count && ReachesEnd(); attempt++)
        {
            PipeTile pipe = decoys[attempt];
            if (pipe.rotationLocked)
                continue;
            pipe.SetRotationImmediate(((pipe.currentRotation + 90) % 360));
        }
    }

    bool CanStillReachEnd(Vector2Int extraBlock)
    {
        blocked[extraBlock.x, extraBlock.y] = true;
        bool ok = TryBuildPath(null);
        blocked[extraBlock.x, extraBlock.y] = false;
        return ok;
    }

    bool TryBuildPath(List<Vector2Int> into)
    {
        var path = new List<Vector2Int>();
        var visited = new bool[Size, Size];
        Vector2Int entry = startCell + Deltas[startFeedDirection];
        if (!Walk(entry, path, visited))
            return false;

        if (into != null)
        {
            into.Clear();
            into.AddRange(path);
        }
        return true;
    }

    bool Walk(Vector2Int current, List<Vector2Int> path, bool[,] visited)
    {
        if (!IsOpen(current) || visited[current.x, current.y])
            return false;

        visited[current.x, current.y] = true;
        path.Add(current);

        bool touchesEnd = false;
        for (int direction = 0; direction < 4; direction++)
        {
            if (current + Deltas[direction] == endCell)
                touchesEnd = true;
        }

        if (touchesEnd && path.Count >= 6 && Random.value < 0.5f)
            return true;

        int[] order = { 0, 1, 2, 3 };
        Shuffle(order);
        for (int i = 0; i < order.Length; i++)
        {
            Vector2Int next = current + Deltas[order[i]];
            if (next == endCell || !IsOpen(next))
                continue;
            if (Walk(next, path, visited))
                return true;
        }

        if (touchesEnd && path.Count >= 4)
            return true;

        path.RemoveAt(path.Count - 1);
        visited[current.x, current.y] = false;
        return false;
    }

    bool IsOpen(Vector2Int cell)
    {
        if (cell.x < 0 || cell.y < 0 || cell.x >= Size || cell.y >= Size)
            return false;
        if (cell == startCell || cell == endCell)
            return false;
        return !blocked[cell.x, cell.y];
    }

    PipeTile ChoosePathTemplate(int required)
    {
        bool opposite = IsOpposite(required);
        if (opposite)
            return Random.value < 0.4f ? teeTemplate : straightTemplate;
        return Random.value < 0.45f ? teeTemplate : cornerTemplate;
    }

    PipeTile RandomTemplate()
    {
        int roll = Random.Range(0, 4);
        if (roll == 0) return straightTemplate;
        if (roll == 1) return cornerTemplate;
        if (roll == 2) return teeTemplate;
        return crossTemplate;
    }

    int FindTurns(PipeTile template, int required, bool wantCover)
    {
        var matching = new List<int>();
        var missing = new List<int>();
        for (int turns = 0; turns < 4; turns++)
        {
            if (template.Covers(turns, required))
                matching.Add(turns);
            else
                missing.Add(turns);
        }

        List<int> pool = wantCover ? matching : missing;
        if (pool.Count == 0)
            return wantCover ? 0 : -1;
        return pool[Random.Range(0, pool.Count)];
    }

    void LockSome(List<PipeTile> pipes, int count, bool keepSolvedRotation)
    {
        Shuffle(pipes);
        int locked = 0;
        foreach (PipeTile pipe in pipes)
        {
            if (locked >= count)
                break;

            if (keepSolvedRotation)
            {
                int required = solutionMask[new Vector2Int(pipe.gridX, pipe.gridY)];
                int turns = FindTurns(pipe, required, true);
                pipe.SetRotationImmediate(turns * 90);
            }

            pipe.SetLocked(true);
            locked++;
        }
    }

    PipeTile SpawnPipe(PipeTile template, Vector2Int cell, int rotation, bool locked)
    {
        PipeTile pipe = Instantiate(template, board);
        pipe.gameObject.SetActive(true);
        pipe.name = template.pipeType + " " + cell.x + "," + cell.y;
        pipe.gridX = cell.x;
        pipe.gridY = cell.y;
        pipe.GetComponent<RectTransform>().anchoredPosition = new Vector2(ColX[cell.x], RowY[cell.y]);
        pipe.SetRotationImmediate(rotation);
        pipe.SetLocked(locked);
        spawned.Add(pipe);
        return pipe;
    }

    void SpawnBlocked(Vector2Int cell)
    {
        var go = new GameObject("blocked " + cell.x + "," + cell.y, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(board, false);
        blockedObjects.Add(go);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(112f, 112f);
        rect.anchoredPosition = new Vector2(ColX[cell.x], RowY[cell.y]);
        var image = go.GetComponent<Image>();
        image.sprite = BlockedSprite();
        image.raycastTarget = false;
        image.color = Color.white;
    }

    Sprite BlockedSprite()
    {
        if (blockedSprite != null)
            return blockedSprite;

        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var fill = new Color(0.16f, 0.16f, 0.18f, 0.95f);
        var edge = new Color(0.42f, 0.42f, 0.46f, 1f);
        var mark = new Color(0.78f, 0.24f, 0.24f, 1f);
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                bool border = x < 3 || y < 3 || x >= n - 3 || y >= n - 3;
                bool cross = Mathf.Abs(x - y) < 4 || Mathf.Abs(x - (n - 1 - y)) < 4;
                tex.SetPixel(x, y, cross ? mark : (border ? edge : fill));
            }
        }

        tex.Apply();
        blockedSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return blockedSprite;
    }

    static bool IsOpposite(int mask)
    {
        return mask == ((1 << 0) | (1 << 2)) || mask == ((1 << 1) | (1 << 3));
    }

    static int DirectionTo(Vector2Int from, Vector2Int to)
    {
        if (to.y < from.y) return 0;
        if (to.x > from.x) return 1;
        if (to.y > from.y) return 2;
        return 3;
    }

    static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T swap = list[i];
            list[i] = list[j];
            list[j] = swap;
        }
    }

    bool ReachesEnd()
    {
        return TracePower(out _);
    }

    bool TracePower(out HashSet<PipeTile> powered)
    {
        powered = new HashSet<PipeTile>();
        if (allPipes == null || allPipes.Length == 0)
            return false;

        var map = new Dictionary<Vector2Int, PipeTile>();
        foreach (PipeTile pipe in allPipes)
        {
            if (pipe != null && pipe.gameObject.activeSelf)
                map[new Vector2Int(pipe.gridX, pipe.gridY)] = pipe;
        }

        var queue = new Queue<PipeTile>();
        bool reachedEnd = false;
        Vector2Int entry = startCell + Deltas[startFeedDirection];
        if (map.TryGetValue(entry, out PipeTile first) && first.HasOpening(PipeTile.Opposite(startFeedDirection)))
            queue.Enqueue(first);

        while (queue.Count > 0)
        {
            PipeTile pipe = queue.Dequeue();
            if (!powered.Add(pipe))
                continue;

            for (int direction = 0; direction < 4; direction++)
            {
                if (!pipe.HasOpening(direction))
                    continue;

                Vector2Int next = pipe.Step(direction);
                if (next == endCell)
                {
                    reachedEnd = true;
                    continue;
                }

                if (!map.TryGetValue(next, out PipeTile neighbor) || powered.Contains(neighbor))
                    continue;
                if (!neighbor.HasOpening(PipeTile.Opposite(direction)))
                    continue;

                queue.Enqueue(neighbor);
            }
        }

        return reachedEnd;
    }

    public void CheckConnection()
    {
        if (allPipes == null || allPipes.Length == 0)
            allPipes = spawned.ToArray();

        bool reachedEnd = TracePower(out HashSet<PipeTile> powered);
        foreach (PipeTile pipe in allPipes)
        {
            if (pipe != null)
                pipe.SetCharged(powered.Contains(pipe));
        }

        if (endOff != null)
            endOff.SetActive(!reachedEnd);
        if (endOn != null)
            endOn.SetActive(reachedEnd);

        if (reachedEnd && !solved)
        {
            solved = true;
            Solved?.Invoke();
        }
        else if (!reachedEnd)
        {
            solved = false;
        }
    }
}
