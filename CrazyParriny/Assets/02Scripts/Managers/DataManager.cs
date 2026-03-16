using UnityEngine;

public class DataManager : Singleton<DataManager>
{
    public override void Init() { }

    public void SaveRanking(eDifficulty difficulty, float time)
    {
        float[] ranks = LoadRanking(difficulty);

        for (int i = 0; i < ranks.Length; i++)
        {
            if (ranks[i] <= 0f || time < ranks[i])
            {
                for (int j = ranks.Length - 1; j > i; j--)
                    ranks[j] = ranks[j - 1];
                ranks[i] = time;
                break;
            }
        }

        for (int i = 0; i < ranks.Length; i++)
            PlayerPrefs.SetFloat($"Rank_{difficulty}_{i + 1}", ranks[i]);
        PlayerPrefs.Save();
    }

    public float[] LoadRanking(eDifficulty difficulty)
    {
        var ranks = new float[3];
        for (int i = 0; i < 3; i++)
            ranks[i] = PlayerPrefs.GetFloat($"Rank_{difficulty}_{i + 1}", 0f);
        return ranks;
    }
}
