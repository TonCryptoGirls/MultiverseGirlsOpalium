using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    private bool isPrimaryInstance;

    public enum GameState { Camp, Battle }
    public GameState CurrentState { get; private set; } = GameState.Camp;
    [Header("Всегда доступные карты")]
    public CardStats alwaysAvailableCard; // ← ПЕРЕМЕСТИ СЮДА!

    [Header("Passive Aura System")]
public PassiveAuraManager auraManager;

    [Header("Игрок")]
    public int currentLevel = 1;
    public float currentXP = 0;
    public float xpToNextLevel = 100;
    public float coins = 0;
    private float healthSaveTimer = 0f;
    [Header("CardUpgrades")]
    public int cardRegenLevel = 0;
    [Header("Online Settings")]
public bool isOnlineMode = false;
public string onlineSceneName = "OnlineScene";
[Header("Online Button")]
public GameObject onlineButton; // Перетащить кнопку из сцены


    [Header("Mana System")]

    [SerializeField] private int _maxMana = 30;
    [SerializeField] private float _currentMana = 10;
    [SerializeField] private float _manaRegenCooldown = 1f;
    [SerializeField] private float _manaRegenRate = 1f;
    private float manaRegenTimer;
    public GameObject restoreCardsButton; // Перетащи сюда кнопку из сце
    
    // Замени публичные переменные на свойства с защитой:
    public int maxMana
    {
        get => _maxMana;
        set => _maxMana = Mathf.Max(30, value); // Не меньше 1
    }

 
    public float currentMana 
    { 
       get => _currentMana; 
       set => _currentMana = Mathf.Clamp(value, 0, _maxMana); 
    }

    public float manaRegenCooldown 
    { 
       get => _manaRegenCooldown; 
       set => _manaRegenCooldown = Mathf.Max(0.1f, value); // Не меньше 0.1
    }

    public float manaRegenRate
    {
       get => _manaRegenRate;
       set => _manaRegenRate = Mathf.Max(0.1f, value); // Не меньше 0.1
    }

    [Header("Player Upgrades")]
    public float handDamage = 10f;
    public event Action<Vector3> OnManualAttack;
    public int handDamageLevel = 0;
    public int manaRegenLevel = 0;
    public int maxManaLevel = 0;
    public float hpRegenRate = 0f;
    public int hpRegenLevel = 0;
    private float hpRegenTimer = 0f;

    [Header("Camp Regen")]
[SerializeField] private float _campHealInterval = 5f;
[SerializeField] private int _campHealAmount = 1;
public float campHealTimer = 0f;

public float campHealInterval
{
    get => _campHealInterval;
    set => _campHealInterval = Mathf.Max(5f, value); // минимум 5 секунд
}

