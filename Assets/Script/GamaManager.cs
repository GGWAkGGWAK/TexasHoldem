using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GamaManager : MonoBehaviour
{
    public int smallBlind;       //��������ε�
    public int BigBlind;         //�����ε�
    public float duration;       //�෹�̼�
    public float bettingTime;    //���� ���� �ð�
    public int pots;            //��
    public int beforeBettingChip;       //���� ����Ĩ
    public int beforeRaiseChip;         //����Ĩ�� ������ Ĩ�� ����

    public Text potsText;

    private void Awake()
    {
        Transform canvas = GameObject.Find("Canvas").transform;
        potsText = canvas.Find("��").GetComponent<Text>();
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
