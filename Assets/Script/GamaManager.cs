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

    public Text potsText;           //�� �ؽ�Ʈ

    public List<Player> turnOrder = new List<Player>();
    public int currentIndex = -1;

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
        BuildTurnOrderBySeats();
        SetFirstPlayer(0); // ù ��: 0�� �÷��̾�
    }
    void Update()
    {
        potsText.text = "Pots: " + pots.ToString();
    }
    public void BuildTurnOrderBySeats()
    {
        turnOrder.Clear();

        GameObject[] seatObjs = GameObject.FindGameObjectsWithTag("Seat");
        if (seatObjs == null || seatObjs.Length == 0) return;

        // Seat ������Ʈ ����
        var seats = new List<Seat>();
        foreach (var go in seatObjs)
        {
            var s = go.GetComponent<Seat>();
            if (s != null) seats.Add(s);
        }

        // �� �߿�: �¼��� "����(����) ����"�� ���� (��� ���� �θ� �ؿ� ���� �� ������)
        seats.Sort((a, b) =>
            a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex())
        );

        // (���) �̸� ���� ���� ������ ���� �ʹٸ�:
        // seats.Sort((a,b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        // �¼� ������� Player�� ã��, ����/���� ���� �������� ����
        foreach (var seat in seats)
        {
            Player p = seat.GetComponentInChildren<Player>(true); // ��Ȱ�� �ڽĵ� Ž���Ϸ��� true
            if (p != null && seat.isSeated && p.canPlay)
            {
                turnOrder.Add(p);
            }
        }

        // ��� �� false�� �ʱ�ȭ
        foreach (var p in turnOrder) p.isMyTurn = false;

        // ���� �α�
        Debug.Log($"[TurnOrder] �¼� ���� �÷��̾� ��: {turnOrder.Count}");
    }

    public void SetFirstPlayer(int index)
    {
        if (turnOrder.Count == 0)
        {
            currentIndex = -1;
            return;
        }

        foreach (var p in turnOrder) p.isMyTurn = false;

        currentIndex = Mathf.Clamp(index, 0, turnOrder.Count - 1);
        turnOrder[currentIndex].isMyTurn = true;

        Debug.Log($"ù ��: {turnOrder[currentIndex].name}");
    }

    // --- ���� ������ ���� ��ȿ �÷��̾�� �� �ѱ�� ---
    public void NextTurnFrom(Player actor)
    {
        if (turnOrder.Count == 0) return;

        int idx = turnOrder.IndexOf(actor);
        if (idx < 0) idx = currentIndex;

        actor.isMyTurn = false;

        // ���� ��ȿ �ĺ� Ž��: canPlay=true, Ĩ>0
        for (int step = 1; step <= turnOrder.Count; step++)
        {
            int next = (idx + step) % turnOrder.Count;
            var cand = turnOrder[next];

            if (cand != null && cand.canPlay && cand.playerChip > 0)
            {
                currentIndex = next;
                cand.isMyTurn = true;
                Debug.Log($"���� ��: {cand.name}");
                return;
            }
        }

        // ��ȿ�� ���� ���ڰ� ���ٸ�: ���� ���� ó�� ����Ʈ
        Debug.Log("��ȿ�� ���� �÷��̾� ���� �� ��Ʈ��Ʈ/���� ���� ó�� �ʿ�");
    }
}
