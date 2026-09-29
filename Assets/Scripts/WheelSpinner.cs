using System.Collections;
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
        public Sprite image;
        public string rewardId;
        public bool isBomb;

        [Min(1)]
        public int baseAmount = 1;

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

    [Header("Diğer UI Bağlantıları")]
    [SerializeField] private TMP_Text zoneText;
    [SerializeField] private TMP_Text collectedText;
    [SerializeField] private Button nextButton;
    [SerializeField] private Button leaveButton;

    [Header("Her zone için artış oranı")]
    [SerializeField, Min(0f)] private float growthPerZone = 0.1f;

    [Header("Üstteki yuvadan başlayarak saat yönünde sırala")]
    [SerializeField] private Reward[] rewards = new Reward[8];

    private enum State
    {
        Ready,
        Spinning,
        RewardOpen,
        Collected,
        Lost,
        Exited,
        Loading
    }

    private State state = State.Ready;
    private TMP_Text confirmLabel;
    private bool configured;

    private void OnValidate()
    {
        spinButton = GetComponent<Button>();
    }

    private void Awake()
    {
        spinButton = GetComponent<Button>();

        if (rewardPanel != null)
            rewardPanel.SetActive(false);

        if (confirmButton != null)
            confirmLabel =
                confirmButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void OnEnable()
    {
        spinButton.onClick.AddListener(Spin);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(Confirm);

        if (nextButton != null)
            nextButton.onClick.AddListener(NextZone);

        if (leaveButton != null)
            leaveButton.onClick.AddListener(Leave);
    }

    private void OnDisable()
    {
        if (spinButton != null)
            spinButton.onClick.RemoveListener(Spin);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(Confirm);

        if (nextButton != null)
            nextButton.onClick.RemoveListener(NextZone);

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

            foreach (Reward reward in rewards)
            {
                if (reward.amountText != null)
                {
                    reward.amountText.text = reward.isBomb
                        ? ""
                        : "x" + FormatAmount(AmountFor(reward));
                }
            }
        }

        RefreshUI();
    }

    private bool ValidateSetup()
    {
        if (wheel == null ||
            rewardPanel == null ||
            rewardImage == null ||
            rewardAmount == null ||
            confirmButton == null ||
            confirmLabel == null ||
            zoneText == null ||
            collectedText == null ||
            nextButton == null ||
            leaveButton == null)
        {
            return SetupError(
                "Wheel Spinner: Inspector'daki UI bağlantılarını tamamla."
            );
        }

        if (rewards == null || rewards.Length < 2)
        {
            return SetupError(
                "Rewards listesinde çarktaki tüm yuvalar olmalı."
            );
        }

        int bombs = 0;

        for (int i = 0; i < rewards.Length; i++)
        {
            Reward reward = rewards[i];

            if (reward == null || reward.image == null)
                return SetupError($"Element {i}: Image eksik.");

            if (reward.isBomb)
            {
                bombs++;
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

        int expectedBombs = GameManager.IsSafe ? 0 : 1;

        if (bombs != expectedBombs)
        {
            return SetupError(
                "Bronze tam 1 bomba, Silver ve Golden 0 bomba içermeli."
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
            (long)System.Math.Ceiling(reward.baseAmount * multiplier)
        );
    }

    private string FormatAmount(long amount)
    {
        var culture =
            System.Globalization.CultureInfo.InvariantCulture;

        if (amount >= 1000000)
        {
            return (amount / 1000000m)
                .ToString("0.##", culture) + "M";
        }

        if (amount >= 1000)
        {
            return (amount / 1000m)
                .ToString("0.##", culture) + "K";
        }

        return amount.ToString(culture);
    }

    private void RefreshUI()
    {
        spinButton.interactable =
            configured && state == State.Ready;

        if (nextButton != null)
        {
            nextButton.gameObject.SetActive(
                configured && state == State.Collected
            );
        }

        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(GameManager.IsSafe);

            leaveButton.interactable =
                configured &&
                (state == State.Ready || state == State.Collected);
        }

        if (zoneText != null)
        {
            string zoneType = GameManager.IsSuper
                ? " - SUPER"
                : GameManager.IsSafe ? " - SAFE" : "";

            zoneText.text = "ZONE " + GameManager.Zone + zoneType;
        }

        if (collectedText != null)
        {
            collectedText.text = state == State.Exited
                ? "SAVED THIS SESSION\n" + GameManager.Summary(true)
                : "COLLECTED\n" + GameManager.Summary();
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

        float targetAngle = selectedSlice * (360f / rewards.Length);
        float startAngle = wheel.localEulerAngles.z;

        float remainingAngle =
            Mathf.Repeat(startAngle - targetAngle, 360f);

        float endAngle =
            startAngle - fullTurns * 360f - remainingAngle;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            float angle = Mathf.Lerp(startAngle, endAngle, eased);

            wheel.localRotation = Quaternion.Euler(0f, 0f, angle);

            yield return null;
        }

        wheel.localRotation =
            Quaternion.Euler(0f, 0f, targetAngle);

        Reward result = rewards[selectedSlice];

        rewardImage.gameObject.SetActive(true);
        rewardImage.enabled = true;
        rewardImage.sprite = result.image;
        rewardImage.preserveAspect = true;

        if (rewardFlash != null)
            rewardFlash.SetActive(!result.isBomb);

        if (result.isBomb)
        {
            GameManager.LoseRewards();

            state = State.Lost;
            rewardAmount.text = "BOMB!\nALL REWARDS LOST";
            confirmLabel.text = "RESTART";
        }
        else
        {
            long amount = AmountFor(result);

            GameManager.AddReward(result.rewardId.Trim(), amount);

            state = State.RewardOpen;
            rewardAmount.text = "x" + FormatAmount(amount);
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
        }
        else if (state == State.RewardOpen)
        {
            rewardPanel.SetActive(false);

            state = State.Collected;
            RefreshUI();
        }
    }

    private void NextZone()
    {
        if (state != State.Collected)
            return;

        int next = GameManager.Zone + 1;
        int scene = next % 30 == 0 ? 2 : next % 5 == 0 ? 1 : 0;

        if (!CanLoad(scene))
            return;

        GameManager.NextZone();
        LoadZoneScene();
    }

    private void Leave()
    {
        if (!configured ||
            !GameManager.IsSafe ||
            (state != State.Ready && state != State.Collected))
        {
            return;
        }

        GameManager.BankRewards();

        state = State.Exited;

        // Yazı bu nesnenin altında olabilir; yalnızca Image'ı kapat.
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