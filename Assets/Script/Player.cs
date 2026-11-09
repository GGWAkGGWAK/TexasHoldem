using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public int playerChip;
    public bool canPlay;
    public bool isMyTurn;

    public bool isAllIn;
    public int contributedThisHand = 0;
    public int contributedThisRound = 0;

    private bool isAdjustingRaise = false;
    private int raiseStep = 10000;

    [SerializeField] private GameManager gm;
    [SerializeField] private Deck deck;
    [SerializeField] private Slider raiseSlider;
    [SerializeField] private Text raiseValueText;
    [SerializeField] private GameObject raisePanel;

    private void Awake()
    {
        gm = GameObject.Find("GameManager").GetComponent<GameManager>();
        deck = GameObject.Find("Deck").GetComponent<Deck>();

        Transform canvas = GameObject.Find("Canvas").transform;
        raisePanel = canvas.Find("RaisePanel").gameObject;
        raiseSlider = raisePanel.transform.Find("RaiseSlider").GetComponent<Slider>();
        raiseValueText = raisePanel.transform.Find("RaiseValueText").GetComponent<Text>();

        raisePanel.SetActive(false);

        playerChip = 3000000;
        canPlay = true;
        isMyTurn = false;
        isAllIn = false;
    }

    void Start()
    {
        raiseSlider.onValueChanged.AddListener(_ => SnapSliderValue());
    }

    private void InitializeRaiseSlider()
    {
        int maxTotal = contributedThisRound + playerChip;
        int minTotal = Mathf.Min(gm.MinimumRaiseTo, maxTotal);

        raiseSlider.minValue = minTotal;
        raiseSlider.maxValue = maxTotal;
        raiseSlider.SetValueWithoutNotify(minTotal);
        raiseValueText.text = minTotal.ToString("N0");
    }

    public void OnRaiseButtonClicked()
    {
        if (!isMyTurn || !gm.CanIncreaseBet(this)) return;

        if (!isAdjustingRaise)
        {
            isAdjustingRaise = true;
            raisePanel.SetActive(true);
            InitializeRaiseSlider();
        }
        else
        {
            Raise(Mathf.RoundToInt(raiseSlider.value));
        }
    }

    private void SnapSliderValue()
    {
        float value = raiseSlider.value;
        float min = raiseSlider.minValue;
        float snapped = min + Mathf.Floor((value - min) / raiseStep) * raiseStep;
        if (Mathf.Approximately(value, raiseSlider.maxValue))
            snapped = raiseSlider.maxValue;

        raiseSlider.SetValueWithoutNotify(snapped);
        raiseValueText.text = Mathf.RoundToInt(snapped).ToString("N0");
    }

    public void Betting(int chips)
    {
        if (gm.beforeBettingChip == 0)
            Raise(contributedThisRound + chips);
    }

    // total is the target street contribution, including any chips already posted.
    public void Raise(int total)
    {
        if (!isMyTurn || !gm.CanIncreaseBet(this)) return;

        int maxTotal = contributedThisRound + playerChip;
        if (total > maxTotal || (total < gm.MinimumRaiseTo && total != maxTotal)) return;

        CommitChips(total - contributedThisRound);
        FinishAction();
    }

    public void Call()
    {
        if (!isMyTurn) return;

        CommitChips(Mathf.Min(gm.GetCallCost(this), playerChip));
        FinishAction();
    }

    public void Fold()
    {
        if (!isMyTurn) return;

        canPlay = false;
        FinishAction();
    }

    public void Check()
    {
        if (!isMyTurn || gm.GetCallCost(this) > 0) return;

        FinishAction();
    }

    public void Allin()
    {
        if (!isMyTurn) return;

        if (playerChip <= gm.GetCallCost(this))
            Call();
        else
            Raise(contributedThisRound + playerChip);
    }

    private void CommitChips(int chips)
    {
        playerChip -= chips;
        contributedThisHand += chips;
        contributedThisRound += chips;
        gm.pots += chips;
        isAllIn = playerChip == 0;
    }

    private void FinishAction()
    {
        isMyTurn = false;
        if (isAdjustingRaise)
        {
            isAdjustingRaise = false;
            raisePanel.SetActive(false);
        }

        gm.RegisterAction(this);
    }
}
