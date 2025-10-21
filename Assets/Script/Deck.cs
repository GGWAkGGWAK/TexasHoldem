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
    public void InitializeDeck()      //덱 생성
    {
        deck.Clear();
        /*for (int i = 0; i < originalDeck.Count; i++)
        {
            // 1) CardData 가져오기
            CardData data = originalDeck[i].cardData;

            // 2) 새로운 Card 오브젝트 생성
            GameObject cardObj = Instantiate(cardPrefab, this.transform);
            Card newCard = cardObj.GetComponent<Card>();

            // 3) CardData 주입
            newCard.Initialize(data);

            // 4) 덱에 추가
            deck.Add(newCard);
        }*/
    }

    public void ShuffleDeck()       //덱 셔플
    {
        for (int i = 0; i < deck.Count; i++)
        {
            int randIndex = Random.Range(i, deck.Count);
            Card temp = deck[i];
            deck[i] = deck[randIndex];
            deck[randIndex] = temp;
        }
        Debug.Log("덱 셔플 완료");
    }

    public void DrawingCard()       //카드나눠주기
    {
        
    }
}
