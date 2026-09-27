using System.Collections;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

public class FestivalBossManager : MonoBehaviourPunCallbacks
{
    public static FestivalBossManager Instance;
    
    [Header("Настройки")]
    public int maxPlayers = 20;
    public string mainMenuScene = "MainMenu";
    public GameObject bossPrefab;
    public Transform bossSpawnPoint;
    
    [Header("Награды")]
    public int crystalReward = 1000;
    public int coinReward = 10000;
    
    [Header("Photon")]
    public PhotonView photonViewPrefab;
    
    public FestivalBoss currentBoss; 
    private Dictionary<int, PlayerData> playerData = new Dictionary<int, PlayerData>();
    private bool bossAlive = false;
    private bool isInitialized = false;

    private bool rewardGivenForThisBoss = false;

    private Dictionary<int, float> playerDamageHistory = new Dictionary<int, float>();
private Dictionary<int, float> playerDPS = new Dictionary<int, float>();
private float dpsUpdateTimer = 0f;
private float dpsUpdateInterval = 1f;

    
    private const string SAVE_KEY_BOSS_HP = "FestivalBoss_Health";
private const string SAVE_KEY_BOSS_DATE = "FestivalBoss_Date";
    
    // Используйте "new" для скрытия унаследованного photonView
    private PhotonView _photonView;
    public new PhotonView photonView
    {
        get
        {
            if (_photonView == null)
                _photonView = GetComponent<PhotonView>();
            return _photonView;
        }
    }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
{
    // Проверка подключения
    if (!CheckConnection())
    {
        Debug.Log("Нет подключения к Photon, возвращаемся в меню");
        StartCoroutine(ReturnToMainMenuWithDelay(1f));
        return;
    }
    
    Debug.Log("FestivalBossManager: ожидаем подключения от OnlineConnectBoss");
}
private void Update()
{
    if (!PhotonNetwork.IsMasterClient) return;
    
    dpsUpdateTimer += Time.deltaTime;
    if (dpsUpdateTimer >= dpsUpdateInterval)
    {
        dpsUpdateTimer = 0f;
        UpdateDPS();
    }
}
private void UpdateDPS()
{
    playerDPS.Clear();
    
    foreach (var dmg in playerDamageHistory)
    {
        playerDPS[dmg.Key] = dmg.Value / dpsUpdateInterval;
    }
    playerDamageHistory.Clear();
    
    // Отправляем топ DPS всем игрокам
    if (playerDPS.Count > 0)
    {
        var topDPS = playerDPS.OrderByDescending(x => x.Value).First();
        string playerName = GetPlayerNameByActorNumber(topDPS.Key);
        photonView.RPC("RPC_UpdateDPSText", RpcTarget.All, topDPS.Value, playerName);
    }
}
[PunRPC]
private void RPC_UpdateDPSText(float dps, string playerName)
{
    if (FestivalBossUI.Instance != null)
    {
        FestivalBossUI.Instance.UpdateDPSDisplay(dps, playerName);
    }
}

private IEnumerator ReturnToMainMenuWithDelay(float delay)
{
    yield return new WaitForSeconds(delay);
    ReturnToMainMenu();
}
    
    private bool CheckConnection()
    {
        // Проверка интернета
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            Debug.Log("Нет интернет-соединения");
            return false;
        }
        
        // Проверка подключения к Photon
        if (!PhotonNetwork.IsConnected)
        {
            Debug.Log("Не подключено к Photon");
            return false;
        }
        
        return true;
    }
    
    private void ConnectToRoom()
{
    // ПРОВЕРКА: готов ли Photon к операциям с комнатами
    if (!PhotonNetwork.IsConnectedAndReady)
    {
        Debug.Log("Photon еще не готов, ждем...");
        StartCoroutine(WaitAndConnectToRoom());
        return;
    }
    
    // Поиск существующей комнаты или создание новой
    RoomOptions roomOptions = new RoomOptions();
    roomOptions.MaxPlayers = (byte)maxPlayers;
    roomOptions.IsVisible = true;
    
    PhotonNetwork.JoinRandomOrCreateRoom(roomOptions: roomOptions);
}
    private IEnumerator WaitAndConnectToRoom()
{
    float timeout = 5f;
    float startTime = Time.time;
    
    while (!PhotonNetwork.IsConnectedAndReady && (Time.time - startTime) < timeout)
    {
        yield return new WaitForSeconds(0.2f);
    }
    
    if (PhotonNetwork.IsConnectedAndReady)
    {
        ConnectToRoom();
    }
    else
    {
        Debug.LogError("Photon не готов после ожидания");
        ReturnToMainMenu();
    }
}

    public override void OnJoinedRoom()
{
    if (isInitialized) return;
    isInitialized = true;
    
    Debug.Log($"Присоединились к комнате: {PhotonNetwork.CurrentRoom.Name}");
    
    // ✅ ТОЛЬКО мастер-клиент создает босса
    if (PhotonNetwork.IsMasterClient)
    {
        // Проверяем, есть ли уже босс в сцене
        FestivalBoss existingBoss = FindObjectOfType<FestivalBoss>();
        
        if (existingBoss != null)
        {
            // Если босс уже есть (например, из предыдущей сессии)
            currentBoss = existingBoss;
            float savedHealth = LoadBossHealth();
            currentBoss.currentHP = savedHealth;
            currentBoss.UpdateHealthUI();
            currentBoss.StartAutoAttack();
            Debug.Log($"Найден существующий босс с HP: {savedHealth}");
        }
        else if (currentBoss == null)
        {
            // Создаем нового босса
            CreateBoss();
        }
    }
    
    SyncPlayerData();
    InitializeUI();
}

