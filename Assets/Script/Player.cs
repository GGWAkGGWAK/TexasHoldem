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
    public int contributedThisHand;

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
            gm.beforeRaiseChip = chip - currentToCall; // 첫 오픈은 레이즈 크기 정의
            playerChip -= chip;
            contributedThisHand += chip;
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
            contributedThisHand += toCall;
            isMyTurn = false;

            // 스트리트 변화 감지를 위한 현재 스트리트 저장
            var streetBeforeAction = gm.currentStreet;
            Debug.Log($"[{name} Call] Before RegisterAction - Street: {streetBeforeAction}");

            gm.RegisterAction(this, ActionType.Call, false);

            var streetAfterAction = gm.currentStreet;
            Debug.Log($"[{name} Call] After RegisterAction - Street: {streetAfterAction}");

            // 스트리트가 바뀌었으면 NextTurnFrom 호출하지 않음!
            if (streetBeforeAction == streetAfterAction)
            {
                Debug.Log($"[{name} Call] Street unchanged - calling NextTurnFrom");
                gm.NextTurnFrom(this);
            }
            else
            {
                Debug.Log($"[{name} Call] Street changed ({streetBeforeAction} → {streetAfterAction}) - NextTurnFrom skipped");
            }
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
        if (!isMyTurn) return;

        canPlay = false;
        isMyTurn = false;

        // 스트리트 변화 감지
        var streetBeforeAction = gm.currentStreet;
        Debug.Log($"[{name} Fold] Before RegisterAction - Street: {streetBeforeAction}");

        gm.RegisterAction(this, ActionType.Fold, false);

        var streetAfterAction = gm.currentStreet;
        Debug.Log($"[{name} Fold] After RegisterAction - Street: {streetAfterAction}");

        // 스트리트가 바뀌었으면 NextTurnFrom 호출하지 않음!
        if (streetBeforeAction == streetAfterAction)
        {
            Debug.Log($"[{name} Fold] Street unchanged - calling NextTurnFrom");
            gm.NextTurnFrom(this);
        }
        else
        {
            Debug.Log($"[{name} Fold] Street changed ({streetBeforeAction} → {streetAfterAction}) - NextTurnFrom skipped");
        }

        if (raisePanel != null && raisePanel.activeSelf)
            raisePanel.SetActive(false);
    }

    public void Check()
    {
        if (!isMyTurn) return;

        isMyTurn = false;

        // 스트리트 변화 감지
        var streetBeforeAction = gm.currentStreet;
        Debug.Log($"[{name} Check] Before RegisterAction - Street: {streetBeforeAction}");

        gm.RegisterAction(this, ActionType.Check, false);

        var streetAfterAction = gm.currentStreet;
        Debug.Log($"[{name} Check] After RegisterAction - Street: {streetAfterAction}");

        // 스트리트가 바뀌었으면 NextTurnFrom 호출하지 않음!
        if (streetBeforeAction == streetAfterAction)
        {
            Debug.Log($"[{name} Check] Street unchanged - calling NextTurnFrom");
            gm.NextTurnFrom(this);
        }
        else
        {
            Debug.Log($"[{name} Check] Street changed ({streetBeforeAction} → {streetAfterAction}) - NextTurnFrom skipped");
        }

        if (raisePanel != null && raisePanel.activeSelf)
            raisePanel.SetActive(false);
    }

    public void Raise(int amount)
    {
        if (!isMyTurn) return;

        int totalBet = gm.beforeBettingChip + amount;

        if (playerChip >= totalBet)
        {
            gm.pots += totalBet;
            playerChip -= totalBet;
            contributedThisHand += totalBet;
            isMyTurn = false;

            var streetBeforeAction = gm.currentStreet;
            Debug.Log($"[{name} Raise] Before RegisterAction - Street: {streetBeforeAction}, TotalBet: {totalBet}");

            // 레이즈된 총 금액을 GameManager에 전달
            gm.RegisterAction(this, ActionType.Raise, true, totalBet);  // 파라미터 추가

            var streetAfterAction = gm.currentStreet;
            Debug.Log($"[{name} Raise] After RegisterAction - Street: {streetAfterAction}");

            if (streetBeforeAction == streetAfterAction)
            {
                gm.NextTurnFrom(this);
            }
        }
        else
        {
            Allin();
        }

        if (raisePanel != null && raisePanel.activeSelf)
            raisePanel.SetActive(false);
    }

    public void Allin()
    {
        if (!isMyTurn) return;

        int allInAmount = playerChip;
        bool isRaiseAction = allInAmount > gm.beforeBettingChip;

        gm.pots += allInAmount;
        contributedThisHand += allInAmount;
        playerChip = 0;
        isMyTurn = false;

        // 스트리트 변화 감지
        var streetBeforeAction = gm.currentStreet;
        Debug.Log($"[{name} Allin] Before RegisterAction - Street: {streetBeforeAction}, Amount: {allInAmount}, IsRaise: {isRaiseAction}");

        gm.RegisterAction(this, ActionType.AllIn, isRaiseAction);

        var streetAfterAction = gm.currentStreet;
        Debug.Log($"[{name} Allin] After RegisterAction - Street: {streetAfterAction}");

        // 스트리트가 바뀌었으면 NextTurnFrom 호출하지 않음!
        if (streetBeforeAction == streetAfterAction)
        {
            Debug.Log($"[{name} Allin] Street unchanged - calling NextTurnFrom");
            gm.NextTurnFrom(this);
        }
        else
        {
            Debug.Log($"[{name} Allin] Street changed ({streetBeforeAction} → {streetAfterAction}) - NextTurnFrom skipped");
        }

        if (raisePanel != null && raisePanel.activeSelf)
            raisePanel.SetActive(false);
    }
}
