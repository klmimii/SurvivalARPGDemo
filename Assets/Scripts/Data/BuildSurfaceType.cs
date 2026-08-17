using System;

//Flag允许一个建筑同时接受多个表面例如Ground|floor
[Flags]
public enum BuildSurfaceType
{
    None = 0,
    Ground = 1 << 0,
    Floor = 1 << 1
}