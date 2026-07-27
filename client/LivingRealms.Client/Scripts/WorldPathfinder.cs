using Godot;

namespace LivingRealms.Client;

public sealed class WorldPathfinder : IDisposable
{
    private readonly AStarGrid2D _grid = new();
    private readonly Vector2 _minimum;
    private readonly float _cellSize;
    private readonly int _width;
    private readonly int _height;
    private readonly float _agentClearance;
    private readonly HashSet<Vector2I> _baseSolidCells = [];
    private HashSet<Vector2I> _dynamicSolidCells = [];
    private HashSet<Vector2I> _passableCells = [];

    public WorldPathfinder(
        Vector2 minimum,
        Vector2 maximum,
        float cellSize,
        IEnumerable<WorldPathObstacle> obstacles,
        float agentClearance)
    {
        _minimum = minimum;
        _cellSize = cellSize;
        _agentClearance = agentClearance;
        _width = Mathf.CeilToInt((maximum.X - minimum.X) / cellSize);
        _height = Mathf.CeilToInt((maximum.Y - minimum.Y) / cellSize);
        _grid.Region = new Rect2I(0, 0, _width, _height);
        _grid.CellSize = new Vector2(cellSize, cellSize);
        _grid.Offset = minimum + Vector2.One * (cellSize * 0.5f);
        _grid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles;
        _grid.Update();

        _baseSolidCells = RasterizeObstacles(obstacles);
        RebuildSolidity();
    }

    public void SetDynamicObstacles(IEnumerable<WorldPathObstacle> obstacles)
    {
        _dynamicSolidCells = RasterizeObstacles(obstacles);
        RebuildSolidity();
    }

    public void SetPassableAreas(IEnumerable<WorldPathObstacle> areas)
    {
        _passableCells = RasterizeObstacles(areas);
        RebuildSolidity();
    }

    private void RebuildSolidity()
    {
        for (var z = 0; z < _height; z++)
        {
            for (var x = 0; x < _width; x++)
            {
                _grid.SetPointSolid(new Vector2I(x, z), false);
            }
        }
        foreach (var cell in _baseSolidCells.Concat(_dynamicSolidCells))
        {
            if (!_passableCells.Contains(cell))
            {
                _grid.SetPointSolid(cell);
            }
        }
    }

    private HashSet<Vector2I> RasterizeObstacles(IEnumerable<WorldPathObstacle> obstacles)
    {
        var solidCells = new HashSet<Vector2I>();
        var obstacleList = obstacles.ToArray();
        if (obstacleList.Length == 0)
        {
            return solidCells;
        }
        for (var z = 0; z < _height; z++)
        {
            for (var x = 0; x < _width; x++)
            {
                var center = CellToWorld(new Vector2I(x, z));
                if (obstacleList.Any(obstacle => obstacle.Contains(center.X, center.Y, _agentClearance)))
                {
                    solidCells.Add(new Vector2I(x, z));
                }
            }
        }
        return solidCells;
    }

    public IReadOnlyList<Vector3> FindPath(
        Vector3 from,
        Vector3 target,
        IReadOnlyCollection<Vector3>? avoidedPoints = null,
        float avoidanceRadius = 0)
    {
        var start = FindNearestWalkable(WorldToCell(from));
        var end = FindNearestWalkable(WorldToCell(target));
        if (start is null || end is null)
        {
            return [];
        }

        var temporaryBlocks = ApplyTemporaryBlocks(
            avoidedPoints,
            avoidanceRadius,
            start.Value,
            end.Value);
        Vector2[] points;
        try
        {
            points = _grid.GetPointPath(start.Value, end.Value);
        }
        finally
        {
            foreach (var cell in temporaryBlocks)
            {
                _grid.SetPointSolid(cell, false);
            }
        }

        if (points.Length == 0)
        {
            return [];
        }

        var path = new List<Vector3>(points.Length + 1);
        foreach (var point in points)
        {
            path.Add(new Vector3(point.X, from.Y, point.Y));
        }
        path.Add(new Vector3(target.X, from.Y, target.Z));
        return path;
    }

