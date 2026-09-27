using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CardInventoryManager : MonoBehaviour
{
    public static CardInventoryManager Instance;

    [Header("Карты игрока")]
    public List<CardStats> allOwnedCards = new();
    public List<CardStats> activeCardStats = new();
    public int maxActiveCards = 3;

    [Header("Слоты инвентаря")]
    public List<InventorySlot> inventorySlots = new();

    [Header("Слоты колоды")]
    public List<DeckSlotReceiver> deckSlots = new();

    public delegate void CardAdded(CardStats stats);
    public event CardAdded onCardAdded;
    [Header("Inventory State")]
    private bool isInventoryOpen = false;

    public bool IsInventoryOpen => isInventoryOpen;

    public delegate void DeckSlotChangedHandler(int slotIndex, CardStats newCard);
    public event DeckSlotChangedHandler onDeckSlotChanged;
    public event System.Action onDeckSlotsRestored;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (deckSlots == null) deckSlots = new List<DeckSlotReceiver>();
        StartCoroutine(DelayedLoadInventory());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // ПЕРЕМЕСТИ логику поиска слотов сюда
        StartCoroutine(FindDeckSlotsOnSceneLoad());
    }

    private IEnumerator FindDeckSlotsOnSceneLoad()
    {
        yield return null; // Ждём один кадр

        // Ищем все DeckSlotReceiver на сцене
        var foundSlots = FindObjectsOfType<DeckSlotReceiver>()
            .Where(slot => slot.IsDeckSlot())
            .ToList();

        if (foundSlots.Count > 0)
        {
            deckSlots = foundSlots;
            // Сортируем по slotIndex для правильного порядка
            deckSlots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
            
        }

        RefreshInventorySlots();
        RestoreCardsToSlots();
    }

    [Header("Стартовые карты")]
    public List<CardStats> startingCards = new List<CardStats>();


    private IEnumerator DelayedLoadInventory()
    {
        while (CardDatabase.Instance == null)
            yield return null;

        SaveLoadManager.Instance?.LoadInventory(this);

        foreach (var card in startingCards)
        {
            if (card != null && !allOwnedCards.Any(owned => owned != null && owned.uid == card.uid))
            {
                allOwnedCards.Add(card);
            }
        }

        RefreshInventorySlots();

        yield return null;
        RestoreCardsToSlots();
    }
    public void LoadInventoryCards()
    {
        // После загрузки карт из SaveLoadManager
        foreach (var card in allOwnedCards)
        {
            if (card != null && card.currentHP <= 0)
            {
                card.currentHP = card.maxHP; // Сбрасываем только если HP = 0
            }
        }
    }



    private IEnumerator AddStartingCards()
    {
        // Проверяем чтобы не добавлять повторно при перезагрузке сцены
        if (PlayerPrefs.GetInt("StartingCardsAdded", 0) == 1)
        {
            
            yield break;
        }

        yield return null; // Ждем один кадр

        // Находим карту Dori в базе данных
        CardStats doriCard = CardDatabase.Instance?.GetCardByUID("dori");

        // Если не нашли по UID "dori", пробуем найти по имени
        if (doriCard == null)
        {
            doriCard = CardDatabase.Instance?.allCards?
                .FirstOrDefault(c => c != null && c.cardName.ToLower().Contains("dori"));
        }

        if (doriCard == null)
        {
            
            yield break;
        }

        // Проверяем по UID, а не по ссылке
        if (allOwnedCards.Any(c => c != null && c.uid == doriCard.uid))
        {
            
            yield break;
        }

        AddCard(doriCard);
        

        // Немедленно сохраняем
        SaveCurrentInventory();

        // Даем время на сохранение
        yield return null;

        // Двойная проверка
        bool successfullyAdded = allOwnedCards.Any(c => c != null && c.uid == doriCard.uid);
        

        PlayerPrefs.SetInt("StartingCardsAdded", 1);
        PlayerPrefs.Save();

        
    }

    #region Работа с картами

    public void AddCard(CardStats card)
    {
        if (card == null)
        {
            
            return;
        }

        if (allOwnedCards.Any(c => c != null && c.uid == card.uid))
        {
            
            return;
        }

        allOwnedCards.Add(card);
        onCardAdded?.Invoke(card);

       

        int freeIndex = inventorySlots.FindIndex(s => s != null && s.assignedCard == null);
        if (freeIndex >= 0)
            ReturnCardToInventorySlot(card, freeIndex);

        SaveCurrentInventory();
    }




    public bool ActivateCard(CardStats card)
    {
        if (!allOwnedCards.Contains(card) || activeCardStats.Contains(card)) return false;
        if (activeCardStats.Count >= maxActiveCards) return false;

        activeCardStats.Add(card);
        SaveCurrentInventory();
        return true;
    }

    public void DeactivateCard(CardStats card)
    {
        if (activeCardStats.Remove(card))
            SaveCurrentInventory();
    }

    public void ClearActiveCards()
    {
        activeCardStats.Clear();
        SaveCurrentInventory();
    }

    public CardStats GetCardByUID(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return null;
        return allOwnedCards.FirstOrDefault(c => c != null && c.uid == uid);
    }

    #endregion

    #region Работа со слотами

    public void RefreshInventorySlots()
    {
        foreach (var slot in inventorySlots)
        {
            if (slot != null && slot.assignedCard != null)
                slot.SetCard(slot.assignedCard);
        }
    }

    public bool TryAssignCardToInventorySlot(CardStats stats, int index, Transform cardObject = null)
    {
        if (stats == null) return false;
        if (index < 0 || index >= inventorySlots.Count) return false;

        var slot = inventorySlots[index];
        if (slot == null || slot.assignedCard != null) return false;

        stats.lastInventorySlotIndex = index;
        slot.SetCard(stats);

        if (cardObject != null)
        {
            cardObject.SetParent(slot.transform, false);
            cardObject.localPosition = Vector3.zero;
        }

        return true;
    }

    public void ReturnCardToSlot(CardStats stats, int index, Transform cardObject = null)
    {
        if (stats == null || index < 0) return;

        if (index < inventorySlots.Count)
            ReturnCardToInventorySlot(stats, index, cardObject);
        else if (index < inventorySlots.Count + deckSlots.Count)
            ReturnCardToDeckSlot(stats, index - inventorySlots.Count, cardObject);
          
    }

    public void ReturnCardToInventorySlot(CardStats stats, int index, Transform cardObject = null)
    {
        if (stats == null) return;
        if (index < 0 || index >= inventorySlots.Count) return;

        var slot = inventorySlots[index];
        if (slot == null) return;

        stats.lastInventorySlotIndex = index;
        slot.SetCard(stats);

        if (cardObject != null)
        {
            cardObject.SetParent(slot.transform, false);
            cardObject.localPosition = Vector3.zero;
        }
    }
    
    public void SetInventoryOpen(bool open)
    {
        isInventoryOpen = open;
      

        if (!open)
        {
            ReturnAllDraggableCardsToHome();
        }
    }

    public void ReturnAllDraggableCardsToHome()
{
    var allDraggableCards = FindObjectsOfType<DraggableCard>();

    foreach (var draggable in allDraggableCards)
    {
        if (draggable != null && !draggable.isSlotPrefab)
        {
            // ВОЗВРАЩАЕМ ВСЕ КАРТЫ, КОТОРЫЕ НЕ ЯВЛЯЮТСЯ ПРЕФАБАМИ В СЛОТАХ
            draggable.ReturnToHome();
        }
    }
}

    private bool IsCardAPrefabInSlot(DraggableCard draggable)
    {
        // Проверяем, является ли эта карта префабом в слоте колоды
        var deckSlot = draggable.transform.parent?.GetComponent<DeckSlotReceiver>();
        if (deckSlot != null && deckSlot.assignedCard != null)
        {
            return true;
        }
        return false;
    }

    public void ReturnCardToDeckSlot(CardStats stats, int index, Transform cardObject = null)
    {
        if (stats == null) return;
        if (index < 0 || index >= deckSlots.Count) return;

        var slot = deckSlots[index];
        if (slot == null) return;

        stats.lastInventorySlotIndex = inventorySlots.Count + index;
        slot.AssignCard(stats);

        if (cardObject != null)
        {
            cardObject.SetParent(slot.transform, false);
            cardObject.localPosition = Vector3.zero;
        }

        onDeckSlotChanged?.Invoke(index, stats);
    }

    public bool TryAssignCardToDeckSlot(CardStats stats, int index, Transform cardObject = null)
    {
        if (stats == null) return false;
        if (index < 0 || index >= deckSlots.Count) return false;

        var slot = deckSlots[index];
        if (slot == null || slot.assignedCard != null) return false;

        stats.lastInventorySlotIndex = inventorySlots.Count + index;
        
        // Вызываем AssignCard который уже содержит логику сохранения
        slot.AssignCard(stats);

        if (cardObject != null)
        {
            cardObject.SetParent(slot.transform, false);
            cardObject.localPosition = Vector3.zero;
        }

        onDeckSlotChanged?.Invoke(index, stats);
        SaveCurrentInventory(); // Дополнительное сохранение
        return true;
    }

    #endregion

    public void RestoreCardsToSlots()
    {
        if (deckSlots == null || deckSlots.Count == 0)
        {
            Debug.LogWarning("[CardInventoryManager] deckSlots is null or empty - trying to find slots");
            var foundSlots = FindObjectsOfType<DeckSlotReceiver>()
                .Where(slot => slot.IsDeckSlot())
                .ToList();

            if (foundSlots.Count > 0)
            {
                deckSlots = foundSlots;
                deckSlots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
                
            }
            else
            {
                
                return;
            }
        }

      

        // Suspend persistence while rebuilding the deck UI to avoid saving partial state.
        bool oldSuspendState = SaveLoadManager.Instance != null ? SaveLoadManager.Instance._suspendSave : false;
        if (SaveLoadManager.Instance != null)
            SaveLoadManager.Instance._suspendSave = true;

        try
        {
            foreach (var slot in deckSlots)
            {
                if (slot == null)
                {
                   
                    continue;
                }

                string savedUID = SaveLoadManager.Instance?.GetDeckSlotUID(slot.slotIndex);
                CardStats card = null;

                if (!string.IsNullOrEmpty(savedUID))
                {
                    card = GetCardByUID(savedUID) ?? CardDatabase.Instance?.GetCardByUID(savedUID);
                  
                }

                if (card != null)
                {
                    // ВОССТАНАВЛИВАЕМ КАРТУ В СЛОТ
                    slot.AssignCard(card, null, false);

                    if (card.prefab != null)
                    {
                        // Очищаем старые префабы
                        for (int i = slot.transform.childCount - 1; i >= 0; i--)
                        {
                            var child = slot.transform.GetChild(i);
                            if (child.GetComponent<CardUI>() != null || child.GetComponent<CardBehaviour>() != null)
                            {
                                Destroy(child.gameObject);
                            }
                        }

                        // Создаем новый префаб
                        GameObject cardInstance = Instantiate(card.prefab, slot.transform, false);
                        cardInstance.transform.localPosition = Vector3.zero;
                        cardInstance.transform.localRotation = Quaternion.identity;

                        var cardUI = cardInstance.GetComponent<CardUI>();
                        if (cardUI != null) cardUI.Setup(card);

                        var draggable = cardInstance.GetComponent<DraggableCard>();
                        if (draggable != null) draggable.enabled = false;
                    }

                    
                }
                else
                {
                    // Очищаем без сохранения
                    slot.ClearSlot(false);
                   
                }
            }
        }
        finally
        {
            // ВОССТАНАВЛИВАЕМ СОСТОЯНИЕ СОХРАНЕНИЯ
            if (SaveLoadManager.Instance != null)
                SaveLoadManager.Instance._suspendSave = oldSuspendState;
        }

        onDeckSlotsRestored?.Invoke();
    }
    public void ValidateAlwaysAvailableCard()
{
    if (GameManager.Instance?.alwaysAvailableCard != null)
    {
        if (!allOwnedCards.Contains(GameManager.Instance.alwaysAvailableCard))
        {
            allOwnedCards.Add(GameManager.Instance.alwaysAvailableCard);
            
        }
    }
}



    private void SaveCurrentInventory()
    {
        SaveLoadManager.Instance?.SaveInventory(allOwnedCards, deckSlots, inventorySlots);
        SaveLoadManager.Instance?.SaveDeckToPlayerPrefs(deckSlots);
    }

    private IEnumerator RestoreDeckSlotsNextFrame()
    {
        yield return null;

        if (deckSlots == null || deckSlots.Count == 0)
        {
            var foundSlots = FindObjectsOfType<DeckSlotReceiver>()
                .Where(slot => slot.IsDeckSlot())
                .ToList();
                
            if (foundSlots.Count > 0)
            {
                deckSlots = foundSlots;
                deckSlots.Sort((a, b) => a.slotIndex.CompareTo(b.slotIndex));
            }
        }

        RestoreCardsToSlots();
    }
}
