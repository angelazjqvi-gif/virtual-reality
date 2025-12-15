using UnityEngine;

public class EnemySelectable : MonoBehaviour
{
    public BattleManager battle;
    public BattleUnit unit;

    // 如果你要“每个玩家各选各的目标”，就把 separateTargetPerPlayer 勾上，
    // 并且这里填 who=该玩家索引（0..N-1）
    public int who = 0;

    void Reset()
    {
        unit = GetComponent<BattleUnit>();
    }

    void OnMouseDown()
    {
        if (battle == null || unit == null) return;

        // 共用目标时：推荐用 battle.SelectEnemyTarget(unit);
        // 分开目标时：用 battle.SelectEnemyTarget(unit, who);
        if (battle.separateTargetPerPlayer)
            battle.SelectEnemyTarget(unit, who);
        else
            battle.SelectEnemyTarget(unit);
    }
}
