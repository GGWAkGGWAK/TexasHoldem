using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<Card> deck = new List<Card>();      //���� ��
    [SerializeField]
    private List<Card> originalDeck = new List<Card>();     //�⺻ ��
    void Start()
    {
        InitializeDeck();
        ShuffleDeck();
    }
    public void InitializeDeck()      //�� ����
    {
        deck.Clear();
        deck = originalDeck;
        Debug.Log("�� ����");
    }

    public void ShuffleDeck()       //�� ����
    {
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
        
    }
}
