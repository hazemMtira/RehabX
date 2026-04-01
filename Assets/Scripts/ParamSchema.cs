using UnityEngine;

[CreateAssetMenu(menuName = "Game/Param Schema", fileName = "Param_targetScore")]
public class ParamSchema : ScriptableObject
{
    public enum ParamType { Range, Int, Bool, Select, Float, Vector3 }

    [Header("Identity")]
    public string key   = "targetScore";
    public string label = "Target Score";
    public string unit  = "pts";

    [Header("Type")]
    public ParamType type = ParamType.Int;

    [Header("Range / Int / Float constraints")]
    public float min        = 0;
    public float max        = 100;
    public float step       = 1;
    public float defaultVal = 15;

    [Header("Vector3 (only used if type = Vector3)")]
    public Vector3 defaultVector3 = Vector3.zero;
    public Vector3 minVector3     = new Vector3(-10, -10, -10);
    public Vector3 maxVector3     = new Vector3(10, 10, 10);

    [Header("Select options (only for Type = Select)")]
    public string[] options = new string[0];
}