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

        raisePanel.SetActive(false); // 처음엔 꺼두기
    }
    void Start()
    {
        playerChip = 3000000;
        isMyTurn = false;
        canPlay = true;
        raiseSlider.onValueChanged.AddListener(_ => SnapSliderValue());
    }
    void Update()
    {

    }
    private void InitializeRaiseSlider()
    {
        int minRaise = (gm.beforeRaiseChip == 0)
            ? gm.BigBlind * 2
            : gm.beforeBettingChip + gm.beforeRaiseChip;

        raiseSlider.minValue = minRaise;
        raiseSlider.maxValue = playerChip;

        // 이벤트 발생 없이 초기값 설정
        raiseSlider.SetValueWithoutNotify(minRaise);
        // 표시 갱신
        raiseValueText.text = minRaise.ToString("N0");
    }
    public void OnRaiseButtonClicked()
    {
        if (isMyTurn)
        {
            if (!isAdjustingRaise)
            {
                // 1️⃣ 첫 클릭 → 슬라이더 패널 활성화
                isAdjustingRaise = true;
                raisePanel.SetActive(true);
                InitializeRaiseSlider();

                Debug.Log("슬라이더 활성화 (Raise 금액 조정 중)");
            }
            else
            {
                // 2️⃣ 두 번째 클릭 → Raise 실행 & 패널 비활성화
                isAdjustingRaise = false;
                raisePanel.SetActive(false);

                int chipAmount = Mathf.RoundToInt(raiseSlider.value);
                Raise(chipAmount);

                Debug.Log($"레이즈 실행! 금액: {chipAmount}");
            }
        }
        else
        {
            Debug.Log("아직 차례가 아닙니다!");
        }
    }
    private void SnapSliderValue()
    {
        float snapped = Mathf.Floor(raiseSlider.value / raiseStep) * raiseStep;

        // 이미 스냅된 값이면 이벤트 루프 방지
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
        if (isMyTurn)
        {
            int toCall = gm.beforeBettingChip;
            if (playerChip >= toCall)
            {
                gm.pots += toCall;
                playerChip -= toCall;
                isMyTurn = false;
            }
            else
            {
                Allin();
            }
        }
        else
        {
            Debug.Log("아직 차례가 아닙니다!");
        }
        
    }
    public void Fold()                  //폴드
    {
        if (isMyTurn)
        {
            canPlay = false;
            isMyTurn = false;
        }
        else
        {
            Debug.Log("아직 차례가 아닙니다!");
        }
    }
    public void Check()                 //체크
    {
        if (isMyTurn)
        {
            isMyTurn = false;
        }
        else
        {
            Debug.Log("아직 차례가 아닙니다!");
        }
    }
    public void Raise(int chip)                 //레이즈
    {
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
            }
        }
    }
    public void Allin()         //올인
    {
        if (isMyTurn)
        {
            if (playerChip <= 0)
            {
                Debug.LogWarning("올인 불가: 남은 칩이 없습니다.");
                return;
            }

            int allinAmount = playerChip;        // 올인 금액
            int toCall = gm.beforeBettingChip;   // 현재 콜해야 하는 금액

            // 팟에 칩 추가
            gm.pots += allinAmount;

            // 칩 소모 및 턴 종료
            playerChip = 0;
            isMyTurn = false;

            // 조건 1: 올인 칩이 콜 금액보다 적으면 => 단순 콜로 간주
            if (allinAmount < toCall)
            {
                return;
            }

            // 조건 2: 올인 금액이 콜 금액보다 많으면 => 레이즈로 인정
            if (allinAmount > toCall)
            {
                int raiseSize = allinAmount - toCall;  // 이번에 증가한 금액

                // 첫 레이즈라면
                if (gm.beforeRaiseChip == 0)
                {
                    gm.beforeRaiseChip = raiseSize;
                    gm.beforeBettingChip = allinAmount;
                }
                else // 두 번째 이상 레이즈
                {
                    gm.beforeRaiseChip = raiseSize;
                    gm.beforeBettingChip = allinAmount;
                }
            }
            else
            {
                // 정확히 콜 금액 == 올인 금액일 때 → 단순 콜
                return;
            }
        }
        else
        {
            Debug.Log("아직 차례가 아닙니다!");
        }
    }
}
