using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
[RequireComponent(typeof(Button))]
public class WheelSpinner : MonoBehaviour
{
    [System.Serializable]
    public class Reward
    {
        public Sprite image => sliceImage != null ? sliceImage.sprite : null;
        [Tooltip("Hierarchy'deki bu yuvanın küçük ödül Image objesi.")]
        public Image sliceImage;
        public string rewardId;
        public bool isBomb;
        [Tooltip("Bu yuva sandıksa işaretle.")]
        public bool isChest;
        [Min(0)] public int cardMultiplier = 1;
        [Min(0)] public int weaponMultiplier = 1;
        public ChestItem[] cards = new ChestItem[0];
        public ChestItem[] weapons = new ChestItem[0];
        [Min(1)] public int baseAmount = 1;
        public TMP_Text amountText;
    }
    [System.Serializable]
    public class ChestItem
    {
        [Tooltip("Collected'da görünecek benzersiz ad. Örn: Card Armor / Weapon Shotgun")]
        public string id;
        public Sprite image;
    }

    [Header("Sandık açılışı")]
    [SerializeField] private Sprite cardFrame;
    [SerializeField] private Sprite cardBackground;
    private GameObject chestContents;
    private Button chestButton;
    private ChestItem pendingCard, pendingWeapon;
    private long cardAmount, weaponAmount;
    private Vector3 originalRewardScale;

