using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class GameManager
{
    public static int Zone { get; private set; } = 1;
    public static bool IsSafe => Zone % 5 == 0;
    public static bool IsSuper => Zone % 30 == 0;
    public static int SceneIndex => IsSuper ? 2 : IsSafe ? 1 : 0;

    private static readonly Dictionary<string, long> collected = new Dictionary<string, long>();
    private static readonly Dictionary<string, long> banked = new Dictionary<string, long>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        banked.Clear();
        Restart();
    }

    public static void AddReward(string id, long amount)
    {
        collected.TryGetValue(id, out long previous);
        collected[id] = previous + amount;
    }

    public static void LoseRewards() => collected.Clear();
    public static void NextZone() => Zone++;

    public static void Restart()
    {
        Zone = 1;
        collected.Clear();
    }

    public static void BankRewards()
    {
        foreach (var item in collected)
        {
            banked.TryGetValue(item.Key, out long previous);
            banked[item.Key] = previous + item.Value;
        }
        collected.Clear();
    }

    public static string Summary(bool saved = false)
    {
        var data = saved ? banked : collected;
        if (data.Count == 0) return "-";
        var text = new StringBuilder();
        foreach (var item in data)
            text.AppendLine(item.Key + ": " + item.Value.ToString("N0"));
        return text.ToString().TrimEnd();
    }
}