public int campHealAmount
{
    get => _campHealAmount;
    set => _campHealAmount = Mathf.Max(1, value); // минимум 1 HP
}
    

    [Header("Карточки")]
    public List<CardBehaviour> activeCards = new();
    public event Action OnCardListChanged;

    [Header("UI победы/поражения")]
    public GameObject winPanel, losePanel;
    public GameObject restartButton, menuButton;
    public Text resultText;

    [Header("UI кнопки")]
    public GameObject chestButton, shopButton, battleButton, pauseButton;
    public GameObject AdButton, SettingsButton, tablicabutton, donatebutton;
    [Header("Mana System")]
    


    private bool gameEnded, waveStarted;
    private float recentMonsterDamage;

    // --- PAUSE SYSTEM ---
    private bool isPaused = false;

    [Header("UI паузы")]
    public GameObject pausePanel;
    [Header("Wave Cooldown")]
    public float waveCooldownDuration = 2f;
    private float waveCooldownTimer = 0f;
    private bool isWaveCooldown = false;


    // локальные ссылки для отписки
    private Action deckRestoredHandler;
    private bool dataLoaded = false;


    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            isPrimaryInstance = true;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        if (campHealInterval <= 0 || campHealInterval < 5f) 
        campHealInterval = 5f;  // минимум 5 секунд

        if (campHealAmount <= 0) 
        campHealAmount = 1;     // минимум 1 HP

        // Скрыть UI при старте
        winPanel?.SetActive(false);
        losePanel?.SetActive(false);
        restartButton?.SetActive(false);
        menuButton?.SetActive(false);
        if (resultText) resultText.text = "";
        
        

        if (!dataLoaded)
    {
        SaveSystem.LoadGame(this);
        dataLoaded = true;
    }
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        Debug.Log("GameManager Start() начал выполнение");
        Scene currentScene = SceneManager.GetActiveScene();
        isOnlineMode = (currentScene.name == onlineSceneName);
        if (isOnlineMode)
        {
            Debug.Log("Активирован онлайн-режим");
            DisableOnlineIncompatibleSystems();
        }

        if (maxMana <= 0) maxMana = 10;
        if (currentMana <= 0) currentMana = maxMana;
        if (manaRegenCooldown <= 0) manaRegenCooldown = 1f;
        if (manaRegenRate <= 0) manaRegenRate = 1f;

        Debug.Log("Проверяем CardUpgradeManager.Instance");
        if (CardUpgradeManager.Instance != null)
        {
            Debug.Log("CardUpgradeManager найден, вызываем LoadUpgrades");
            CardUpgradeManager.Instance.LoadUpgrades();
        }
        else
        {
            Debug.LogWarning("CardUpgradeManager.Instance is null - пропускаем");
        }



        Time.timeScale = 1f;
        isPaused = false;
        Debug.Log("Запускаем корутины");
        StartCoroutine(DelayedSignIn());
        if (battleButton != null)
        {
            battleButton.GetComponent<Button>().interactable = false;
        }
        
        // Подпишемся на событие восстановления слотов
        StartCoroutine(SubscribeToDeckRestored());
        if (alwaysAvailableCard != null)
        {
            StartCoroutine(AddAlwaysAvailableCardWithDelay());
        }
        
        StartCoroutine(EnableBattleButtonAfterDelay(2f));
        Debug.Log("GameManager Start() завершил выполнение");
    
    }
    private void DisableOnlineIncompatibleSystems()
{
    // Отключаем автохил в онлайн-режиме
    campHealInterval = 0f;
    
    // Отключаем обычные враги
    if (EnemyManager.Instance != null)
    {
        EnemyManager.Instance.enabled = false;
    }
    
    // Скрываем ненужные кнопки для онлайн-режима
    if (battleButton != null) battleButton.SetActive(false);
    if (chestButton != null) chestButton.SetActive(false);
    if (shopButton != null) shopButton.SetActive(false);
    if (restoreCardsButton != null) restoreCardsButton.SetActive(false);
    
    Debug.Log("Системы, несовместимые с онлайн-режимом, отключены");
}
    IEnumerator DelayedSignIn()
    {
        yield return new WaitForSeconds(2f); // Даем игре прогрузиться
    }
    /*
    public void OnCrystalsCollected()
    {
        GooglePlayManager.I?.ReportCrystalsToLeaderboard();
    }*/
    private IEnumerator AddAlwaysAvailableCardWithDelay()
    {
        // Ждем пока CardInventoryManager инициализируется
        yield return new WaitUntil(() => CardInventoryManager.Instance != null);

        AddAlwaysAvailableCard();
        
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {

        // ГАРАНТИРУЕМ ЧТО ВРЕМЯ ВОССТАНОВЛЕНО ПРИ ЗАГРУЗКЕ СЦЕНЫ
        Time.timeScale = 1f;
        isPaused = false;
        StartCoroutine(UpdateCardsHealthAfterLoad());
    }
    

    private IEnumerator UpdateCardsHealthAfterLoad()
    {
        yield return new WaitForSeconds(0.5f);

        foreach (var card in activeCards)
        {
            if (card != null)
            {
                card.ForceUpdateHealthFromStats();
            }
        }
    }

    private IEnumerator SubscribeToDeckRestored()
    {
        yield return new WaitUntil(() => CardInventoryManager.Instance != null);

        deckRestoredHandler = () =>
        {
            // ждём 1 кадр, чтобы спавн успел завершиться
            StartCoroutine(RefreshActiveCardsNextFrame());
        };

        CardInventoryManager.Instance.onDeckSlotsRestored += deckRestoredHandler;
    }

    private void OnDestroy()
    {
        if (!isPrimaryInstance || Instance != this)
            return;

        SaveSystem.SaveGame(this);
        SceneManager.sceneLoaded -= OnSceneLoaded;
        Time.timeScale = 1f;
        

        if (CardInventoryManager.Instance != null && deckRestoredHandler != null)
            CardInventoryManager.Instance.onDeckSlotsRestored -= deckRestoredHandler;

        Instance = null;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && CurrentState == GameState.Battle)
        {
            // Автоматическая пауза при потере фокуса в бою
            PauseGame();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && CurrentState == GameState.Battle)
        {
            PauseGame();
        }
        
    }

    private void OnApplicationQuit() => SaveSystem.SaveGame(this);

    private void Update()
{
    // ПЕРВОЕ: проверяем онлайн-режим
    if (isOnlineMode)
    {
        CheckOnlinePlayerDeath();
        return;
    }
    
    // ОБЫЧНЫЙ РЕЖИМ:
    healthSaveTimer += Time.deltaTime;
    if (healthSaveTimer >= 5f)
    {
        SaveSystem.SaveGame(this);
        healthSaveTimer = 0f;
    }
    
    if (CurrentState == GameState.Camp)
    {
        CheckForDeadCards();
        
        campHealTimer += Time.deltaTime;
        float timeLeft = campHealInterval - campHealTimer;
        UIManager.Instance?.UpdateHealCountdown(Mathf.Max(timeLeft, 0));

        if (campHealTimer >= campHealInterval)
        {
            campHealTimer = 0f;
            HealCardsInCamp();
        }
    }
    else
    {
        campHealTimer = 0f;
        UIManager.Instance?.UpdateHealCountdown(0);
    }

    RegenerateMana();

    if (!gameEnded) CheckGameOver();
}
    public void EnterOnlineScene()
{
    if (Application.internetReachability == NetworkReachability.NotReachable)
    {
        Debug.Log("Нет интернет-соединения");
        return;
    }
    
    SaveSystem.SaveGame(this);
    SceneManager.LoadScene("ConnectBoss");  // ← название вашей сцены
}


