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

        [Min(1)] public int baseAmount = 1;

        public TMP_Text amountText;
    }

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

        int requiredBombCount = GameManager.IsSafe ? 0 : 1;

        if (bombCount != requiredBombCount)
        {
            return SetupError(
                "Bronze 1 bomba içermeli. Silver ve Golden'da bomba olmamalı."
            );
        }

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
            confirmLabel.text = "CLAIM";
        }

        rewardPanel.SetActive(true);
        RefreshUI();
    }

    private void Confirm()
    {
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

        GameManager.AddReward(
            pendingReward.rewardId.Trim(),
            pendingAmount
        );

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
