/*using UnityEngine;

[CreateAssetMenu(menuName = "Game/Param Schema", fileName = "Param_targetScore")]
public class ParamSchema : ScriptableObject
{
    public enum ParamType { Range, Int, Bool, Select, Float }
 
    [Header("Identity")]
    [Tooltip("camelCase key — must match the field name used in the game's LevelConfig.")]
    public string key   = "targetScore";
    public string label = "Target Score";
    public string unit  = "pts";
 
    [Header("Type")]
    public ParamType type = ParamType.Int;
 
    [Header("Range / Int constraints (ignored for Bool / Select)")]
    public float min         = 0;
    public float max         = 100;
    public float step        = 1;
    public float defaultVal  = 15;
 
    [Header("Select options (only for Type = Select)")]
    public string[] options = new string[0];
}*/