    public Vector3 GetNearestWalkablePosition(Vector3 position)
    {
        var cell = FindNearestWalkable(WorldToCell(position));
        if (cell is null)
        {
            return position;
        }

        var point = CellToWorld(cell.Value);
        return new Vector3(point.X, position.Y, point.Y);
    }

    private HashSet<Vector2I> ApplyTemporaryBlocks(
        IReadOnlyCollection<Vector3>? avoidedPoints,
        float radius,
        Vector2I start,
        Vector2I end)
    {
        if (avoidedPoints is null || avoidedPoints.Count == 0 || radius <= 0)
        {
            return [];
        }

        var changed = new HashSet<Vector2I>();
        var cellRadius = Mathf.CeilToInt(radius / _cellSize);
        foreach (var avoided in avoidedPoints)
        {
            var center = WorldToCell(avoided);
            for (var z = -cellRadius; z <= cellRadius; z++)
            {
                for (var x = -cellRadius; x <= cellRadius; x++)
                {
                    var candidate = center + new Vector2I(x, z);
                    if (candidate == start || candidate == end || !IsInsideGrid(candidate) ||
                        _grid.IsPointSolid(candidate))
                    {
                        continue;
                    }

                    var candidateWorld = CellToWorld(candidate);
                    if (candidateWorld.DistanceTo(new Vector2(avoided.X, avoided.Z)) > radius)
                    {
                        continue;
                    }
                    _grid.SetPointSolid(candidate);
                    changed.Add(candidate);
                }
            }
        }
        return changed;
    }

    private Vector2I WorldToCell(Vector3 world) => new(
        Mathf.FloorToInt((world.X - _minimum.X) / _cellSize),
        Mathf.FloorToInt((world.Z - _minimum.Y) / _cellSize));

    private Vector2 CellToWorld(Vector2I cell) => new(
        _minimum.X + (cell.X + 0.5f) * _cellSize,
        _minimum.Y + (cell.Y + 0.5f) * _cellSize);

    private Vector2I? FindNearestWalkable(Vector2I requested)
    {
        if (IsWalkable(requested))
        {
            return requested;
        }

        for (var radius = 1; radius <= 8; radius++)
        {
            for (var z = -radius; z <= radius; z++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (Math.Abs(x) != radius && Math.Abs(z) != radius)
                    {
                        continue;
                    }
                    var candidate = requested + new Vector2I(x, z);
                    if (IsWalkable(candidate))
                    {
                        return candidate;
                    }
                }
            }
        }
        return null;
    }

    private bool IsInsideGrid(Vector2I cell) =>
        cell.X >= 0 && cell.X < _width && cell.Y >= 0 && cell.Y < _height;

    private bool IsWalkable(Vector2I cell) => IsInsideGrid(cell) && !_grid.IsPointSolid(cell);

    public void Dispose() => _grid.Dispose();
}

public readonly record struct WorldPathObstacle(float MinimumX, float MaximumX, float MinimumZ, float MaximumZ)
{
    public static WorldPathObstacle FromBox(Vector3 position, Vector3 size) => new(
        position.X - size.X * 0.5f,
        position.X + size.X * 0.5f,
        position.Z - size.Z * 0.5f,
        position.Z + size.Z * 0.5f);

    public static WorldPathObstacle FromRotatedBox(
        Vector3 position,
        Vector3 size,
        float rotationY)
    {
        var halfX = size.X * 0.5f;
        var halfZ = size.Z * 0.5f;
        var cosine = Mathf.Abs(Mathf.Cos(rotationY));
        var sine = Mathf.Abs(Mathf.Sin(rotationY));
        var extentX = cosine * halfX + sine * halfZ;
        var extentZ = sine * halfX + cosine * halfZ;
        return new WorldPathObstacle(
            position.X - extentX,
            position.X + extentX,
            position.Z - extentZ,
            position.Z + extentZ);
    }

    public bool Contains(float x, float z, float clearance) =>
        x >= MinimumX - clearance && x <= MaximumX + clearance &&
        z >= MinimumZ - clearance && z <= MaximumZ + clearance;
}