private void CheckOnlinePlayerDeath()
{
    if (!isOnlineMode) return;
    
    bool anyCardAlive = false;
    foreach (var card in activeCards)
    {
        if (card != null && !card.isDead && card.stats.currentHP > 0)
        {
            anyCardAlive = true;
            break;
        }
    }
    
    if (!anyCardAlive && activeCards.Count > 0)
    {
        Debug.Log("Все карты мертвы - возврат в меню");
        FestivalBossManager.Instance?.ReturnToMainMenu();
    }
}

private void ReturnToMainMenuFromOnline()
{
    if (FestivalBossManager.Instance != null)
    {
        FestivalBossManager.Instance.ReturnToMainMenu();
    }
    else
    {
        SceneManager.LoadScene("MainMenu");
    }
}

   

    // ===================== ЛОГИКА РЕГЕНЕРАЦИИ =====================
    private void RegenerateMana()
    {
        manaRegenTimer += Time.deltaTime;
        if (manaRegenTimer >= manaRegenCooldown)
        {
            if (currentMana < maxMana) currentMana++;
            manaRegenTimer = 0f;
        }
    }
    private void AddAlwaysAvailableCard()
{
    if (CardInventoryManager.Instance != null && alwaysAvailableCard != null)
    {
        if (!CardInventoryManager.Instance.allOwnedCards.Contains(alwaysAvailableCard))
        {
            CardInventoryManager.Instance.allOwnedCards.Add(alwaysAvailableCard);
            Debug.Log($"Всегда доступная карта добавлена: {alwaysAvailableCard.cardName}");
        }
    }
}
    private void RegenerateHp()
    {
        if (CurrentState != GameState.Camp) return;

        hpRegenTimer += Time.deltaTime;
        if (hpRegenTimer < campHealInterval) return;

        hpRegenTimer = 0f;
        foreach (var c in activeCards)
        {
            if (c == null || c.stats == null) continue;
            float percent = campHealAmount / c.stats.maxHP;
            c.HealPercent(percent);
        }
    }

    
    private void HealCardsInCamp()
{
    // ✅ Убираем дублирование таймера (он уже в Update)
    float healAmount = campHealAmount + hpRegenRate;
    
    Debug.Log($"=== АВТО-ХИЛ В ЛАГЕРЕ ===");

    foreach (var c in activeCards)
    {
        if (c == null || c.stats == null) continue;

        if (c.stats.currentHP <= 0 || c.isDead)
{
    // ✅ При воскрешении сбрасываем баффы
    c.stats.damagePerSecond = c.originalDamagePerSecond;
    c.attackInterval = c.originalAttackInterval;
    
    c.stats.currentHP = Mathf.Max(healAmount, 1f);
    c.isDead = false;
    
    // Включаем функциональность
    c.enabled = true;
    foreach (var col in c.GetComponents<Collider>())
        col.enabled = true;
    
    c.StopAllCoroutines();
    c.StartCoroutine(c.InstantFadeIn());
    
    // ✅ Проверяем, не зарегистрирована ли уже карта
    if (!activeCards.Contains(c))
    {
        RegisterCard(c);
    }
    
    Debug.Log($"🔰 ВОСКРЕШЕНА: {c.stats.cardName}, урон сброшен до {c.originalDamagePerSecond}");
}
        else
        {
            float newHP = c.stats.currentHP + healAmount;
            c.stats.currentHP = Mathf.Min(newHP, c.stats.maxHP);
        }
    }

    ForceUpdateAllCardsState();
    
    Debug.Log($"Авто-хил завершен");
}

