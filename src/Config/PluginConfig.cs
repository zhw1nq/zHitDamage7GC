namespace zHitDamage7GC.Config;

public sealed class PluginConfig
{
    public bool Enabled { get; set; } = true;
    public bool FreeAccess { get; set; } = true;
    public string RequiredPermission { get; set; } = "";
    public List<string> Commands { get; set; } = ["hit", "hits", "damage", "dmg", "damages"];
    public bool CommandsEnabled { get; set; } = true;
    public bool Debug { get; set; } = false;
    public bool OnlyAttackerVisible { get; set; } = true;
    public bool SpectatorsCanSee { get; set; } = true;

    public ParticleConfig Particles { get; set; } = new();
    public ShotgunConfig Shotgun { get; set; } = new();
    public DisplayConfig Display { get; set; } = new();
    public SoundConfig Sounds { get; set; } = new();

    public float Distance
    {
        get => Display.Distance;
        set => Display.Distance = value;
    }
}

public sealed class ParticleConfig
{
    public string PathPattern { get; set; } = "particles/thuyskieu/hit_damage/7mau_dmg_{digit}_{side}{crit}.vpcf";
    public string ChildPathPattern { get; set; } = "particles/thuyskieu/hit_damage/7mau_dmg_{digit}_{side}{crit}_child.vpcf";
    public bool EnableChild { get; set; } = true;
    public string SideRight { get; set; } = "fr";
    public string SideLeft { get; set; } = "fl";
    public string CritSuffix { get; set; } = "_crit";
    public string NonCritSuffix { get; set; } = "";
}

public sealed class ShotgunConfig
{
    public List<string> Weapons { get; set; } = ["xm1014", "nova", "mag7", "sawedoff"];
    public float AccumulationDelaySeconds { get; set; } = 0.1f;
}

public sealed class DisplayConfig
{
    public float Distance { get; set; } = 40.0f;
    public float DurationSeconds { get; set; } = 0.5f;
    public float ChildDurationSeconds { get; set; } = 2.5f;
    public float SpacingBase { get; set; } = 6.0f;
    public float DistanceScaleThreshold { get; set; } = 700.0f;
    public float HeightOffsetStandingNormal { get; set; } = 35.0f;
    public float HeightOffsetStandingCrit { get; set; } = 60.0f;
    public float HeightOffsetDuckingNormal { get; set; } = 25.0f;
    public float HeightOffsetDuckingCrit { get; set; } = 45.0f;
    public float RandomSpreadHorizontal { get; set; } = 1.0f;
    public float RandomSpreadVertical { get; set; } = 10.0f;
}

public sealed class SoundConfig
{
    public bool Enabled { get; set; } = false;
    public string HitSoundEvent { get; set; } = "";
    public string CritSoundEvent { get; set; } = "";
    public float Volume { get; set; } = 0.5f;
    public float Pitch { get; set; } = 1.0f;
}
