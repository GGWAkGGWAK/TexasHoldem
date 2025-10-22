using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<Seat> seats = new List<Seat>();
    public List<Card> deck = new List<Card>();      //���� ��
    [SerializeField]
    private List<Card> originalDeck = new List<Card>();     //�⺻ ��
    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
    }
    public void InitializeDeck()      //�� �ʱ�ȭ �� ����
    {
        deck.Clear();
        deck.AddRange(originalDeck);
        Debug.Log("�� ����");
    }

    public void ShuffleDeck()       //�� ����
    {
        InitializeDeck();
        for (int i = 0; i < deck.Count; i++)
        {
            int randIndex = Random.Range(i, deck.Count);
            Card temp = deck[i];
            deck[i] = deck[randIndex];
            deck[randIndex] = temp;
        }
        Debug.Log("�� ���� �Ϸ�");
    }

    public void DrawingCard()       //ī�峪���ֱ�
    {
        seats.Clear();

        GameObject[] allSeats = GameObject.FindGameObjectsWithTag("Seat");

        for(int i = 0; i< allSeats.Length; i++)
        {
            GameObject seatObj = allSeats[i];
            Seat seat = seatObj.GetComponent<Seat>();

            if(seat!=null && seat.isSeated)
            {
                seats.Add(seat);
            }
        }
        Debug.Log($"���� �������� �¼� ��: {seats.Count}");
        if (deck.Count > 0)
        {
            for (int i = 0; i < seats.Count; i++)           //�ö�1
            {

                Debug.Log(deck[0]);
                deck.RemoveAt(0);
            }

            for (int i = 0; i < seats.Count; i++)           //�ö�2
            {

                Debug.Log(deck[0]);
                deck.RemoveAt(0);
            }
        }
        else
        {
            Debug.Log("�������� �÷��̾� X");
        }
        
        
    }
}
