using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BattleManager : MonoBehaviour
{
    public enum TurnState { PlayerTurn, EnemyTurn, Busy, End }

    [System.Serializable]
    public class PlayerSlot
    {
        public BattleUnit unit;     // 该玩家 BattleUnit（必须 isPlayer=true）
        public Button attackButton; // 该玩家攻击按钮（可为空：为空则不显示/不可点击）
    }

    [Header("Players (N)")]
    public List<PlayerSlot> players = new List<PlayerSlot>();

    [Header("Enemies (N)")]
    public List<BattleUnit> enemies = new List<BattleUnit>();

    [Header("Enemy Turn")]
    public float enemyDelayBeforeAttack = 0.4f;

    [Header("FX fallback")]
    public float fxFallbackTime = 0.6f;

    [Header("Wait Safety")]
    public float maxWaitEnterAttack = 0.5f;
    public float maxWaitAttackTotal = 2.0f;
    public float maxWaitFx = 2.0f;
    public float maxWaitDeath = 2.0f;

    [Header("Scene Names")]
    public string worldSceneName = "world1";

    [Header("Target Select")]
    public bool separateTargetPerPlayer = false; // false=全队共用一个选中目标；true=每个玩家单独目标

    private TurnState state = TurnState.PlayerTurn;

    // 每个玩家本回合是否已行动
    private List<bool> acted = new List<bool>();

    // 选中目标：共用 or 每人一个
    private BattleUnit sharedSelectedTarget = null;
    private List<BattleUnit> perPlayerSelectedTarget = new List<BattleUnit>();

    void Start()
    {
        RebuildRuntimeLists();
        BindButtons();
        StartNewPlayerPhase();
        SetState(TurnState.PlayerTurn);
    }

    void RebuildRuntimeLists()
    {
        acted = new List<bool>(players.Count);
        perPlayerSelectedTarget = new List<BattleUnit>(players.Count);

        for (int i = 0; i < players.Count; i++)
        {
            acted.Add(false);
            perPlayerSelectedTarget.Add(null);
        }
    }

    void BindButtons()
    {
        for (int i = 0; i < players.Count; i++)
        {
            int idx = i;
            var btn = players[i].attackButton;
            if (btn == null) continue;

            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => OnClickPlayerAttack(idx));
        }
    }

    void StartNewPlayerPhase()
    {
        for (int i = 0; i < acted.Count; i++) acted[i] = false;
        RefreshButtons();
    }

    void SetState(TurnState s)
    {
        state = s;
        RefreshButtons();
    }

    void RefreshButtons()
    {
        bool canClick = (state == TurnState.PlayerTurn);

        for (int i = 0; i < players.Count; i++)
        {
            var slot = players[i];
            if (slot.attackButton == null) continue;

            bool alive = IsAlive(slot.unit);
            slot.attackButton.interactable = canClick && alive && !acted[i];
        }
    }

    bool IsAlive(BattleUnit u)
    {
        return u != null && u.gameObject.activeInHierarchy && !u.IsDead();
    }

    bool IsEnemyAlive(BattleUnit e)
    {
        return e != null && e.gameObject.activeInHierarchy && !e.IsDead();
    }

    // =========================
    // ✅ 点敌人选目标（共用）
    // =========================
    public void SelectEnemyTarget(BattleUnit enemy)
    {
        if (enemy == null) return;
        if (!IsEnemyAlive(enemy)) return;
        if (state != TurnState.PlayerTurn) return;

        sharedSelectedTarget = enemy;

        if (!separateTargetPerPlayer)
        {
            // 共用时不需要额外处理
        }
    }

    // =========================
    // ✅ 点敌人选目标（指定某玩家）
    // EnemySelectable 也可以用这个（传 who）
    // =========================
    public void SelectEnemyTarget(BattleUnit enemy, int who)
    {
        if (enemy == null) return;
        if (!IsEnemyAlive(enemy)) return;
        if (state != TurnState.PlayerTurn) return;

        if (!separateTargetPerPlayer)
        {
            sharedSelectedTarget = enemy;
            return;
        }

        if (who < 0 || who >= players.Count) return;
        perPlayerSelectedTarget[who] = enemy;
    }

    void ClearSelectedIfInvalid()
    {
        if (sharedSelectedTarget != null && !IsEnemyAlive(sharedSelectedTarget))
            sharedSelectedTarget = null;

        for (int i = 0; i < perPlayerSelectedTarget.Count; i++)
        {
            var t = perPlayerSelectedTarget[i];
            if (t != null && !IsEnemyAlive(t)) perPlayerSelectedTarget[i] = null;
        }
    }

    BattleUnit ResolvePlayerTarget(int who)
    {
        ClearSelectedIfInvalid();

        BattleUnit selected = null;

        if (!separateTargetPerPlayer)
        {
            selected = sharedSelectedTarget;
        }
        else
        {
            if (who >= 0 && who < perPlayerSelectedTarget.Count)
                selected = perPlayerSelectedTarget[who];
        }

        if (IsEnemyAlive(selected)) return selected;

        // 没选/选的死了：回退到第一个存活敌人
        return GetFirstAliveEnemy();
    }

    void OnClickPlayerAttack(int who)
    {
        if (state != TurnState.PlayerTurn) return;
        if (who < 0 || who >= players.Count) return;

        var attacker = players[who].unit;
        if (!IsAlive(attacker) || acted[who]) return;

        StartCoroutine(PlayerAttackFlow(who));
    }

    IEnumerator PlayerAttackFlow(int who)
    {
        SetState(TurnState.Busy);

        var attacker = players[who].unit;
        if (!IsAlive(attacker))
        {
            // 这个玩家已经死了，直接当他行动结束
            acted[who] = true;
            if (AllPlayersActedOrDead()) StartCoroutine(EnemyTeamAttackFlow());
            else SetState(TurnState.PlayerTurn);
            yield break;
        }

        BattleUnit target = ResolvePlayerTarget(who);
        if (target == null)
        {
            yield return EndBattleAndReturn(playerWin: true);
            yield break;
        }

        // ✅ 触发攻击动画（动画事件里会调用 SpawnAttackFxNow）
        attacker.TriggerAttack(); // :contentReference[oaicite:1]{index=1}

        // ✅ 等攻击动画播完再扣血（保持你原逻辑）
        yield return WaitAttackFinish(attacker);

        target.TakeDamage(attacker.atk); // :contentReference[oaicite:2]{index=2}

        if (target.IsDead())
        {
            // 目标死亡清理选中（避免还指向它）
            if (sharedSelectedTarget == target) sharedSelectedTarget = null;
            for (int i = 0; i < perPlayerSelectedTarget.Count; i++)
                if (perPlayerSelectedTarget[i] == target) perPlayerSelectedTarget[i] = null;

            yield return PlayDeathAndRemove(target);
        }

        if (AllEnemiesDead())
        {
            yield return EndBattleAndReturn(playerWin: true);
            yield break;
        }

        acted[who] = true;

        if (AllPlayersActedOrDead())
            StartCoroutine(EnemyTeamAttackFlow());
        else
            SetState(TurnState.PlayerTurn);
    }

    bool AllPlayersActedOrDead()
    {
        for (int i = 0; i < players.Count; i++)
        {
            bool done = acted[i] || !IsAlive(players[i].unit);
            if (!done) return false;
        }
        return true;
    }

    IEnumerator EnemyTeamAttackFlow()
    {
        SetState(TurnState.Busy);

        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (!IsEnemyAlive(e)) continue;

            yield return new WaitForSeconds(enemyDelayBeforeAttack);

            BattleUnit targetPlayer = PickAlivePlayer();
            if (targetPlayer == null)
            {
                yield return EndBattleAndReturn(playerWin: false);
                yield break;
            }

            e.TriggerAttack();
            yield return WaitAttackFinish(e);

            targetPlayer.TakeDamage(e.atk);

            if (targetPlayer.IsDead())
                yield return PlayDeathAndRemove(targetPlayer);

            if (AllPlayersDead())
            {
                yield return EndBattleAndReturn(playerWin: false);
                yield break;
            }
        }

        StartNewPlayerPhase();
        SetState(TurnState.PlayerTurn);
    }

    // =========================================================
    // ✅ Animation Event 调用：在“挥刀那一帧”生成特效
    //    玩家：打到该玩家 ResolvePlayerTarget(who) 的受击点
    // =========================================================
    public void SpawnAttackFxNow(BattleUnit attacker)
    {
        if (attacker == null) return;

        Transform hit = null;

        if (attacker.isPlayer)
        {
            int who = FindPlayerIndex(attacker);

            // 找不到就按共用目标兜底
            BattleUnit target = (who >= 0) ? ResolvePlayerTarget(who) : GetFirstAliveEnemy();
            if (target != null) hit = (target.hitPoint != null) ? target.hitPoint : target.transform;
        }
        else
        {
            var target = PickAlivePlayer();
            if (target != null) hit = (target.hitPoint != null) ? target.hitPoint : target.transform;
        }

        if (hit == null) return;

        StartCoroutine(PlayFxAndWait(attacker.attackFxPrefab, hit));
    }

    int FindPlayerIndex(BattleUnit u)
    {
        for (int i = 0; i < players.Count; i++)
            if (players[i].unit == u) return i;
        return -1;
    }

    BattleUnit PickAlivePlayer()
    {
        List<BattleUnit> alive = new List<BattleUnit>();
        for (int i = 0; i < players.Count; i++)
        {
            var u = players[i].unit;
            if (IsAlive(u)) alive.Add(u);
        }
        if (alive.Count == 0) return null;
        return alive[Random.Range(0, alive.Count)];
    }

    BattleUnit GetFirstAliveEnemy()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (IsEnemyAlive(e)) return e;
        }
        return null;
    }

    bool AllEnemiesDead()
    {
        for (int i = 0; i < enemies.Count; i++)
        {
            var e = enemies[i];
            if (IsEnemyAlive(e)) return false;
        }
        return true;
    }

    bool AllPlayersDead()
    {
        for (int i = 0; i < players.Count; i++)
            if (IsAlive(players[i].unit)) return false;
        return true;
    }

    IEnumerator EndBattleAndReturn(bool playerWin)
    {
        state = TurnState.End;
        RefreshButtons();

        if (GameSession.I != null)
        {
            if (playerWin) GameSession.I.EndBattle_PlayerWin();
            else GameSession.I.EndBattle_PlayerLose();
        }

        yield return new WaitForSeconds(0.15f);
        SceneManager.LoadScene(worldSceneName);
    }

    // ======================================================
    // ✅ 等攻击结束（不触发Attack，只负责等待）
    // ======================================================
    IEnumerator WaitAttackFinish(BattleUnit unit)
    {
        if (unit == null || unit.animator == null) yield break;
        if (string.IsNullOrEmpty(unit.attackStateName)) yield break;

        yield return null; // 给 Animator 1 帧切状态

        float t = 0f;
        while (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName) && t < maxWaitEnterAttack)
        {
            t += Time.deltaTime;
            yield return null;
        }

        if (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName))
            yield break;

        float t2 = 0f;
        while (unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.attackStateName) && t2 < maxWaitAttackTotal)
        {
            t2 += Time.deltaTime;
            yield return null;
        }
    }

    IEnumerator PlayFxAndWait(GameObject fxPrefab, Transform point)
    {
        if (fxPrefab == null || point == null)
        {
            yield return new WaitForSeconds(fxFallbackTime);
            yield break;
        }

        GameObject fx = Instantiate(fxPrefab, point.position, Quaternion.identity);

        var sr = fx.GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.sortingOrder = 999;

        var anim = fx.GetComponent<Animator>();
        if (anim == null)
        {
            yield return new WaitForSeconds(fxFallbackTime);
            Destroy(fx);
            yield break;
        }

        yield return null;

        float t = 0f;
        while (anim != null && t < maxWaitFx)
        {
            var st = anim.GetCurrentAnimatorStateInfo(0);
            if (!st.loop && st.normalizedTime >= 1f) break;
            t += Time.deltaTime;
            yield return null;
        }

        Destroy(fx);
    }

    IEnumerator PlayDeathAndRemove(BattleUnit unit)
    {
        if (unit == null) yield break;

        if (unit.animator == null || string.IsNullOrEmpty(unit.deathStateName))
        {
            unit.HideOrDestroy();
            yield break;
        }

        unit.TriggerDie();
        yield return null;

        float tEnter = 0f;
        while (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.deathStateName) && tEnter < 0.5f)
        {
            tEnter += Time.deltaTime;
            yield return null;
        }

        if (!unit.animator.GetCurrentAnimatorStateInfo(0).IsName(unit.deathStateName))
        {
            unit.HideOrDestroy();
            yield break;
        }

        float t = 0f;
        while (t < maxWaitDeath)
        {
            var st = unit.animator.GetCurrentAnimatorStateInfo(0);
            if (!st.loop && st.normalizedTime >= 1f) break;
            t += Time.deltaTime;
            yield return null;
        }

        unit.HideOrDestroy();
    }
}