    [Header("Çark")]
    [SerializeField] private RectTransform wheel;
    [SerializeField] private Button spinButton;
    [SerializeField, Min(1)] private int fullTurns = 5;
    [SerializeField, Min(0.1f)] private float duration = 4f;
    [Header("Ödül Penceresi")]
    [SerializeField] private GameObject rewardPanel;
    [SerializeField] private Image rewardImage;
    [SerializeField] private TMP_Text rewardAmount;
    [SerializeField] private Button confirmButton;
    [SerializeField] private GameObject rewardFlash;
    [Header("Ekrandaki Yazılar")]
    [SerializeField] private TMP_Text collectedText;
    [Header("Sadece Silver ve Golden için")]
    [SerializeField] private Button leaveButton;
    [Header("Her zone için başlangıç miktarına eklenecek artış")]
    [SerializeField, Min(0f)] private float growthPerZone = 0.1f;
    [Header("Üstteki yuvadan başlayarak saat yönünde")]
    [SerializeField] private Reward[] rewards = new Reward[8];
    private enum State
    {
        Ready,
        Spinning,
        RewardOpen,
        ChestClosed,
        ChestOpening,
        Lost,
        Exited,
        Loading
    }
    private State state = State.Ready;
    private TMP_Text confirmLabel;
    private bool configured;
    private Reward pendingReward;
    private long pendingAmount;
    private void OnValidate()
{
    spinButton = GetComponent<Button>();
    Canvas canvas = GetComponentInParent<Canvas>();
    if (canvas == null)
        return;
    Button[] buttons = canvas.GetComponentsInChildren<Button>(true);
    confirmButton = FindButton(buttons, "ui_button_spin_reward_claim");
    leaveButton = FindButton(buttons, "ui_button_spin_leave");
}
    private Button FindButton(Button[] buttons, string objectName)
{
        foreach (Button button in buttons)
        {
            if (button.name == objectName)
                return button;
        }
        return null;
}
    private void Awake()
    {
        spinButton = GetComponent<Button>();
        if (rewardPanel != null)
            rewardPanel.SetActive(false);
        if (rewardImage != null)
        {
            originalRewardScale = rewardImage.rectTransform.localScale;
            chestButton = rewardImage.GetComponent<Button>();
            if (chestButton == null) chestButton = rewardImage.gameObject.AddComponent<Button>();
            chestButton.targetGraphic = rewardImage;
            chestButton.transition = Selectable.Transition.None;
            chestButton.onClick.AddListener(OpenChest);
            rewardImage.raycastTarget = false;
            chestButton.interactable = false;
        }
        if (confirmButton != null)
        {
            confirmLabel =
                confirmButton.GetComponentInChildren<TMP_Text>(true);
        }
    }
    private void OnEnable()
    {
        spinButton.onClick.AddListener(Spin);
        if (confirmButton != null)
            confirmButton.onClick.AddListener(Confirm);
        if (leaveButton != null)
            leaveButton.onClick.AddListener(Leave);
    }
    private void OnDisable()
    {
        if (spinButton != null)
            spinButton.onClick.RemoveListener(Spin);
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(Confirm);
        if (leaveButton != null)
            leaveButton.onClick.RemoveListener(Leave);
        StopAllCoroutines();
        if (state == State.Spinning)
            state = State.Ready;
    }
    private void Start()
    {
        if (SceneManager.GetActiveScene().buildIndex
            != GameManager.SceneIndex)
        {
            LoadZoneScene();
            return;
        }
        configured = ValidateSetup();
        if (configured)
        {
            wheel.localRotation = Quaternion.identity;
            RefreshSliceVisuals(false);
        }
        RefreshUI();
    }
    public void RefreshSliceVisuals(bool editorPreview)
    {
        if (rewards == null) return;
        foreach (Reward reward in rewards)
        {
            if (reward == null) continue;
            if (reward.sliceImage != null &&
                !reward.sliceImage.preserveAspect)
            {
#if UNITY_EDITOR
                if (editorPreview)
                    UnityEditor.Undo.RecordObject(reward.sliceImage, "Update slice image");
#endif
                reward.sliceImage.preserveAspect = true;
#if UNITY_EDITOR
                if (editorPreview)
                {
                    UnityEditor.EditorUtility.SetDirty(reward.sliceImage);
                    UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(
                        reward.sliceImage);
                }
#endif
            }
            if (reward.amountText == null) continue;
            long amount = editorPreview
                ? System.Math.Max(1, reward.baseAmount)
                : AmountFor(reward);
            string label = reward.isBomb ? "" : "x" + FormatAmount(amount);
            if (reward.amountText.text == label) continue;
#if UNITY_EDITOR
            if (editorPreview)
                UnityEditor.Undo.RecordObject(reward.amountText, "Update slice amount");
#endif
            reward.amountText.text = label;
#if UNITY_EDITOR
            if (editorPreview)
            {
                UnityEditor.EditorUtility.SetDirty(reward.amountText);
                UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(
                    reward.amountText);
            }
#endif
        }
    }
    private bool ValidateSetup()
    {
        if (wheel == null ||
            rewardPanel == null ||
            rewardImage == null ||
            rewardAmount == null ||
            confirmButton == null ||
            confirmLabel == null ||
            collectedText == null)
        {
            return SetupError(
                "Wheel Spinner: Inspector'daki UI alanlarını doldur."
            );
        }
        if (GameManager.IsSafe && leaveButton == null)
        {
            return SetupError(
                "Silver ve Golden sahnelerinde Leave Button bağlanmalı."
            );
        }
        if (rewards == null || rewards.Length < 2)
        {
            return SetupError(
                "Rewards listesine çarkın bütün yuvalarını ekle."
            );
        }
        int bombCount = 0;
        for (int i = 0; i < rewards.Length; i++)
        {
            Reward reward = rewards[i];
            if (reward == null || reward.image == null)
                return SetupError($"Element {i}: Image eksik.");
            if (reward.sliceImage == null)
                return SetupError($"Element {i}: Slice Image alanını bağla.");
            if (reward.isBomb)
            {
                bombCount++;
                continue;
            }
            if (string.IsNullOrWhiteSpace(reward.rewardId) ||
                reward.baseAmount < 1 ||
                reward.amountText == null)
            {
                return SetupError(
                    $"Element {i}: Reward Id, Base Amount ve " +
                    "Amount Text alanlarını doldur."
                );
            }
        }
        foreach (Reward reward in rewards)
        {
            if (!reward.isChest || reward.isBomb) continue;
            if (reward.cardMultiplier < 0 || reward.weaponMultiplier < 0 ||
                (reward.cardMultiplier == 0 && reward.weaponMultiplier == 0))
                return SetupError("Sandıkta en az bir ödül çarpanı pozitif olmalı.");
            if (!ValidPool(reward.cards, reward.cardMultiplier) ||
                !ValidPool(reward.weapons, reward.weaponMultiplier))
                return SetupError("Sandık: kullanılan Cards/Weapons listesinin bütün Id ve Image alanlarını doldur.");
        }

        int requiredBombCount = GameManager.IsSafe ? 0 : 1;
        if (bombCount != requiredBombCount)
        {
            return SetupError(
                "Bronze 1 bomba içermeli. Silver ve Golden'da bomba olmamalı."
            );
        }
        return true;
    }
    private bool ValidPool(ChestItem[] pool, int multiplier)
    {
        if (multiplier == 0) return true;
        if (pool == null || pool.Length == 0) return false;
        foreach (ChestItem item in pool)
            if (item == null || item.image == null || string.IsNullOrWhiteSpace(item.id))
                return false;
        return true;
    }

