using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;

[Serializable]
public sealed class NFTCardReward
{
    public string card_id;
    public string card_name;
    public string rarity;
    public string nft_name;
    public string collection;
    public string description;
}

[Serializable]
public sealed class NFTRewardResponse
{
    public bool success;
    public string wallet_address;
    public int nft_count;
    public int eligible_nfts;
    public NFTCardReward[] reward_cards;
    public string message;
}

public sealed class NFTRewardSystem : MonoBehaviour
{
    private const string InstallIdKey = "NFTRewardInstallId";

    public static NFTRewardSystem Instance { get; private set; }

    [Header("Settings")]
    [SerializeField, FormerlySerializedAs("apiURL")]
    private string apiUrl = "https://opalium-multiverse.com/nft-collections.php";

    [SerializeField, Min(1)] private int requestTimeoutSeconds = 15;
    [SerializeField, Min(0.1f)] private float rewardPanelDurationSeconds = 5f;

    [Header("UI")]
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private Text rewardText;

    private bool requestInProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void OnWalletConnected(string walletAddress)
    {
        if (string.IsNullOrWhiteSpace(walletAddress))
        {
            Debug.LogWarning("NFT reward check skipped: wallet address is empty.");
            return;
        }

        if (requestInProgress)
            return;

        StartCoroutine(CheckNFTRewards(walletAddress.Trim()));
    }

    public void ManualCheckNFT(string walletAddress)
    {
        OnWalletConnected(walletAddress);
    }

    private IEnumerator CheckNFTRewards(string walletAddress)
    {
        requestInProgress = true;

        string installId = GetOrCreateAnonymousInstallId();
        string url =
            $"{apiUrl}?action=check-and-reward" +
            $"&wallet_address={UnityWebRequest.EscapeURL(walletAddress)}" +
            $"&user_id={UnityWebRequest.EscapeURL($"unity_{installId}")}";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = requestTimeoutSeconds;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"NFT reward request failed with status {request.responseCode}: {request.error}");
                requestInProgress = false;
                yield break;
            }

            string responseJson = request.downloadHandler?.text;
            if (string.IsNullOrWhiteSpace(responseJson))
            {
                Debug.LogWarning("NFT reward service returned an empty response.");
                requestInProgress = false;
                yield break;
            }

            NFTRewardResponse response;
            try
            {
                response = JsonUtility.FromJson<NFTRewardResponse>(responseJson);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Unable to parse NFT reward response: {exception.Message}");
                requestInProgress = false;
                yield break;
            }

            if (response == null || !response.success)
            {
                Debug.LogWarning(response?.message ?? "NFT reward service reported an unsuccessful result.");
                requestInProgress = false;
                yield break;
            }

            if (response.reward_cards == null || response.reward_cards.Length == 0)
            {
                requestInProgress = false;
                yield break;
            }

            int cardsAdded = GiveCardsToPlayer(response.reward_cards);
            if (cardsAdded > 0)
            {
                SaveInventory();
                ShowRewardMessage(response.reward_cards, response.eligible_nfts);
            }
        }

        requestInProgress = false;
    }

    private static string GetOrCreateAnonymousInstallId()
    {
        string installId = PlayerPrefs.GetString(InstallIdKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(installId))
            return installId;

        installId = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(InstallIdKey, installId);
        PlayerPrefs.Save();
        return installId;
    }

    private static int GiveCardsToPlayer(NFTCardReward[] cards)
    {
        if (CardDatabase.Instance == null || CardInventoryManager.Instance == null)
        {
            Debug.LogWarning("NFT rewards cannot be applied because a required manager is unavailable.");
            return 0;
        }

        int added = 0;
        foreach (NFTCardReward reward in cards)
        {
            if (reward == null || string.IsNullOrWhiteSpace(reward.card_name))
                continue;

            CardStats card = CardDatabase.Instance.GetCardByName(reward.card_name);
            if (card == null)
            {
                Debug.LogWarning($"Reward card is not present in the local database: {reward.card_name}");
                continue;
            }

            int countBefore = CardInventoryManager.Instance.allOwnedCards.Count;
            CardInventoryManager.Instance.AddCard(card);
            if (CardInventoryManager.Instance.allOwnedCards.Count > countBefore)
                added++;
        }

        return added;
    }

    private static void SaveInventory()
    {
        if (SaveLoadManager.Instance == null || CardInventoryManager.Instance == null)
            return;

        SaveLoadManager.Instance.SaveInventory(
            CardInventoryManager.Instance.allOwnedCards,
            CardInventoryManager.Instance.deckSlots,
            CardInventoryManager.Instance.inventorySlots);
    }

    private void ShowRewardMessage(NFTCardReward[] cards, int eligibleCount)
    {
        StringBuilder message = new StringBuilder();
        message.AppendLine($"Found {eligibleCount} eligible NFT rewards:");

        foreach (NFTCardReward card in cards)
        {
            if (card == null) continue;
            message.AppendLine($"• {card.card_name} ({card.rarity})");
        }

        if (rewardText != null)
            rewardText.text = message.ToString();

        if (rewardPanel != null)
        {
            rewardPanel.SetActive(true);
            StartCoroutine(HideRewardPanel());
        }

        SoundManager.Instance?.PlaySFX("GetCard");
    }

    private IEnumerator HideRewardPanel()
    {
        yield return new WaitForSeconds(rewardPanelDurationSeconds);
        rewardPanel?.SetActive(false);
    }
}