private void ForceUpdateAllCardsState()
{
    Debug.Log("=== ПРИНУДИТЕЛЬНОЕ ОБНОВЛЕНИЕ ВСЕХ КАРТ ===");
    
    if (activeCards == null) 
    {
        Debug.Log("activeCards is null");
        return;
    }
    
    int updatedCount = 0;
    int resurrectedCount = 0;
    
    foreach (var card in activeCards)
    {
        if (card != null && card.stats != null)
        {
            // Сохраняем предыдущее состояние
            bool wasDead = card.isDead;
            
            // ПРИНУДИТЕЛЬНО ОБНОВЛЯЕМ КАРТУ
            card.ForceUpdateHealthFromStats();
            
            // ДОПОЛНИТЕЛЬНАЯ ПРОВЕРКА ДЛЯ МЕРТВЫХ КАРТ С HP > 0
            if (card.stats.currentHP > 0 && card.isDead)
            {
                Debug.LogWarning($"Correcting inconsistent card state: {card.stats.cardName} has positive HP but is marked dead.");
                card.isDead = false;
                card.enabled = true;
                
                // Включаем коллайдеры
                foreach (var col in card.GetComponents<Collider>())
                    col.enabled = true;
                    
                // Возвращаем прозрачность
                card.StopAllCoroutines();
                card.StartCoroutine(card.InstantFadeIn());
                
                // Регистрируем карту обратно
                RegisterCard(card);
                
                resurrectedCount++;
            }
            
            // Считаем статистику
            if (wasDead && !card.isDead)
            {
                resurrectedCount++;
            }
            
            updatedCount++;
            
            Debug.Log($"Карта: {card.stats.cardName}, HP: {card.stats.currentHP}, isDead: {card.isDead}, enabled: {card.enabled}");
        }
    }
    
    Debug.Log($"✅ ОБНОВЛЕНО: {updatedCount} карт, ВОСКРЕШЕНО: {resurrectedCount}");
}

    
public void DebugForceUpdateAllCards()
{
    Debug.Log("=== РУЧНОЕ ОБНОВЛЕНИЕ КАРТ ===");
    
    if (activeCards == null || activeCards.Count == 0)
    {
        Debug.Log("Нет активных карт для обновления");
        return;
    }
    
    int updatedCount = 0;
    int resurrectedCount = 0;
    
    foreach (var card in activeCards)
    {
        if (card != null && card.stats != null)
        {
            // Принудительно обновляем состояние
            bool wasDead = card.isDead;
            card.ForceUpdateHealthFromStats();
            
            if (wasDead && !card.isDead)
            {
                resurrectedCount++;
                Debug.Log($"🔰 ВОСКРЕШЕНА: {card.stats.cardName}");
            }
            else if (!wasDead && card.isDead)
            {
                Debug.Log($"💀 УМЕРЛА: {card.stats.cardName}");
            }
            
            updatedCount++;
        }
    }
    
    Debug.Log($"✅ Обновлено карт: {updatedCount}, Воскрешено: {resurrectedCount}");
    
    // Показываем сообщение игрок
}

    // ===================== ПРОКАЧКА И РЕСУРСЫ =====================
    public void GainXP(float amount)
{
    float finalAmount = amount;
    
    // Бонус от аур
    if (auraManager != null)
        finalAmount *= auraManager.GetXPMultiplier();
    
    // БОНУС ОТ СТРИКА
    finalAmount *= OpaliumStreakManager.GetXPMultiplier();

    currentXP += finalAmount;
    coins += finalAmount;

    // ОПАЛИУМ: добавляем XP
    OpaliumLevelSystem.AddXP((int)finalAmount);

    // ОПАЛИУМ: отправляем в рейтинг
string nickname = OpaliumProfileManager.Instance != null 
    ? OpaliumProfileManager.Instance.Data.nickname 
    : "Player";

OpaliumRankingManager.SubmitScore(
    nickname, 
    OpaliumLevelSystem.CurrentXP, 
    OpaliumLevelSystem.GetCurrentRank().ToString()
);
    
    // ОПАЛИУМ: обновляем стрик
    OpaliumStreakManager.OnPlayerAction();
    
    // ОПАЛИУМ: обновляем профиль
    if (OpaliumProfileManager.Instance != null)
    {
        OpaliumProfileManager.Instance.OnBattleComplete();
    }
    
    // ОПАЛИУМ: проверяем достижения
    var newAchievement = OpaliumAchievementSystem.CheckAndUnlockAny();
    if (newAchievement != null)
    {
        OpaliumAchievementPopup.Show(newAchievement);
    }

    OpaliumLevelSystem.Rank currentRank = OpaliumLevelSystem.GetCurrentRank();
if (!OpaliumRankRewards.IsRewardReceived(currentRank))
{
    OpaliumRankRewards.CheckAndReward(currentRank);
}

    while (currentXP >= xpToNextLevel)
    {
        currentXP -= xpToNextLevel;
        LevelUp();
    }
}
    private void LevelUp()
    {
        currentLevel++;
        xpToNextLevel *= 1.05f;
      
    }

    // ===================== РАБОТА С КАРТАМИ =====================
    public void RefreshActiveCards()
    {
        activeCards.Clear();
        foreach (var c in FindObjectsOfType<CardBehaviour>())
            RegisterCard(c);
    }

    public void RegisterCard(CardBehaviour card)
    {
        if (activeCards.Contains(card)) return;
        activeCards.Add(card);
        OnCardListChanged?.Invoke();
    }

    public void UnregisterCard(CardBehaviour card)
    {
        if (!activeCards.Remove(card)) return;
        OnCardListChanged?.Invoke();
        CheckGameOver();
    }

    public bool IsCardBoosted(CardBehaviour card) =>
        card && card.stats && card.stats.level > 2;

    public CardBehaviour GetNearestCard(CardBehaviour src)
    {
        CardBehaviour nearest = null;
        float min = float.MaxValue;

        foreach (var c in activeCards)
        {
            if (c == src) continue;
            float d = Vector3.Distance(c.transform.position, src.transform.position);
            if (d < min)
            {
                min = d;
                nearest = c;
            }
        }

        return nearest;
    }

    private IEnumerator RefreshActiveCardsNextFrame()
    {
        yield return null;
        RefreshActiveCards();
    }

    // ===================== СОСТОЯНИЕ И БОИ =====================
    public void GoToCamp()
{
    if (isPaused) ResumeGame();
    CurrentState = GameState.Camp;
    waveStarted = false;
    hpRegenTimer = 0f;
    StartWaveCooldown();
    
    // ✅ СНАЧАЛА останавливаем все активные эффекты
    ResetAllCardsEffects();
    // ✅ ПОТОМ сбрасываем урон до базового
    ResetAllCardsTemporaryBuffs();
    // ✅ ЗАТЕМ обновляем состояние
    ForceUpdateAllCardsState();

    if (auraManager != null)
        auraManager.OnBattleEnd();

    if (battleButton != null)
        battleButton.SetActive(true);

    chestButton?.SetActive(true);
    shopButton?.SetActive(true);
    battleButton?.SetActive(true);
    pauseButton?.SetActive(false);
    AdButton?.SetActive(true);
    SettingsButton?.SetActive(true);
    tablicabutton?.SetActive(true);
    donatebutton?.SetActive(true);
    
    if (onlineButton != null && Application.internetReachability != NetworkReachability.NotReachable)
        onlineButton.SetActive(true);

    if (EnemyManager.Instance != null)
        EnemyManager.Instance.SaveWaveProgress();
    
    FindObjectOfType<BattleObjectsManager>()?.OnBattleEnd();

    if (AdManager.Instance != null) AdManager.Instance.ShowInterstitial();

    SaveSystem.SaveGame(this);
}
    private void ResetAllCardsTemporaryBuffs()
{
    Debug.Log("Сброс всех временных баффов карт");
    
    foreach (var card in activeCards)
    {
        if (card != null)
        {
            card.ResetToBaseStats();
        }
    }
}
    private void ResetAllCardsEffects()
{
    Debug.Log("=== СБРОС ЭФФЕКТОВ ВСЕХ КАРТ ===");
    
    foreach (var card in activeCards)
    {
        if (card != null)
        {
            // ✅ СНАЧАЛА восстанавливаем урон, ПОТОМ останавливаем корутины
            if (card.stats != null)
            {
                card.stats.isImmuneToDamage = false;
                card.attackInterval = card.originalAttackInterval;
                card.stats.damagePerSecond = card.originalDamagePerSecond;
                Debug.Log($"Сброшены эффекты для: {card.stats.cardName}, урон={card.stats.damagePerSecond}");
            }
            
            card.StopAllCoroutines();
        }
    }
    
    Debug.Log($"Сброшены эффекты для {activeCards.Count} карт");
}
private void CheckForDeadCards()
{
    if (restoreCardsButton == null) return;

    bool hasDeadCards = false;
    
    foreach (var card in activeCards)
    {
        if (card != null && card.isDead)
        {
            hasDeadCards = true;
            break;
        }
    }

    // Показываем кнопку только если есть мертвые карты
    if (restoreCardsButton.activeSelf != hasDeadCards)
    {
        restoreCardsButton.SetActive(hasDeadCards);
        Debug.Log(hasDeadCards ? "🔴 Показана кнопка восстановления - есть мертвые карты" : "🟢 Скрыта кнопка восстановления - все карты живы");
    }
}
/// <summary>
/// Кнопка "Восстановить карты" - воскрешает все мертвые карты
/// </summary>
/// <summary>
/// Кнопка "Восстановить карты" - воскрешает все мертвые карты с 1 HP
/// </summary>
public void OnRestoreCardsButtonClick()
{
    Debug.Log("=== ВОССТАНОВЛЕНИЕ ВСЕХ КАРТ С 1 HP ===");
    
    if (activeCards == null) return;
    
    int resurrectedCount = 0;
    
    foreach (var card in activeCards)
    {
        if (card != null && card.isDead && card.stats != null)
        {
            // Воскрешаем карту с 1 HP
            card.stats.currentHP = 1f;
            card.isDead = false;
            card.enabled = true;
            
            // Включаем коллайдеры
            foreach (var col in card.GetComponents<Collider>())
                col.enabled = true;
                
            // Возвращаем прозрачность
            card.StopAllCoroutines();
            card.StartCoroutine(card.InstantFadeIn());
            
            // Обновляем UI
            card.UpdateHPUI();
            
            resurrectedCount++;
            Debug.Log($"🔰 ВОСКРЕШЕНА: {card.stats.cardName} с 1 HP");
        }
    }
    
    Debug.Log($"✅ ВОСКРЕШЕНО: {resurrectedCount} карт с 1 HP");
    
    // НЕ скрываем кнопку - она сама скроется в CheckForDeadCards если мертвых карт не осталось
    // Кнопка останется видимой если еще есть другие мертвые карты
    
    // Сохраняем игру
    SaveSystem.SaveGame(this);
}



    private IEnumerator EnableBattleButtonAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        if (battleButton != null)
        {
            battleButton.GetComponent<Button>().interactable = true;
            isWaveCooldown = false; // Важно сбросить флаг кулдауна
        }
    }

    
    public bool CanStartBattle()
    {
        return !isWaveCooldown && CurrentState == GameState.Camp && battleButton != null && battleButton.activeInHierarchy;
    }


    public void StartBattle()
    {
        if (CurrentState != GameState.Camp || isWaveCooldown) return;
        
        CurrentState = GameState.Battle;
        gameEnded = false;
        waveStarted = true;

        ResetAllCardsEffects();
        

        winPanel?.SetActive(false);
        losePanel?.SetActive(false);
        restartButton?.SetActive(false);
        menuButton?.SetActive(false);
        if (resultText) resultText.text = "";

        chestButton?.SetActive(false);
        shopButton?.SetActive(false);
        pauseButton?.SetActive(true);
        AdButton?.SetActive(false);
        SettingsButton?.SetActive(false);
        battleButton?.SetActive(false);
        tablicabutton?.SetActive(false);
        donatebutton?.SetActive(false);
        FindObjectOfType<BattleObjectsManager>()?.OnBattleStart();

        if (auraManager != null)
        auraManager.OnBattleStart();

        if (onlineButton != null)
        {
            onlineButton.SetActive(false);
        }

        EnemyManager.Instance?.StartNextWave();
        SoundManager.Instance?.PlaySFX("StartWave");
    }
    public void StartWaveCooldown()
    {
        isWaveCooldown = true;
        waveCooldownTimer = 2f; // Изменил с 3f на 2f
        waveCooldownTimer = waveCooldownDuration;
        if (battleButton != null)
        {
            battleButton.GetComponent<Button>().interactable = false;
        }
        StartCoroutine(EnableBattleButtonAfterDelay(2f));
    
    }


    private void CheckGameOver()
    {
        if (!waveStarted) return;

        if (EnemyManager.Instance && EnemyManager.Instance.AllEnemiesDefeated())
        {
            ShowWinPanel();
            gameEnded = true;
            SaveSystem.SaveGame(this);
            SaveAfterWave(); // ← сохраняем после победы
            
        }
        else if (activeCards.Count == 0)
        {
            ShowLosePanel();
            gameEnded = true;
            SaveSystem.SaveGame(this);
            StartWaveCooldown();
            
        }
    }

    public void ShowWinPanel()
    {
        winPanel?.SetActive(true);
        resultText.text = LocalizationManager.Instance.Get("win");
        restartButton?.SetActive(true);
        menuButton?.SetActive(true);
        pauseButton?.SetActive(false);
        AdButton?.SetActive(true);
        ResetAllCardsEffects();

        ShowAdAfterWin();
    }
    private void ShowAdAfterWin()
    {
        if (AdManager.Instance != null)
        {
            try
            {
                AdManager.Instance.ShowInterstitial();
            }
            catch (System.Exception ex)
            {
                
            }
        }
        else
        {
           
        }

    }


    public void ShowLosePanel()
    {
        losePanel?.SetActive(true);
        resultText.text = LocalizationManager.Instance.Get("lose"); 
        restartButton?.SetActive(true);
        menuButton?.SetActive(true);
        AdButton?.SetActive(true);
        pauseButton?.SetActive(false);
        ResetAllCardsEffects();
        StartWaveCooldown();

        ShowAdAfterLose();
    }
    private void SaveAfterWave()
    {
        // Сохраняем прогресс игрока
        SaveSystem.SaveGame(this);

        // Сохраняем инвентарь и слоты
        if (CardInventoryManager.Instance != null && SaveLoadManager.Instance != null)
        {
            SaveLoadManager.Instance.SaveInventory(
                CardInventoryManager.Instance.allOwnedCards,
                CardInventoryManager.Instance.deckSlots,
                CardInventoryManager.Instance.inventorySlots
            );
        }

        
    }


    public void GoToMenu() 
{
    // Если игра завершена (проигрыш), сбрасываем здоровье карт
    if (gameEnded)
    {
        ResetCardsHealthAfterLoss();
    }
    
    // Сохраняем здоровье карт перед переходом в меню
    SaveCardsHealthBeforeMenu();
    
    // Загружаем главное меню
    SceneManager.LoadScene("MainMenu");
}

    /// <summary>
    /// Сбрасывает здоровье карт после проигрыша
    /// </summary>
    private void ResetCardsHealthAfterLoss()
    {
        Debug.Log("Сброс здоровья карт после проигрыша...");

        if (CardInventoryManager.Instance != null)
        {
            foreach (var card in CardInventoryManager.Instance.allOwnedCards)
            {
                if (card != null)
                {
                    // Сбрасываем здоровье к 50% от максимума
                    card.currentHP = Mathf.Max(1, card.maxHP * 0.1f);
                    Debug.Log($"Сброшено здоровье {card.cardName} до {card.currentHP}/{card.maxHP}");
                }
            }
        }
    }

    private void SaveCardsHealthBeforeMenu()
    {
        Debug.Log("Сохранение здоровья карт перед переходом в меню...");

        // 1. Сохраняем через основную систему сохранения (сохраняет активные карты)
        SaveSystem.SaveGame(this);

        // 2. Синхронизируем здоровье активных карт с инвентарем
        if (CardInventoryManager.Instance != null)
        {
            foreach (var cardBehaviour in activeCards)
            {
                if (cardBehaviour != null && cardBehaviour.stats != null)
                {
                    var inventoryCard = CardInventoryManager.Instance.GetCardByUID(cardBehaviour.stats.uid);
                    if (inventoryCard != null)
                    {
                        inventoryCard.currentHP = cardBehaviour.stats.currentHP;
                        inventoryCard.maxHP = cardBehaviour.stats.maxHP;
                        Debug.Log($"Сохранено здоровье для {cardBehaviour.stats.cardName}: {inventoryCard.currentHP}/{inventoryCard.maxHP}");
                    }
                }
            }

            // 3. Сохраняем инвентарь
            if (SaveLoadManager.Instance != null)
            {
                SaveLoadManager.Instance.SaveInventory(
                    CardInventoryManager.Instance.allOwnedCards,
                    CardInventoryManager.Instance.deckSlots,
                    CardInventoryManager.Instance.inventorySlots
                );
            }
        }

        Debug.Log("Сохранение здоровья карт завершено");
    }



    // ===================== БОЕВАЯ ЛОГИКА =====================
    public void SpendMana(int amount) =>
        currentMana = Mathf.Max(0, currentMana - amount);

    public void ManualAttack()
    {
        var enemy = EnemyManager.Instance?.GetFirstEnemy();
        if (enemy != null)
        {
            enemy.TakeDamage(handDamage);
            OnManualAttack?.Invoke(enemy.transform.position);
        }
    }

    // ===================== СОХРАНЕНИЯ =====================
    public void LoadFromSave(SaveData save)
    {
        currentLevel = save.playerLevel;
        currentXP = save.currentXP;
        xpToNextLevel = save.xpToNextLevel;
        coins = save.coins;
        currentMana = (int)save.currentMana;
        maxMana = (int)save.maxMana;
        manaRegenCooldown = save.manaRegenCooldown;
        handDamageLevel = save.playerDamageLevel;
        manaRegenLevel = save.manaRegenLevel;
        handDamage = save.playerHandDamage;
        manaRegenCooldown = save.playerManaRegenCooldown;
        maxMana = save.playerMaxMana;
        hpRegenRate = save.playerHpRegenRate;
        this.campHealAmount = save.campHealAmount;
        this.campHealInterval = save.campHealInterval;

        maxManaLevel = save.maxManaLevel;
        hpRegenLevel = save.hpRegenLevel;

        // ВАЖНОЕ ИЗМЕНЕНИЕ: НЕ загружаем кристалы из сохранения если CrystalManager уже имеет актуальные данные
        if (CrystalManager.Instance != null)
        {
            // Проверяем, есть ли уже сохраненные кристалы в PlayerPrefs
            bool hasCrystalsInPrefs = PlayerPrefs.HasKey("PlayerCrystals");

            if (hasCrystalsInPrefs)
            {
                // Используем кристалы из PlayerPrefs (актуальные)
                Debug.Log("Используем актуальные кристалы из PlayerPrefs");
                // CrystalManager уже загрузил их в Awake()
            }
            else
            {
                // Используем кристалы из сохранения (только если нет в PlayerPrefs)
                Debug.Log("Используем кристалы из старого сохранения");
                CrystalManager.Instance.SetCrystals(save.totalCrystals);
            }
        }

        Scene currentScene = SceneManager.GetActiveScene();
        if (currentScene.name != "Shop" && EnemyManager.Instance != null)
        {
            EnemyManager.Instance.SetWaveProgress(save.currentWave, save.currentLocation);
        }

        if (handDamage <= 0)
        {
            handDamage = 10f;
            Debug.LogWarning("Hand damage was 0, reset to 10");
        }

        if (CardInventoryManager.Instance != null && save.deckSlotsUIDs != null)
        {
            for (int i = 0; i < save.deckSlotsUIDs.Count && i < CardInventoryManager.Instance.deckSlots.Count; i++)
            {
                string uid = save.deckSlotsUIDs[i];
                var slot = CardInventoryManager.Instance.deckSlots[i];

                if (!string.IsNullOrEmpty(uid))
                {
                    var card = CardDatabase.Instance?.GetCardByUID(uid);
                    if (card != null) slot.AssignCard(card);
                    else slot.ClearSlot();
                }
                else
                {
                    slot.ClearSlot();
                }
            }
        }

        if (alwaysAvailableCard != null)
        {
            StartCoroutine(AddAlwaysAvailableCardAfterLoad());
        }
    }


    private IEnumerator ApplyCrystalsNextFrame(float crystals)
    {
        yield return null;
        CrystalManager.Instance?.SetCrystals(crystals);
    }
    

    public SaveData GetSaveData()
    {
        SaveData data = new SaveData
        {
            playerLevel = currentLevel,
            currentXP = currentXP,
            xpToNextLevel = xpToNextLevel,
            coins = coins,
            currentMana = currentMana,
            maxMana = maxMana,
            manaRegenCooldown = manaRegenCooldown,
            playerDamageLevel = handDamageLevel,
            manaRegenLevel = manaRegenLevel,
            maxManaLevel = maxManaLevel,
            hpRegenLevel = hpRegenLevel,
            campHealAmount = this.campHealAmount,
            campHealInterval = this.campHealInterval,


            playerHandDamage = handDamage,
            playerManaRegenCooldown = manaRegenCooldown,
            playerMaxMana = maxMana,
            playerHpRegenRate = hpRegenRate,

            // ПРОВЕРКА НА NULL ДЛЯ МЕНЕДЖЕРОВ
            totalCrystals = CrystalManager.Instance != null ? CrystalManager.Instance.TotalCrystals : 0,
            currentWave = EnemyManager.Instance != null ? EnemyManager.Instance.WaveNumber : 0,
            currentLocation = EnemyManager.Instance != null ? EnemyManager.Instance.LocationIndex : 0
        };
        

        // ПРОВЕРКА CardInventoryManager И deckSlots
        if (CardInventoryManager.Instance != null && CardInventoryManager.Instance.deckSlots != null)
        {
            data.deckSlotsUIDs.Clear();
            foreach (var slot in CardInventoryManager.Instance.deckSlots)
            {
                if (slot != null && slot.assignedCard != null)
                    data.deckSlotsUIDs.Add(slot.assignedCard.uid);
                else
                    data.deckSlotsUIDs.Add("");
            }
        }
        else
        {
            Debug.LogWarning("CardInventoryManager или deckSlots is null, clearing deckSlotsUIDs");
            data.deckSlotsUIDs.Clear();
        }

        return data;
    }

    // ===================== ПАУЗА =====================
    public void PauseGame()
    {
        if (isPaused || CurrentState != GameState.Battle) return;


        isPaused = true;
        Time.timeScale = 0f;
        pausePanel?.SetActive(true);
       
    }

    public void ResumeGame()
    {
        if (!isPaused) return;
        isPaused = false;
        Time.timeScale = 1f;
        pausePanel?.SetActive(false);
    }

    public void TogglePause() =>
        (isPaused ? (Action)ResumeGame : PauseGame)();

    // ===================== ДЕБАФЫ / ЭФФЕКТЫ =====================
    public void AddMonsterDamage(float amount) => recentMonsterDamage += amount;

    public float GetRecentDamageTakenByMonster()
    {
        float dmg = recentMonsterDamage;
        recentMonsterDamage = 0f;
        return dmg;
    }
    private IEnumerator AddAlwaysAvailableCardAfterLoad()
    {
        yield return null; // Ждем один кадр
        AddAlwaysAvailableCard();
    }

    private void ShowAdAfterLose()
    {

        if (AdManager.Instance != null)
        {
            try
            {
                AdManager.Instance.ShowInterstitial();
            }
            catch (System.Exception ex)
            {
                
            }

        }
        SoundManager.Instance?.PlaySFX("LoseBattle");
    }
    public void ApplyPoisonToAllCards(float duration, float damagePercent, float interval)
    {
        foreach (var card in activeCards)
        {
            if (card != null)
            {
                ApplyBleedToCard(card, duration, damagePercent);
            }
        }
    }
    public void ApplyBleedToCard(CardBehaviour card, float duration, float damagePercent)
    {
        if (card != null)
        {
            card.ApplyBleed(duration, damagePercent);
        }
    }
    public void ApplyBleedToRandomCard(float dur, float dps) { }
    public void ReduceRandomCardDamage(float factor, float dur) { }
    public void ReduceCardUltimateCharge(float amount) { }
    public void StunRandomCard(float dur) { }
    public void ReducePlayerMana(int amount) => SpendMana(amount);
    public void SilenceAllCards(float dur) { }
    public void HealRandomCard(float amount) { }
    public void HealAllCards(float amount) { }
    public void ReduceAllCardDamage(float factor, float dur) { }
}