    private bool SetupError(string message)
    {
        Debug.LogError(message, this);
        return false;
    }
    private long AmountFor(Reward reward)
    {
        decimal multiplier =
            1m + (GameManager.Zone - 1) * (decimal)growthPerZone;
        return System.Math.Max(
            1L,
            (long)System.Math.Ceiling(
                reward.baseAmount * multiplier
            )
        );
    }
    private string FormatAmount(long amount)
    {
        var culture = CultureInfo.InvariantCulture;
        if (amount >= 1000000)
            return (amount / 1000000m).ToString("0.##", culture) + "M";
        if (amount >= 1000)
            return (amount / 1000m).ToString("0.##", culture) + "K";
        return amount.ToString(culture);
    }
    private void RefreshUI()
    {
        if (spinButton != null)
        {
            spinButton.interactable =
                configured && state == State.Ready;
        }
        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(GameManager.IsSafe);
            leaveButton.interactable =
                configured && state == State.Ready;
        }
        if (collectedText != null)
        {
            collectedText.text = state == State.Exited
                ? "SAVED THIS SESSION\n" + GameManager.Summary(true)
                : "Collected\n" + GameManager.Summary();
        }
    }
    private void Spin()
    {
        if (!configured || state != State.Ready)
            return;
        StartCoroutine(SpinRoutine());
    }
    private IEnumerator SpinRoutine()
    {
        state = State.Spinning;
        RefreshUI();
        int selectedSlice = Random.Range(0, rewards.Length);
        float targetAngle =
            selectedSlice * (360f / rewards.Length);
        float startAngle = wheel.localEulerAngles.z;
        float remainingAngle =
            Mathf.Repeat(startAngle - targetAngle, 360f);
        float endAngle =
            startAngle - fullTurns * 360f - remainingAngle;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress =
                Mathf.Clamp01(elapsed / duration);
            float eased =
                1f - Mathf.Pow(1f - progress, 3f);
            float angle =
                Mathf.Lerp(startAngle, endAngle, eased);
            wheel.localRotation =
                Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
        wheel.localRotation =
            Quaternion.Euler(0f, 0f, targetAngle);
        ShowResult(rewards[selectedSlice]);
    }
    private void ShowResult(Reward result)
    {
        ResetChestView();
        rewardImage.enabled = true;
        rewardImage.sprite = result.image;
        rewardImage.preserveAspect = true;
        if (rewardFlash != null)
            rewardFlash.SetActive(!result.isBomb);
        if (result.isBomb)
        {
            pendingReward = null;
            pendingAmount = 0;
            GameManager.LoseRewards();
            state = State.Lost;
            rewardAmount.text = "BOMB!\nALL REWARDS LOST";
            confirmLabel.text = "RESTART";
        }
        else
        {
            pendingReward = result;
            pendingAmount = AmountFor(result);
            state = State.RewardOpen;
            rewardAmount.text = "x" + FormatAmount(pendingAmount);
            confirmLabel.text = result.isChest ? "OPEN" : "CLAIM";
            if (result.isChest)
            {
                state = State.ChestClosed;
                rewardImage.raycastTarget = true;
                chestButton.interactable = true;
            }
        }
        rewardPanel.SetActive(true);
        RefreshUI();
    }
    private void Confirm()
    {
        if (state == State.ChestClosed)
        {
            OpenChest();
            return;
        }
        if (state == State.Lost || state == State.Exited)
        {
            if (!CanLoad(0))
                return;
            GameManager.Restart();
            LoadZoneScene();
            return;
        }
        if (state != State.RewardOpen || pendingReward == null)
            return;
        int nextZone = GameManager.Zone + 1;
        int nextScene = nextZone % 30 == 0
            ? 2
            : nextZone % 5 == 0
                ? 1
                : 0;
        if (!CanLoad(nextScene))
            return;
        if (pendingReward.isChest)
        {
            if (pendingCard != null) GameManager.AddReward(pendingCard.id.Trim(), cardAmount);
            if (pendingWeapon != null) GameManager.AddReward(pendingWeapon.id.Trim(), weaponAmount);
        }
        else
        {
            GameManager.AddReward(pendingReward.rewardId.Trim(), pendingAmount);
        }
        pendingReward = null;
        pendingAmount = 0;
        rewardPanel.SetActive(false);
        GameManager.NextZone();
        LoadZoneScene();
    }
    private void Leave()
    {
        if (!configured ||
            !GameManager.IsSafe ||
            state != State.Ready)
        {
            return;
        }
        ResetChestView();
        GameManager.BankRewards();
        state = State.Exited;
        rewardImage.enabled = false;
        if (rewardFlash != null)
            rewardFlash.SetActive(false);
        rewardAmount.text = "REWARDS SAVED!";
        confirmLabel.text = "PLAY AGAIN";
        rewardPanel.SetActive(true);
        RefreshUI();
    }
    private void ResetChestView()
    {
        if (chestContents != null) Destroy(chestContents);
        chestContents = null;
        pendingCard = pendingWeapon = null;
        cardAmount = weaponAmount = 0;
        rewardAmount.gameObject.SetActive(true);
        rewardImage.rectTransform.localScale = originalRewardScale;
        rewardImage.raycastTarget = false;
        if (chestButton != null) chestButton.interactable = false;
        confirmButton.interactable = true;
    }

    private void OpenChest()
    {
        if (state != State.ChestClosed || pendingReward == null) return;
        // Calculate once, before animation. Repeated clicks cannot reroll or award twice.
        try
        {
            cardAmount = checked(pendingAmount * pendingReward.cardMultiplier);
            weaponAmount = checked(pendingAmount * pendingReward.weaponMultiplier);
        }
        catch (System.OverflowException)
        {
            Debug.LogError("Sandık miktarı çok büyük.", this);
            return;
        }
        if (pendingReward.cardMultiplier > 0)
            pendingCard = pendingReward.cards[Random.Range(0, pendingReward.cards.Length)];
        if (pendingReward.weaponMultiplier > 0)
            pendingWeapon = pendingReward.weapons[Random.Range(0, pendingReward.weapons.Length)];
        state = State.ChestOpening;
        chestButton.interactable = false;
        rewardImage.raycastTarget = false;
        confirmButton.interactable = false;
        StartCoroutine(RevealChest());
    }

    private IEnumerator RevealChest()
    {
        RectTransform chest = rewardImage.rectTransform;
        float elapsed = 0f;
        while (elapsed < 0.45f)
        {
            elapsed += Time.unscaledDeltaTime;
            float pulse = Mathf.Sin(elapsed * 45f) * 0.07f;
            chest.localScale = Vector3.Scale(originalRewardScale,
                new Vector3(1f + pulse, 1f - pulse, 1f));
            yield return null;
        }
        chest.localScale = originalRewardScale;
        rewardImage.enabled = false;
        rewardAmount.gameObject.SetActive(false);
        BuildChestContents();
        elapsed = 0f;
        while (elapsed < 0.25f)
        {
            elapsed += Time.unscaledDeltaTime;
            chestContents.transform.localScale = Vector3.one *
                Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(elapsed / 0.25f));
            yield return null;
        }
        chestContents.transform.localScale = Vector3.one;
        state = State.RewardOpen;
        confirmLabel.text = "CLAIM";
        confirmButton.interactable = true;
        RefreshUI();
    }

    private void BuildChestContents()
    {
        RectTransform source = rewardImage.rectTransform;
        RectTransform root = CreateRect("ui_group_spin_chest_contents", source.parent);
        chestContents = root.gameObject;
        root.anchorMin = source.anchorMin;
        root.anchorMax = source.anchorMax;
        root.pivot = source.pivot;
        root.sizeDelta = source.sizeDelta;
        root.anchoredPosition = source.anchoredPosition;
        int count = (pendingCard != null ? 1 : 0) + (pendingWeapon != null ? 1 : 0);
        if (pendingCard != null)
            BuildLoot(root, pendingCard, cardAmount, count == 2 ? -125f : 0f, true);
        if (pendingWeapon != null)
            BuildLoot(root, pendingWeapon, weaponAmount, count == 2 ? 125f : 0f, false);
    }

    private RectTransform CreateRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        var rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private void BuildLoot(Transform parent, ChestItem item, long amount, float x, bool isCard)
    {
        RectTransform slot = CreateRect(isCard ? "ui_group_spin_chest_card" : "ui_group_spin_chest_weapon", parent);
        slot.sizeDelta = new Vector2(220f, 220f);
        slot.anchoredPosition = new Vector2(x, 0f);
        if (isCard && cardFrame != null)
        {
            RectTransform frameRect = CreateRect("ui_image_spin_chest_frame", slot);
            frameRect.sizeDelta = slot.sizeDelta;
            var frame = frameRect.gameObject.AddComponent<Image>();
            frame.sprite = cardFrame;
            frame.type = cardFrame.border.sqrMagnitude > 0 ? Image.Type.Sliced : Image.Type.Simple;
            frame.raycastTarget = false;
        }
        if (cardBackground != null)
        {
            RectTransform bgRect = CreateRect("ui_image_spin_chest_card_bg", slot);
            bgRect.sizeDelta = slot.sizeDelta;
            Image bg = bgRect.gameObject.AddComponent<Image>();
            bg.sprite = cardBackground;
            bgRect.SetAsFirstSibling();
            bg.raycastTarget = false;
        }
        RectTransform iconRect = CreateRect("ui_image_spin_chest_item_value", slot);
        iconRect.sizeDelta = new Vector2(190f, 190f);
        iconRect.anchoredPosition = Vector2.zero;
        Image icon = iconRect.gameObject.AddComponent<Image>();
        icon.sprite = item.image;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform textRect = CreateRect("ui_text_spin_chest_amount_value", slot);
        textRect.sizeDelta = new Vector2(220f, 40f);
        textRect.anchoredPosition = new Vector2(0, -140f);
        TextMeshProUGUI label = textRect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = rewardAmount.font;
        label.fontSize = 28;
        label.color = Color.white;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.text = "x" + FormatAmount(amount);
    }

    private bool CanLoad(int index)
    {
        if (Application.CanStreamedLevelBeLoaded(index))
            return true;
        Debug.LogError(
            $"Scene List eksik: index {index}. " +
            "Sıralama: 0 Bronze, 1 Silver, 2 Golden.",
            this
        );
        return false;
    }
    private void LoadZoneScene()
    {
        if (!CanLoad(GameManager.SceneIndex))
        {
            configured = false;
            RefreshUI();
            return;
        }
        state = State.Loading;
        RefreshUI();
        SceneManager.LoadSceneAsync(GameManager.SceneIndex);
    }
}
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(WheelSpinner))]
public class WheelSpinnerInspector : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        bool changed = DrawDefaultInspector();
        if (Application.isPlaying) return;
        var spinner = (WheelSpinner)target;
        if (changed)
            spinner.RefreshSliceVisuals(true);
        if (GUILayout.Button("Refresh Wheel Preview"))
            spinner.RefreshSliceVisuals(true);
    }
}
#endif
