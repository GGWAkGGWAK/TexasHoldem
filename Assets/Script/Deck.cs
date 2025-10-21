using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Deck : MonoBehaviour
{
    public List<Card> deck = new List<Card>();
    private List<Card> originalDeck = new List<Card>();
    public GameObject cardPrefab;
    void Start()
    {
        FindOriginalDeck();

        ShuffleDeck();
    }
    public void FindOriginalDeck()
    {
        Deck originalDeckObject = GameObject.FindWithTag("OriginalDeck").GetComponent<Deck>();
        originalDeck = originalDeckObject.deck;
    }
    public void InitializeDeck()      //�� ����
    {
        deck.Clear();
        /*for (int i = 0; i < originalDeck.Count; i++)
        {
            // 1) CardData ��������
            CardData data = originalDeck[i].cardData;

            // 2) ���ο� Card ������Ʈ ����
            GameObject cardObj = Instantiate(cardPrefab, this.transform);
            Card newCard = cardObj.GetComponent<Card>();

            // 3) CardData ����
            newCard.Initialize(data);

            // 4) ���� �߰�
            deck.Add(newCard);
        }*/
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
