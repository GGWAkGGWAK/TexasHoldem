using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public int playerChip;      //�÷��̾� Ĩ
    public bool canPlay;        //������ ��������
    public bool isMyTurn;       //���� ������

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

    public void Betting(int chip)       //����
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
    public void Call()                  //��
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
    public void Fold()                  //����
    {
        canPlay = false;
        isMyTurn = false;
    }
    public void Check()                 //üũ
    {
        isMyTurn = false;
    }
    public void Raise(int chip)                 //������
    {
        //���� �� �ؾ� �ϴ� �ݾ�
        int currentToCall = gm.beforeBettingChip;

        // ù ������: ������ �����ε��� 2�� �̻�
        if(gm.beforeRaiseChip == 0)
        {
            int minTo = gm.BigBlind * 2;
            if(chip >= minTo)
            {
                gm.pots += chip;
                gm.beforeRaiseChip = chip - currentToCall;      //�̹� �������� ũ�� ����
                gm.beforeBettingChip = chip;
                playerChip -= chip;
                isMyTurn = false;
            }
        }
        else
        {
            int minTo = currentToCall + gm.beforeRaiseChip;     //�ι�° ������ ���ʹ� �ּ� ������ ũ�� = ���� �������� ũ��

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
    public void Allin()         //����
    {
        if (playerChip <= 0)
        {
            Debug.LogWarning("���� �Ұ�: ���� Ĩ�� �����ϴ�.");
            return;
        }

        int allinAmount = playerChip;        // ���� �ݾ�
        int toCall = gm.beforeBettingChip;   // ���� ���ؾ� �ϴ� �ݾ�

        // �̿� Ĩ �߰�
        gm.pots += allinAmount;

        // Ĩ �Ҹ� �� �� ����
        playerChip = 0;
        isMyTurn = false;

        // ���� 1: ���� Ĩ�� �� �ݾ׺��� ������ => �ܼ� �ݷ� ����
        if (allinAmount < toCall)
        {
            return;
        }

        // ���� 2: ���� �ݾ��� �� �ݾ׺��� ������ => ������� ����
        if (allinAmount > toCall)
        {
            int raiseSize = allinAmount - toCall;  // �̹��� ������ �ݾ�

            // ù ��������
            if (gm.beforeRaiseChip == 0)
            {
                gm.beforeRaiseChip = raiseSize;
                gm.beforeBettingChip = allinAmount;
            }
            else // �� ��° �̻� ������
            {
                gm.beforeRaiseChip = raiseSize;
                gm.beforeBettingChip = allinAmount;
            }
        }
        else
        {
            // ��Ȯ�� �� �ݾ� == ���� �ݾ��� �� �� �ܼ� ��
            return;
        }
    }
}
