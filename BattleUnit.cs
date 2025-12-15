using UnityEngine;

public class BattleUnit : MonoBehaviour
{
    [Header("Stats")]
    public int maxHp = 30;
    public int hp = 30;
    public int atk = 10;

    [Header("Team")]
    public bool isPlayer = true; // true=玩家，false=敌人

    [Header("Animator")]
    public Animator animator;              // 拖本角色 Animator
    public string attackStateName;         // 例如：man_battle_attack / women_battle_attack / enemy_attack
    public string deathStateName = "death";// 例如：man_die / women_die / death

    [Header("FX")]
    public GameObject attackFxPrefab;      // 攻击特效 prefab（可选）
    public Transform hitPoint;             // 受击点（可选）

    [Header("Death")]
    public bool destroyOnDeath = false;

    public void TakeDamage(int dmg)
    {
        hp -= dmg;
        if (hp < 0) hp = 0;
        Debug.Log($"{name} takes {dmg}, HP = {hp}/{maxHp}");
    }

    public bool IsDead() => hp <= 0;

    public void TriggerAttack()
    {
        if (animator == null) return;
        animator.ResetTrigger("Attack");
        animator.SetTrigger("Attack");
    }

    public void TriggerDie()
    {
        if (animator == null) return;
        animator.ResetTrigger("Die");
        animator.SetTrigger("Die");
    }

    public void HideOrDestroy()
    {
        if (destroyOnDeath) Destroy(gameObject);
        else gameObject.SetActive(false);
    }
}
