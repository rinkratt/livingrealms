using Godot;

namespace LivingRealms.Client;

public partial class HarvestResourceNode : StaticBody3D
{
    private Node3D _visual = null!;
    private Label3D _label = null!;
    private CollisionShape3D _collision = null!;
    private float _impactSeconds;

    public Guid ResourceId { get; private set; }
    public string ResourceName { get; private set; } = string.Empty;
    public string Kind { get; private set; } = string.Empty;
    public string ResourceOwnerName { get; private set; } = string.Empty;
    public int Remaining { get; private set; }
    public int Capacity { get; private set; }
    public bool IsAvailable => Remaining > 0;

    public void Configure(ResourceNodeData data)
    {
        ResourceId = data.Id;
        ResourceName = data.Name;
        Kind = data.Kind;
        ResourceOwnerName = data.Owner;
        Position = data.Position;
        Remaining = data.Remaining;
        Capacity = data.Capacity;
    }

    public override void _Ready()
    {
        CollisionLayer = 1;
        CollisionMask = 2 | 4 | 8;
        _visual = new Node3D { Name = "ResourceVisual" };
        AddChild(_visual);
        if (Kind.Equals("Wood", StringComparison.OrdinalIgnoreCase))
        {
            BuildTree();
        }
        else
        {
            BuildStone();
        }
        _label = new Label3D
        {
            Position = new Vector3(0, Kind.Equals("Wood", StringComparison.OrdinalIgnoreCase) ? 5.7f : 2.6f, 0),
            FontSize = 24,
            OutlineSize = 7,
            Modulate = new Color("e4bd62"),
            OutlineModulate = new Color(0, 0, 0, 0.92f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled
        };
        AddChild(_label);
        ApplyState();
    }

    public override void _Process(double delta)
    {
        if (_impactSeconds <= 0)
        {
            _visual.Rotation = _visual.Rotation.Lerp(Vector3.Zero, (float)delta * 9.0f);
            return;
        }
        _impactSeconds = Mathf.Max(0, _impactSeconds - (float)delta);
        var strength = _impactSeconds / 0.32f;
        _visual.Rotation = new Vector3(
            Mathf.Sin(_impactSeconds * 58.0f) * 0.035f * strength,
            0,
            Mathf.Cos(_impactSeconds * 51.0f) * 0.035f * strength);
    }

    public void ApplyData(ResourceNodeData data)
    {
        Remaining = data.Remaining;
        Capacity = data.Capacity;
        ApplyState();
    }

    public void PlayGatherImpact()
    {
        _impactSeconds = 0.32f;
    }

    private void ApplyState()
    {
        if (!IsInstanceValid(_visual))
        {
            return;
        }
        _visual.Visible = IsAvailable;
        _collision.Disabled = !IsAvailable;
        _label.Text = IsAvailable
            ? $"{ResourceName}\n{Remaining}/{Capacity}  •  H TO {(Kind.Equals("Wood", StringComparison.OrdinalIgnoreCase) ? "CHOP" : "MINE")}"
            : $"{ResourceName}\nDEPLETED  •  REGROWING";
        _label.Modulate = IsAvailable ? new Color("e4bd62") : new Color("9a9384");
    }

    private void BuildTree()
    {
        _collision = new CollisionShape3D
        {
            Shape = new CylinderShape3D { Radius = 0.58f, Height = 5.0f },
            Position = new Vector3(0, 2.5f, 0)
        };
        AddChild(_collision);
        AddMesh(new CylinderMesh
        {
            TopRadius = 0.31f,
            BottomRadius = 0.70f,
            Height = 5.0f,
            RadialSegments = 12
        }, new Vector3(0, 2.5f, 0), new Color("5a381e"));

        var wood = new Color("604126");

        var branchEnds = new[]
        {
            new Vector3(-1.75f, 4.35f, 0.25f), new Vector3(-1.10f, 4.85f, -1.35f),
            new Vector3(0.30f, 5.35f, -1.65f), new Vector3(1.65f, 4.55f, -0.55f),
            new Vector3(1.55f, 4.95f, 1.10f), new Vector3(0.25f, 5.65f, 1.55f),
            new Vector3(-1.25f, 5.20f, 1.25f)
        };
        var leafColors = new[] { new Color("21452a"), new Color("315d31"), new Color("46723a"), new Color("2a512d") };
        for (var index = 0; index < branchEnds.Length; index++)
        {
            var start = new Vector3(0, 2.75f + (index % 3) * 0.38f, 0);
            var end = branchEnds[index];
            AddBranch(start, end, 0.22f - (index % 2) * 0.025f, wood);

            var radial = new Vector3(end.X, 0, end.Z).Normalized();
            var tangent = new Vector3(-radial.Z, 0, radial.X);
            foreach (var (offset, size, colorOffset) in new[]
                     {
                         (Vector3.Zero, new Vector3(1.0f, 0.92f, 0.88f), 0),
                         (tangent * 0.58f + new Vector3(0, 0.22f, 0), new Vector3(0.82f, 0.74f, 0.94f), 1),
                         (-tangent * 0.48f + radial * 0.20f + new Vector3(0, -0.18f, 0), new Vector3(0.76f, 0.82f, 0.78f), 2)
                     })
            {
                AddMesh(
                    new SphereMesh { Radius = 0.72f, Height = 1.44f, RadialSegments = 14, Rings = 8 },
                    end + offset,
                    leafColors[(index + colorOffset) % leafColors.Length],
                    size,
                    new Vector3(index * 0.09f, colorOffset * 0.24f, index * 0.17f));
            }
        }

        foreach (var (position, scale, color) in new[]
                 {
                     (new Vector3(-0.45f, 6.15f, -0.30f), new Vector3(0.90f, 1.05f, 0.82f), leafColors[2]),
                     (new Vector3(0.48f, 6.10f, 0.22f), new Vector3(0.82f, 0.95f, 0.90f), leafColors[1]),
                     (new Vector3(-0.10f, 5.70f, 0.15f), new Vector3(0.92f, 0.82f, 0.95f), leafColors[3])
                 })
        {
            AddMesh(new SphereMesh { Radius = 0.78f, Height = 1.56f, RadialSegments = 14, Rings = 8 },
                position, color, scale);
        }
    }

    private void BuildStone()
    {
        _collision = new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(2.9f, 1.8f, 2.5f) },
            Position = new Vector3(0, 0.9f, 0)
        };
        AddChild(_collision);
        AddMesh(new SphereMesh { Radius = 1.0f, Height = 2.0f, RadialSegments = 9, Rings = 5 },
            new Vector3(0, 0.9f, 0), new Color("595b59"), new Vector3(1.55f, 0.95f, 1.35f));
        AddMesh(new SphereMesh { Radius = 0.8f, Height = 1.6f, RadialSegments = 8, Rings = 4 },
            new Vector3(1.1f, 0.55f, 0.25f), new Color("70716b"), new Vector3(0.9f, 0.75f, 0.8f));
        AddMesh(new BoxMesh { Size = new Vector3(0.12f, 1.0f, 1.1f) },
            new Vector3(-0.25f, 1.1f, 1.15f), new Color("c4b06d"), Vector3.One);
    }

    private void AddBranch(Vector3 start, Vector3 end, float radius, Color color)
    {
        var direction = end - start;
        var length = direction.Length();
        if (length <= 0.001f)
        {
            return;
        }

        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.93f
        };
        var mesh = new CylinderMesh
        {
            TopRadius = radius * 0.52f,
            BottomRadius = radius,
            Height = length,
            RadialSegments = 9,
            Material = material
        };
        _visual.AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            Position = (start + end) * 0.5f,
            Quaternion = new Quaternion(Vector3.Up, direction / length)
        });
    }

    private void AddMesh(
        PrimitiveMesh mesh,
        Vector3 position,
        Color color,
        Vector3 scale = default,
        Vector3 rotation = default)
    {
        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Roughness = 0.88f
        };
        mesh.Material = material;
        var instance = new MeshInstance3D
        {
            Mesh = mesh,
            Position = position,
            Scale = scale == default ? Vector3.One : scale,
            Rotation = rotation
        };
        _visual.AddChild(instance);
    }
}