[PunRPC]
private void RPC_SyncBossHealth(float health)
{
    if (currentBoss != null)
    {
        currentBoss.currentHP = health;
        currentBoss.UpdateHealthUI();
    }
}

    
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.Log($"Ошибка входа в комнату: {message}");
        if (returnCode == ErrorCode.NoRandomMatchFound || returnCode == ErrorCode.GameFull)
        {
            ReturnToMainMenu();
        }
    }
    
    private void SyncPlayerData()
    {
        // Отправляем свои данные другим игрокам
        var playerData = new PlayerData
        {
            crystals = CrystalManager.Instance != null ? (int)CrystalManager.Instance.TotalCrystals : 0,
            coins = (int)GameManager.Instance.coins,
            playerName = PhotonNetwork.NickName,
            actorNumber = PhotonNetwork.LocalPlayer.ActorNumber
        };
        
        photonView.RPC("ReceivePlayerData", RpcTarget.Others, JsonUtility.ToJson(playerData));
    }
    
    [PunRPC]
    private void ReceivePlayerData(string dataJson, PhotonMessageInfo info)
    {
        var data = JsonUtility.FromJson<PlayerData>(dataJson);
        if (data == null || info.Sender == null) return;

        data.actorNumber = info.Sender.ActorNumber;
        data.playerName = info.Sender.NickName;
        playerData[info.Sender.ActorNumber] = data;
        
        // Обновляем UI
        UpdatePlayersUI();
    }
    
    private void CreateBoss()
{
    if (currentBoss != null)
    {
        Debug.Log("Босс уже существует");
        return;
    }
    
    if (bossPrefab != null && bossSpawnPoint != null && PhotonNetwork.IsMasterClient)
    {
        // Создаем босса как дочерний объект BossContainer
        GameObject bossObj = PhotonNetwork.Instantiate(bossPrefab.name, 
            bossSpawnPoint.position, bossSpawnPoint.rotation);
        
        // Делаем босса дочерним объектом BossContainer
        bossObj.transform.SetParent(bossSpawnPoint);
        
        currentBoss = bossObj.GetComponent<FestivalBoss>();
        
        float savedHealth = LoadBossHealth();
        currentBoss.currentHP = savedHealth;
        currentBoss.UpdateHealthUI();
        
        bossAlive = true;
        currentBoss.StartAutoAttack();
        
        Debug.Log($"Босс создан с HP: {savedHealth}");
    }
}
    
    private void InitializeUI()
    {
        // Инициализация UI для онлайн-режима
        if (FestivalBossUI.Instance != null)
        {
            FestivalBossUI.Instance.Initialize();
        }
    }
    
    private void UpdatePlayersUI()
    {
        if (FestivalBossUI.Instance != null)
        {
            FestivalBossUI.Instance.UpdatePlayersList(playerData);
        }
    }
    private void SaveBossHealth(float health)
{
    PlayerPrefs.SetFloat(SAVE_KEY_BOSS_HP, health);
    PlayerPrefs.SetString(SAVE_KEY_BOSS_DATE, System.DateTime.Now.ToString());
    PlayerPrefs.Save();
}

private float LoadBossHealth()
{
    if (!PlayerPrefs.HasKey(SAVE_KEY_BOSS_HP)) return 1000000f; // maxHealth
    
    // Проверка на 7 дней (опционально)
    if (PlayerPrefs.HasKey(SAVE_KEY_BOSS_DATE))
    {
        System.DateTime savedDate;
        if (System.DateTime.TryParse(PlayerPrefs.GetString(SAVE_KEY_BOSS_DATE), out savedDate))
        {
            if ((System.DateTime.Now - savedDate).TotalDays > 7)
                return 1000000f; // сброс через 7 дней
        }
    }
    
    return PlayerPrefs.GetFloat(SAVE_KEY_BOSS_HP);
}
 
