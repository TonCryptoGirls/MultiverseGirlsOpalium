using UnityEngine;
using System.IO;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Класс для сохранения и загрузки данных игры.
/// </summary>
public static class SaveSystem
{
    private static readonly string savePath = Path.Combine(Application.persistentDataPath, "game-progress.json");
    private static readonly string backupPath = Path.Combine(Application.persistentDataPath, "game-progress.backup.json");
    private static Coroutine autoSaveCoroutine;
    private static bool isSaving = false;
    private static readonly object saveLock = new object();

    private static void WriteSaveDataAtomic(SaveData data)
    {
        if (data == null) throw new System.ArgumentNullException(nameof(data));

        string json = JsonUtility.ToJson(data, true);
        string tempPath = savePath + ".tmp";

        File.WriteAllText(tempPath, json);

        if (File.Exists(savePath))
        {
            File.Copy(savePath, backupPath, true);
            File.Delete(savePath);
        }

        File.Move(tempPath, savePath);
    }

    private static SaveData ReadSaveData()
    {
        if (!File.Exists(savePath)) return null;

        string json = File.ReadAllText(savePath);
        if (string.IsNullOrWhiteSpace(json)) return null;

        return JsonUtility.FromJson<SaveData>(json);
    }


    public static void StartAutoSave()
    {
        if (autoSaveCoroutine == null && GameManager.Instance != null)
        {
            autoSaveCoroutine = GameManager.Instance.StartCoroutine(AutoSaveHealth());
        }
    }

    public static void StopAutoSave()
    {
        if (autoSaveCoroutine != null && GameManager.Instance != null)
        {
            GameManager.Instance.StopCoroutine(autoSaveCoroutine);
            autoSaveCoroutine = null;
        }
    }

