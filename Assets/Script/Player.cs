using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public int playerChip;      //플레이어 칩
    public bool canPlay;        //팟참여 상태인지
    public bool isMyTurn;       //나의 턴인지

    [SerializeField]
    private GamaManager gm;
    private void Awake()
    {
        gm = GameObject.FindGameObjectWithTag("GameManager").GetComponent<GamaManager>();
    }
    void Start()
    {
        playerChip = 3000000;
    }
    void Update()
    {
        
    }

    public void Betting(int chip)       //베팅
    {

        if (chip >= playerChip)
        {
            Allin();
            return;
        }

        if (playerChip> gm.BigBlind)
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
    public void Call()                  //콜
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
    public void Fold()                  //폴드
    {
        canPlay = false;
        isMyTurn = false;
    }
    public void Check()                 //체크
    {
        isMyTurn = false;
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
}
