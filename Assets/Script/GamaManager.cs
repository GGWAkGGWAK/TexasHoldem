using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GamaManager : MonoBehaviour
{
    public int smallBlind;       //스몰블라인드
    public int BigBlind;         //빅블라인드
    public float duration;       //듀레이션
    public float bettingTime;    //베팅 제한 시간
    public int pots;            //팟
    public int beforeBettingChip;       //직전 베팅칩
    public int beforeRaiseChip;         //베팅칩과 레이즈 칩의 차액

    public Text potsText;

    private void Awake()
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        potsText = canvas.Find("팟").GetComponent<Text>();
    }
    void Start()
    {
        smallBlind = 10000;
        BigBlind = 20000;
        duration = 180;
        beforeBettingChip = 20000;
    }

    void Update()
    {
        potsText.text = "Pots: " + pots.ToString();
    }
}
