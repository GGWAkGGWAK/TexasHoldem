using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public int playerChip;
    public bool canPlay;
    public bool isMyTurn;

    public bool isAllIn;

    private bool isAdjustingRaise = false;
    private int raiseStep = 10000;

    [SerializeField] private GamaManager gm;
    [SerializeField] private Deck deck;
    [SerializeField] private Slider raiseSlider;
    [SerializeField] private Text raiseValueText;
    [SerializeField] private GameObject raisePanel;

    private void Awake()
    {
        gm = GameObject.Find("GameManager").GetComponent<GamaManager>();
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
        int minRaise = (gm.beforeRaiseChip == 0)
            ? gm.BigBlind * 2
            : gm.beforeBettingChip + gm.beforeRaiseChip;

        raiseSlider.minValue = minRaise;
        raiseSlider.maxValue = playerChip;
        raiseSlider.SetValueWithoutNotify(minRaise);
        raiseValueText.text = minRaise.ToString("N0");
    }

    public void OnRaiseButtonClicked()
    {
        if (!isMyTurn)
        {
            Debug.Log("아직 차례가 아닙니다!");
            return;
        }

        if (!isAdjustingRaise)
        {
            isAdjustingRaise = true;
            raisePanel.SetActive(true);
            InitializeRaiseSlider();
        }
        else
        {
            isAdjustingRaise = false;
            raisePanel.SetActive(false);
            int chipAmount = Mathf.RoundToInt(raiseSlider.value);
            Raise(chipAmount);
        }
    }

    private void SnapSliderValue()
    {
        float snapped = Mathf.Floor(raiseSlider.value / raiseStep) * raiseStep;
        if (!Mathf.Approximately(snapped, raiseSlider.value))
            raiseSlider.SetValueWithoutNotify(snapped);
        raiseValueText.text = ((int)snapped).ToString("N0");
    }

    public void Betting(int chip)
    {
        if (!isMyTurn) return;

        if (chip >= playerChip) { Allin(); return; }

        int currentToCall = gm.beforeBettingChip; // 보통 0인 스트리트에서 오픈
        if (playerChip > gm.BigBlind && chip >= gm.BigBlind)
        {
            gm.pots += chip;
            gm.beforeBettingChip = chip;
            gm.beforeRaiseChip = chip - currentToCall; // ✅ 첫 오픈은 레이즈 크기 정의
            playerChip -= chip;
            isMyTurn = false;

            if (raisePanel != null && raisePanel.activeSelf)
                raisePanel.SetActive(false);

            gm.RegisterAction(this, ActionType.Bet, false);
            gm.NextTurnFrom(this);
        }
    }

    public void Call()
    {
        if (!isMyTurn) return;

        int toCall = gm.beforeBettingChip;

        if (playerChip >= toCall)
        {
            gm.pots += toCall;
            playerChip -= toCall;
            isMyTurn = false;

            gm.RegisterAction(this, ActionType.Call, false);
            gm.NextTurnFrom(this);
        }
        else
        {
            Allin();
        }

        if (raisePanel != null && raisePanel.activeSelf)
            raisePanel.SetActive(false);

    }

    public void Fold()
    {
        if (!isMyTurn) { Debug.Log("아직 차례가 아닙니다!"); return; }

        // 🔻 슬라이더 패널 닫기
        isAdjustingRaise = false;
        if (raisePanel != null && raisePanel.activeSelf)
            raisePanel.SetActive(false);

        canPlay = false;
        isMyTurn = false;

        gm.RegisterAction(this, ActionType.Fold, isRaise: false);
        gm.HandleFoldAndPassTurn(this);
    }

    public void Check()
    {
        if (!isMyTurn) return;

        isMyTurn = false;
        gm.RegisterAction(this, ActionType.Check, false);
        gm.NextTurnFrom(this);
    }

    public void Raise(int chip)
    {
        if (!isMyTurn) return;

        int currentToCall = gm.beforeBettingChip;
        bool valid = false;

        if (gm.beforeRaiseChip == 0)
        {
            int minTo = gm.BigBlind * 2;
            if (chip >= minTo)
            {
                valid = true;
                gm.beforeRaiseChip = chip - currentToCall;
            }
        }
        else
        {
            int minTo = currentToCall + gm.beforeRaiseChip;
            if (chip >= minTo)
            {
                valid = true;
                gm.beforeRaiseChip = chip - currentToCall;
            }
        }

        if (valid)
        {
            gm.pots += chip;
            gm.beforeBettingChip = chip;
            playerChip -= chip;
            isMyTurn = false;

            if (raisePanel != null && raisePanel.activeSelf)
                raisePanel.SetActive(false);

            gm.RegisterAction(this, ActionType.Raise, true);
            gm.NextTurnFrom(this);
        }
    }

    public void Allin()
    {
        if (!isMyTurn) return;
        if (playerChip <= 0)
        {
            Debug.LogWarning("올인 불가: 남은 칩이 없습니다.");
            return;
        }

        int allinAmount = playerChip;
        int toCall = gm.beforeBettingChip;

        gm.pots += allinAmount;
        playerChip = 0;
        isMyTurn = false;
        isAllIn = true;

        bool isRaise = allinAmount > toCall;
        if (isRaise)
        {
            gm.beforeRaiseChip = allinAmount - toCall;
            gm.beforeBettingChip = allinAmount;
        }

        if (raisePanel != null && raisePanel.activeSelf)
            raisePanel.SetActive(false);

        gm.RegisterAction(this, ActionType.AllIn, isRaise);
        gm.NextTurnFrom(this);
    }
}
