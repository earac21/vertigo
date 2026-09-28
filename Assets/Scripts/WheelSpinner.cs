using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class WheelSpinner : MonoBehaviour
{
    [System.Serializable]
    public class Reward
    {
        public Sprite image;
        public string amount;
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

    [Header("Üstteki ödülden başlayarak saat yönünde sırala")]
    [SerializeField] private Reward[] rewards = new Reward[8];

    private bool isSpinning;
    private bool isRewardOpen;

    private void OnValidate()
    {
        spinButton = GetComponent<Button>();
    }

    private void Awake()
    {
        spinButton = GetComponent<Button>();

        if (rewardPanel != null)
            rewardPanel.SetActive(false);
    }

    private void OnEnable()
    {
        spinButton.onClick.AddListener(Spin);

        if (confirmButton != null)
            confirmButton.onClick.AddListener(CloseReward);
    }

    private void OnDisable()
    {
        spinButton.onClick.RemoveListener(Spin);

        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(CloseReward);

        StopAllCoroutines();

        isSpinning = false;
        CloseReward();
    }

    private void Spin()
    {
        if (isSpinning || isRewardOpen || wheel == null)
            return;

        if (rewardPanel == null || rewardImage == null ||
            rewardAmount == null || confirmButton == null ||
            rewards == null || rewards.Length == 0)
        {
            Debug.LogWarning("Inspector'daki ödül alanlarını doldur.");
            return;
        }

        foreach (Reward reward in rewards)
        {
            if (reward == null || reward.image == null)
            {
                Debug.LogWarning("Her ödül için bir görsel seç.");
                return;
            }
        }

        StartCoroutine(SpinRoutine());
    }

    private IEnumerator SpinRoutine()
    {
        isSpinning = true;
        spinButton.interactable = false;

        int selectedSlice = Random.Range(0, rewards.Length);
        float sliceAngle = 360f / rewards.Length;
        float targetAngle = selectedSlice * sliceAngle;

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

            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            float angle = Mathf.Lerp(startAngle, endAngle, easedProgress);

            wheel.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        wheel.localRotation = Quaternion.Euler(0f, 0f, targetAngle);
        isSpinning = false;

        ShowReward(selectedSlice);
    }

    private void ShowReward(int index)
    {
        Reward selectedReward = rewards[index];

        rewardImage.sprite = selectedReward.image;
        rewardImage.preserveAspect = true;
        rewardAmount.text = selectedReward.amount;

        isRewardOpen = true;
        rewardPanel.SetActive(true);
    }

    private void CloseReward()
    {
        if (rewardPanel != null)
            rewardPanel.SetActive(false);

        isRewardOpen = false;
        spinButton.interactable = true;
    }
}