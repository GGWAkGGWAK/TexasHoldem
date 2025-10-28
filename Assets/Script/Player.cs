using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public class Player : MonoBehaviour
{
    public int playerChip;      //플레이어 칩
    public bool canPlay;        //팟참여 상태인지
    public bool isMyTurn;       //나의 턴인지

    private bool isAdjustingRaise = false;
    private int raiseStep = 10000;

    [SerializeField]
    private GamaManager gm;
    [SerializeField]
    private Deck deck;
    [SerializeField]
    private Slider raiseSlider;
    [SerializeField]
    private Text raiseValueText;
    [SerializeField]
    private GameObject raisePanel;

    private void Awake()
    {
        gm = GameObject.Find("GameManager").GetComponent<GamaManager>();
        deck = GameObject.Find("Deck").GetComponent<Deck>();
        Transform canvas = GameObject.Find("Canvas").transform;
        raisePanel = canvas.Find("RaisePanel").gameObject;
        raiseSlider = raisePanel.transform.Find("RaiseSlider").GetComponent<Slider>();
        raiseValueText = raisePanel.transform.Find("RaiseValueText").GetComponent<Text>();
        
        isMyTurn = false;
        canPlay = true;

        raisePanel.SetActive(false); // 처음엔 꺼두기
    }
    void Start()
    {
        playerChip = 3000000;
        raiseSlider.onValueChanged.AddListener(_ => SnapSliderValue());
    }
    private void InitializeRaiseSlider()
    {
        int minRaise = (gm.beforeRaiseChip == 0)
            ? gm.BigBlind * 2
            : gm.beforeBettingChip + gm.beforeRaiseChip;

        // 10,000 단위 정렬
        int step = 10000;
        int minSnap = Mathf.CeilToInt(minRaise / (float)step) * step;
        int maxSnap = Mathf.FloorToInt(playerChip / (float)step) * step;
        maxSnap = Mathf.Max(maxSnap, minSnap); // 범위 보호

        raiseSlider.minValue = minSnap;
        raiseSlider.maxValue = maxSnap;

        raiseSlider.SetValueWithoutNotify(minSnap);
        raiseValueText.text = minSnap.ToString("N0");
    }
    public void OnRaiseButtonClicked()
    {
        if (!isMyTurn) { Debug.Log("아직 차례가 아닙니다!"); return; }

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
    public void Betting(int chip)       //베팅
    {
        if (isMyTurn)
        {
            if (chip >= playerChip)
            {
                Allin();
                return;
            }

            if (playerChip > gm.BigBlind)
            {
                if (chip >= gm.BigBlind)
                {
                    gm.pots += chip;
                    gm.beforeBettingChip = chip;
                    playerChip -= chip;
                    isMyTurn = false;
                }
            }
        }
        else
        {
            Debug.Log("아직 차례가 아닙니다!");
        }
    }
    public void Call()                  //콜
    {
        Debug.Log($"[{name}] {System.Reflection.MethodBase.GetCurrentMethod().Name} 실행됨 / isMyTurn={isMyTurn}");

        if (!isMyTurn) { Debug.Log("아직 차례가 아닙니다!"); return; }

        int toCall = gm.beforeBettingChip;

        if (playerChip >= toCall)
        {
            gm.pots += toCall;
            playerChip -= toCall;
            isMyTurn = false;
            gm.NextTurnFrom(this);         // ✅ 여기서만 턴 넘김
        }
        else
        {
            Allin(passTurn: true);         // ✅ 올인 내부에서 턴 넘김
        }

    }
    public void Fold()                  //폴드
    {
        Debug.Log($"[{name}] {System.Reflection.MethodBase.GetCurrentMethod().Name} 실행됨 / isMyTurn={isMyTurn}");

        if (isMyTurn)
        {
            canPlay = false;
            isMyTurn = false;
            gm.NextTurnFrom(this);
        }
        else
        {
            Debug.Log("아직 차례가 아닙니다!");
        }
    }
    public void Check()                 //체크
    {
        Debug.Log($"[{name}] {System.Reflection.MethodBase.GetCurrentMethod().Name} 실행됨 / isMyTurn={isMyTurn}");

        if (isMyTurn)
        {
            isMyTurn = false;
            gm.NextTurnFrom(this);
        }
        else
        {
            Debug.Log("아직 차례가 아닙니다!");
        }
    }
    public void Raise(int chip)                 //레이즈
    {
        Debug.Log($"[{name}] {System.Reflection.MethodBase.GetCurrentMethod().Name} 실행됨 / isMyTurn={isMyTurn}");

        //현재 콜 해야 하는 금액
        int currentToCall = gm.beforeBettingChip;

        // 첫 레이즈: 무조건 빅블라인드의 2배 이상
        if(gm.beforeRaiseChip == 0)
        {
            int minTo = gm.BigBlind * 2;
            if(chip >= minTo)
            {
                gm.pots += chip;
                gm.beforeRaiseChip = chip - currentToCall;      //이번 레이즈의 크기 저장
                gm.beforeBettingChip = chip;
                playerChip -= chip;
                isMyTurn = false;
                gm.NextTurnFrom(this);
            }
        }
        else
        {
            int minTo = currentToCall + gm.beforeRaiseChip;     //두번째 레이즈 부터는 최소 레이즈 크기 = 직전 레이즈의 크기

            if (chip >= minTo)
            {
                gm.pots += chip;
                gm.beforeRaiseChip = chip - currentToCall;
                gm.beforeBettingChip = chip;
                playerChip -= chip;
                isMyTurn = false;
                gm.NextTurnFrom(this);
            }
        }
    }
    public void Allin(bool passTurn = true)
    {
        Debug.Log($"[{name}] {System.Reflection.MethodBase.GetCurrentMethod().Name} 실행됨 / isMyTurn={isMyTurn}");

        if (!isMyTurn)
        {
            Debug.Log("아직 차례가 아닙니다!");
            return;
        }

        // 남은 칩이 0 이하일 경우 방어
        if (playerChip <= 0)
        {
            Debug.LogWarning("올인 불가: 남은 칩이 없습니다.");
            return;
        }

        int allinAmount = playerChip;       // 올인 금액 (보유칩 전액)
        int toCall = gm.beforeBettingChip;  // 현재 콜해야 하는 금액

        // 🔹 팟에 칩 추가
        gm.pots += allinAmount;

        // 🔹 플레이어 칩 차감 및 턴 종료
        playerChip = 0;
        isMyTurn = false;

        // 🔹 올인 금액이 콜 금액보다 작으면 단순 콜 (추가 상태 갱신 X)
        if (allinAmount < toCall)
        {
            Debug.Log("올인 칩이 콜 금액보다 적음 → 단순 콜로 간주");
            if (passTurn)
                gm.NextTurnFrom(this);
            return;
        }

        // 🔹 올인 금액이 콜 금액보다 많으면 레이즈로 인정
        if (allinAmount > toCall)
        {
            int raiseSize = allinAmount - toCall;  // 증가분 계산

            if (gm.beforeRaiseChip == 0)
            {
                // 첫 번째 레이즈
                gm.beforeRaiseChip = raiseSize;
                gm.beforeBettingChip = allinAmount;
                Debug.Log($"첫 올인 레이즈! RaiseSize: {raiseSize}");
            }
            else
            {
                // 두 번째 이상 레이즈
                gm.beforeRaiseChip = raiseSize;
                gm.beforeBettingChip = allinAmount;
                Debug.Log($"추가 올인 레이즈! RaiseSize: {raiseSize}");
            }

            if (passTurn)
                gm.NextTurnFrom(this);
            return;
        }

        // 🔹 정확히 콜 금액과 같은 경우 → 단순 콜
        Debug.Log("올인 금액이 콜 금액과 동일 → 단순 콜로 간주");
        if (passTurn)
            gm.NextTurnFrom(this);
    }
}