    private static IEnumerator AutoSaveHealth()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);
            if (GameManager.Instance != null)
            {
                SaveGame(GameManager.Instance);
            }
        }
    }

    /// <summary>
    /// Сохраняем прогресс игрока.
    /// </summary>
    /// <summary>
    /// Сохраняем прогресс игрока.
    /// </summary>
    public static void SaveGame(GameManager gameManager)
    {
        lock (saveLock)
        {
            if (isSaving)
            {
                Debug.LogWarning("Сохранение уже выполняется, пропускаем...");
                return;
            }
            isSaving = true;
        try
            {
                if (gameManager == null)
        {
            Debug.LogError("GameManager is null - cannot save");
            return;
        }

                try
                {
                    SaveData data = gameManager.GetSaveData();

                    if (data == null)
                    {
                        Debug.LogError("SaveData is null - cannot save");
                        return;
                    }

                    if (data.cardHealthData == null) data.cardHealthData = new List<SaveData.CardHealthData>();
                    if (data.ownedCardUIDs == null) data.ownedCardUIDs = new List<string>();
                    if (data.deckSlotsUIDs == null) data.deckSlotsUIDs = new List<string>();
                    if (data.cardLevels == null) data.cardLevels = new List<SaveData.CardLevelData>();


                    // ПРОВЕРКА МЕНЕДЖЕРОВ ПЕРЕД ИСПОЛЬЗОВАНИЕМ
                    if (EnemyManager.Instance != null)
                    {
                        data.currentWave = EnemyManager.Instance.GetWaveNumber();
                        data.currentLocation = EnemyManager.Instance.GetLocationLevel();
                    }
                    else
                    {
                        Debug.LogWarning("EnemyManager is null, using default wave values");
                        data.currentWave = 0;
                        data.currentLocation = 0;
                    }

                    // ПРОВЕРКА ПЕРЕД СОХРАНЕНИЕМ ДАННЫХ
                    SaveCardHealthData(data);
                    SaveDeckSlotsData(data);
                    SaveCardLevelsData(data);

                    WriteSaveDataAtomic(data);

                    Debug.Log("Игра успешно сохранена!");
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"Ошибка сохранения: {ex.Message}\n{ex.StackTrace}");

                    RetrySave(gameManager);
                }
                }
        finally
        {
            lock (saveLock)
            {
                isSaving = false;
            }
        }
    }
        
            
    }
    private static void RetrySave(GameManager gameManager)
    {
        try
        {
            Debug.Log("Повторная попытка сохранения через 0.1 секунды...");

            // Небольшая задержка через корутину
            if (gameManager != null)
            {
                gameManager.StartCoroutine(DelayedRetrySave(gameManager, 0.1f));
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Ошибка при повторной попытке сохранения: {ex.Message}");
        }
    }
    private static IEnumerator DelayedRetrySave(GameManager gameManager, float delay)
    {
        yield return new WaitForSeconds(delay);

        try
        {
            SaveData data = gameManager.GetSaveData();
            WriteSaveDataAtomic(data);

            Debug.Log("Повторное сохранение успешно!");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Повторное сохранение не удалось: {ex.Message}");
        }
    }




    private static void SaveCardHealthData(SaveData data)
{
    if (data == null) return;

    if (data.cardHealthData == null)
    {
        data.cardHealthData = new List<SaveData.CardHealthData>();
    }

    data.cardHealthData.Clear();

    Debug.Log("=== СОХРАНЕНИЕ ТОЛЬКО ЗДОРОВЬЯ КАРТ ===");

    if (GameManager.Instance == null || GameManager.Instance.activeCards == null) return;

    foreach (var cardBehaviour in GameManager.Instance.activeCards)
    {
        if (cardBehaviour != null && cardBehaviour.stats != null)
        {
            // СОХРАНЯЕМ ТОЛЬКО ЗДОРОВЬЕ, но не другие статы (урон, скорость и т.д.)
            data.cardHealthData.Add(new SaveData.CardHealthData
            {
                uid = cardBehaviour.stats.uid,
                currentHP = cardBehaviour.stats.currentHP,
                maxHP = cardBehaviour.stats.maxHP // сохраняем maxHP т.к. он может меняться от уровней
            });
            Debug.Log($"Сохранено здоровье карты: {cardBehaviour.stats.cardName}");
        }
    }
}



    private static void SaveCardLevelsData(SaveData data)
    {
        if (data == null) return;
        
        if (data.cardLevels == null)
        {
            data.cardLevels = new List<SaveData.CardLevelData>();
        }
    
        data.cardLevels.Clear();

        // Сохраняем уровни всех карт в инвентаре
        if (CardInventoryManager.Instance != null && CardInventoryManager.Instance.allOwnedCards != null)
        {
            foreach (var card in CardInventoryManager.Instance.allOwnedCards)
            {
                if (card != null)
                {
                    data.cardLevels.Add(new SaveData.CardLevelData
                    {
                        uid = card.uid,
                        level = card.level
                    });
                    Debug.Log($"Сохранен уровень для {card.cardName}: уровень {card.level}");
                }
            }
        }
    }

    private static void SaveDeckSlotsData(SaveData data)
    {
        if (data == null) return;

        if (data.deckSlotsUIDs == null)
        {
            data.deckSlotsUIDs = new List<string>();
        }

        data.deckSlotsUIDs.Clear();

        // Сохраняем deck slots из CardInventoryManager
        if (CardInventoryManager.Instance != null && CardInventoryManager.Instance.deckSlots != null)
        {
            foreach (var slot in CardInventoryManager.Instance.deckSlots)
            {
                if (slot != null)
                {
                    string uid = slot.assignedCard != null ? slot.assignedCard.uid : "";
                    data.deckSlotsUIDs.Add(uid);
                    Debug.Log($"Сохранен слот: {uid}");
                }
            }
        }
    }

    /// <summary>
    /// Загружаем прогресс игрока.
    /// </summary>
    public static void LoadGame(GameManager gameManager)
    {
        if (gameManager == null)
        {
            Debug.LogError("GameManager is null - cannot load");
            return;
        }

        if (!File.Exists(savePath))
        {
            Debug.Log("Файл сохранения не существует");
            return;
        }

        try
        {
            FileInfo fileInfo = new FileInfo(savePath);
            if (fileInfo.Length == 0)
            {
                Debug.LogWarning("Файл сохранения пуст");
                File.Delete(savePath);
                return;
            }

            SaveData data = ReadSaveData();
            if (data != null)
            {
                if (data.cardHealthData == null) data.cardHealthData = new List<SaveData.CardHealthData>();
                if (data.ownedCardUIDs == null) data.ownedCardUIDs = new List<string>();
                if (data.deckSlotsUIDs == null) data.deckSlotsUIDs = new List<string>();
                if (data.cardLevels == null) data.cardLevels = new List<SaveData.CardLevelData>();

                gameManager.LoadFromSave(data);
                LoadCardLevelsData(data);
                LoadDeckSlotsData(data);
                gameManager.StartCoroutine(DelayedLoadHealth(data));
            }
            else
            {
                Debug.LogError("Не удалось прочитать данные сохранения");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Ошибка загрузки: {ex.Message}");
            CreateBackupOfCorruptedSave();
        }
    }
    private static IEnumerator DelayedLoadHealth(SaveData data)
{
    yield return null; // Ждём один кадр
    LoadCardHealthData(data);
}

    private static void LoadDeckSlotsData(SaveData data)
    {
        if (data?.deckSlotsUIDs == null)
        {
            Debug.Log("Нет данных о слотах для загрузки");
            return;
        }

        if (CardInventoryManager.Instance != null && CardInventoryManager.Instance.deckSlots != null)
        {
            for (int i = 0; i < data.deckSlotsUIDs.Count && i < CardInventoryManager.Instance.deckSlots.Count; i++)
            {
                var uid = data.deckSlotsUIDs[i];
                if (!string.IsNullOrEmpty(uid))
                {
                    var card = CardInventoryManager.Instance.GetCardByUID(uid);
                    if (card != null)
                    {
                        CardInventoryManager.Instance.deckSlots[i].AssignCard(card);
                        Debug.Log($"Загружена карта в слот {i}: {card.cardName}");
                    }
                }
            }
        }
    }
    private static void LoadCardHealthData(SaveData data)
{
    if (data?.cardHealthData == null)
    {
        Debug.Log("Нет данных о здоровье для загрузки");
        return;
    }

    Debug.Log("=== ЗАГРУЗКА ЗДОРОВЬЯ АКТИВНЫХ КАРТ ===");

    // Сначала ждем полной инициализации всех карт
    if (GameManager.Instance != null && GameManager.Instance.activeCards != null)
    {
        foreach (var cardBehaviour in GameManager.Instance.activeCards)
        {
            if (cardBehaviour != null && cardBehaviour.stats != null)
            {
                var healthData = data.cardHealthData.Find(x => x.uid == cardBehaviour.stats.uid);
                if (healthData != null)
                {
                    // Загружаем сохраненное здоровье
                    cardBehaviour.stats.currentHP = healthData.currentHP;
                    cardBehaviour.stats.maxHP = healthData.maxHP;

                    // ОБНОВЛЯЕМ UI КАРТЫ
                    cardBehaviour.UpdateHPUI();

                    // ПРОВЕРЯЕМ И СИНХРОНИЗИРУЕМ СОСТОЯНИЕ КАРТЫ
                    if (cardBehaviour.stats.currentHP <= 0)
                    {
                        // Гарантируем смерть карты
                        cardBehaviour.isDead = true;
                        cardBehaviour.StopAllCoroutines();
                        cardBehaviour.StartCoroutine(cardBehaviour.FadeOut(0.1f)); // Быстрое затемнение
                        cardBehaviour.enabled = false;
                        Debug.Log($"Карта {cardBehaviour.stats.cardName} мертва после загрузки");
                    }
                    else
                    {
                        // Гарантируем что карта жива
                        cardBehaviour.isDead = false;
                        cardBehaviour.StopAllCoroutines();
                        cardBehaviour.StartCoroutine(cardBehaviour.InstantFadeIn());
                        cardBehaviour.enabled = true;
                        Debug.Log($"Карта {cardBehaviour.stats.cardName} жива после загрузки: {cardBehaviour.stats.currentHP}/{cardBehaviour.stats.maxHP}");
                    }
                }
            }
        }
    }
}



    private static void LoadCardLevelsData(SaveData data)
{
    if (data?.cardLevels == null) return;

    Debug.Log("=== ЗАГРУЗКА УРОВНЕЙ КАРТ ===");

    // Загружаем уровни карт только в инвентарь (НЕ в активные карты на поле)
    if (CardInventoryManager.Instance != null)
    {
        foreach (var levelData in data.cardLevels)
        {
            if (levelData != null)
            {
                var card = CardInventoryManager.Instance.GetCardByUID(levelData.uid);
                if (card != null)
                {
                    // Применяем уровень к карте в инвентаре
                    card.ApplyLevel(levelData.level, true); // ← ВАЖНО: preserveCurrentHealth = true
                    Debug.Log($"Загружен уровень для карты в инвентаре {card.cardName}: уровень {card.level}");
                }
            }
        }
    }

    // НЕ применяем уровни к активным картам на поле!
    // Они уже имеют правильные уровни и мы не хотим сбрасывать здоровье
}

public static List<string> LoadDeckForOnline()
{
    List<string> deck = new List<string>();
    
    if (!SaveExists())
    {
        Debug.Log("Файл сохранения не существует, возвращаем пустую колоду");
        return deck;
    }

    try
    {
        SaveData data = ReadSaveData();
        if (data?.deckSlotsUIDs != null)
        {
            foreach (string uid in data.deckSlotsUIDs)
            {
                if (!string.IsNullOrEmpty(uid))
                    deck.Add(uid);
            }

            Debug.Log($"Загружено {deck.Count} карт из сохранения для онлайн-режима");
        }
    }
    catch (System.Exception ex)
    {
        Debug.LogError($"Ошибка загрузки колоды для онлайн-режима: {ex.Message}");
    }
    
    return deck;
}

// Альтернативный метод для загрузки CardStats объектов
public static List<CardStats> LoadDeckCardsForOnline()
{
    List<CardStats> deckCards = new List<CardStats>();
    
    var deckUIDs = LoadDeckForOnline();
    foreach (string uid in deckUIDs)
    {
        if (CardDatabase.Instance != null)
        {
            var card = CardDatabase.Instance.GetCardByUID(uid);
            if (card != null)
            {
                deckCards.Add(card);
            }
        }
    }
    
    return deckCards;
}

    /// <summary>
    /// Создает резервную копию поврежденного файла сохранения
    /// </summary>
    private static void CreateBackupOfCorruptedSave()
    {
        try
        {
            if (File.Exists(savePath))
            {
                string corruptedPath = savePath + ".corrupted." + System.DateTime.UtcNow.Ticks;
                File.Move(savePath, corruptedPath);
                Debug.Log($"Создана резервная копия поврежденного файла: {corruptedPath}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Не удалось создать резервную копию: {ex.Message}");
        }
    }

    /// <summary>
    /// Проверяет существование файла сохранения
    /// </summary>
    public static bool SaveExists()
    {
        return File.Exists(savePath);
    }

    /// <summary>
    /// Удаляет файл сохранения
    /// </summary>
    public static void DeleteSave()
    {
        try
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
                Debug.Log("Файл сохранения удален");
            }

            if (File.Exists(backupPath))
                File.Delete(backupPath);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Не удалось удалить файл сохранения: {ex.Message}");
        }
    }
}
