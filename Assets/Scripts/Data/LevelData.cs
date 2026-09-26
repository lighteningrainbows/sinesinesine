using UnityEngine;

[CreateAssetMenu(
    fileName = "LevelData",
    menuName = "Game/Data/Level Data"
)]
public class LevelData : ScriptableObject
{
    [System.Serializable]
    public class LevelInfo
    {
        public int level;
        public int requiredExperience;
        public int hpBonus;
        public int attackBonus;
    }

    [SerializeField]
    private LevelInfo[] levels;

    public LevelInfo[] Levels => levels;

    public LevelInfo GetLevelInfo(int level)
    {
        foreach (LevelInfo info in levels)
        {
            if (info.level == level)
            {
                return info;
            }
        }

        return null;
    }
}