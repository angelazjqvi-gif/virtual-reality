using UnityEngine;

public class AttackEventEmitter : MonoBehaviour
{
    [Header("Refs")]
    public BattleManager battleManager; // 拖场景里的 BattleManager
    public BattleUnit unit;             // 默认自动获取

    void Awake()
    {
        if (unit == null) unit = GetComponent<BattleUnit>();
    }

    //在攻击动画某一帧 Add Event 调用这个函数
    public void AE_SpawnAttackFx()
    {
        if (battleManager == null || unit == null) return;
        battleManager.SpawnAttackFxNow(unit);
    }
}