public void SetHealth(float health)
{
    if (currentBoss != null)
    {
        currentBoss.currentHP = health;
        currentBoss.UpdateHealthUI();
    }
}
private string GetPlayerNameByActorNumber(int actorNumber)
{
    foreach (var player in PhotonNetwork.PlayerList)
    {
        if (player.ActorNumber == actorNumber)
            return player.NickName;
    }
    return "Игрок";
}
    
public void DealDamageToBoss(float damage)
{
    if (currentBoss == null || !PhotonNetwork.InRoom) return;
    if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f) return;

    photonView.RPC(nameof(RPC_DealDamage), RpcTarget.MasterClient, damage,
        PhotonNetwork.LocalPlayer.ActorNumber);
}
    
    [PunRPC]
private void RPC_DealDamage(float damage, int attackerActorNumber, PhotonMessageInfo info)
{
    if (!PhotonNetwork.IsMasterClient || currentBoss == null || !bossAlive) return;
    if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f) return;
    if (info.Sender == null || info.Sender.ActorNumber != attackerActorNumber) return;

    if (playerDamageHistory.ContainsKey(attackerActorNumber))
        playerDamageHistory[attackerActorNumber] += damage;
    else
        playerDamageHistory[attackerActorNumber] = damage;

    float oldHealth = currentBoss.currentHP;
    currentBoss.TakeDamage(damage);

    bool isKillingBlow = oldHealth > 0 && currentBoss.currentHP <= 0;
    if (!isKillingBlow || rewardGivenForThisBoss) return;

    rewardGivenForThisBoss = true;
    bossAlive = false;
    photonView.RPC(nameof(RPC_RewardPlayer), RpcTarget.All, attackerActorNumber);
    Invoke(nameof(RespawnBoss), 10f);
}
    
    [PunRPC]
private void RPC_RewardPlayer(int playerActorNumber)
{
    string killerName = "";
    
    if (PhotonNetwork.LocalPlayer.ActorNumber == playerActorNumber)
    {
        // Награда убийце
        CrystalManager.Instance?.AddCrystals(crystalReward, Vector3.zero);
        if (GameManager.Instance != null)
            GameManager.Instance.coins += coinReward;
        killerName = PhotonNetwork.NickName;
    }
    else
    {
        // Находим имя убийцы
        foreach (var player in PhotonNetwork.PlayerList)
        {
            if (player.ActorNumber == playerActorNumber)
            {
                killerName = player.NickName;
                break;
            }
        }
    }
    
    // Показываем уведомление всем
    if (FestivalBossUI.Instance != null)
    {
        FestivalBossUI.Instance.ShowBossDefeatedNotification(killerName);
    }
    
    bossAlive = false;
}
    
    private void RespawnBoss()
    {
        rewardGivenForThisBoss = false;
        
        if (PhotonNetwork.IsMasterClient && currentBoss != null)
        {
            currentBoss.Respawn();
            currentBoss.StartAutoAttack(); // Перезапускаем атаку после респавна
            bossAlive = true;
            photonView.RPC("RPC_BossRespawned", RpcTarget.All);
        }
    }
    
    [PunRPC]
    private void RPC_BossRespawned()
    {
        bossAlive = true;
        if (FestivalBossUI.Instance != null)
        {
            FestivalBossUI.Instance.OnBossRespawn();
            FestivalBossUI.Instance.ShowBossRespawnNotification(); 
        }
    }
    
    public void ReturnToMainMenu()
{
    // Сохраняем здоровье босса перед выходом
    if (currentBoss != null && PhotonNetwork.IsMasterClient)
    {
        SaveBossHealth(currentBoss.currentHP);
    }
    
    if (PhotonNetwork.InRoom)
    {
        PhotonNetwork.LeaveRoom();
    }
    else
    {
        SceneManager.LoadScene(mainMenuScene);
    }
}

public override void OnLeftRoom()
{
    isInitialized = false;
    playerData.Clear();
    playerDamageHistory.Clear();
    playerDPS.Clear();

    // Этот метод вызывается только если мы были в комнате
    SceneManager.LoadScene(mainMenuScene);
}
    
    // НОВЫЙ МЕТОД - вызывается из OnlineSceneInitializer
    public void OnSceneInitialized()
    {
        Debug.Log("FestivalBossManager: сцена инициализирована");
        
        if (FestivalBossUI.Instance != null)
        {
            FestivalBossUI.Instance.Initialize();
        }
    }
    
    [System.Serializable]
    public class PlayerData
    {
        public int crystals;
        public int coins;
        public string playerName;
        public int actorNumber;
    }
}
